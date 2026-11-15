using Mymo.Map;
using Mymo.Map.Bll;
using Mymo.Map.Dal;
using Mymo.Map.Data;
using Mymo.Map.Hubs;
using Mymo.Shared;
using Mymo.Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddMapDal(builder.Configuration);
builder.Services.AddMapBll();
builder.Services.AddScoped<IMapRealtime, MapRealtime>();
builder.Services.AddRabbitConsumer<UpdatePlaceAdCommand>(Queues.UpdatePlaceAd, async (sp, cmd, _) =>
    await sp.GetRequiredService<MapService>().ApplyPlaceAd(cmd));
builder.Services.AddKafkaConsumer<PostCreatedEvent>(Topics.PostCreated, "mymo-map", async (sp, evt, _) =>
    await sp.GetRequiredService<MapService>().BroadcastPost(evt));

var app = builder.Build();
MymoService.Use(app);
MapEndpoints.Map(app);
app.MapHub<MapHub>("/hubs/map");

using (var scope = app.Services.CreateScope())
{
    await MapSeed.Run(scope.ServiceProvider.GetRequiredService<MapDbContext>());
}

app.Run();
