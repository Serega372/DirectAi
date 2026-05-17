using AuthService.Application.Models.Auth;

namespace AuthService.Application.Interfaces;

public interface IAccessTokenService
{
    Task<TokenVerifyResponse?> VerifyAsync(TokenVerifyRequest request, CancellationToken cancellationToken = default);
}