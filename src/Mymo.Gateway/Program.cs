var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
app.UseCors();
app.UseWebSockets();
app.MapGet("/health", () => Results.Ok(new { ok = true, role = "gateway" }));

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger-docs/identity/swagger/v1/swagger.json", "1. Mymo Identity Service (Auth/Users/Admin)");
    c.SwaggerEndpoint("/swagger-docs/social/swagger/v1/swagger.json", "2. Mymo Social Service (Posts/Friends/Notifications)");
    c.SwaggerEndpoint("/swagger-docs/map/swagger/v1/swagger.json", "3. Mymo Map Service (Places/Checkins/Weather)");
    c.SwaggerEndpoint("/swagger-docs/chat/swagger/v1/swagger.json", "4. Mymo Chat Service (Conversations/Messages)");
    c.SwaggerEndpoint("/swagger-docs/business/swagger/v1/swagger.json", "5. Mymo Business Service (Venues/Campaigns/VIP)");
    c.RoutePrefix = "swagger";
});

app.MapReverseProxy();
app.Run();
