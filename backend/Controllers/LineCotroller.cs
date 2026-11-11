using KrizevciBus.Api.Data;
using KrizevciBus.Api.Dtos;
using KrizevciBus.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrizevciBus.Api.Controllers;

[ApiController]
[Route("api/lines")]
public class LinesController(AppDbContext db) : ControllerBase
{
    static readonly TimeZoneInfo Zagreb = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");

    [HttpGet]
    public async Task<List<LineDto>> GetAll()
    {
        var lines = await LinesQuery().OrderBy(l => l.Id).ToListAsync();
        return lines.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LineDto>> Get(int id)
    {
        var line = await LinesQuery().FirstOrDefaultAsync(l => l.Id == id);
        return line is null ? NotFound() : ToDto(line);
    }

    // GET /api/lines/1/departures            -> vozni red za danas
    // GET /api/lines/1/departures?day=Weekend -> vozni red za vikend
    [HttpGet("{id:int}/departures")]
    public async Task<ActionResult<List<DepartureDto>>> GetDepartures(int id, [FromQuery] DayType? day)
    {
        if (!await db.Lines.AnyAsync(l => l.Id == id)) return NotFound();

        var dayType = day ?? TodayType();
        var deps = await db.Departures.AsNoTracking()
            .Where(d => d.LineId == id && d.DayType == dayType)
            .OrderBy(d => d.Direction).ThenBy(d => d.Time)
            .ToListAsync();

        return deps.Select(d => new DepartureDto(d.Direction, d.DayType, d.Time.ToString("HH:mm"))).ToList();
    }

    IQueryable<Line> LinesQuery() => db.Lines.AsNoTracking()
        .Include(l => l.LineStops).ThenInclude(ls => ls.Stop)
        .Include(l => l.RoutePoints)
        .AsSplitQuery();

    static LineDto ToDto(Line l) => new(
        l.Id, l.Name, l.Color,
        Enum.GetValues<Direction>()
            .Select(d => new DirectionDto(
                d,
                l.LineStops.Where(x => x.Direction == d).OrderBy(x => x.Sequence)
                    .Select(x => new StopDto(x.Stop.Id, x.Stop.Code, x.Stop.Name, x.Stop.Lat, x.Stop.Lng))
                    .ToList(),
                l.RoutePoints.Where(p => p.Direction == d).OrderBy(p => p.Sequence)
                    .Select(p => new[] { p.Lat, p.Lng })
                    .ToList()))
            .Where(d => d.Stops.Count > 0)
            .ToList());

    // Praznici zasad nisu uključeni
    static DayType TodayType()
    {
        var today = TimeZoneInfo.ConvertTime(DateTime.UtcNow, Zagreb).DayOfWeek;
        return today is DayOfWeek.Saturday or DayOfWeek.Sunday ? DayType.Weekend : DayType.Weekday;
    }
}