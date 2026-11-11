using KrizevciBus.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KrizevciBus.Api.Controllers;

[ApiController]
[Route("api/stops")]
public class StopsController(EtaService eta) : ControllerBase
{
    // GET /api/stops/POST_K/arrivals -> kad stižu busevi na stajalište Pošta (strana prema Kolodvoru)
    [HttpGet("{code}/arrivals")]
    public async Task<ActionResult<List<ArrivalDto>>> GetArrivals(string code)
    {
        var arrivals = await eta.GetArrivals(code);
        return arrivals is null ? NotFound() : arrivals;
    }
}
