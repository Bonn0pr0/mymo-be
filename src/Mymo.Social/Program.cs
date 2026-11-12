using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Mymo.Shared;
using Mymo.Social;
using Mymo.Social.Bll;
using Mymo.Social.Dal;
using Mymo.Social.Data;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration);
builder.Services.AddDbContext<SocialDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<ISocialRepository, SocialRepository>();
builder.Services.AddScoped<PostService>();
builder.Services.AddScoped<FriendService>();
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();
var uploads = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "uploads"));
Directory.CreateDirectory(uploads);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploads),
    RequestPath = "/uploads",
});
MymoService.Use(app);
SocialEndpoints.Map(app);

using (var scope = app.Services.CreateScope())
{
    await SocialSeed.Run(scope.ServiceProvider.GetRequiredService<SocialDbContext>());
}

app.Run();
