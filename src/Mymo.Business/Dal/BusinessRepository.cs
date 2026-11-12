using Microsoft.EntityFrameworkCore;
using Mymo.Business.Data;

namespace Mymo.Business.Dal;

public class BusinessRepository : IBusinessRepository
{
    private readonly BusinessDbContext _db;

    public BusinessRepository(BusinessDbContext db) => _db = db;

    public Task Save() => _db.SaveChangesAsync();

    public Task<List<BusinessRow>> OfOwner(Guid? ownerId) =>
        _db.Businesses.Where(b => b.OwnerId == ownerId).ToListAsync();

    public Task<BusinessRow?> Find(Guid id) => _db.Businesses.FirstOrDefaultAsync(b => b.BusinessId == id);

    public void Add(BusinessRow row) => _db.Businesses.Add(row);

    public Task<BusinessRow?> Owned(Guid id, Guid? ownerId) =>
        _db.Businesses.FirstOrDefaultAsync(b => b.BusinessId == id && b.OwnerId == ownerId);

    public Task<BusinessRow?> LatestOf(Guid? ownerId) =>
        _db.Businesses.Where(b => b.OwnerId == ownerId).OrderByDescending(b => b.CreatedAt).FirstOrDefaultAsync();

    public Task<BusinessRow?> FirstOf(Guid? ownerId) =>
        _db.Businesses.FirstOrDefaultAsync(b => b.OwnerId == ownerId);

    public Task<MenuRow?> MenuOf(Guid placeId) => _db.Menus.FirstOrDefaultAsync(m => m.PlaceId == placeId);

    public Task<List<MenuItemRow>> MenuItems(Guid menuId) =>
        _db.MenuItems.Where(i => i.MenuId == menuId).OrderBy(i => i.DisplayOrder).ToListAsync();

    public void AddMenu(MenuRow row) => _db.Menus.Add(row);

    public Task<List<PromotionRow>> Promotions(Guid placeId) =>
        _db.Promotions.Where(p => p.PlaceId == placeId).ToListAsync();

    public void AddPromotion(PromotionRow row) => _db.Promotions.Add(row);

    public Task<List<CampaignRow>> CampaignsOf(Guid businessId) =>
        _db.Campaigns.Where(c => c.BusinessId == businessId).ToListAsync();

    public Task<PackageRow?> Package(string code) => _db.Packages.FirstOrDefaultAsync(p => p.Code == code);

    public void AddCampaign(CampaignRow row) => _db.Campaigns.Add(row);

    public Task<CampaignRow?> ActiveCampaign(Guid businessId) =>
        _db.Campaigns.Where(c => c.BusinessId == businessId && c.PaymentStatus == "Success" && c.EndDate > DateTime.UtcNow)
            .OrderByDescending(c => c.EndDate).FirstOrDefaultAsync();

    public Task<CampaignRow?> FindCampaign(Guid id) => _db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == id);

    public Task<PremiumOrderRow?> ActivePremium(Guid userId) =>
        _db.PremiumOrders.Where(o => o.UserId == userId && o.PaymentStatus == "Success" && o.ValidUntil > DateTime.UtcNow)
            .OrderByDescending(o => o.ValidUntil).FirstOrDefaultAsync();

    public Task<PremiumOrderRow?> PendingPremium(Guid userId) =>
        _db.PremiumOrders.Where(o => o.UserId == userId && o.PaymentStatus != "Success")
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();

    public void AddPremium(PremiumOrderRow row) => _db.PremiumOrders.Add(row);

    public Task<PremiumOrderRow?> FindPremium(Guid id) => _db.PremiumOrders.FirstOrDefaultAsync(o => o.OrderId == id);

    public Task<List<PackageRow>> ActivePackages() =>
        _db.Packages.Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();

    public Task<List<BusinessRow>> Pending() =>
        _db.Businesses.Where(b => b.Status == "Pending").ToListAsync();

    public Task<List<PackageRow>> Packages() => _db.Packages.OrderBy(p => p.SortOrder).ToListAsync();

    public Task<List<PremiumOrderRow>> RecentPremiums() =>
        _db.PremiumOrders.OrderByDescending(o => o.CreatedAt).Take(50).ToListAsync();
}
