namespace RoomBooking.Domain;

/// <summary>A meeting room that can be booked.</summary>
public sealed class Room
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Number of people the room seats.</summary>
    public int Capacity { get; set; }
}
