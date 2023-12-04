namespace NineChronicles.Headless.Properties;

public class JwtOptions
{
    public bool EnableJwtAuthentication { get; }

    public string Key { get; } = "";

    public string Issuer { get; } = "planetariumhq.com";
}
