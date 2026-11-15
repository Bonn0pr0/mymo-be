using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Mymo.Shared;

namespace Mymo.Map.Hubs;

[Authorize]
public class MapHub : Hub
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public MapHub(IHttpClientFactory http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task UpdateMapArea(double lat, double lng) => await Touch(lat, lng);

    public async Task Heartbeat() => await Touch(null, null);

    private async Task Touch(double? lat, double? lng)
    {
        var id = HttpUser.Id(Context.User!);
        if (id == null) return;
        await Internal.PostAsync<object>(_http, $"{_config["Services:Identity"]}/internal/users/{id}/presence", new
        {
            isOnline = true,
            latitude = lat,
            longitude = lng,
        });
    }
}
