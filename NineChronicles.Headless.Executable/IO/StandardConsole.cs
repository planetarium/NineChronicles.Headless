using System;
using System.IO;

namespace NineChronicles.Headless.Executable.IO
{
    public class StandardConsole : IConsole
    {
        public StandardConsole()
        {
            In = Console.In;
            Out = Console.Out;
            Error = Console.Error;
        }

        public TextReader In { get; }

        public TextWriter Out { get; }

        public TextWriter Error { get; }
        public Stream OpenStandardOutput()
        {
            return Console.OpenStandardOutput();
        }
    }
}
