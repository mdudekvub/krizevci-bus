using Microsoft.AspNetCore.SignalR;
 
namespace KrizevciBus.Api.Hubs;
 
// Klijenti (web) se spajaju na /hubs/vehicles i slušaju "VehicleUpdated".
// Hub je prazan jer server samo šalje, klijenti ništa ne pozivaju.
public class VehicleHub : Hub
{
}
 