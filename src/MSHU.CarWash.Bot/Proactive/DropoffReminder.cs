﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.ServiceBus;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.WindowsAzure.Storage.Table;
using MSHU.CarWash.Bot.Dialogs;
using MSHU.CarWash.Bot.Extensions;
using MSHU.CarWash.Bot.States;
using MSHU.CarWash.ClassLibrary.Extensions;
using MSHU.CarWash.ClassLibrary.Models.ServiceBus;
using Newtonsoft.Json;

namespace MSHU.CarWash.Bot.Proactive
{
    /// <summary>
    /// Proactive messaging service for sending out reminders to the users to drop-off their keys.
    /// </summary>
    public class DropoffReminder
    {
        /// <summary>
        /// Service Bus queue name.
        /// </summary>
        private const string ServiceBusQueueName = "bot-dropoff-reminder";
        private const string ServiceBusQueueNameDev = "bot-dropoff-reminder-dev";

        private readonly QueueClient _queueClient;
        private readonly CloudTable _table;
        private readonly EndpointService _endpoint;
        private readonly StateAccessors _accessors;
        private readonly BotFrameworkAdapter _botFrameworkAdapter;
        private readonly DialogSet _dialogs;
        private readonly IHostingEnvironment _env;
        private readonly TelemetryClient _telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="DropoffReminder"/> class.
        /// </summary>
        /// <param name="accessors">The state accessors for managing bot state.</param>
        /// <param name="adapterIntegration">The <see cref="BotFrameworkAdapter"/> connects the bot to the service endpoint of the given channel.</param>
        /// <param name="env">Provides information about the web hosting environment an application is running in.</param>
        /// <param name="botConfig">The parsed .bot config file.</param>
        /// <param name="services">External services.</param>
        public DropoffReminder(StateAccessors accessors, IAdapterIntegration adapterIntegration, IHostingEnvironment env, BotConfiguration botConfig, BotServices services)
        {
            _accessors = accessors;
            _env = env;
            _botFrameworkAdapter = (BotFrameworkAdapter)adapterIntegration;
            _telemetryClient = new TelemetryClient();

            _dialogs = new DialogSet(_accessors.DialogStateAccessor);
            _dialogs.Add(new FindReservationDialog());

            // Verify Endpoint configuration.
            var endpointConfig = env.IsProduction() ? CarWashBot.EndpointConfiguration : CarWashBot.EndpointConfigurationDev;
            if (!services.EndpointServices.ContainsKey(endpointConfig))
                throw new ArgumentException($"Invalid configuration. Please check your '.bot' file for a Endpoint service named '{endpointConfig}'.");

            _endpoint = services.EndpointServices[endpointConfig];

            // Verify Storage configuration.
            if (!services.StorageServices.ContainsKey(CarWashBot.StorageConfiguration))
                throw new ArgumentException($"Invalid configuration. Please check your '.bot' file for a Storage service named '{CarWashBot.StorageConfiguration}'.");

            var tableClient = services.StorageServices[CarWashBot.StorageConfiguration].CreateCloudTableClient();
            _table = tableClient.GetTableReference(CarWashBot.UserStorageTableName);

            // Verify ServiceBus configuration.
            if (!services.ServiceBusServices.ContainsKey(CarWashBot.ServiceBusConfiguration))
                throw new ArgumentException($"Invalid configuration. Please check your '.bot' file for a Service Bus service named '{CarWashBot.ServiceBusConfiguration}'.");

            var queueName = env.IsProduction() ? ServiceBusQueueName : ServiceBusQueueNameDev;
            _queueClient = new QueueClient(services.ServiceBusServices[CarWashBot.ServiceBusConfiguration], queueName, ReceiveMode.PeekLock, null);
        }

        /// <summary>
        /// Register Service Bus message handler.
        /// This will trigger <see cref="ProcessMessagesAsync(Message, CancellationToken)"/> when a new message is inserted into the queue.
        /// </summary>
        public void RegisterHandler()
        {
            // Configure the MessageHandler Options in terms of exception handling, number of concurrent messages to deliver etc.
            var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                // Maximum number of Concurrent calls to the callback `ProcessMessagesAsync`, set to 1 for simplicity.
                // Set it according to how many messages the application wants to process in parallel.
                MaxConcurrentCalls = 1,

                // Indicates whether MessagePump should automatically complete the messages after returning from User Callback.
                // False below indicates the Complete will be handled by the User Callback as in `ProcessMessagesAsync` below.
                AutoComplete = false,
            };

            // Register the function that will process messages
            _queueClient.RegisterMessageHandler(ProcessMessagesAsync, messageHandlerOptions);
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs args)
        {
            var context = args.ExceptionReceivedContext;
            _telemetryClient.TrackException(args.Exception, new Dictionary<string, string>
            {
                { "Endpoint", context.Endpoint },
                { "Entity Path", context.EntityPath },
                { "Executing Action", context.Action },
            });

            return Task.CompletedTask;
        }

