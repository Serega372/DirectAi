using AuthService.Application.Configs;
using AuthService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace AuthService.Application.Services;

public sealed class PasswordHasherService : IPasswordHasherService
{
    private readonly ILogger<PasswordHasherService> _logger;

    private readonly PasswordHasherConfig _config;

    public PasswordHasherService(
        IOptions<PasswordHasherConfig> config,
        ILogger<PasswordHasherService> logger)
    {
        _config = config.Value;
        _logger = logger;

        _logger.LogInformation($"{GetType().Name} was initialized");
    }

    public string GeneratePasswordHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_config.SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, _config.Iterations, HashAlgorithmName.SHA256, 
            _config.OutputHashLenth
        );

        return Convert.ToBase64String(hash);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        var hashToCompare = Convert.FromBase64String(GeneratePasswordHash(password));
        var originalHash = Convert.FromBase64String(hashedPassword);

        return CryptographicOperations.FixedTimeEquals(hashToCompare, originalHash);
    }
}