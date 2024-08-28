namespace NineChronicles.Headless.Executor.Constants;

public class Paths
{
    public static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".planetarium",
        "headless"
    );
    public static readonly string HeadlessPath = Path.Combine(BasePath, "versions");

    public static readonly string AppsettingsPath = Path.Combine(BasePath, "appsettings");

    public static readonly string AppsettingsTplPath = Path.Combine(BasePath, "templates");

    public static readonly string StorePath = Path.Combine(BasePath, "store");

    public static readonly string GenesisBlockPath = Path.Combine(BasePath, "genesis-block");
}
