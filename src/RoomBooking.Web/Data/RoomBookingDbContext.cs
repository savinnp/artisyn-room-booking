using Microsoft.EntityFrameworkCore;
using RoomBooking.Domain;

namespace RoomBooking.Web.Data;

public sealed class RoomBookingDbContext(DbContextOptions<RoomBookingDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(room =>
        {
            room.Property(r => r.Name).HasMaxLength(100);

            // Inserted when the database is created on first start.
            room.HasData(
                new Room { Id = 1, Name = "Oak", Capacity = 4 },
                new Room { Id = 2, Name = "Maple", Capacity = 6 },
                new Room { Id = 3, Name = "Pine", Capacity = 10 },
                new Room { Id = 4, Name = "Cedar", Capacity = 16 });
        });

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.Property(b => b.Title).HasMaxLength(BookingRules.TitleMaxLength);
            booking.Property(b => b.Organizer).HasMaxLength(BookingRules.OrganizerMaxLength);
            booking.HasOne<Room>().WithMany().HasForeignKey(b => b.RoomId);
            booking.HasIndex(b => new { b.RoomId, b.Start });
        });
    }
}
