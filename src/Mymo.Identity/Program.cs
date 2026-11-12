using Microsoft.EntityFrameworkCore;
using Mymo.Identity;
using Mymo.Identity.Bll;
using Mymo.Identity.Dal;
using Mymo.Identity.Data;
using Mymo.Shared;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration);
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AdminService>();

var app = builder.Build();
MymoService.Use(app);
IdentityEndpoints.Map(app);

using (var scope = app.Services.CreateScope())
{
    await IdentitySeed.Run(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
}

app.Run();
