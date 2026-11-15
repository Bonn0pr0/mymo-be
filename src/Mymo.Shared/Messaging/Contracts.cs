namespace Mymo.Shared.Messaging;

public static class Queues
{
    public const string SetUserRole = "identity.commands.set-role";
    public const string SetUserPremium = "identity.commands.set-premium";
    public const string CreateReport = "identity.commands.create-report";
    public const string UpdatePlaceAd = "map.commands.update-place-ad";
}

public static class Topics
{
    public const string PostCreated = "mymo.social.post-created";
}

public record SetUserRoleCommand(Guid UserId, string Role);

public record SetUserPremiumCommand(Guid UserId, bool IsPremium);

public record CreateReportCommand(
    Guid ReporterId,
    string TargetType,
    Guid TargetId,
    string Reason,
    string? TargetSummary);

public record UpdatePlaceAdCommand(Guid PlaceId, string AdPackageId, string Status);

public record PostCreatedEvent(
    Guid PostId,
    Guid UserId,
    string? DisplayName,
    string? AvatarUrl,
    string? Caption,
    double? Latitude,
    double? Longitude,
    DateTime CreatedAt);
