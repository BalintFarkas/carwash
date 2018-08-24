﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSHU.CarWash.ClassLibrary;
using MSHU.CarWash.PWA.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MSHU.CarWash.PWA.Controllers
{
    /// <summary>
    /// Managing reservations
    /// </summary>
    [Produces("application/json")]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReservationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly User _user;
        private readonly ICalendarService _calendarService;

        /// <summary>
        /// Wash time unit in minutes
        /// </summary>
        private const int TimeUnit = 12;

        /// <summary>
        /// Number of concurrent active reservations permitted
        /// </summary>
        private const int UserConcurrentReservationLimit = 2;

        /// <summary>
        /// Daily limits per company
        /// </summary>
        private static readonly List<Company> CompanyLimit = new List<Company>
        {
            new Company(Company.Carwash, 0),
            new Company(Company.Microsoft, 14),
            new Company(Company.Sap, 16),
            new Company(Company.Graphisoft, 5)
        };

        /// <summary>
        /// Bookable slots and their capacity (in washes and not in minutes!)
        /// </summary>
        private static readonly List<Slot> Slots = new List<Slot>
        {
            new Slot {StartTime = 8, EndTime = 11, Capacity = 12},
            new Slot {StartTime = 11, EndTime = 14, Capacity = 12},
            new Slot {StartTime = 14, EndTime = 17, Capacity = 11}
        };

        /// <inheritdoc />
        public ReservationsController(ApplicationDbContext context, UsersController usersController, ICalendarService calendarService)
        {
            _context = context;
            _user = usersController.GetCurrentUser();
            _calendarService = calendarService;
        }

        // GET: api/reservations
        /// <summary>
        /// Get my reservations
        /// </summary>
        /// <returns>List of <see cref="ReservationViewModel"/></returns>
        /// <response code="200">OK</response>
        /// <response code="401">Unathorized</response>
        [ProducesResponseType(typeof(IEnumerable<ReservationViewModel>), 200)]
        [HttpGet]
        public IEnumerable<object> GetReservation()
        {
            return _context.Reservation
                .Where(r => r.UserId == _user.Id)
                .OrderByDescending(r => r.StartDate)
                .Select(reservation => new ReservationViewModel(reservation));
        }

        // GET: api/reservations/{id}
        /// <summary>
        /// Get a specific reservation by id
        /// </summary>
        /// <param name="id">reservation id</param>
        /// <returns><see cref="ReservationViewModel"/></returns>
        /// <response code="200">OK</response>
        /// <response code="400">BadRequest if <paramref name="id"/> is missing or not well-formated.</response>
        /// <response code="401">Unathorized</response>
        /// <response code="403">Forbidden if user is not admin but tries to get another user's reservation.</response>
        /// <response code="404">NotFound if reservation not found.</response>
        [ProducesResponseType(typeof(ReservationViewModel), 200)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReservation([FromRoute] string id)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var reservation = await _context.Reservation.FindAsync(id);

            if (reservation == null) return NotFound();

            if (reservation.UserId != _user.Id && !(_user.IsAdmin || _user.IsCarwashAdmin)) return Forbid();

            return Ok(new ReservationViewModel(reservation));
        }

        // PUT: api/reservations/{id}
        /// <summary>
        /// Update an existing reservation
        /// </summary>
        /// <param name="id">Reservation id</param>
        /// <param name="reservation"><see cref="Reservation"/></param>
        /// <returns>No content</returns>
        /// <response code="200">OK</response>
        /// <response code="400">BadRequest if no service choosen / StartDate and EndDate isn't on the same day / a Date is in the past / StartDate and EndDate are not valid slot start/end times / user/company limit has been met / there is no more time in that slot.</response>
        /// <response code="401">Unathorized</response>
        /// <response code="403">Forbidden if user is not admin but tries to update another user's reservation.</response>
        [ProducesResponseType(typeof(ReservationViewModel), 200)]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutReservation([FromRoute] string id, [FromBody] Reservation reservation)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (id != reservation.Id) return BadRequest();

            var dbReservation = await _context.Reservation.FindAsync(id);

            if (dbReservation == null) return NotFound();

            if (dbReservation.UserId != _user.Id && !(_user.IsAdmin || _user.IsCarwashAdmin)) return Forbid();
            if (reservation.UserId == null) reservation.UserId = _user.Id;
            if (reservation.UserId != _user.Id && !(_user.IsAdmin || _user.IsCarwashAdmin)) return Forbid();

            dbReservation.VehiclePlateNumber = reservation.VehiclePlateNumber.ToUpper();
            dbReservation.Location = reservation.Location;
            dbReservation.Services = reservation.Services;
            dbReservation.Private = reservation.Private ?? false;
            dbReservation.StartDate = reservation.StartDate.ToLocalTime();
            dbReservation.Comment = reservation.Comment;

            if (!Slots.Any(s => s.StartTime == dbReservation.StartDate.Hour))
                return BadRequest("Reservation can be made to slots only.");
            if (reservation.EndDate == null)
                dbReservation.EndDate = new DateTime(
                    dbReservation.StartDate.Year,
                    dbReservation.StartDate.Month,
                    dbReservation.StartDate.Day,
                    Slots.Find(s => s.StartTime == dbReservation.StartDate.Hour).EndTime,
                    0, 0);
            else dbReservation.EndDate = ((DateTime)reservation.EndDate).ToLocalTime();

            // Validation
            if (dbReservation.Services == null) return BadRequest("No service choosen.");
            if (dbReservation.StartDate.Date != ((DateTime)dbReservation.EndDate).Date)
                return BadRequest("Reservation date range should be located entirely on the same day.");
            if (dbReservation.StartDate < DateTime.Now || dbReservation.EndDate < DateTime.Now)
                return BadRequest("Cannot reserve in the past.");
            if (!Slots.Any(s => s.StartTime == dbReservation.StartDate.Hour && s.EndTime == ((DateTime)dbReservation.EndDate).Hour))
                return BadRequest("Reservation can be made to slots only.");

            // Time requirement calculation
            dbReservation.TimeRequirement = dbReservation.Services.Contains(ServiceType.Carpet) ? 2 * TimeUnit : TimeUnit;

            #region Business logic
            // Check if there is enough time on that day
            if (!IsEnoughTimeOnDate(dbReservation.StartDate, (int)dbReservation.TimeRequirement))
                return BadRequest("Company limit has been met for this day or there is not enough time at all.");

            // Check if there is enough time in that slot
            if (!IsEnoughTimeInSlot(dbReservation.StartDate, (int)dbReservation.TimeRequirement))
                return BadRequest("There is not enough time in that slot.");
            #endregion

            // Update calendar event using Microsoft Graph
            if (dbReservation.UserId == _user.Id)
            {
                dbReservation.User = _user;
                dbReservation.OutlookEventId = await _calendarService.UpdateEventAsync(dbReservation);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Reservation.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok(new ReservationViewModel(dbReservation));
        }

        // POST: api/Reservations
        /// <summary>
        /// Add a new reservation
        /// </summary>
        /// <param name="reservation"><see cref="Reservation"/></param>
        /// <returns>The newly created <see cref="Reservation"/></returns>
        /// <response code="201">Created</response>
        /// <response code="400">BadRequest if no service choosen / StartDate and EndDate isn't on the same day / a Date is in the past / StartDate and EndDate are not valid slot start/end times / user/company limit has been met / there is no more time in that slot.</response>
        /// <response code="401">Unathorized</response>
        /// <response code="403">Forbidden if user is not admin but tries to reserve for another user.</response>
        [ProducesResponseType(typeof(ReservationViewModel), 201)]
        [HttpPost]
        public async Task<IActionResult> PostReservation([FromBody] Reservation reservation)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Defaults
            if (reservation.UserId == null) reservation.UserId = _user.Id;
            if (reservation.Private == null) reservation.Private = false;
            reservation.State = State.SubmittedNotActual;
            reservation.Mpv = false;
            reservation.VehiclePlateNumber = reservation.VehiclePlateNumber.ToUpper();
            reservation.CarwashComment = null;
            reservation.CreatedById = _user.Id;
            reservation.CreatedOn = DateTime.Now;
            reservation.StartDate = reservation.StartDate.ToLocalTime();

            if (!Slots.Any(s => s.StartTime == reservation.StartDate.Hour))
                return BadRequest("Reservation can be made to slots only.");
            if (reservation.EndDate == null)
                reservation.EndDate = new DateTime(
                    reservation.StartDate.Year,
                    reservation.StartDate.Month,
                    reservation.StartDate.Day,
                    Slots.Find(s => s.StartTime == reservation.StartDate.Hour).EndTime,
                    0, 0);
            else reservation.EndDate = ((DateTime)reservation.EndDate).ToLocalTime();

            // Validation
            if (reservation.UserId != _user.Id && !(_user.IsAdmin || _user.IsCarwashAdmin)) return Forbid();
            if (reservation.Services == null) return BadRequest("No service choosen.");
            if (reservation.StartDate.Date != ((DateTime)reservation.EndDate).Date)
                return BadRequest("Reservation date range should be located entirely on the same day.");
            if (reservation.StartDate < DateTime.Now || reservation.EndDate < DateTime.Now)
                return BadRequest("Cannot reserve in the past.");
            if (!Slots.Any(s => s.StartTime == reservation.StartDate.Hour && s.EndTime == ((DateTime)reservation.EndDate).Hour))
                return BadRequest("Reservation can be made to slots only.");

            // Time requirement calculation
            reservation.TimeRequirement = reservation.Services.Contains(ServiceType.Carpet) ? 2 * TimeUnit : TimeUnit;

            #region Business logic
            // Checks whether user has met the active concurrent reservation limit
            if (await IsUserConcurrentReservationLimitMetAsync())
                return BadRequest($"Cannot have more than {UserConcurrentReservationLimit} concurrent active reservations.");

            // Check if there is enough time on that day
            if (!IsEnoughTimeOnDate(reservation.StartDate, (int)reservation.TimeRequirement))
                return BadRequest("Company limit has been met for this day or there is not enough time at all.");

            // Check if there is enough time in that slot
            if (!IsEnoughTimeInSlot(reservation.StartDate, (int)reservation.TimeRequirement))
                return BadRequest("There is not enough time in that slot.");
            #endregion

            // Add calendar event using Microsoft Graph
            if (reservation.UserId == _user.Id)
            {
                reservation.User = _user;
                reservation.OutlookEventId = await _calendarService.CreateEventAsync(reservation);
            }

            _context.Reservation.Add(reservation);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetReservation", new { id = reservation.Id }, new ReservationViewModel(reservation));
        }

        // DELETE: api/Reservations/5
        /// <summary>
        /// Delete an existing reservation
        /// </summary>
        /// <param name="id">reservation id</param>
        /// <returns>The deleted <see cref="Reservation"/></returns>
        /// <response code="200">OK</response>
        /// <response code="400">BadRequest if <paramref name="id"/> is missing or not well-formated.</response>
        /// <response code="401">Unathorized</response>
        /// <response code="403">Forbidden if user is not admin but tries to delete another user's reservation.</response>
        /// <response code="404">NotFound if reservation not found.</response>
        [ProducesResponseType(typeof(ReservationViewModel), 200)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReservation([FromRoute] string id)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var reservation = await _context.Reservation.FindAsync(id);
            if (reservation == null) return NotFound();

            if (reservation.UserId != _user.Id && !(_user.IsAdmin || _user.IsCarwashAdmin)) return Forbid();

            _context.Reservation.Remove(reservation);
            await _context.SaveChangesAsync();

            // Delete calendar event using Microsoft Graph
            await _calendarService.DeleteEventAsync(reservation.OutlookEventId);

            return Ok(new ReservationViewModel(reservation));
        }

        // GET: api/reservations/company
        /// <summary>
        /// Get reservations in my company
        /// </summary>
        /// <returns>List of <see cref="AdminReservationViewModel"/></returns>
        /// <response code="200">OK</response>
        /// <response code="401">Unathorized</response>
        /// <response code="403">Forbidden if user is not admin.</response>
        [ProducesResponseType(typeof(IEnumerable<AdminReservationViewModel>), 200)]
        [HttpGet, Route("company")]
        public async Task<IActionResult> GetCompanyReservations()
        {
            if (!_user.IsAdmin) return Forbid();

            var reservations = await _context.Reservation
                .Include(r => r.User)
                .Where(r => r.User.Company == _user.Company && r.UserId != _user.Id)
                .OrderByDescending(r => r.StartDate)
                .Select(reservation => new AdminReservationViewModel
                {
                    Id = reservation.Id,
                    UserId = reservation.UserId,
                    VehiclePlateNumber = reservation.VehiclePlateNumber,
                    Location = reservation.Location,
                    State = reservation.State,
                    Services = reservation.Services,
                    Private = reservation.Private,
                    Mpv = reservation.Mpv,
                    StartDate = reservation.StartDate,
                    EndDate = (DateTime)reservation.EndDate,
                    Comment = reservation.Comment,
                    CarwashComment = reservation.CarwashComment,
                    User = new UserViewModel
                    {
                        Id = reservation.User.Id,
                        FirstName = reservation.User.FirstName,
                        LastName = reservation.User.LastName,
                        Company = reservation.User.Company,
                        IsAdmin = reservation.User.IsAdmin,
                        IsCarwashAdmin = reservation.User.IsCarwashAdmin
                    }
                })
                .ToListAsync();

            return Ok(reservations);
        }

        // GET: api/reservations/obfuscated
        /// <summary>
        /// Get all future reservation data for the next <paramref name="daysAhead"/> days
        /// </summary>
        /// <param name="daysAhead">Days ahead to return reservation data</param>
        /// <returns>List of <see cref="ReservationViewModel"/></returns>
        /// <response code="200">OK</response>
        /// <response code="401">Unathorized</response>
        [ProducesResponseType(typeof(IEnumerable<ObfuscatedReservationViewModel>), 200)]
        [HttpGet, Route("obfuscated")]
        public IEnumerable<object> GetObfuscatedReservations(int daysAhead = 365)
        {
            return _context.Reservation
                .Where(r => r.EndDate >= DateTime.Now && r.StartDate <= DateTime.Now.AddDays(daysAhead))
                .Include(r => r.User)
                .OrderBy(r => r.StartDate)
                .Select(reservation => new ObfuscatedReservationViewModel
                {
                    Company = reservation.User.Company,
                    Services = reservation.Services,
                    TimeRequirement = reservation.TimeRequirement,
                    StartDate = reservation.StartDate,
                    EndDate = (DateTime)reservation.EndDate,
                });
        }

        // GET: api/reservations/notavailabledates
        /// <summary>
        /// Get the list of future dates that are not available
        /// </summary>
        /// <returns>List of <see cref="DateTime"/></returns>
        /// <response code="200">OK</response>
        /// <response code="401">Unathorized</response>
        [ProducesResponseType(typeof(NotAvailableDatesAndTimesViewModel), 200)]
        [HttpGet, Route("notavailabledates")]
        public async Task<object> GetNotAvailableDatesAndTimes(int daysAhead = 365)
        {
            /*
             * Get not available dates
             */

            var userCompanyLimit = CompanyLimit.Find(c => c.Name == _user.Company).DailyLimit;

            // Must be separated to force client evaluation because of this EF issue:
            // https://github.com/aspnet/EntityFrameworkCore/issues/11453
            // Current milestone to be fixed is EF 3.0.0
            var queryResult = await _context.Reservation
                .Where(r => r.EndDate >= DateTime.Now && r.StartDate <= DateTime.Now.AddDays(daysAhead))
                .Include(r => r.User)
                .Where(r => r.User.Company == _user.Company)
                .GroupBy(r => r.StartDate.Date)
                .Select(g => new
                {
                    g.Key.Date,
                    TimeSum = g.Sum(r => r.TimeRequirement)
                })
                .Where(d => d.TimeSum >= userCompanyLimit * TimeUnit)
                .ToListAsync();

            var notAvailableDates = queryResult.Select(d => d.Date).ToList();

            if (!notAvailableDates.Contains(DateTime.Today))
            {
                // Cannot use SumAsync because of this EF issue:
                // https://github.com/aspnet/EntityFrameworkCore/issues/12314
                // Current milestone to be fixed is EF 2.1.3
                var toBeDoneTodayTime = _context.Reservation
                    .Where(r => r.StartDate >= DateTime.Now && r.StartDate.Date == DateTime.Today)
                    .Sum(r => r.TimeRequirement);
                if (toBeDoneTodayTime >= GetRemainingSlotCapacityToday() * TimeUnit) notAvailableDates.Add(DateTime.Today);
            }

            /*
             * Get not available times
             */

            var slotReservationAggregate = await _context.Reservation
                .Where(r => r.EndDate >= DateTime.Now && r.StartDate <= DateTime.Now.AddDays(daysAhead))
                .GroupBy(r => r.StartDate)
                .Select(g => new
                {
                    DateTime = g.Key,
                    TimeSum = g.Sum(r => r.TimeRequirement)
                })
                .ToListAsync();

            var notAvailableTimes = slotReservationAggregate
                .Where(d => d.TimeSum >= Slots.Find(s => s.StartTime == d.DateTime.Hour)?.Capacity * TimeUnit)
                .Select(d => d.DateTime)
                .ToList();

            return new NotAvailableDatesAndTimesViewModel { Dates = notAvailableDates, Times = notAvailableTimes };
        }

        // GET: api/reservations/lastsettings
        /// <summary>
        /// Get some settings from the last reservation made by the user to be used as defaults for a new reservation
        /// </summary>
        /// <returns>an object containing the plate number and location last used</returns>
        /// <response code="200">OK</response>
        /// <response code="204">NoContent if user has no reservation yet.</response>
        /// <response code="401">Unathorized</response>
        [ProducesResponseType(typeof(LastSettingsViewModel), 200)]
        [HttpGet, Route("lastsettings")]
        public async Task<IActionResult> GetLastSettings()
        {
            var lastReservation = await _context.Reservation
                .Where(r => r.UserId == _user.Id)
                .OrderByDescending(r => r.CreatedOn)
                .FirstOrDefaultAsync();

            if (lastReservation == null) return NoContent();

            return Ok(new LastSettingsViewModel
            {
                VehiclePlateNumber = lastReservation.VehiclePlateNumber,
                Location = lastReservation.Location
            });
        }

        // GET: api/reservations/reservationprecentage
        /// <summary>
        /// Gets a list of slots and their reservation precentage on a given date
        /// </summary>
        /// <param name="date">the date to filter on</param>
        /// <returns>List of <see cref="ReservationPrecentageViewModel"/></returns>
        [ProducesResponseType(typeof(List<ReservationPrecentageViewModel>), 200)]
        [HttpGet, Route("reservationprecentage")]
        public async Task<IActionResult> GetReservationPrecentage(DateTime date)
        {
            var slotReservationAggregate = await _context.Reservation
                .Where(r => r.StartDate.Date == date.Date)
                .GroupBy(r => r.StartDate)
                .Select(g => new
                {
                    DateTime = g.Key,
                    TimeSum = g.Sum(r => r.TimeRequirement)
                })
                .ToListAsync();

            var slotReservationPrecentage = new List<ReservationPrecentageViewModel>();
            foreach (var a in slotReservationAggregate)
            {
                var slotCapacity = Slots.Find(s => s.StartTime == a.DateTime.Hour)?.Capacity;
                if (slotCapacity == null) continue;
                slotReservationPrecentage.Add(new ReservationPrecentageViewModel
                {
                    StartTime = a.DateTime,
                    Precentage = a.TimeSum == null || a.TimeSum == 0 ? 0 : Math.Round((double)a.TimeSum / (double)(slotCapacity * TimeUnit), 2)
                });
            }

            return Ok(slotReservationPrecentage);
        }

        /// <summary>
        /// Sums the capacity of all not started slots, what are left from the day
        /// </summary>
        /// <remarks>
        /// eg. It is 9:00 AM.
        /// The slot 8-11 has already started.
        /// The slot 11-14 is not yet started, so add the capacity (eg. 12) to the sum.
        /// The slot 14-17 is not yet started, so add the capacity (eg. 11) to the sum.
        /// Sum will be 23.
        /// </remarks>
        /// <returns>Capacity of slots (not time in minutes!)</returns>
        private static int GetRemainingSlotCapacityToday()
        {
            var capacity = 0;

            foreach (var slot in Slots)
            {
                if (DateTime.Now.Hour < slot.StartTime) capacity += slot.Capacity;
            }

            return capacity;
        }

        /// <summary>
        /// Checks whether user has met the active concurrent reservation limit: <see cref="UserConcurrentReservationLimit"/>
        /// </summary>
        /// <returns>true if user has met the limit and is not admin</returns>
        private async Task<bool> IsUserConcurrentReservationLimitMetAsync()
        {
            if (_user.IsAdmin || _user.IsCarwashAdmin) return false;

            var activeReservationCount = await _context.Reservation.Where(r => r.UserId == _user.Id && r.State != State.Done).CountAsync();

            return activeReservationCount >= UserConcurrentReservationLimit;
        }

        /// <summary>
        /// Checks if there is enough time on that day
        /// </summary>
        /// <param name="date">Date of reservation</param>
        /// <param name="timeRequirement">time requirement of the reservation in minutes</param>
        /// <returns>true if there is enough time left or user is carwash admin</returns>
        private bool IsEnoughTimeOnDate(DateTime date, int timeRequirement)
        {
            if (_user.IsCarwashAdmin) return true;

            var userCompanyLimit = CompanyLimit.Find(c => c.Name == _user.Company).DailyLimit;

            // Cannot use SumAsync because of this EF issue:
            // https://github.com/aspnet/EntityFrameworkCore/issues/12314
            // Current milestone to be fixed is EF 2.1.3
            var reservedTimeOnDate = _context.Reservation
                .Where(r => r.StartDate.Date == date.Date && r.User.Company == _user.Company)
                .Sum(r => r.TimeRequirement);

            if (reservedTimeOnDate + timeRequirement > userCompanyLimit * TimeUnit) return false;

            if (date.Date == DateTime.Today)
            {
                // Cannot use SumAsync because of this EF issue:
                // https://github.com/aspnet/EntityFrameworkCore/issues/12314
                // Current milestone to be fixed is EF 2.1.3
                var toBeDoneTodayTime = _context.Reservation
                    .Where(r => r.StartDate >= DateTime.Now && r.StartDate.Date == DateTime.Today)
                    .Sum(r => r.TimeRequirement);
                if (toBeDoneTodayTime + timeRequirement > GetRemainingSlotCapacityToday() * TimeUnit) return false;
            }

            return true;
        }

        /// <summary>
        /// Check if there is enough time in that slot
        /// </summary>
        /// <param name="dateTime">Date and time of reservation</param>
        /// <param name="timeRequirement">time requirement of the reservation in minutes</param>
        /// <returns>true if there is enough time left or user is carwash admin</returns>
        private bool IsEnoughTimeInSlot(DateTime dateTime, int timeRequirement)
        {
            if (_user.IsCarwashAdmin) return true;

            // Cannot use SumAsync because of this EF issue:
            // https://github.com/aspnet/EntityFrameworkCore/issues/12314
            // Current milestone to be fixed is EF 2.1.3
            var reservedTimeInSlot = _context.Reservation
                .Where(r => r.StartDate == dateTime)
                .Sum(r => r.TimeRequirement);

            return reservedTimeInSlot + timeRequirement <=
                   Slots.Find(s => s.StartTime == dateTime.Hour)?.Capacity * TimeUnit;
        }
    }

    internal class ReservationViewModel
    {
        public ReservationViewModel() { }

        public ReservationViewModel(Reservation reservation)
        {
            Id = reservation.Id;
            UserId = reservation.UserId;
            VehiclePlateNumber = reservation.VehiclePlateNumber;
            Location = reservation.Location;
            State = reservation.State;
            Services = reservation.Services;
            Private = reservation.Private;
            Mpv = reservation.Mpv;
            StartDate = reservation.StartDate;
            if (reservation.EndDate != null) EndDate = (DateTime)reservation.EndDate;
            Comment = reservation.Comment;
            CarwashComment = reservation.CarwashComment;
        }

        public string Id { get; set; }
        public string UserId { get; set; }
        public string VehiclePlateNumber { get; set; }
        public string Location { get; set; }
        public State State { get; set; }
        public List<ServiceType> Services { get; set; }
        public bool? Private { get; set; }
        public bool? Mpv { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Comment { get; set; }
        public string CarwashComment { get; set; }
    }

    internal class AdminReservationViewModel
    {
        public AdminReservationViewModel() { }

        public string Id { get; set; }
        public string UserId { get; set; }
        public UserViewModel User { get; set; }
        public string VehiclePlateNumber { get; set; }
        public string Location { get; set; }
        public State State { get; set; }
        public List<ServiceType> Services { get; set; }
        public bool? Private { get; set; }
        public bool? Mpv { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Comment { get; set; }
        public string CarwashComment { get; set; }
    }

    internal class ObfuscatedReservationViewModel
    {
        public ObfuscatedReservationViewModel() { }

        public ObfuscatedReservationViewModel(Reservation reservation)
        {
            Company = reservation.User.Company;
            Services = reservation.Services;
            TimeRequirement = reservation.TimeRequirement;
            StartDate = reservation.StartDate;
            if (reservation.EndDate != null) EndDate = (DateTime)reservation.EndDate;
        }

        public string Company { get; set; }
        public List<ServiceType> Services { get; set; }
        public int? TimeRequirement { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    internal class NotAvailableDatesAndTimesViewModel
    {
        public IEnumerable<DateTime> Dates { get; set; }
        public IEnumerable<DateTime> Times { get; set; }
    }

    internal class LastSettingsViewModel
    {
        public string VehiclePlateNumber { get; set; }
        public string Location { get; set; }
    }

    internal class ReservationPrecentageViewModel
    {
        public DateTime StartTime { get; set; }
        public double Precentage { get; set; }
    }

    internal class Slot
    {
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public int Capacity { get; set; }
    }
}