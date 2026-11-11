using KrizevciBus.Api.Auth;
using KrizevciBus.Api.Data;
using KrizevciBus.Api.Dtos;
using KrizevciBus.Api.Hubs;
using KrizevciBus.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KrizevciBus.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController(AppDbContext db, IHubContext<VehicleHub> hub) : ControllerBase
{
    [HttpGet]
    public async Task<List<VehicleDto>> GetAll()
    {
        var vehicles = await db.Vehicles.AsNoTracking()
            .Include(v => v.Position)
            .OrderBy(v => v.Id)
            .ToListAsync();

        return vehicles.Select(ToDto).ToList();
    }

    // Prima poziciju od simulatora (kasnije od busa). Traži X-Api-Key.
    [HttpPost("{id:int}/position")]
    [RequireApiKey]
    public async Task<IActionResult> UpdatePosition(int id, PositionUpdateDto dto)
    {
        if (dto.Lat is < -90 or > 90 || dto.Lng is < -180 or > 180)
            return BadRequest("Neispravne koordinate");

        var vehicle = await db.Vehicles.Include(v => v.Position).FirstOrDefaultAsync(v => v.Id == id);
        if (vehicle is null) return NotFound();

        vehicle.Position ??= new VehiclePosition { VehicleId = id };
        vehicle.Position.Lat = dto.Lat;
        vehicle.Position.Lng = dto.Lng;
        vehicle.Position.Speed = dto.Speed;
        vehicle.Position.Heading = dto.Heading;
        vehicle.Position.Timestamp = DateTime.UtcNow;

        if (dto.Direction is not null)
            vehicle.CurrentDirection = dto.Direction;

        await db.SaveChangesAsync();

        // Javi svim spojenim klijentima novu poziciju
        await hub.Clients.All.SendAsync("VehicleUpdated", ToDto(vehicle));

        return NoContent();
    }

    static VehicleDto ToDto(Vehicle v) => new(
        v.Id, v.Name, v.LineId, v.CurrentDirection, v.IsActive,
        v.Position is null ? null
            : new PositionDto(v.Position.Lat, v.Position.Lng, v.Position.Speed, v.Position.Heading, v.Position.Timestamp));
}