using GameHub.Data;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class BookingValidationService : IBookingValidationService
    {
        private static readonly BookingStatus[] BlockingStatuses =
        {
            BookingStatus.Pending, BookingStatus.Confirmed,
            BookingStatus.CheckedIn, BookingStatus.InProgress
        };

        private readonly ApplicationDbContext _dbContext;

        public BookingValidationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<BookingAvailabilityResult> CheckAvailabilityAsync(
            int courtId, DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId = null)
        {
            var result = new BookingAvailabilityResult();

            var court = await _dbContext.Courts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == courtId);
            if (court == null || !court.IsActive)
            {
                result.Message = "Court is inactive or does not exist.";
                return result;
            }
            result.CourtValid = true;

            if (court.Status is CourtStatus.Maintenance or CourtStatus.Closed)
            {
                result.Message = court.Status == CourtStatus.Maintenance
                    ? "This court is currently under maintenance."
                    : "This court is currently closed.";
                return result;
            }

            var schedule = await _dbContext.CourtSchedules.AsNoTracking()
                .FirstOrDefaultAsync(s => s.CourtId == courtId && s.DayOfWeek == date.DayOfWeek && s.IsActive);
            if (schedule == null || schedule.IsClosed)
            {
                result.Message = "This court has no operating schedule for the selected day.";
                return result;
            }
            result.ScheduleValid = true;

            if (start < schedule.OpeningTime || end > schedule.ClosingTime)
            {
                result.Message = $"Requested time is outside operating hours ({schedule.OpeningTime:HH\\:mm} - {schedule.ClosingTime:HH\\:mm}).";
                return result;
            }
            result.WithinHours = true;

            var buffer = Math.Max(0, schedule.BufferMinutes);
            var bufferedStart = start.AddMinutes(-buffer);
            var bufferedEnd = end.AddMinutes(buffer);

            var hasConflict = await _dbContext.Bookings.AnyAsync(b =>
                b.CourtId == courtId &&
                b.BookingDate == date &&
                b.Id != excludeBookingId &&
                b.IsActive &&
                BlockingStatuses.Contains(b.Status) &&
                b.StartTime < bufferedEnd &&
                b.EndTime > bufferedStart);

            result.NoConflict = !hasConflict;
            if (hasConflict)
            {
                result.Message = "This time slot overlaps with an existing active booking.";
                result.ConflictDetail = $"Court {court.Name} on {date:MMM dd, yyyy} from {start:HH\\:mm} to {end:HH\\:mm} has a conflict.";
            }
            else
            {
                result.IsAvailable = true;
                result.Message = "Available";
            }

            return result;
        }

        public async Task<bool> HasActiveConflictAsync(
            int courtId, DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId = null)
        {
            var schedule = await _dbContext.CourtSchedules.AsNoTracking()
                .FirstOrDefaultAsync(s => s.CourtId == courtId && s.DayOfWeek == date.DayOfWeek && s.IsActive);
            if (schedule == null || schedule.IsClosed) return true;

            var buffer = Math.Max(0, schedule.BufferMinutes);
            var bufferedStart = start.AddMinutes(-buffer);
            var bufferedEnd = end.AddMinutes(buffer);

            return await _dbContext.Bookings.AnyAsync(b =>
                b.CourtId == courtId &&
                b.BookingDate == date &&
                b.Id != excludeBookingId &&
                b.IsActive &&
                BlockingStatuses.Contains(b.Status) &&
                b.StartTime < bufferedEnd &&
                b.EndTime > bufferedStart);
        }

        public async Task<BookingValidationResult> ValidateBookingAsync(
            int customerId, int sportId, int facilityId, int courtId,
            DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime,
            int durationMinutes, int playerCount, decimal manualDiscountAmount,
            BookingSource source, bool isWalkIn, int? excludeBookingId = null)
        {
            var result = new BookingValidationResult { IsValid = true };
            var settings = await GetSettingsAsync();

            if (endTime <= startTime)
            {
                result.Errors.Add("End time must be after start time.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (bookingDate < today)
            {
                result.Errors.Add("Booking date cannot be in the past.");
            }

            if (bookingDate > today.AddDays(settings.AdvanceBookingDays))
            {
                result.Errors.Add($"Booking date cannot exceed {settings.AdvanceBookingDays} days in advance.");
            }

            if (isWalkIn && !settings.AllowWalkIn)
            {
                result.Errors.Add("Walk-in bookings are currently disabled.");
            }

            if (source == BookingSource.Website && !settings.AllowOnlineBooking)
            {
                result.Errors.Add("Online booking source is currently disabled.");
            }

            var customer = await _dbContext.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer == null || !customer.IsActive)
            {
                result.Errors.Add("Selected customer is invalid or inactive.");
            }
            else if (customer.IsBlacklisted)
            {
                result.Errors.Add("Blacklisted customers cannot create bookings.");
            }

            var court = await _dbContext.Courts.AsNoTracking()
                .Include(c => c.Sport).Include(c => c.Facility)
                .FirstOrDefaultAsync(c => c.Id == courtId);
            if (court == null || !court.IsActive)
            {
                result.Errors.Add("Selected court is invalid or inactive.");
            }
            else
            {
                if (court.SportId != sportId || court.FacilityId != facilityId)
                {
                    result.Errors.Add("Court does not match the selected sport and facility.");
                }

                if (court.Status is CourtStatus.Maintenance or CourtStatus.Closed)
                {
                    result.Errors.Add(court.Status == CourtStatus.Maintenance
                        ? "Selected court is currently under maintenance."
                        : "Selected court is currently closed.");
                }
            }

            var availability = await CheckAvailabilityAsync(courtId, bookingDate, startTime, endTime, excludeBookingId);
            result.Availability = availability;
            if (!availability.IsAvailable)
            {
                result.Errors.Add(availability.Message);
            }

            if (manualDiscountAmount < 0)
            {
                result.Errors.Add("Manual discount cannot be negative.");
            }

            if (durationMinutes <= 0)
            {
                result.Errors.Add("Duration must be greater than zero.");
            }

            if (playerCount < 1)
            {
                result.Errors.Add("Player count must be at least 1.");
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        public async Task<BookingValidationResult> ValidateRescheduleAsync(
            int bookingId, int courtId, DateOnly bookingDate,
            TimeOnly startTime, TimeOnly endTime, int durationMinutes)
        {
            var booking = await _dbContext.Bookings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null)
            {
                return new BookingValidationResult
                {
                    IsValid = false,
                    Errors = { "Booking not found." }
                };
            }

            if (!IsEditableStatus(booking.Status))
            {
                return new BookingValidationResult
                {
                    IsValid = false,
                    Errors = { "This booking can no longer be modified." }
                };
            }

            return await ValidateBookingAsync(
                booking.CustomerId, booking.SportId, booking.FacilityId, courtId,
                bookingDate, startTime, endTime, durationMinutes,
                booking.PlayerCount, booking.ManualDiscountAmount,
                booking.Source, booking.IsWalkIn, excludeBookingId: bookingId);
        }

        private static bool IsEditableStatus(BookingStatus status) =>
            status is BookingStatus.Draft or BookingStatus.Pending or BookingStatus.Confirmed;

        private async Task<SystemSettings> GetSettingsAsync() =>
            await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync()
            ?? new SystemSettings
            {
                ArenaName = "GameHub Arena", CurrencySymbol = "Rs",
                TaxPercentage = 0, OpeningTime = new TimeOnly(6, 0),
                ClosingTime = new TimeOnly(23, 0), SlotDurationMinutes = 60,
                BufferMinutes = 10, AdvanceBookingDays = 14,
                AllowWalkIn = true, AllowOnlineBooking = true,
                BookingPrefix = "BKG", Currency = "PKR",
                Phone = "", Email = "", InvoicePrefix = "INV",
                ReceiptPrefix = "RCPT"
            };
    }
}
