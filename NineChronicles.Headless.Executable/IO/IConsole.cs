using System.IO;

namespace NineChronicles.Headless.Executable.IO
{
    public interface IConsole
    {
        TextReader In { get; }
        TextWriter Out { get; }
        TextWriter Error { get; }
        Stream OpenStandardOutput();
    }
}
