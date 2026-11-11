using KrizevciBus.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KrizevciBus.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<Stop> Stops => Set<Stop>();
    public DbSet<LineStop> LineStops => Set<LineStop>();
    public DbSet<RoutePoint> RoutePoints => Set<RoutePoint>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePosition> VehiclePositions => Set<VehiclePosition>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<LineStop>().HasKey(x => new { x.LineId, x.Sequence });
        b.Entity<RoutePoint>().HasIndex(x => new { x.LineId, x.Sequence }).IsUnique();
        b.Entity<VehiclePosition>().HasKey(x => x.VehicleId);
        b.Entity<Vehicle>()
            .HasOne(v => v.Position).WithOne()
            .HasForeignKey<VehiclePosition>(p => p.VehicleId);
    }
}