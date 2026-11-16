using Mymo.Chat;
using Mymo.Chat.Bll;
using Mymo.Chat.Dal;
using Mymo.Chat.Data;
using Mymo.Chat.Hubs;
using Mymo.Shared;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration, "Mymo Chat API");
builder.Services.AddSignalR();
builder.Services.AddChatDal(builder.Configuration);
builder.Services.AddChatBll();
builder.Services.AddScoped<IChatRealtime, ChatRealtime>();

var app = builder.Build();
MymoService.Use(app, "Mymo Chat API");
ChatEndpoints.Map(app);
app.MapHub<ChatHub>("/hubs/chat");

using (var scope = app.Services.CreateScope())
{
    try
    {
        await ChatSeed.Run(scope.ServiceProvider.GetRequiredService<ChatDbContext>());
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Database seed skipped or failed on startup: {Message}", ex.Message);
    }
}

app.Run();
