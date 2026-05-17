using AuthService.Application.Interfaces;
using AuthService.Application.Models.Auth;
using AuthService.Core;
using AuthService.Core.Entities;
using AuthService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace AuthService.Application.Services;

public sealed class AuthService
{
    private readonly IUserRepository _userRepository;

    private readonly IRoleRepository _roleRepository;

    private readonly IAccessTokenRepository _accessTokenRepository;

    private readonly IRefreshTokenRepository _refreshTokenRepository;

    private readonly IPasswordHasherService _passwordHasherService;

    private readonly IUnitOfWork _unitOfWork;

    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IAccessTokenRepository accessTokenRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasherService passwordHasherService,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _accessTokenRepository = accessTokenRepository;
        _passwordHasherService = passwordHasherService;
        _unitOfWork = unitOfWork;
        _logger = logger;

        _logger.LogInformation($"{GetType().Name} was initialized");
    }

    public async Task<RegisterResponse?> RegisterAsync(RegisterRequest request, bool isAdmin = false, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            Expression<Func<UserEntity, bool>> userByEmailFilter = entity => entity.Email == request.Email;
            var existingUser = (await _userRepository.GetAsync(userByEmailFilter, cancellationToken: cancellationToken)).FirstOrDefault();
            if (existingUser is not null)
            {
                return null;
            }

            var allRoles = await _roleRepository.GetAsync(cancellationToken: cancellationToken);
            var defaultUserRole = allRoles.FirstOrDefault(role => role.Name == ApplicationConstants.DefaultUserRole);
            if (defaultUserRole is null)
            {
                throw new ApplicationException();
            }

            var handleRoleId = request.RoleId ?? defaultUserRole.Id;
            var hashedPassword = _passwordHasherService.GeneratePasswordHash(request.Password);
            var userEntityToCreate = new UserEntity
            {
                Id = default,
                Name = request.Name,
                Username = request.Username,
                Email = request.Email,
                PasswordHash = hashedPassword,
                RoleId = isAdmin
                    ? handleRoleId
                    : defaultUserRole.Id
            };

            var createdUser = await _userRepository.AddAsync(userEntityToCreate, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var refreshTokenDto = await _refreshTokenRepository.CreateAsync(createdUser.Id, cancellationToken);
            if (refreshTokenDto is null)
            {
                throw new ApplicationException();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var accessTokenDto = await _accessTokenRepository.CreateAsync(createdUser.Id, refreshTokenDto.Id, cancellationToken);
            if (accessTokenDto is null)
            {
                throw new ApplicationException();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);

            return new RegisterResponse
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

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            Expression<Func<UserEntity, bool>> userByEmailFilter = entity => entity.Email == request.Email;
            var existingUser = (await _userRepository.GetAsync(userByEmailFilter, cancellationToken: cancellationToken)).FirstOrDefault();
            if (existingUser is null)
            {
                return null;
            }

            var isHashesEquals = _passwordHasherService.VerifyPassword(request.Password, existingUser.PasswordHash);
            if (!isHashesEquals)
            {
                return null;
            }

            var refreshTokenDto = await _refreshTokenRepository.CreateAsync(existingUser.Id, cancellationToken);
            if (refreshTokenDto is null)
            {
                throw new ApplicationException();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var accessTokenDto = await _accessTokenRepository.CreateAsync(existingUser.Id, refreshTokenDto.Id, cancellationToken);
            if (accessTokenDto is null)
            {
                throw new ApplicationException();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);

            return new LoginResponse
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

    public async Task<bool> Logout(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var foundRefreshToken = await _refreshTokenRepository.GetWithAccessTokensAsync(request.RefreshToken, true, cancellationToken: cancellationToken);
        if (foundRefreshToken is null)
        {
            return false;
        }

        foundRefreshToken.RevocationDate = DateTime.UtcNow;
        foreach (var accessToken in foundRefreshToken.AccessTokens)
        {
            accessToken.RevocationDate = DateTime.UtcNow;
        }

        _refreshTokenRepository.Update(foundRefreshToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}