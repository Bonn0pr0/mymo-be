using System.Net.Http.Json;
using System.Text.Json;
using Mymo.Business.Dal;
using Mymo.Business.Data;
using Mymo.Shared;
using Mymo.Shared.Messaging;

namespace Mymo.Business.Bll;

public class BusinessService
{
    private readonly IBusinessRepository _data;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly ICommandBus _commands;

    public BusinessService(IBusinessRepository data, IConfiguration config, IHttpClientFactory http, ICommandBus commands)
    {
        _data = data;
        _config = config;
        _http = http;
        _commands = commands;
    }

    static object BizDto(BusinessRow row) => new
    {
        row.BusinessId,
        row.Name,
        row.Description,
        row.LogoUrl,
        row.Phone,
        row.Email,
        row.Website,
        vibes = row.Vibes.Split(',', StringSplitOptions.RemoveEmptyEntries),
        row.Verified,
        row.Status,
        row.Address,
        row.Latitude,
        row.Longitude,
        row.RejectReason,
        row.CreatedAt,
        row.OwnerId,
    };

    public async Task<ServiceResult> Mine(ClaimsPrincipal user)
    {
        var list = await _data.OfOwner(HttpUser.Id(user));
        return ServiceResult.Ok(ApiResponse<object>.Ok(new { businesses = list.Select(BizDto) }));
    }

    public ServiceResult PlaceModels() => ServiceResult.Ok(ApiResponse<object>.Ok(Array.Empty<object>()));

    public async Task<ServiceResult> Get(Guid id)
    {
        var row = await _data.Find(id);
        return row == null ? ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy quán")) : ServiceResult.Ok(ApiResponse<object>.Ok(BizDto(row)));
    }

