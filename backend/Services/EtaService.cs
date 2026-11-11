using KrizevciBus.Api.Data;
using KrizevciBus.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KrizevciBus.Api.Services;

public record ArrivalDto(
    int LineId, string LineName, string Color, Direction Direction,
    int Minutes, bool Live, string? VehicleName);

// Procjena dolaska busa na stanicu.
// Vrijednosti vožnje odgovaraju voznom redu: 18 min po smjeru, 20 s po stanici, 2 min na okretištu.
public class EtaService(AppDbContext db)
{
    public const double TripSeconds = 18 * 60;
    public const double DwellSeconds = 20;
    public const double LayoverSeconds = 2 * 60;
    const double StaleSeconds = 60;

    static readonly TimeZoneInfo Zagreb = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");

    public async Task<List<ArrivalDto>?> GetArrivals(string stopCode)
    {
        if (!await db.Stops.AnyAsync(s => s.Code == stopCode)) return null;

        var lines = await db.Lines.AsNoTracking()
            .Include(l => l.LineStops).ThenInclude(ls => ls.Stop)
            .Include(l => l.RoutePoints)
            .Include(l => l.Departures)
            .AsSplitQuery()
            .ToListAsync();

        var vehicles = await db.Vehicles.AsNoTracking()
            .Include(v => v.Position)
            .Where(v => v.IsActive && v.LineId != null)
            .ToListAsync();

        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, Zagreb);
        var dayType = nowLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? DayType.Weekend : DayType.Weekday;

        var result = new List<ArrivalDto>();

        foreach (var line in lines)
        {
            var trips = Enum.GetValues<Direction>()
                .Select(d => Trip.Build(line, d))
                .OfType<Trip>()
                .ToDictionary(t => t.Direction);

            foreach (var trip in trips.Values)
            {
                if (!trip.StopDistance.TryGetValue(stopCode, out var target)) continue;

                // 1) Bus uživo
                double? best = null;
                string? vehicleName = null;
                foreach (var v in vehicles.Where(v => v.LineId == line.Id))
                {
                    if (v.Position is null || v.CurrentDirection is null) continue;
                    if ((nowUtc - v.Position.Timestamp).TotalSeconds > StaleSeconds) continue;
                    if (!trips.TryGetValue(v.CurrentDirection.Value, out var current)) continue;

                    var pos = current.Project(v.Position.Lat, v.Position.Lng);
                    double? secs;
                    if (current.Direction == trip.Direction)
                        secs = pos <= target - 5 ? trip.SecondsBetween(pos, target) : null; // već prošao
                    else
                        secs = current.SecondsBetween(pos, current.Total) + LayoverSeconds + trip.SecondsBetween(0, target);

                    if (secs is not null && (best is null || secs < best))
                    {
                        best = secs;
                        vehicleName = v.Name;
                    }
                }

                if (best is not null)
                {
                    result.Add(new(line.Id, line.Name, line.Color, trip.Direction,
                        (int)Math.Ceiling(best.Value / 60), true, vehicleName));
                    continue;
                }

                // 2) Vozni red: polazak s okretišta + vrijeme vožnje do ove stanice
                var offset = TimeSpan.FromSeconds(trip.SecondsBetween(0, target));
                var next = line.Departures
                    .Where(d => d.Direction == trip.Direction && d.DayType == dayType)
                    .Select(d => d.Time.ToTimeSpan() + offset)
                    .Where(t => t >= nowLocal.TimeOfDay)
                    .Order()
                    .Select(t => (TimeSpan?)t)
                    .FirstOrDefault();

                if (next is not null)
                    result.Add(new(line.Id, line.Name, line.Color, trip.Direction,
                        (int)Math.Ceiling((next.Value - nowLocal.TimeOfDay).TotalMinutes), false, null));
            }
        }

        return result.OrderBy(a => a.Minutes).ToList();
    }

    // Jedan smjer jedne linije: trasa, udaljenosti stanica po trasi i prosječna brzina
    sealed class Trip
    {
        public Direction Direction { get; private init; }
        public double Total { get; private init; }
        public Dictionary<string, double> StopDistance { get; } = [];

        List<double[]> points = [];
        double[] cumulative = [];
        List<double> intermediateStops = [];
        double speed; // m/s

        public static Trip? Build(Line line, Direction direction)
        {
            var points = line.RoutePoints.Where(p => p.Direction == direction)
                .OrderBy(p => p.Sequence).Select(p => new[] { p.Lat, p.Lng }).ToList();
            var stops = line.LineStops.Where(s => s.Direction == direction)
                .OrderBy(s => s.Sequence).Select(s => s.Stop).ToList();
            if (points.Count < 2 || stops.Count < 2) return null;

            var cum = new double[points.Count];
            for (int i = 1; i < points.Count; i++)
                cum[i] = cum[i - 1] + Geo.Distance(points[i - 1], points[i]);

            var trip = new Trip { Direction = direction, Total = cum[^1], points = points, cumulative = cum };

            // Stanice se traže redom po trasi, jer trasa može dvaput proći blizu istog mjesta
            int from = 0;
            for (int i = 0; i < stops.Count; i++)
            {
                from = Geo.NearestIndex(points, stops[i].Lat, stops[i].Lng, from);
                var at = i == stops.Count - 1 ? trip.Total : cum[from];
                trip.StopDistance[stops[i].Code] = at;
                if (i > 0 && i < stops.Count - 1) trip.intermediateStops.Add(at);
            }

            trip.speed = trip.Total / (TripSeconds - DwellSeconds * trip.intermediateStops.Count);
            return trip;
        }

        // Udaljenost duž trase do točke trase najbliže danoj poziciji
        public double Project(double lat, double lng) => cumulative[Geo.NearestIndex(points, lat, lng)];

        // Vrijeme vožnje između dvije udaljenosti na trasi, sa stajanjima između
        public double SecondsBetween(double from, double to) =>
            Math.Max(0, to - from) / speed +
            DwellSeconds * intermediateStops.Count(s => s > from + 1 && s < to - 1);
    }
}
