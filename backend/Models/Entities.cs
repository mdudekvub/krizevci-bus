namespace KrizevciBus.Api.Models;

public enum Direction { ToKolodvor, ToGroblje }
public enum DayType { Weekday, Weekend }

public class Line
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2563eb";
    public List<LineStop> LineStops { get; set; } = [];
    public List<RoutePoint> RoutePoints { get; set; } = [];
    public List<Departure> Departures { get; set; } = [];
}

// Fizičko stajalište na jednoj strani ceste
public class Stop
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class LineStop
{
    public int LineId { get; set; }
    public Line Line { get; set; } = null!;
    public Direction Direction { get; set; }
    public int Sequence { get; set; }
    public int StopId { get; set; }
    public Stop Stop { get; set; } = null!;
}

public class RoutePoint
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public Direction Direction { get; set; }
    public int Sequence { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

// Polazak s početne stanice smjera (ToKolodvor = s Groblja, ToGroblje = s Kolodvora)
public class Departure
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public Direction Direction { get; set; }
    public DayType DayType { get; set; }
    public TimeOnly Time { get; set; }
}

public class Vehicle
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? LineId { get; set; }
    public Line? Line { get; set; }
    public Direction? CurrentDirection { get; set; }
    public bool IsActive { get; set; }
    public VehiclePosition? Position { get; set; }
}

public class VehiclePosition
{
    public int VehicleId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Speed { get; set; }
    public double Heading { get; set; }
    public DateTime Timestamp { get; set; }
}