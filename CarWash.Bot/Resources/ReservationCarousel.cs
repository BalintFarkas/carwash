﻿using System.Collections.Generic;
using CarWash.ClassLibrary.Enums;
using CarWash.ClassLibrary.Models;
using Microsoft.Bot.Schema;

namespace CarWash.Bot.Resources
{
    /// <summary>
    /// Reservation chooser, carousel.
    /// </summary>
    public class ReservationCarousel
    {
        private readonly List<ThumbnailCard> _cards = new List<ThumbnailCard>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ReservationCarousel"/> class.
        /// </summary>
        /// <param name="reservations">List of reservations to be displayed in the carousel.</param>
        public ReservationCarousel(IEnumerable<Reservation> reservations)
        {
            foreach (var reservation in reservations)
            {
                var services = new List<string>();
                reservation.Services.ForEach(s => services.Add(s.ToFriendlyString()));

                _cards.Add(new ThumbnailCard
                {
                    Title = reservation.VehiclePlateNumber,
                    Subtitle = reservation.StartDate.ToString("MMMM d, h:mm tt") + reservation.EndDate?.ToString(" - h:mm tt"),
                    Text = string.Join(", ", services),
                    Images = new List<CardImage> { new CardImage($"https://carwashu.azurewebsites.net/images/state{(int)reservation.State}.png") },
                    Buttons = new List<CardAction> { new CardAction(ActionTypes.PostBack, "This one", value: reservation.Id) },
                });
            }
        }

        /// <summary>
        /// Converts the list of cards to a list of attachments.
        /// </summary>
        /// <returns>A list of attachments containing the cards.</returns>
        public List<Attachment> ToAttachmentList()
        {
            var attachments = new List<Attachment>();

            foreach (var card in _cards)
            {
                attachments.Add(card.ToAttachment());
            }

            return attachments;
        }
    }
}
