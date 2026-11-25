using System.Globalization;

namespace RoomBooking.Domain;

/// <summary>The rules a new booking must satisfy.</summary>
/// <remarks>All times are office-local; no time zone conversion happens here.</remarks>
public static class BookingRules
{
    public static readonly TimeOnly OpeningTime = new(9, 0);
    public static readonly TimeOnly ClosingTime = new(19, 0);
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(15);

    public const int TitleMaxLength = 200;
    public const int OrganizerMaxLength = 100;

    /// <summary>Checks a new booking against the rules.</summary>
    /// <param name="request">The booking to check.</param>
    /// <param name="existingBookings">
    /// Bookings that already exist. Bookings of other rooms are ignored, so the caller may pass more than needed.
    /// </param>
    /// <returns>Every broken rule; an empty list means the booking is valid.</returns>
    public static IReadOnlyList<BookingError> Validate(BookingRequest request, IEnumerable<Booking> existingBookings)
    {
        var errors = new List<BookingError>();

        CheckText(request.Title, "A topic", TitleMaxLength,
            BookingErrorCode.TitleRequired, BookingErrorCode.TitleTooLong, errors);
        CheckText(request.Organizer, "The organizer's name", OrganizerMaxLength,
            BookingErrorCode.OrganizerRequired, BookingErrorCode.OrganizerTooLong, errors);

        var timeErrors = CheckTimes(request.Start, request.End);
        errors.AddRange(timeErrors);

        // Overlap only makes sense for an interval that is valid on its own.
        if (timeErrors.Count == 0)
        {
            var conflict = existingBookings
                .Where(b => b.RoomId == request.RoomId && Overlaps(request.Start, request.End, b.Start, b.End))
                .OrderBy(b => b.Start)
                .FirstOrDefault();

            if (conflict is not null)
            {
                errors.Add(new BookingError(BookingErrorCode.Overlap, Invariant(
                    $"The room is already booked from {conflict.Start:HH:mm} to {conflict.End:HH:mm} (\"{conflict.Title}\").")));
            }
        }

        return errors;
    }

    /// <summary>
    /// Whether two half-open intervals [aStart, aEnd) and [bStart, bEnd) overlap.
    /// Back-to-back intervals (one ends exactly when the other starts) do not overlap.
    /// </summary>
    public static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart < bEnd && bStart < aEnd;

    private static void CheckText(string? value, string what, int maxLength,
        BookingErrorCode requiredCode, BookingErrorCode tooLongCode, List<BookingError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new BookingError(requiredCode, $"{what} is required."));
        }
        else if (value.Trim().Length > maxLength)
        {
            errors.Add(new BookingError(tooLongCode, $"{what} must be at most {maxLength} characters long."));
        }
    }

    private static List<BookingError> CheckTimes(DateTime start, DateTime end)
    {
        var errors = new List<BookingError>();

        if (start >= end)
        {
            errors.Add(new BookingError(BookingErrorCode.StartNotBeforeEnd,
                "The start time must be before the end time."));
            return errors;
        }

        if (end - start < MinimumDuration)
        {
            errors.Add(new BookingError(BookingErrorCode.TooShort,
                Invariant($"A booking must last at least {MinimumDuration.TotalMinutes:0} minutes.")));
        }

        if (start.Date != end.Date)
        {
            errors.Add(new BookingError(BookingErrorCode.SpansMultipleDays,
                "A booking must start and end on the same day."));
        }
        else if (TimeOnly.FromDateTime(start) < OpeningTime || TimeOnly.FromDateTime(end) > ClosingTime)
        {
            errors.Add(new BookingError(BookingErrorCode.OutsideWorkingHours,
                Invariant($"Bookings are only allowed during working hours, {OpeningTime:HH:mm}–{ClosingTime:HH:mm}.")));
        }

        return errors;
    }

    // Messages must not depend on the server culture (e.g. its time separator).
    private static string Invariant(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);
}
