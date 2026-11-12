using Mymo.Business.Data;

namespace Mymo.Business.Dal;

public interface IBusinessRepository
{
    Task Save();
    Task<List<BusinessRow>> OfOwner(Guid? ownerId);
    Task<BusinessRow?> Find(Guid id);
    void Add(BusinessRow row);
    Task<BusinessRow?> Owned(Guid id, Guid? ownerId);
    Task<BusinessRow?> LatestOf(Guid? ownerId);
    Task<BusinessRow?> FirstOf(Guid? ownerId);
    Task<MenuRow?> MenuOf(Guid placeId);
    Task<List<MenuItemRow>> MenuItems(Guid menuId);
    void AddMenu(MenuRow row);
    Task<List<PromotionRow>> Promotions(Guid placeId);
    void AddPromotion(PromotionRow row);
    Task<List<CampaignRow>> CampaignsOf(Guid businessId);
    Task<PackageRow?> Package(string code);
    void AddCampaign(CampaignRow row);
    Task<CampaignRow?> ActiveCampaign(Guid businessId);
    Task<CampaignRow?> FindCampaign(Guid id);
    Task<PremiumOrderRow?> ActivePremium(Guid userId);
    Task<PremiumOrderRow?> PendingPremium(Guid userId);
    void AddPremium(PremiumOrderRow row);
    Task<PremiumOrderRow?> FindPremium(Guid id);
    Task<List<PackageRow>> ActivePackages();
    Task<List<BusinessRow>> Pending();
    Task<List<PackageRow>> Packages();
    Task<List<PremiumOrderRow>> RecentPremiums();
}
