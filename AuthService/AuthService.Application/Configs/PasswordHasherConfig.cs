namespace AuthService.Application.Configs;

public sealed class PasswordHasherConfig
{
    public int Iterations { get; set; }

    public int SaltBytes { get; set; }

    public int OutputHashLenth { get; set; }
}