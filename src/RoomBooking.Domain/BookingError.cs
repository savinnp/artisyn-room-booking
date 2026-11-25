namespace RoomBooking.Domain;

public enum BookingErrorCode
{
    TitleRequired,
    TitleTooLong,
    OrganizerRequired,
    OrganizerTooLong,
    StartNotBeforeEnd,
    TooShort,
    SpansMultipleDays,
    OutsideWorkingHours,
    Overlap,

    /// <summary>The requested room does not exist. Reported by the application, not by <see cref="BookingRules"/>.</summary>
    UnknownRoom,
}

/// <summary>A broken booking rule with a message that can be shown to the user as is.</summary>
public sealed record BookingError(BookingErrorCode Code, string Message);
