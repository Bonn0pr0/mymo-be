using Microsoft.EntityFrameworkCore;
using Mymo.Business;
using Mymo.Business.Bll;
using Mymo.Business.Dal;
using Mymo.Business.Data;
using Mymo.Shared;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration);
builder.Services.AddDbContext<BusinessDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<BusinessService>();

var app = builder.Build();
MymoService.Use(app);
BusinessEndpoints.Map(app);

using (var scope = app.Services.CreateScope())
{
    await BusinessSeed.Run(scope.ServiceProvider.GetRequiredService<BusinessDbContext>());
}

app.Run();
