using Microsoft.AspNetCore.SignalR;
using Mymo.Map.Bll;
using Mymo.Map.Hubs;

namespace Mymo.Map;

public class MapRealtime : IMapRealtime
{
    private readonly IHubContext<MapHub> _hub;

    public MapRealtime(IHubContext<MapHub> hub) => _hub = hub;

    public Task MapPostCreated(object payload) =>
        _hub.Clients.All.SendAsync("MapPostCreated", payload);
}
