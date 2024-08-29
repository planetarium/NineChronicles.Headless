using NineChronicles.Headless.Executor.Constants;

namespace NineChronicles.Headless.Executor;

public class PlanetManager
{
    public void ListLocalVersions()
    {
        if (!Directory.Exists(Paths.HeadlessPath))
        {
            Console.WriteLine($"Headless folder not found: {Paths.HeadlessPath}");
            return;
        }

        var directories = Directory.GetDirectories(Paths.HeadlessPath);
        if (directories.Length == 0)
        {
            Console.WriteLine("No local versions found.");
            return;
        }

        Console.WriteLine("Installed versions:");
        foreach (var dir in directories)
        {
            Console.WriteLine(Path.GetFileName(dir));
        }
    }
}
