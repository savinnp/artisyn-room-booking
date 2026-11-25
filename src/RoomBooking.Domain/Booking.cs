namespace RoomBooking.Domain;

/// <summary>A reservation of a room for a time interval.</summary>
/// <remarks>
/// <see cref="Start"/> and <see cref="End"/> are office-local times without a UTC offset.
/// The interval is half-open: [Start, End).
/// </remarks>
public sealed class Booking
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    /// <summary>Topic of the meeting.</summary>
    public required string Title { get; set; }

    /// <summary>Name of the person who made the booking.</summary>
    public required string Organizer { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }
}
