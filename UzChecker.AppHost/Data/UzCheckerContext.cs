using Microsoft.EntityFrameworkCore;
using UzChecker.AppHost.Data.Entities;

namespace UzChecker.AppHost.Data;

public sealed class UzCheckerContext : DbContext
{
    public UzCheckerContext(DbContextOptions<UzCheckerContext> opts) : base(opts)
    {
    }

    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Wagon> Wagons => Set<Wagon>();
    public DbSet<Trip> Trips => Set<Trip>();
}