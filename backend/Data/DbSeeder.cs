using System.Text.Json;
using System.Text.Json.Serialization;
using KrizevciBus.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KrizevciBus.Api.Data;

public static class DbSeeder
{
    record SeedStop(string Code, string Name, double Lat, double Lng);
    record SeedDirection(Direction Direction, List<string> Stops, List<double[]>? Route);
    record SeedDepartures(DayType Day, Direction Direction, List<string> Times);
    record SeedLine(string Name, string Color, List<SeedDirection> Directions, List<SeedDepartures> Departures);
    record SeedVehicle(string Name, string? Line);
    record SeedData(List<SeedStop> Stops, List<SeedLine> Lines, List<SeedVehicle> Vehicles);

    public static async Task SeedAsync(AppDbContext db, string path)
    {
        if (await db.Lines.AnyAsync()) return;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var data = JsonSerializer.Deserialize<SeedData>(await File.ReadAllTextAsync(path), options)!;

        var stops = data.Stops.ToDictionary(
            s => s.Code,
            s => new Stop { Code = s.Code, Name = s.Name, Lat = s.Lat, Lng = s.Lng });
        db.Stops.AddRange(stops.Values);

        foreach (var l in data.Lines)
        {
            var line = new Line { Name = l.Name, Color = l.Color };

            foreach (var d in l.Directions)
            {
                for (int i = 0; i < d.Stops.Count; i++)
                {
                    if (!stops.TryGetValue(d.Stops[i], out var stop))
                        throw new InvalidOperationException(
                            $"Nepoznata stanica '{d.Stops[i]}' u {l.Name} ({d.Direction})");

                    line.LineStops.Add(new LineStop { Stop = stop, Direction = d.Direction, Sequence = i });
                }

                // Trasa po ulicama iz seed.json (tools/generate-routes.mjs),
                // a ako je nema, ravna linija od stanice do stanice
                var route = d.Route is { Count: >= 2 }
                    ? d.Route
                    : d.Stops.Select(c => new[] { stops[c].Lat, stops[c].Lng }).ToList();

                for (int i = 0; i < route.Count; i++)
                    line.RoutePoints.Add(new RoutePoint
                        { Direction = d.Direction, Sequence = i, Lat = route[i][0], Lng = route[i][1] });
            }

            foreach (var dep in l.Departures)
                foreach (var t in dep.Times)
                    line.Departures.Add(new Departure
                        { Direction = dep.Direction, DayType = dep.Day, Time = TimeOnly.Parse(t) });

            db.Lines.Add(line);
        }
        await db.SaveChangesAsync();

        foreach (var v in data.Vehicles)
        {
            var line = v.Line is null ? null : await db.Lines.FirstAsync(x => x.Name == v.Line);
            db.Vehicles.Add(new Vehicle { Name = v.Name, LineId = line?.Id, IsActive = line is not null });
        }
        await db.SaveChangesAsync();
    }
}