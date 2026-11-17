using System.Text.Json;
using Mymo.Business.Bll;
using Mymo.Shared;

namespace Mymo.Business;

public static class BusinessEndpoints
{
    public static void Map(WebApplication app)
    {
        var biz = app.MapGroup("/api/business").RequireAuthorization();
        biz.MapGet("/me", async (ClaimsPrincipal user, BusinessService biz) => (await biz.Mine(user)).ToHttp());
        biz.MapGet("/place-models", (BusinessService biz) => biz.PlaceModels().ToHttp());
        biz.MapGet("/places", async (ClaimsPrincipal user, BusinessService biz) => (await biz.MyPlaces(user)).ToHttp());
        biz.MapPost("/places", async (ClaimsPrincipal user, BusinessService biz, JsonElement body) => (await biz.CreatePlace(user, body)).ToHttp());
        biz.MapGet("/places/{id:guid}", async (Guid id, BusinessService biz) => (await biz.OnePlace(id)).ToHttp());
        biz.MapPut("/places/{id:guid}", async (Guid id, BusinessService biz, JsonElement body) => (await biz.UpdatePlace(id, body)).ToHttp());
        biz.MapGet("/places/{id:guid}/analytics", (BusinessService biz) => biz.Analytics().ToHttp());
        biz.MapPut("/places/{id:guid}/model-3d", (Guid id, BusinessService biz) => biz.Model(id).ToHttp());
        biz.MapGet("/places/{id:guid}/menu", async (Guid id, BusinessService biz) => (await biz.Menu(id)).ToHttp());
        biz.MapPost("/places/{id:guid}/menu", async (Guid id, BusinessService biz, JsonElement body) => (await biz.SaveMenu(id, body)).ToHttp());
        biz.MapGet("/places/{id:guid}/promotions", async (Guid id, BusinessService biz) => (await biz.Promotions(id)).ToHttp());
        biz.MapPost("/places/{id:guid}/promotions", async (Guid id, BusinessService biz, JsonElement body) => (await biz.AddPromotion(id, body)).ToHttp());
        biz.MapGet("/places/{id:guid}/photos", (Guid id, BusinessService biz) => biz.Photos(id).ToHttp());
        biz.MapGet("/campaigns", async (ClaimsPrincipal user, BusinessService biz) => (await biz.Campaigns(user)).ToHttp());
        biz.MapPost("/campaigns", async (ClaimsPrincipal user, BusinessService biz, JsonElement body) => (await biz.CreateCampaign(user, body)).ToHttp());
        biz.MapGet("/campaigns/active-package", async (ClaimsPrincipal user, BusinessService biz) => (await biz.ActivePackage(user)).ToHttp());
        biz.MapPut("/campaigns/{id:guid}/complete-payment", async (Guid id, BusinessService biz) => (await biz.PayCampaign(id)).ToHttp());
        biz.MapGet("/campaigns/{id:guid}/bank-transfer", async (Guid id, BusinessService biz) => (await biz.CampaignBank(id)).ToHttp());
        biz.MapPost("/campaigns/{id:guid}/bank-transfer/ack", async (Guid id, BusinessService biz) => (await biz.AckCampaign(id)).ToHttp());
        biz.MapGet("/billing", async (ClaimsPrincipal user, BusinessService biz) => (await biz.Billing(user)).ToHttp());
        biz.MapPost("/register", async (ClaimsPrincipal user, BusinessService biz, JsonElement body) => (await biz.Register(user, body)).ToHttp());
        biz.MapGet("/registration", async (ClaimsPrincipal user, BusinessService biz) => (await biz.Registration(user)).ToHttp());
        biz.MapPost("/", async (ClaimsPrincipal user, BusinessService biz, JsonElement body) => (await biz.Create(user, body)).ToHttp());
        biz.MapGet("/{id:guid}", async (Guid id, BusinessService biz) => (await biz.Get(id)).ToHttp());
        biz.MapPut("/{id:guid}", async (Guid id, ClaimsPrincipal user, BusinessService biz, JsonElement body) => (await biz.Update(id, user, body)).ToHttp());
        biz.MapPut("/{id:guid}/vibes", async (Guid id, ClaimsPrincipal user, BusinessService biz, JsonElement body) => (await biz.Vibes(id, user, body)).ToHttp());

        app.MapGet("/api/places/{id:guid}/menu", async (Guid id, BusinessService biz) => (await biz.Menu(id)).ToHttp());
        app.MapGet("/api/places/{id:guid}/promotions", async (Guid id, BusinessService biz) => (await biz.Promotions(id)).ToHttp());

        var premium = app.MapGroup("/api/users/premium").RequireAuthorization();
        premium.MapGet("/status", async (ClaimsPrincipal user, BusinessService biz) => (await biz.PremiumStatus(user)).ToHttp());
        premium.MapPost("/orders", async (ClaimsPrincipal user, BusinessService biz, JsonElement body) => (await biz.CreatePremium(user, body)).ToHttp());
        premium.MapGet("/orders/{id:guid}", async (Guid id, BusinessService biz) => (await biz.GetPremium(id)).ToHttp());
        premium.MapGet("/orders/{id:guid}/bank-transfer", async (Guid id, BusinessService biz) => (await biz.PremiumBank(id)).ToHttp());
        premium.MapPost("/orders/{id:guid}/bank-transfer/ack", async (Guid id, BusinessService biz) => (await biz.AckPremium(id)).ToHttp());

        app.MapGet("/api/catalog/product-packages", async (BusinessService biz) => (await biz.Catalog()).ToHttp());

        var adminBiz = app.MapGroup("/api/admin/businesses").RequireAuthorization(p => p.RequireRole("Admin"));
        adminBiz.MapGet("/pending", async (BusinessService biz) => (await biz.Pending()).ToHttp());
        adminBiz.MapPost("/{id:guid}/approve", async (Guid id, BusinessService biz) => (await biz.SetStatus(id, "Approved")).ToHttp());
        adminBiz.MapPost("/{id:guid}/reject", async (Guid id, BusinessService biz) => (await biz.SetStatus(id, "Rejected")).ToHttp());

        var adminPay = app.MapGroup("/api/admin/billing").RequireAuthorization(p => p.RequireRole("Admin"));
        adminPay.MapGet("/packages", async (BusinessService biz) => (await biz.AdminPackages()).ToHttp());
        adminPay.MapGet("/payments", async (BusinessService biz) => (await biz.Payments()).ToHttp());
        adminPay.MapPost("/premium-orders/{id:guid}/confirm", async (Guid id, BusinessService biz) => (await biz.ConfirmPremium(id)).ToHttp());
        adminPay.MapPost("/campaigns/{id:guid}/confirm", async (Guid id, BusinessService biz) => (await biz.PayCampaign(id)).ToHttp());

        // Upload ảnh / video lên Cloudinary
        app.MapPost("/api/upload", async (IFormFile file, CloudinaryService cloudinary) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(ApiResponse<object>.Fail("Không có file"));

            await using var stream = file.OpenReadStream();
            var result = await cloudinary.UploadAsync(stream, file.FileName);

            return result.Success
                ? Results.Ok(ApiResponse<object>.Ok(new { url = result.Url, publicId = result.PublicId }))
                : Results.BadRequest(ApiResponse<object>.Fail(result.Error ?? "Upload thất bại"));
        }).RequireAuthorization().DisableAntiforgery();
    }
}
