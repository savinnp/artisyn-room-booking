using System.Globalization;
using RoomBooking.Domain;
using RoomBooking.Web.Bookings;

namespace RoomBooking.Web.Api;

public sealed record RoomDto(int Id, string Name, int Capacity);

public sealed record BookingDto(int Id, int RoomId, string Title, string Organizer, DateTime Start, DateTime End);

/// <summary>Body of <c>POST /api/bookings</c>. Fields are nullable so that missing ones get a clear message.</summary>
public sealed record CreateBookingDto(int? RoomId, string? Title, string? Organizer, DateTime? Start, DateTime? End);

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/rooms", GetRoomsAsync);
        api.MapGet("/bookings", GetBookingsAsync);
        api.MapPost("/bookings", CreateBookingAsync);
        api.MapDelete("/bookings/{id:int}", DeleteBookingAsync);
        return app;
    }

    private static async Task<IResult> GetRoomsAsync(BookingService service, CancellationToken cancellationToken)
    {
        var rooms = await service.GetRoomsAsync(cancellationToken);
        return Results.Ok(rooms.Select(r => new RoomDto(r.Id, r.Name, r.Capacity)));
    }

    private static async Task<IResult> GetBookingsAsync(string? date, BookingService service, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return Problem(StatusCodes.Status400BadRequest, "Invalid date",
                ["The 'date' query parameter is required and must look like 2026-11-25."]);
        }

        var bookings = await service.GetBookingsAsync(day, cancellationToken);
        return Results.Ok(bookings.Select(ToDto));
    }

    private static async Task<IResult> CreateBookingAsync(CreateBookingDto body, BookingService service, CancellationToken cancellationToken)
    {
        var inputErrors = new List<string>();
        if (body.RoomId is null)
        {
            inputErrors.Add("The room id is required.");
        }
        CheckTime(body.Start, "start", inputErrors);
        CheckTime(body.End, "end", inputErrors);
        if (inputErrors.Count > 0)
        {
            return Problem(StatusCodes.Status400BadRequest, "Invalid booking", inputErrors);
        }

        var request = new BookingRequest(body.RoomId!.Value, body.Title ?? "", body.Organizer ?? "", body.Start!.Value, body.End!.Value);
        var result = await service.CreateAsync(request, cancellationToken);

        if (result.Booking is { } booking)
        {
            return Results.Json(ToDto(booking), statusCode: StatusCodes.Status201Created);
        }

        var messages = result.Errors.Select(e => e.Message).ToList();
        return result.IsConflict
            ? Problem(StatusCodes.Status409Conflict, "Booking conflict", messages)
            : Problem(StatusCodes.Status400BadRequest, "Invalid booking", messages);
    }

    private static async Task<IResult> DeleteBookingAsync(int id, BookingService service, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(id, cancellationToken)
            ? Results.NoContent()
            : Problem(StatusCodes.Status404NotFound, "Booking not found", [$"Booking {id} does not exist."]);
    }

    private static void CheckTime(DateTime? value, string name, List<string> errors)
    {
        if (value is null)
        {
            errors.Add($"The {name} time is required.");
        }
        else if (value.Value.Kind != DateTimeKind.Unspecified)
        {
            // A value with "Z" or "+03:00" would be silently converted to another clock; office time is expected instead.
            errors.Add($"The {name} time must be office-local time without a UTC offset, like 2026-11-25T10:00:00.");
        }
    }

    private static BookingDto ToDto(Booking b) => new(b.Id, b.RoomId, b.Title, b.Organizer, b.Start, b.End);

    /// <summary>A ProblemDetails response: all messages joined in "detail" and listed one by one in "errors".</summary>
    private static IResult Problem(int statusCode, string title, IReadOnlyList<string> messages) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: string.Join(" ", messages),
            extensions: new Dictionary<string, object?> { ["errors"] = messages });
}
