﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using MSHU.CarWash.Bot.CognitiveModels;
using MSHU.CarWash.Bot.Resources;
using MSHU.CarWash.Bot.Services;
using MSHU.CarWash.Bot.States;
using MSHU.CarWash.ClassLibrary.Enums;
using MSHU.CarWash.ClassLibrary.Extensions;
using Newtonsoft.Json.Linq;
using static MSHU.CarWash.ClassLibrary.Constants;

namespace MSHU.CarWash.Bot.Dialogs
{
    /// <summary>
    /// New reservation dialog.
    /// </summary>
    public class NewReservationDialog : ComponentDialog
    {
        // Dialogs
        private const string Name = "newReservation";
        private const string ServicesPromptName = "servicesPrompt";
        private const string RecommendedSlotsPromptName = "recommendedSlotsPrompt";

        private readonly IStatePropertyAccessor<NewReservationState> _stateAccessor;
        private readonly TelemetryClient _telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="NewReservationDialog"/> class.
        /// </summary>
        /// <param name="stateAccessor">The <see cref="ConversationState"/> for storing properties at conversation-scope.</param>
        public NewReservationDialog(IStatePropertyAccessor<NewReservationState> stateAccessor) : base(nameof(NewReservationDialog))
        {
            _stateAccessor = stateAccessor ?? throw new ArgumentNullException(nameof(stateAccessor));
            _telemetryClient = new TelemetryClient();

            var dialogSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                // PromptUsualStepAsync, // Can I start your usual order?
                PromptForServicesStepAsync,
                RecommendSlotsStepAsync,
                // PromptForDateStepAsync,
                // PromptForSlotStepAsync,
                // PromptForVehiclePlateNumberStepAsync,
                // PromptForPrivateSteapAsync,
                // PropmtForCommentStepAsync,
                // DisplayReservationStepAsync,
            };

            AddDialog(new WaterfallDialog(Name, dialogSteps));
            AddDialog(AuthDialog.LoginPromptDialog());
            AddDialog(new FindReservationDialog());

            AddDialog(new TextPrompt(ServicesPromptName, ValidateServices));
            AddDialog(new ChoicePrompt(RecommendedSlotsPromptName, ValidateRecommendedSlots));
        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            var state = await _stateAccessor.GetAsync(step.Context, () => null, cancellationToken);
            if (state == null)
            {
                if (step.Options is NewReservationState o) state = o;
                else state = new NewReservationState();

                await _stateAccessor.SetAsync(step.Context, state, cancellationToken);
            }

            // Load LUIS entities
            var options = step.Options as NewReservationDialogOptions ?? new NewReservationDialogOptions();
            foreach (var entity in options.LuisEntities)
            {
                switch (entity.Type)
                {
                    case LuisEntityType.Service:
                        var service = (ServiceModel)entity;
                        state.Services.Add(service.Service);
                        break;
                    case LuisEntityType.DateTime:
                        var dateTime = (CognitiveModels.DateTimeModel)entity;
                        state.Timex = dateTime.Timex;
                        break;
                    case LuisEntityType.Comment:
                        state.Comment = entity.Text;
                        break;
                    case LuisEntityType.Private:
                        state.Private = true;
                        break;
                    case LuisEntityType.VehiclePlateNumber:
                        state.VehiclePlateNumber = entity.Text;
                        break;
                }
            }

            // Load last reservation settings
            if (string.IsNullOrWhiteSpace(state.VehiclePlateNumber))
            {
                try
                {
                    var api = new CarwashService(step, cancellationToken);

                    state.LastSettings = await api.GetLastSettingsAsync(cancellationToken);
                }
                catch (AuthenticationException)
                {
                    await step.Context.SendActivityAsync(AuthDialog.NotAuthenticatedMessage, cancellationToken: cancellationToken);

                    return await step.EndDialogAsync(cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e);
                    await step.Context.SendActivityAsync("I am not able to access the CarWash app right now.", cancellationToken: cancellationToken);

                    return await step.EndDialogAsync(cancellationToken: cancellationToken);
                }
            }

            await _stateAccessor.SetAsync(step.Context, state, cancellationToken);

            return await step.NextAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> PromptForServicesStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            var state = await _stateAccessor.GetAsync(step.Context, cancellationToken: cancellationToken);

            if (state.Services.Count > 0) return await step.NextAsync(cancellationToken: cancellationToken);

            var response = step.Context.Activity.CreateReply();
            response.Attachments = new ServiceSelectionCard(state.LastSettings).ToAttachmentList();

            var activities = new IActivity[]
            {
                new Activity(type: ActivityTypes.Message, text: "Please select from these services!"),
                response,
            };
            await step.Context.SendActivitiesAsync(activities, cancellationToken);

            return await step.PromptAsync(
                ServicesPromptName,
                new PromptOptions { },
                cancellationToken);
        }

        private async Task<DialogTurnResult> RecommendSlotsStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            var state = await _stateAccessor.GetAsync(step.Context, cancellationToken: cancellationToken);

            if (state.Services.Count == 0)
            {
                state.Services = ParseServiceSelectionCardResponse(step.Context.Activity);
                await _stateAccessor.SetAsync(step.Context, state, cancellationToken);
            }

