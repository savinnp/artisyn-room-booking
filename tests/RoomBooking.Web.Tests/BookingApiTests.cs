using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace RoomBooking.Web.Tests;

/// <summary>HTTP API tests. Each test gets a fresh app with an empty database.</summary>
public sealed class BookingApiTests : IDisposable
{
    private const string Day = "2030-01-15";
    private const string OtherDay = "2030-01-16";

    private readonly RoomBookingAppFactory factory = new();
    private readonly HttpClient client;

    public BookingApiTests() => client = factory.CreateClient();

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private sealed record RoomResponse(int Id, string Name, int Capacity);

    private sealed record BookingResponse(int Id, int RoomId, string Title, string Organizer, DateTime Start, DateTime End);

    private sealed record ProblemResponse(int Status, string Title, string Detail, string[] Errors);

    private static object NewBooking(int roomId = 1, string start = "10:00", string end = "11:00",
        string? title = "Planning", string? organizer = "Alex") =>
        new { roomId, title, organizer, start = $"{Day}T{start}:00", end = $"{Day}T{end}:00" };

    private Task<HttpResponseMessage> PostAsync(object body) => client.PostAsJsonAsync("/api/bookings", body);

    private async Task<BookingResponse> CreateAsync(object body)
    {
        var response = await PostAsync(body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
    }

    private async Task<BookingResponse[]> GetBookingsAsync(string day) =>
        (await client.GetFromJsonAsync<BookingResponse[]>($"/api/bookings?date={day}"))!;

    private static async Task<ProblemResponse> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return (await response.Content.ReadFromJsonAsync<ProblemResponse>())!;
    }

    [Fact]
    public async Task Get_rooms_returns_the_seeded_rooms()
    {
        var rooms = await client.GetFromJsonAsync<RoomResponse[]>("/api/rooms");

        RoomResponse[] expected = [new(1, "Oak", 4), new(2, "Maple", 6), new(3, "Pine", 10), new(4, "Cedar", 16)];
        Assert.Equal(expected, rooms);
    }

    [Fact]
    public async Task Get_bookings_for_a_day_without_bookings_returns_an_empty_list()
    {
        Assert.Empty(await GetBookingsAsync(Day));
    }

    [Theory]
    [InlineData("/api/bookings")]
    [InlineData("/api/bookings?date=")]
    [InlineData("/api/bookings?date=2030-13-01")]
    [InlineData("/api/bookings?date=15.01.2030")]
    public async Task Get_bookings_without_a_valid_date_returns_400(string url)
    {
        var problem = await ReadProblemAsync(await client.GetAsync(url), HttpStatusCode.BadRequest);

        Assert.Equal("The 'date' query parameter is required and must look like 2026-11-25.", problem.Detail);
    }

    [Fact]
    public async Task Created_booking_is_returned_and_listed_for_its_day_only()
    {
        var response = await PostAsync(NewBooking(roomId: 2, start: "10:00", end: "11:30"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"start\":\"2030-01-15T10:00:00\"", json); // office-local, no UTC offset

        var created = (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
        Assert.True(created.Id > 0);
        Assert.Equal(
            new BookingResponse(created.Id, 2, "Planning", "Alex", new DateTime(2030, 1, 15, 10, 0, 0), new DateTime(2030, 1, 15, 11, 30, 0)),
            created);

        Assert.Equal([created], await GetBookingsAsync(Day));
        Assert.Empty(await GetBookingsAsync(OtherDay));
    }

    [Fact]
    public async Task Overlapping_booking_in_the_same_room_returns_409()
    {
        await CreateAsync(NewBooking(start: "10:00", end: "11:00", title: "Standup"));

        var problem = await ReadProblemAsync(await PostAsync(NewBooking(start: "10:30", end: "11:30")), HttpStatusCode.Conflict);

        Assert.Equal("The room is already booked from 10:00 to 11:00 (\"Standup\").", problem.Detail);
        Assert.Single(await GetBookingsAsync(Day));
    }

    [Fact]
    public async Task Booking_at_the_same_time_in_another_room_returns_201()
    {
        await CreateAsync(NewBooking(roomId: 1));
        await CreateAsync(NewBooking(roomId: 2));

        Assert.Equal(2, (await GetBookingsAsync(Day)).Length);
    }

    [Theory]
    [InlineData("08:00", "09:30", "Bookings are only allowed during working hours, 09:00–19:00.")]
    [InlineData("18:30", "19:30", "Bookings are only allowed during working hours, 09:00–19:00.")]
    [InlineData("10:00", "10:10", "A booking must last at least 15 minutes.")]
    [InlineData("11:00", "10:00", "The start time must be before the end time.")]
    public async Task Booking_that_breaks_a_time_rule_returns_400(string start, string end, string message)
    {
        var problem = await ReadProblemAsync(await PostAsync(NewBooking(start: start, end: end)), HttpStatusCode.BadRequest);

        Assert.Equal([message], problem.Errors);
        Assert.Equal(message, problem.Detail);
        Assert.Empty(await GetBookingsAsync(Day));
    }

    [Fact]
    public async Task Booking_for_an_unknown_room_returns_400()
    {
        var problem = await ReadProblemAsync(await PostAsync(NewBooking(roomId: 99)), HttpStatusCode.BadRequest);

        Assert.Equal(["Room 99 does not exist."], problem.Errors);
    }

    [Fact]
    public async Task Booking_without_topic_and_organizer_returns_400()
    {
        var problem = await ReadProblemAsync(await PostAsync(NewBooking(title: null, organizer: "  ")), HttpStatusCode.BadRequest);

        Assert.Equal(["A topic is required.", "The organizer's name is required."], problem.Errors);
    }

    [Fact]
    public async Task Booking_without_room_and_times_returns_400()
    {
        var problem = await ReadProblemAsync(await PostAsync(new { title = "Planning", organizer = "Alex" }), HttpStatusCode.BadRequest);

        Assert.Equal(["The room id is required.", "The start time is required.", "The end time is required."], problem.Errors);
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("+03:00")]
    public async Task Times_with_a_utc_offset_return_400(string offset)
    {
        var body = new { roomId = 1, title = "Planning", organizer = "Alex", start = $"{Day}T10:00:00{offset}", end = $"{Day}T11:00:00{offset}" };

        var problem = await ReadProblemAsync(await PostAsync(body), HttpStatusCode.BadRequest);

        Assert.Equal(
            [
                "The start time must be office-local time without a UTC offset, like 2026-11-25T10:00:00.",
                "The end time must be office-local time without a UTC offset, like 2026-11-25T10:00:00.",
            ],
            problem.Errors);
    }

    [Fact]
    public async Task Malformed_json_returns_400_problem()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/bookings", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Deleted_booking_disappears_and_deleting_it_again_returns_404()
    {
        var created = await CreateAsync(NewBooking());

        var response = await client.DeleteAsync($"/api/bookings/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetBookingsAsync(Day));

        var problem = await ReadProblemAsync(await client.DeleteAsync($"/api/bookings/{created.Id}"), HttpStatusCode.NotFound);
        Assert.Equal($"Booking {created.Id} does not exist.", problem.Detail);
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_slot_book_it_only_once()
    {
        var responses = await Task.WhenAll(
            Enumerable.Range(1, 10).Select(i => PostAsync(NewBooking(title: $"Meeting {i}"))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Single(await GetBookingsAsync(Day));
    }
}
