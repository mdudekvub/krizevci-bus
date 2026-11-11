using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

// Pokretanje:
//   dotnet run                       -> stvarna brzina (krug traje 40 min)
//   dotnet run -- --speed 10         -> 10x brže, za testiranje
//   dotnet run -- --api http://localhost:5050

const double TripMinutes = 18;     // vožnja od okretišta do okretišta, uključujući stajanja
const double DwellSeconds = 20;    // stajanje na stanici
const double LayoverMinutes = 2;   // pauza na okretištu (18 + 2 = 20 min po smjeru, 40 min krug)
const double TickSeconds = 1;      // koliko često se šalje pozicija (stvarno vrijeme)

var apiUrl = GetArg("--api") ?? "http://localhost:5050";
var speedFactor = double.Parse(GetArg("--speed") ?? "1", CultureInfo.InvariantCulture);

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var http = new HttpClient { BaseAddress = new Uri(apiUrl) };

List<LineDto> lines;
List<VehicleDto> vehicles;
try
{
    lines = await http.GetFromJsonAsync<List<LineDto>>("api/lines", json) ?? [];
    vehicles = await http.GetFromJsonAsync<List<VehicleDto>>("api/vehicles", json) ?? [];
}
catch (HttpRequestException)
{
    Console.WriteLine($"API nije dostupan na {apiUrl}. Pokreni backend (dotnet run u backend/) pa probaj ponovno.");
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var active = vehicles.Where(v => v.IsActive && v.LineId is not null).ToList();
Console.WriteLine($"Simulator: {active.Count} busa, brzina x{speedFactor}, API {apiUrl}. Ctrl+C za kraj.\n");

var tasks = active.Select(v => RunVehicle(v, lines.First(l => l.Id == v.LineId), cts.Token));
try { await Task.WhenAll(tasks); }
catch (OperationCanceledException) { }
Console.WriteLine("\nSimulator zaustavljen.");


// Bus beskonačno vozi: prema Kolodvoru, pauza, prema Groblju, pauza...
async Task RunVehicle(VehicleDto v, LineDto line, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        foreach (var dir in line.Directions)
        {
            await DriveTrip(v, line, dir, ct);
            Log(v, $"okretište, pauza {LayoverMinutes} min");
            await SimDelay(LayoverMinutes * 60, ct);
        }
    }
}

async Task DriveTrip(VehicleDto v, LineDto line, DirectionDto dir, CancellationToken ct)
{
    var pts = dir.Route;
    if (pts.Count < 2) return;

    // Kumulativna udaljenost do svake točke trase (u metrima)
    var cum = new double[pts.Count];
    for (int i = 1; i < pts.Count; i++)
        cum[i] = cum[i - 1] + Distance(pts[i - 1], pts[i]);
    var total = cum[^1];

    // Međustanice (bez početnog i krajnjeg okretišta) i njihova udaljenost na trasi.
    // Traži se redom od prethodne stanice, jer trasa može dvaput proći blizu istog mjesta.
    var stops = new List<(string Name, double At)>();
    int from = 0;
    foreach (var s in dir.Stops.Skip(1).SkipLast(1))
    {
        from = NearestIndex(pts, s.Lat, s.Lng, from);
        stops.Add((s.Name, cum[from]));
    }

    var driveSeconds = TripMinutes * 60 - DwellSeconds * stops.Count;
    var speed = total / driveSeconds; // m/s

    Log(v, $"{line.Name} kreće {(dir.Direction == "ToKolodvor" ? "prema Kolodvoru" : "prema Groblju")} ({total / 1000:0.0} km)");

    double pos = 0;
    int next = 0;
    while (pos < total)
    {
        ct.ThrowIfCancellationRequested();
        pos = Math.Min(total, pos + speed * TickSeconds * speedFactor);

        if (next < stops.Count && pos >= stops[next].At)
        {
            pos = stops[next].At;
            var (sLat, sLng, sHead) = PointAt(pts, cum, pos);
            await Send(v, sLat, sLng, 0, sHead, dir.Direction, ct);
            Log(v, $"stanica {stops[next].Name}");
            next++;
            await SimDelay(DwellSeconds, ct);
            continue;
        }

        var (lat, lng, heading) = PointAt(pts, cum, pos);
        await Send(v, lat, lng, speed * 3.6, heading, dir.Direction, ct);
        await Task.Delay(TimeSpan.FromSeconds(TickSeconds), ct);
    }

    var end = pts[^1];
    await Send(v, end[0], end[1], 0, 0, dir.Direction, ct);
}

async Task Send(VehicleDto v, double lat, double lng, double speedKmh, double heading, string direction, CancellationToken ct)
{
    try
    {
        var res = await http.PostAsJsonAsync($"api/vehicles/{v.Id}/position",
            new { lat, lng, speed = Math.Round(speedKmh, 1), heading = Math.Round(heading), direction }, json, ct);
        if (!res.IsSuccessStatusCode) Log(v, $"API vratio {(int)res.StatusCode}");
    }
    catch (HttpRequestException)
    {
        Log(v, "API nedostupan, pokušavam dalje");
    }
}

// Čekanje u simuliranom vremenu (ubrzano sa --speed)
Task SimDelay(double simSeconds, CancellationToken ct) =>
    Task.Delay(TimeSpan.FromSeconds(simSeconds / speedFactor), ct);

void Log(VehicleDto v, string msg) =>
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {v.Name}: {msg}");

string? GetArg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

// Pozicija i smjer kretanja na udaljenosti pos duž trase
static (double Lat, double Lng, double Heading) PointAt(List<double[]> pts, double[] cum, double pos)
{
    int i = 0;
    while (i < pts.Count - 2 && cum[i + 1] < pos) i++;
    var segLen = cum[i + 1] - cum[i];
    var t = segLen == 0 ? 0 : (pos - cum[i]) / segLen;
    var a = pts[i];
    var b = pts[i + 1];
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, Bearing(a, b));
}

static int NearestIndex(List<double[]> pts, double lat, double lng, int from = 0)
{
    int best = from;
    double bestD = double.MaxValue;
    for (int i = from; i < pts.Count; i++)
    {
        var d = Distance(pts[i], [lat, lng]);
        if (d < bestD) { bestD = d; best = i; }
    }
    return best;
}

// Haversine udaljenost u metrima
static double Distance(double[] a, double[] b)
{
    const double R = 6371000;
    var dLat = ToRad(b[0] - a[0]);
    var dLng = ToRad(b[1] - a[1]);
    var h = Math.Pow(Math.Sin(dLat / 2), 2) +
            Math.Cos(ToRad(a[0])) * Math.Cos(ToRad(b[0])) * Math.Pow(Math.Sin(dLng / 2), 2);
    return 2 * R * Math.Asin(Math.Sqrt(h));
}

// Smjer kretanja u stupnjevima (0 = sjever, 90 = istok)
static double Bearing(double[] a, double[] b)
{
    var y = Math.Sin(ToRad(b[1] - a[1])) * Math.Cos(ToRad(b[0]));
    var x = Math.Cos(ToRad(a[0])) * Math.Sin(ToRad(b[0])) -
            Math.Sin(ToRad(a[0])) * Math.Cos(ToRad(b[0])) * Math.Cos(ToRad(b[1] - a[1]));
    return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
}

static double ToRad(double deg) => deg * Math.PI / 180;

record StopDto(int Id, string Code, string Name, double Lat, double Lng);
record DirectionDto(string Direction, List<StopDto> Stops, List<double[]> Route);
record LineDto(int Id, string Name, string Color, List<DirectionDto> Directions);
record VehicleDto(int Id, string Name, int? LineId, bool IsActive);