            var recommendedSlots = new List<DateTime>();

            try
            {
                var api = new CarwashService(step, cancellationToken);

                var notAvailable = await api.GetNotAvailableDatesAndTimesAsync(cancellationToken);
                recommendedSlots = GetRecommendedSlots(notAvailable);
            }
            catch (AuthenticationException)
            {
                await step.Context.SendActivityAsync(AuthDialog.NotAuthenticatedMessage, cancellationToken: cancellationToken);

                return await step.EndDialogAsync(cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                _telemetryClient.TrackException(e);
                await step.Context.SendActivityAsync("I am not able to access the CarWash app right now.", cancellationToken: cancellationToken);

                return await step.EndDialogAsync(cancellationToken: cancellationToken);
            }

            var choices = new List<Choice>();

            foreach (var slot in recommendedSlots)
            {
                var timex = TimexProperty.FromDateTime(slot);
                choices.Add(new Choice(timex.ToNaturalLanguage(DateTime.Now)));
            }

            return await step.PromptAsync(
                RecommendedSlotsPromptName,
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Can I recommend you one of these slots? If you want to choose something else, just say skip."),
                    Choices = choices,
                },
                cancellationToken);
        }

        /// <summary>
        /// Validator function to verify if at least one service was choosen.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateServices(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var services = ParseServiceSelectionCardResponse(promptContext.Context.Activity);

            if (services.Count > 0)
            {
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"Please choose at least one service!").ConfigureAwait(false);
                return false;
            }
        }

        /// <summary>
        /// Validator function to verify if at least one service was choosen.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateRecommendedSlots(PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken)
        {
            if (promptContext.Context.Activity.Text.ToLower() == "skip") return true;

            if (promptContext.Recognized.Succeeded)
            {
                // var dateTime = DateTimeRecognizer.RecognizeDateTime(promptContext.Recognized.Value.Value, Culture.English, DateTimeOptions.None, DateTime.Now);
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"Please choose one of the options or say skip!").ConfigureAwait(false);
                return false;
            }
        }

        private List<ServiceType> ParseServiceSelectionCardResponse(Activity activity)
        {
            var servicesStringArray = JObject.FromObject(activity.Value)?["services"]?.ToString()?.Split(',');
            var services = new List<ServiceType>();
            if (servicesStringArray == null) return services;
            foreach (var service in servicesStringArray)
            {
                if (Enum.TryParse(service, true, out ServiceType serviceType)) services.Add(serviceType);
            }

            return services;
        }

        private List<DateTime> GetRecommendedSlots(CarwashService.NotAvailableDatesAndTimes notAvailable)
        {
            var recommendedSlots = new List<DateTime>();

            // Try to find a slot today.
            var isOpenSlotToday = !notAvailable.Dates.Any(d => d == DateTime.Today);
            if (DateTime.Today.IsWeekend()) isOpenSlotToday = false;

            if (isOpenSlotToday)
            {
                try
                {
                    recommendedSlots.Add(FindOpenSlot(DateTime.Today));
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e);
                }
            }

            // Find the next next nearest slot (excluding today).
            var nextDayWithOpenSlots = DateTime.Today.AddDays(1);
            while (notAvailable.Dates.Contains(nextDayWithOpenSlots) || nextDayWithOpenSlots.IsWeekend()) nextDayWithOpenSlots = nextDayWithOpenSlots.AddDays(1);

            try
            {
                recommendedSlots.Add(FindOpenSlot(nextDayWithOpenSlots));
            }
            catch (Exception e)
            {
                _telemetryClient.TrackException(e);
            }

            // Find a slot next week.
            var nextWeek = GetNextWeekday(DayOfWeek.Monday);
            while (notAvailable.Dates.Contains(nextWeek) || nextWeek.IsWeekend()) nextWeek = nextWeek.AddDays(1);

            try
            {
                recommendedSlots.Add(FindOpenSlot(nextWeek));
            }
            catch (Exception e)
            {
                _telemetryClient.TrackException(e);
            }

            return recommendedSlots;

            DateTime FindOpenSlot(DateTime date)
            {
                foreach (var slot in Slots)
                {
                    if (!notAvailable.Times.Any(t => t.Date == date.Date && t.Hour == slot.StartTime))
                    {
                        return new DateTime(date.Year, date.Month, date.Day, slot.StartTime, 0, 0);
                    }
                }

                throw new ArgumentException($"No open slot found on the given date. The API should have specified this date in the not available dates array. Date: {date.ToShortDateString()}");
            }

            DateTime GetNextWeekday(DayOfWeek day)
            {
                DateTime result = DateTime.Today.AddDays(1);
                while (result.DayOfWeek != day) result = result.AddDays(1);

                return result;
            }
        }

        /// <summary>
        /// Options param type for <see cref="NewReservationDialog"/>.
        /// </summary>
        internal class NewReservationDialogOptions
        {
            /// <summary>
            /// Gets or sets the LUIS entities.
            /// </summary>
            /// <value>
            /// List of LUIS entities.
            /// </value>
            internal List<CognitiveModel> LuisEntities { get; set; }
        }
    }
}
