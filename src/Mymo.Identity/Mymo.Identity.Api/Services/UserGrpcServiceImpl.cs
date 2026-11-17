using Grpc.Core;
using Mymo.Identity.Dal;
using Mymo.Shared.Protos;

namespace Mymo.Identity.Api.Services;

public class UserGrpcServiceImpl : UserGrpcService.UserGrpcServiceBase
{
    private readonly IIdentityRepository _repo;
    private readonly ILogger<UserGrpcServiceImpl> _logger;

    public UserGrpcServiceImpl(IIdentityRepository repo, ILogger<UserGrpcServiceImpl> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public override async Task<UserGrpcReply> GetUserById(GetUserByIdRequest request, ServerCallContext context)
    {
        _logger.LogInformation("[gRPC Server] GetUserById called with UserId: {UserId}", request.UserId);

        if (!Guid.TryParse(request.UserId, out var id))
        {
            return new UserGrpcReply { Found = false };
        }

        var user = await _repo.FindUser(id);
        if (user == null || user.IsDeleted)
        {
            return new UserGrpcReply { Found = false };
        }

        return new UserGrpcReply
        {
            Found = true,
            UserId = user.UserId.ToString(),
            Username = user.Username,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl ?? string.Empty,
            IsOnline = user.IsOnline,
            IsPremium = user.IsPremium,
            Role = user.Role
        };
    }

    public override async Task<UsersBatchGrpcReply> GetUsersBatch(GetUsersBatchRequest request, ServerCallContext context)
    {
        _logger.LogInformation("[gRPC Server] GetUsersBatch called with {Count} IDs", request.UserIds.Count);

        var ids = request.UserIds
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var users = await _repo.UsersByIds(ids);
        var reply = new UsersBatchGrpcReply();

        foreach (var user in users)
        {
            reply.Users.Add(new UserGrpcReply
            {
                Found = true,
                UserId = user.UserId.ToString(),
                Username = user.Username,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl ?? string.Empty,
                IsOnline = user.IsOnline,
                IsPremium = user.IsPremium,
                Role = user.Role
            });
        }

        return reply;
    }
}