        private async Task ProcessMessagesAsync(Message message, CancellationToken cancellationToken)
        {
            var json = Encoding.UTF8.GetString(message.Body);
            var reminder = JsonConvert.DeserializeObject<DropoffReminderMessage>(json);

            var userInfos = await _table.RetrieveUserInfoAsync(reminder.UserId);

            if (userInfos == null || userInfos.Count == 0)
            {
                await _queueClient.CompleteAsync(message.SystemProperties.LockToken);

                _telemetryClient.TrackEvent(
                    "Failed to send bot drop-off reminder proactive message: User was not found in the bot's database.",
                    new Dictionary<string, string>
                    {
                        { "User Carwash ID", reminder.UserId },
                        { "Reservation ID", reminder.ReservationId },
                        { "SequenceNumber", message.SystemProperties.SequenceNumber.ToString() },
                    });
            }

            if (!_env.IsProduction()) userInfos = userInfos.Where(i => i.ChannelId == ChannelIds.Emulator).ToList();

            foreach (var userInfo in userInfos)
            {
                try
                {
                    await _botFrameworkAdapter.CreateConversationAsync(
                        userInfo.ChannelId,
                        userInfo.ServiceUrl,
                        new MicrosoftAppCredentials(_endpoint.AppId, _endpoint.AppPassword),
                        new ConversationParameters(bot: userInfo.Bot, members: new List<ChannelAccount> { userInfo.User }, channelData: userInfo.ChannelData),
                        DropOffReminderCallback(reminder.ReservationId, userInfo),
                        cancellationToken);

                    // Same with an existing conversation
                    // var conversation = new ConversationReference(
                    //   null,
                    //   userInfo.User,
                    //   userInfo.Bot,
                    //   new ConversationAccount(null, null, userInfo.CurrentConversation.Conversation.Id, null, null, null),
                    //   userInfo.ChannelId,
                    //   userInfo.ServiceUrl);
                    // await _botFrameworkAdapter.ContinueConversationAsync(_endpoint.AppId, conversation, DropOffReminderCallback(), cancellationToken);
                }
                catch (ErrorResponseException e)
                {
                    _telemetryClient.TrackException(
                        e,
                        new Dictionary<string, string>
                        {
                            { "Error message", e.Message },
                            { "Response body", e.Response.Content },
                            { "Request body", e.Request.Content },
                            { "Request uri", $"{e.Request.Method.ToString()} {e.Request.RequestUri.AbsoluteUri}" },
                            { "Request headers", e.Request.Headers.ToJson() },
                        });
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e);
                }
            }

            // Note: Use the cancellationToken passed as necessary to determine if the queueClient has already been closed.
            // If queueClient has already been Closed, you may chose to not call CompleteAsync() or AbandonAsync() etc. calls
            // to avoid unnecessary exceptions.
            //
            // Complete the message so that it is not received again.
            // This can be done only if the queueClient is created in ReceiveMode.PeekLock mode (which is default).
            await _queueClient.CompleteAsync(message.SystemProperties.LockToken);

            _telemetryClient.TrackEvent(
                "Bot drop-off reminder proactive message sent.",
                new Dictionary<string, string>
                {
                    { "Reservation ID", reminder.ReservationId },
                    { "SequenceNumber", message.SystemProperties.SequenceNumber.ToString() },
                },
                new Dictionary<string, double>
                {
                    { "Proactive message", 1 },
                });
        }

        private BotCallbackHandler DropOffReminderCallback(string reservationId, UserInfoEntity userInfo) =>
            async (turnContext, cancellationToken) =>
            {
                try
                {
                    // Set the From object for the state accessor (CreateConversationAsync() does not fill the Activity.From object)
                    turnContext.Activity.From = userInfo.User;

                    var profile = await _accessors.UserProfileAccessor.GetAsync(turnContext, () => null, cancellationToken);
                    var greeting = profile?.NickName == null ? "Hi!" : $"Hi {profile.NickName}!";

                    // Send the user a proactive reminder message.
                    await turnContext.SendActivitiesAsync(
                        new IActivity[]
                        {
                            new Activity(type: ActivityTypes.Message, text: greeting),
                            new Activity(type: ActivityTypes.Message, text: "Sorry for bothering!"),
                            new Activity(type: ActivityTypes.Message, text: "Just wanted to remind you, that it's time to leave the key at the reception."),
                            new Activity(type: ActivityTypes.Message, text: "And please don't forget to confirm the vehicle location! You can do it here by clicking the 'Confirm key drop-off' button below."),
                        }, cancellationToken);

                    // Create a dialog context
                    var dc = await _dialogs.CreateContextAsync(turnContext, cancellationToken);

                    // Show them the reservation
                    await dc.BeginDialogAsync(
                        nameof(FindReservationDialog),
                        new FindReservationDialog.FindReservationDialogOptions { ReservationId = reservationId },
                        cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e, new Dictionary<string, string>
                    {
                        { "Activity", turnContext?.Activity?.ToJson() ?? "No activity." },
                        { "Reservation ID", reservationId ?? "Reservation ID missing." },
                    });
                }
            };
    }
}
