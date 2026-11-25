using Microsoft.EntityFrameworkCore;
using RoomBooking.Domain;
using RoomBooking.Web.Data;

namespace RoomBooking.Web.Bookings;

/// <summary>Outcome of <see cref="BookingService.CreateAsync"/>.</summary>
public sealed record CreateBookingResult(Booking? Booking, IReadOnlyList<BookingError> Errors)
{
    public bool IsConflict => Errors.Any(e => e.Code == BookingErrorCode.Overlap);
}

/// <summary>Room and booking operations shared by the Blazor UI and the HTTP API.</summary>
public sealed class BookingService(IDbContextFactory<RoomBookingDbContext> dbFactory)
{
    public async Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Rooms.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);
    }

    /// <summary>Bookings that start on the given day, ordered by start time.</summary>
    public async Task<IReadOnlyList<Booking>> GetBookingsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var nextDayStart = dayStart.AddDays(1);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Bookings.AsNoTracking()
            .Where(b => b.Start >= dayStart && b.Start < nextDayStart)
            .OrderBy(b => b.Start).ThenBy(b => b.RoomId)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateBookingResult> CreateAsync(BookingRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // SQLite starts this transaction with BEGIN IMMEDIATE, which takes the write lock up front.
        // That makes "check for overlaps, then insert" atomic, so two concurrent requests cannot double-book a room.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var errors = new List<BookingError>();
        if (!await db.Rooms.AnyAsync(r => r.Id == request.RoomId, cancellationToken))
        {
            errors.Add(new BookingError(BookingErrorCode.UnknownRoom, $"Room {request.RoomId} does not exist."));
        }

        var possiblyOverlapping = await db.Bookings.AsNoTracking()
            .Where(b => b.RoomId == request.RoomId && b.Start < request.End && request.Start < b.End)
            .ToListAsync(cancellationToken);

        errors.AddRange(BookingRules.Validate(request, possiblyOverlapping));
        if (errors.Count > 0)
        {
            return new CreateBookingResult(null, errors);
        }

        var booking = new Booking
        {
            RoomId = request.RoomId,
            Title = request.Title.Trim(),
            Organizer = request.Organizer.Trim(),
            Start = request.Start,
            End = request.End,
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateBookingResult(booking, []);
    }

    /// <returns><c>false</c> if there was no booking with this id.</returns>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Bookings.Where(b => b.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
    }
}
