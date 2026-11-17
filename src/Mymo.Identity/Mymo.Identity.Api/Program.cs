using Mymo.Identity;
using Mymo.Identity.Api.Services;
using Mymo.Identity.Bll;
using Mymo.Identity.Dal;
using Mymo.Identity.Data;
using Mymo.Shared;
using Mymo.Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration, "Mymo Identity API");
builder.Services.AddGrpc();
builder.Services.AddIdentityDal(builder.Configuration);
builder.Services.AddIdentityBll();
builder.Services.AddRabbitConsumer<SetUserRoleCommand>(Queues.SetUserRole, async (sp, cmd, _) =>
    await sp.GetRequiredService<UserService>().ApplyRole(cmd.UserId, cmd.Role));
builder.Services.AddRabbitConsumer<SetUserPremiumCommand>(Queues.SetUserPremium, async (sp, cmd, _) =>
    await sp.GetRequiredService<UserService>().ApplyPremium(cmd.UserId, cmd.IsPremium));
builder.Services.AddRabbitConsumer<CreateReportCommand>(Queues.CreateReport, async (sp, cmd, _) =>
    await sp.GetRequiredService<AdminService>().CreateReport(cmd));

var app = builder.Build();
MymoService.Use(app, "Mymo Identity API");
IdentityEndpoints.Map(app);
app.MapGrpcService<UserGrpcServiceImpl>();

using (var scope = app.Services.CreateScope())
{
    try
    {
        await IdentitySeed.Run(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Database seed skipped or failed on startup: {Message}", ex.Message);
    }
}

app.Run();
