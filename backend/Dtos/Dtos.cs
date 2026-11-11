using KrizevciBus.Api.Models;

namespace KrizevciBus.Api.Dtos;

public record StopDto(int Id, string Code, string Name, double Lat, double Lng);

// Route = lista [lat, lng] parova, direktno za Leaflet polyline
public record DirectionDto(Direction Direction, List<StopDto> Stops, List<double[]> Route);

public record LineDto(int Id, string Name, string Color, List<DirectionDto> Directions);

public record DepartureDto(Direction Direction, DayType DayType, string Time);

public record PositionDto(double Lat, double Lng, double Speed, double Heading, DateTime Timestamp);

public record VehicleDto(int Id, string Name, int? LineId, Direction? Direction, bool IsActive, PositionDto? Position);

// Što simulator (kasnije pravi bus) šalje
public record PositionUpdateDto(double Lat, double Lng, double Speed, double Heading, Direction? Direction);