    public async Task<ServiceResult> Create(ClaimsPrincipal user, JsonElement body)
    {
        var id = HttpUser.Id(user);
        if (id == null) return ServiceResult.Unauthorized();
        var row = new BusinessRow
        {
            BusinessId = Guid.NewGuid(),
            OwnerId = id.Value,
            Name = JsonBody.Text(body, "name") ?? "Quán mới",
            Description = JsonBody.Text(body, "description"),
            Phone = JsonBody.Text(body, "phone"),
            Email = JsonBody.Text(body, "email"),
            Status = "Approved",
            CreatedAt = DateTime.UtcNow,
        };
        _data.Add(row);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(BizDto(row)));
    }

    public async Task<ServiceResult> Update(Guid id, ClaimsPrincipal user, JsonElement body)
    {
        var row = await _data.Owned(id, HttpUser.Id(user));
        if (row == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy quán"));
        row.Name = JsonBody.Text(body, "name") ?? row.Name;
        row.Description = JsonBody.Text(body, "description") ?? row.Description;
        row.Phone = JsonBody.Text(body, "phone") ?? row.Phone;
        row.Email = JsonBody.Text(body, "email") ?? row.Email;
        row.Website = JsonBody.Text(body, "website") ?? row.Website;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(BizDto(row)));
    }

    public async Task<ServiceResult> Vibes(Guid id, ClaimsPrincipal user, JsonElement body)
    {
        var row = await _data.Owned(id, HttpUser.Id(user));
        if (row == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy quán"));
        if (body.TryGetProperty("vibes", out var vibes) && vibes.ValueKind == JsonValueKind.Array)
            row.Vibes = string.Join(',', vibes.EnumerateArray().Select(v => v.GetString()).Where(v => !string.IsNullOrWhiteSpace(v)));
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(BizDto(row)));
    }

    public async Task<ServiceResult> Register(ClaimsPrincipal user, JsonElement body)
    {
        var id = HttpUser.Id(user);
        if (id == null) return ServiceResult.Unauthorized();
        var row = new BusinessRow
        {
            BusinessId = Guid.NewGuid(),
            OwnerId = id.Value,
            Name = JsonBody.Text(body, "businessName") ?? "Quán",
            Description = JsonBody.Text(body, "description"),
            Phone = JsonBody.Text(body, "phone"),
            Address = JsonBody.Text(body, "address"),
            Latitude = JsonBody.Num(body, "latitude"),
            Longitude = JsonBody.Num(body, "longitude"),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
        };
        _data.Add(row);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(BizDto(row)));
    }

    public async Task<ServiceResult> Registration(ClaimsPrincipal user)
    {
        var row = await _data.LatestOf(HttpUser.Id(user));
        return row == null ? ServiceResult.NotFound(ApiResponse<object>.Fail("Chưa đăng ký")) : ServiceResult.Ok(ApiResponse<object>.Ok(BizDto(row)));
    }

    public async Task<ServiceResult> MyPlaces(ClaimsPrincipal user)
    {
        var business = await _data.FirstOf(HttpUser.Id(user));
        if (business == null) return ServiceResult.Ok(ApiResponse<object>.Ok(Array.Empty<object>()));
        var places = await Internal.GetAsync<List<JsonElement>>(_http, $"{_config["Services:Map"]}/internal/places/by-business/{business.BusinessId}") ?? [];
        return ServiceResult.Ok(ApiResponse<object>.Ok(places));
    }

    public async Task<ServiceResult> OnePlace(Guid id)
    {
        var place = await Internal.GetAsync<JsonElement>(_http, $"{_config["Services:Map"]}/internal/places/{id}");
        return place.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy địa điểm"))
            : ServiceResult.Ok(ApiResponse<object>.Ok(place));
    }

    public async Task<ServiceResult> CreatePlace(ClaimsPrincipal user, JsonElement body)
    {
        var business = await _data.FirstOf(HttpUser.Id(user));
        if (business == null) return ServiceResult.BadRequest(ApiResponse<object>.Fail("Bạn chưa có quán"));
        var created = await Internal.PostAsync<JsonElement>(_http, $"{_config["Services:Map"]}/internal/places", new
        {
            businessId = business.BusinessId,
            businessName = business.Name,
            ownerId = business.OwnerId,
            name = JsonBody.Text(body, "name"),
            description = JsonBody.Text(body, "description"),
            categoryId = JsonBody.Text(body, "categoryId"),
            address = JsonBody.Text(body, "address"),
            phone = JsonBody.Text(body, "phone"),
            openingHours = JsonBody.Text(body, "openingHours"),
            latitude = JsonBody.Num(body, "latitude"),
            longitude = JsonBody.Num(body, "longitude"),
            status = "Pending",
        });
        return ServiceResult.Ok(ApiResponse<object>.Ok(created));
    }

    public async Task<ServiceResult> UpdatePlace(Guid id, JsonElement body)
    {
        using var client = _http.CreateClient("internal");
        var response = await client.PutAsJsonAsync($"{_config["Services:Map"]}/internal/places/{id}", body);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        return ServiceResult.Ok(ApiResponse<object>.Ok(updated));
    }

    public ServiceResult Analytics() => ServiceResult.Ok(ApiResponse<object>.Ok(new
    {
        totalCheckIns = 0,
        todayCheckIns = 0,
        weeklyCheckIns = 0,
        monthlyCheckIns = 0,
        totalMapViews = 0,
        dailyCheckIns = Array.Empty<object>(),
        dailyMapViews = Array.Empty<object>(),
    }));

    public ServiceResult Model(Guid id) => ServiceResult.Ok(ApiResponse<object>.Ok(new { placeId = id }));

    public async Task<ServiceResult> Menu(Guid id)
    {
        var menu = await _data.MenuOf(id);
        if (menu == null) return ServiceResult.Ok(ApiResponse<object>.Ok(new { id = Guid.Empty, placeId = id, name = "Menu", description = (string?)null, items = Array.Empty<object>() }));
        var items = await _data.MenuItems(menu.MenuId);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            id = menu.MenuId,
            menu.PlaceId,
            menu.Name,
            menu.Description,
            items = items.Select(i => new { id = i.MenuItemId, i.Name, i.Description, price = i.Price, imageUrl = i.ImageUrl, i.CategoryName, i.DisplayOrder }),
        }));
    }

    public async Task<ServiceResult> SaveMenu(Guid id, JsonElement body)
    {
        var menu = await _data.MenuOf(id);
        if (menu == null)
        {
            menu = new MenuRow { MenuId = Guid.NewGuid(), PlaceId = id, Name = JsonBody.Text(body, "name") ?? "Menu" };
            _data.AddMenu(menu);
        }
        menu.Name = JsonBody.Text(body, "name") ?? menu.Name;
        menu.Description = JsonBody.Text(body, "description");
        await _data.Save();
        return await Menu(id);
    }

    public async Task<ServiceResult> Promotions(Guid id) =>
        ServiceResult.Ok(ApiResponse<object>.Ok(await _data.Promotions(id)));

    public async Task<ServiceResult> AddPromotion(Guid id, JsonElement body)
    {
        var row = new PromotionRow
        {
            PromotionId = Guid.NewGuid(),
            PlaceId = id,
            Title = JsonBody.Text(body, "title") ?? "Ưu đãi",
            Description = JsonBody.Text(body, "description"),
            StartAt = DateTime.UtcNow,
        };
        _data.AddPromotion(row);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(row));
    }

    public ServiceResult Photos(Guid id) => ServiceResult.Ok(ApiResponse<object>.Ok(Array.Empty<object>()));

    public async Task<ServiceResult> Campaigns(ClaimsPrincipal user)
    {
        var business = await _data.FirstOf(HttpUser.Id(user));
        var rows = business == null ? [] : await _data.CampaignsOf(business.BusinessId);
        return ServiceResult.Ok(ApiResponse<object>.Ok(rows));
    }

    public async Task<ServiceResult> CreateCampaign(ClaimsPrincipal user, JsonElement body)
    {
        var business = await _data.FirstOf(HttpUser.Id(user));
        if (business == null) return ServiceResult.BadRequest(ApiResponse<object>.Fail("Bạn chưa có quán"));
        var packageId = JsonBody.Text(body, "packageId") ?? "starter_spot";
        var package = await _data.Package(packageId);
        var row = new CampaignRow
        {
            CampaignId = Guid.NewGuid(),
            BusinessId = business.BusinessId,
            PlaceId = Guid.Parse(JsonBody.Text(body, "placeId") ?? Guid.Empty.ToString()),
            PackageId = packageId,
            AmountVnd = package?.PriceVnd ?? 0,
            TransferReferenceCode = "MM" + Random.Shared.Next(100000, 999999),
            StartDate = DateTime.TryParse(JsonBody.Text(body, "startDate"), out var start) ? start : DateTime.UtcNow,
            EndDate = DateTime.TryParse(JsonBody.Text(body, "endDate"), out var end) ? end : DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
        };
        _data.AddCampaign(row);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(row));
    }

    public async Task<ServiceResult> ActivePackage(ClaimsPrincipal user)
    {
        var business = await _data.FirstOf(HttpUser.Id(user));
        var row = business == null ? null : await _data.ActiveCampaign(business.BusinessId);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new { packageId = row?.PackageId, campaignId = row?.CampaignId, startDate = row?.StartDate, endDate = row?.EndDate }));
    }

    public async Task<ServiceResult> PayCampaign(Guid id)
    {
        var row = await _data.FindCampaign(id);
        if (row == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy chiến dịch"));
        row.PaymentStatus = "Success";
        await _data.Save();
        await _commands.PublishAsync(Queues.UpdatePlaceAd, new UpdatePlaceAdCommand(row.PlaceId, row.PackageId, "Approved"));
        return ServiceResult.Ok(ApiResponse<object>.Ok(row));
    }

    public async Task<ServiceResult> CampaignBank(Guid id)
    {
        var row = await _data.FindCampaign(id);
        if (row == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy chiến dịch"));
        return ServiceResult.Ok(ApiResponse<object>.Ok(Bank(row.CampaignId, row.AmountVnd, row.TransferReferenceCode, "campaignId")));
    }

    public async Task<ServiceResult> AckCampaign(Guid id)
    {
        var row = await _data.FindCampaign(id);
        if (row == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy chiến dịch"));
        row.PaymentStatus = "Submitted";
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(row));
    }

    public async Task<ServiceResult> Billing(ClaimsPrincipal user)
    {
        var business = await _data.FirstOf(HttpUser.Id(user));
        var rows = business == null ? [] : await _data.CampaignsOf(business.BusinessId);
        return ServiceResult.Ok(ApiResponse<object>.Ok(rows));
    }

    public async Task<ServiceResult> PremiumStatus(ClaimsPrincipal user)
    {
        var id = HttpUser.Id(user)!.Value;
        var active = await _data.ActivePremium(id);
        var pending = await _data.PendingPremium(id);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new { activePlanId = active?.PlanId ?? "free", validUntil = active?.ValidUntil, pendingOrderId = pending?.OrderId }));
    }

    public async Task<ServiceResult> CreatePremium(ClaimsPrincipal user, JsonElement body)
    {
        var id = HttpUser.Id(user)!.Value;
        var plan = JsonBody.Text(body, "planId") ?? "monthly";
        var package = await _data.Package(plan);
        var order = new PremiumOrderRow
        {
            OrderId = Guid.NewGuid(),
            UserId = id,
            PlanId = plan,
            AmountVnd = package?.PriceVnd ?? 0,
            TransferReferenceCode = "MP" + Random.Shared.Next(100000, 999999),
            CreatedAt = DateTime.UtcNow,
        };
        _data.AddPremium(order);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(order));
    }

    public async Task<ServiceResult> GetPremium(Guid id)
    {
        var order = await _data.FindPremium(id);
        return order == null ? ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy đơn")) : ServiceResult.Ok(ApiResponse<object>.Ok(order));
    }

    public async Task<ServiceResult> PremiumBank(Guid id)
    {
        var order = await _data.FindPremium(id);
        if (order == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy đơn"));
        return ServiceResult.Ok(ApiResponse<object>.Ok(Bank(order.OrderId, order.AmountVnd, order.TransferReferenceCode, "orderId")));
    }

    public async Task<ServiceResult> AckPremium(Guid id)
    {
        var order = await _data.FindPremium(id);
        if (order == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy đơn"));
        order.PaymentStatus = "Submitted";
        order.PaymentSubmittedAt = DateTime.UtcNow;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(order));
    }

    public async Task<ServiceResult> Catalog()
    {
        var packages = await _data.ActivePackages();
        return ServiceResult.Ok(ApiResponse<object>.Ok(packages.Select(p => new
        {
            p.Segment,
            p.Code,
            p.DisplayName,
            p.PriceVnd,
            p.DurationDays,
            p.BillingUnit,
            p.SortOrder,
        })));
    }

    public async Task<ServiceResult> Pending()
    {
        var rows = await _data.Pending();
        var cards = await Internal.PostAsync<List<UserCard>>(_http, $"{_config["Services:Identity"]}/internal/users/batch", new { ids = rows.Select(r => r.OwnerId) }) ?? [];
        return ServiceResult.Ok(ApiResponse<object>.Ok(rows.Select(r => new
        {
            r.BusinessId,
            r.Name,
            r.Description,
            r.Phone,
            r.Address,
            r.Latitude,
            r.Longitude,
            r.Status,
            r.OwnerId,
            ownerEmail = cards.FirstOrDefault(c => c.UserId == r.OwnerId)?.Username,
            ownerDisplayName = cards.FirstOrDefault(c => c.UserId == r.OwnerId)?.DisplayName,
            r.CreatedAt,
        })));
    }

    public async Task<ServiceResult> SetStatus(Guid id, string status)
    {
        var row = await _data.Find(id);
        if (row == null) return ServiceResult.NotFound();
        row.Status = status;
        if (status == "Approved") row.Verified = true;
        await _data.Save();
        if (status == "Approved")
            await _commands.PublishAsync(Queues.SetUserRole, new SetUserRoleCommand(row.OwnerId, "Business"));
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> AdminPackages() =>
        ServiceResult.Ok(ApiResponse<object>.Ok(new { packages = await _data.Packages() }));

    public async Task<ServiceResult> Payments()
    {
        var orders = await _data.RecentPremiums();
        return ServiceResult.Ok(ApiResponse<object>.Ok(new { items = orders, totalCount = orders.Count }));
    }

    public async Task<ServiceResult> ConfirmPremium(Guid id)
    {
        var order = await _data.FindPremium(id);
        if (order == null) return ServiceResult.NotFound();
        order.PaymentStatus = "Success";
        order.PaidAt = DateTime.UtcNow;
        order.ValidFrom = DateTime.UtcNow;
        var package = await _data.Package(order.PlanId);
        order.ValidUntil = DateTime.UtcNow.AddDays(package?.DurationDays ?? 30);
        await _data.Save();
        await _commands.PublishAsync(Queues.SetUserPremium, new SetUserPremiumCommand(order.UserId, true));
        return ServiceResult.Ok(ApiResponse<object>.Ok(order));
    }

    object Bank(Guid id, decimal amount, string reference, string idName)
    {
        var bin = _config["Bank:BankBin"];
        var account = _config["Bank:AccountNumber"];
        return new Dictionary<string, object?>
        {
            [idName] = id,
            ["amountVnd"] = amount,
            ["transferReferenceCode"] = reference,
            ["bankName"] = _config["Bank:BankName"],
            ["bankBin"] = bin,
            ["accountNumber"] = account,
            ["accountName"] = _config["Bank:AccountName"],
            ["vietQrImageUrl"] = $"https://img.vietqr.io/image/{bin}-{account}-compact.png?amount={amount}&addInfo={reference}",
            ["autoConfirmEnabled"] = false,
        };
    }
}
