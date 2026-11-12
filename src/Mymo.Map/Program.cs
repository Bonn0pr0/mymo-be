using Microsoft.EntityFrameworkCore;
using Mymo.Map;
using Mymo.Map.Bll;
using Mymo.Map.Dal;
using Mymo.Map.Data;
using Mymo.Map.Hubs;
using Mymo.Shared;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddDbContext<MapDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IMapRepository, MapRepository>();
builder.Services.AddScoped<MapService>();

var app = builder.Build();
MymoService.Use(app);
MapEndpoints.Map(app);
app.MapHub<MapHub>("/hubs/map");

using (var scope = app.Services.CreateScope())
{
    await MapSeed.Run(scope.ServiceProvider.GetRequiredService<MapDbContext>());
}

app.Run();
