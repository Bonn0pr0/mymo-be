using Microsoft.Extensions.FileProviders;
using Mymo.Shared;
using Mymo.Social;
using Mymo.Social.Bll;
using Mymo.Social.Dal;
using Mymo.Social.Data;

var builder = WebApplication.CreateBuilder(args);
MymoService.Add(builder.Services, builder.Configuration, "Mymo Social API");
builder.Services.AddSocialDal(builder.Configuration);
builder.Services.AddSocialBll();

var app = builder.Build();
var uploads = Uploads.GetDirectory(builder.Configuration);
Directory.CreateDirectory(uploads);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploads),
    RequestPath = "/uploads",
});
MymoService.Use(app, "Mymo Social API");
SocialEndpoints.Map(app);

using (var scope = app.Services.CreateScope())
{
    await SocialSeed.Run(scope.ServiceProvider.GetRequiredService<SocialDbContext>());
}

app.Run();
