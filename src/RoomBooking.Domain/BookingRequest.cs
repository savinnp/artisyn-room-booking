namespace RoomBooking.Domain;

/// <summary>Data for a new booking, before it has been validated.</summary>
/// <param name="Start">Office-local start time.</param>
/// <param name="End">Office-local end time.</param>
public sealed record BookingRequest(int RoomId, string Title, string Organizer, DateTime Start, DateTime End);
