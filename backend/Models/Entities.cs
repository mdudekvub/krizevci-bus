namespace KrizevciBus.Api.Models;

public class Line
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2563eb";
    public List<LineStop> LineStops { get; set; } = [];
    public List<RoutePoint> RoutePoints { get; set; } = [];
}

public class Stop
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class LineStop
{
    public int LineId { get; set; }
    public Line Line { get; set; } = null!;
    public int StopId { get; set; }
    public Stop Stop { get; set; } = null!;
    public int Sequence { get; set; }
}

public class RoutePoint
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public int Sequence { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class Vehicle
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? LineId { get; set; }
    public Line? Line { get; set; }
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