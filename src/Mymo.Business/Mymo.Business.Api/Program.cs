using Mymo.Business;
using Mymo.Business.Bll;
using Mymo.Business.Dal;
using Mymo.Business.Data;
using Mymo.Shared;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration, "Mymo Business API");
builder.Services.AddBusinessDal(builder.Configuration);
builder.Services.AddBusinessBll();

var app = builder.Build();
MymoService.Use(app, "Mymo Business API");
BusinessEndpoints.Map(app);

using (var scope = app.Services.CreateScope())
{
    try
    {
        await BusinessSeed.Run(scope.ServiceProvider.GetRequiredService<BusinessDbContext>());
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Database seed skipped or failed on startup: {Message}", ex.Message);
    }
}

app.Run();
