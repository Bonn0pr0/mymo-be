using Microsoft.EntityFrameworkCore;
using Mymo.Chat;
using Mymo.Chat.Bll;
using Mymo.Chat.Dal;
using Mymo.Chat.Data;
using Mymo.Chat.Hubs;
using Mymo.Shared;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<ChatService>();

var app = builder.Build();
MymoService.Use(app);
ChatEndpoints.Map(app);
app.MapHub<ChatHub>("/hubs/chat");

using (var scope = app.Services.CreateScope())
{
    await ChatSeed.Run(scope.ServiceProvider.GetRequiredService<ChatDbContext>());
}

app.Run();
