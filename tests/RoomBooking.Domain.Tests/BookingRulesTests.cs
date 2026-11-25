using System.Globalization;
using RoomBooking.Domain;

namespace RoomBooking.Domain.Tests;

public class BookingRulesTests
{
    private const int RoomId = 1;
    private const int OtherRoomId = 2;
    private static readonly DateOnly Day = new(2030, 1, 15);

    private static DateTime At(string time) =>
        Day.ToDateTime(TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture));

    private static BookingRequest Request(string start, string end, int roomId = RoomId,
        string title = "Planning", string organizer = "Alex") =>
        new(roomId, title, organizer, At(start), At(end));

    private static Booking Existing(string start, string end, int roomId = RoomId) => new()
    {
        Id = 1,
        RoomId = roomId,
        Title = "Existing meeting",
        Organizer = "Sam",
        Start = At(start),
        End = At(end),
    };

    private static IReadOnlyList<BookingError> Validate(BookingRequest request, params Booking[] existing) =>
        BookingRules.Validate(request, existing);

    [Fact]
    public void Valid_booking_has_no_errors()
    {
        Assert.Empty(Validate(Request("10:00", "11:00")));
    }

    [Theory]
    [InlineData("10:00", "10:00")]
    [InlineData("11:00", "10:00")]
    public void Start_must_be_before_end(string start, string end)
    {
        var error = Assert.Single(Validate(Request(start, end)));

        Assert.Equal(BookingErrorCode.StartNotBeforeEnd, error.Code);
        Assert.Equal("The start time must be before the end time.", error.Message);
    }

    [Fact]
    public void Booking_shorter_than_15_minutes_is_rejected()
    {
        var error = Assert.Single(Validate(Request("10:00", "10:14")));

        Assert.Equal(BookingErrorCode.TooShort, error.Code);
        Assert.Equal("A booking must last at least 15 minutes.", error.Message);
    }

    [Fact]
    public void Booking_of_exactly_15_minutes_is_allowed()
    {
        Assert.Empty(Validate(Request("10:00", "10:15")));
    }

    [Theory]
    [InlineData("08:59", "10:00")]
    [InlineData("07:00", "08:00")]
    [InlineData("18:30", "19:01")]
    [InlineData("19:00", "20:00")]
    public void Booking_outside_working_hours_is_rejected(string start, string end)
    {
        var error = Assert.Single(Validate(Request(start, end)));

        Assert.Equal(BookingErrorCode.OutsideWorkingHours, error.Code);
        Assert.Equal("Bookings are only allowed during working hours, 09:00–19:00.", error.Message);
    }

    [Theory]
    [InlineData("09:00", "09:30")]
    [InlineData("18:30", "19:00")]
    [InlineData("09:00", "19:00")]
    public void Booking_touching_working_hours_boundaries_is_allowed(string start, string end)
    {
        Assert.Empty(Validate(Request(start, end)));
    }

    [Fact]
    public void Booking_spanning_two_days_is_rejected()
    {
        var request = new BookingRequest(RoomId, "Planning", "Alex", At("18:00"), At("10:00").AddDays(1));

        var error = Assert.Single(Validate(request));

        Assert.Equal(BookingErrorCode.SpansMultipleDays, error.Code);
        Assert.Equal("A booking must start and end on the same day.", error.Message);
    }

    [Theory]
    [InlineData("10:15", "10:45")] // inside the existing booking
    [InlineData("09:30", "11:30")] // contains the existing booking
    [InlineData("09:30", "10:30")] // overlaps its beginning
    [InlineData("10:30", "11:30")] // overlaps its end
    [InlineData("10:00", "11:00")] // same interval
    public void Overlapping_booking_in_the_same_room_is_rejected(string start, string end)
    {
        var error = Assert.Single(Validate(Request(start, end), Existing("10:00", "11:00")));

        Assert.Equal(BookingErrorCode.Overlap, error.Code);
        Assert.Equal("The room is already booked from 10:00 to 11:00 (\"Existing meeting\").", error.Message);
    }

    [Theory]
    [InlineData("09:00", "10:00")] // ends when the existing booking starts
    [InlineData("11:00", "12:00")] // starts when the existing booking ends
    public void Back_to_back_bookings_are_allowed(string start, string end)
    {
        Assert.Empty(Validate(Request(start, end), Existing("10:00", "11:00")));
    }

    [Fact]
    public void Booking_at_the_same_time_in_another_room_is_allowed()
    {
        Assert.Empty(Validate(Request("10:00", "11:00"), Existing("10:00", "11:00", roomId: OtherRoomId)));
    }

    [Theory]
    [InlineData("", "Alex", BookingErrorCode.TitleRequired, "A topic is required.")]
    [InlineData("   ", "Alex", BookingErrorCode.TitleRequired, "A topic is required.")]
    [InlineData("Planning", "", BookingErrorCode.OrganizerRequired, "The organizer's name is required.")]
    [InlineData("Planning", " ", BookingErrorCode.OrganizerRequired, "The organizer's name is required.")]
    public void Topic_and_organizer_are_required(string title, string organizer, BookingErrorCode code, string message)
    {
        var error = Assert.Single(Validate(Request("10:00", "11:00", title: title, organizer: organizer)));

        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Too_long_topic_and_organizer_are_rejected()
    {
        var request = Request("10:00", "11:00",
            title: new string('t', BookingRules.TitleMaxLength + 1),
            organizer: new string('o', BookingRules.OrganizerMaxLength + 1));

        var codes = Validate(request).Select(e => e.Code);

        Assert.Equal([BookingErrorCode.TitleTooLong, BookingErrorCode.OrganizerTooLong], codes);
    }

    [Fact]
    public void All_broken_rules_are_reported_together()
    {
        var request = Request("08:00", "08:10", title: "", organizer: "");

        var codes = Validate(request).Select(e => e.Code);

        Assert.Equal(
            [
                BookingErrorCode.TitleRequired,
                BookingErrorCode.OrganizerRequired,
                BookingErrorCode.TooShort,
                BookingErrorCode.OutsideWorkingHours,
            ],
            codes);
    }
}
