using AuthService.Application.Interfaces;
using AuthService.Application.Models.Auth;
using AuthService.Application.Models.RefreshTokens;
using AuthService.Core.Entities;
using AuthService.Core.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace AuthService.Application.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    private readonly IUserRepository _userRepository;

    private readonly IAccessTokenRepository _accessTokenRepository;

    private readonly IUnitOfWork _unitOfWork;

    private readonly ILogger<RefreshTokenService> _logger;

    private readonly IMapper _mapper;

    public RefreshTokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IAccessTokenRepository accessTokenRepository,
        IUnitOfWork unitOfWork,
        ILogger<RefreshTokenService> logger,
        IMapper mapper)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _accessTokenRepository = accessTokenRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;

        _logger.LogInformation($"{GetType().Name} was initialized");
    }

    public async Task<TokenRefreshResponse?> RefreshTokenAsync(TokenRefreshRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var revokedToken = await RevokeAsync(request.RefreshToken, cancellationToken);
            if (revokedToken is null)
            {
                return null;
            }

            var existingUserByRevokedToken = await _userRepository.GetByIdAsync(revokedToken.UserId, cancellationToken: cancellationToken);
            if (existingUserByRevokedToken is null)
            {
                return null;
            }

            var refreshTokenDto = await _refreshTokenRepository.CreateAsync(existingUserByRevokedToken.Id, cancellationToken);
            if (refreshTokenDto is null)
            {
                throw new ApplicationException();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var accessTokenDto = await _accessTokenRepository.CreateAsync(existingUserByRevokedToken.Id, refreshTokenDto.Id, cancellationToken);
            if (accessTokenDto is null)
            {
                throw new ApplicationException();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);

            return new TokenRefreshResponse
            {
                AccessToken = accessTokenDto.Token,
                RefreshToken = refreshTokenDto.Token
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(transaction, cancellationToken);

            throw;
        }
    }

    public async Task<RefreshTokenDto?> RevokeAsync(Guid refreshToken, CancellationToken cancellationToken = default)
    {
        Expression<Func<RefreshTokenEntity, bool>> refreshTokenPredicate = entity =>
            entity.Token == refreshToken
            && entity.RevocationDate == null
            && entity.ExpirationDate > DateTime.UtcNow;

        var foundRefreshToken = (await _refreshTokenRepository.GetAsync(refreshTokenPredicate, false, cancellationToken: cancellationToken)).FirstOrDefault();
        if (foundRefreshToken is null)
        {
            return null;
        }

        foundRefreshToken.RevocationDate = DateTime.UtcNow;
        _refreshTokenRepository.Update(foundRefreshToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RefreshTokenDto>(foundRefreshToken);
    }

    public async Task<RefreshTokenDto?> CreateAsync(long userId, CancellationToken cancellationToken = default)
    {
        var createdToken = _refreshTokenRepository.CreateAsync(userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RefreshTokenDto>(createdToken);
    }
}