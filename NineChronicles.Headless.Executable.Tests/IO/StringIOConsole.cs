using System;
using System.IO;
using NineChronicles.Headless.Executable.IO;

namespace NineChronicles.Headless.Executable.Tests.IO
{
    public sealed class StringIOConsole : IConsole
    {
        public StringIOConsole(StringReader @in, StringWriter @out, StringWriter error)
        {
            In = @in;
            Out = @out;
            Error = error;
        }

        public StringIOConsole(string input = "")
            : this(new StringReader(input), new StringWriter(), new StringWriter())
        {
        }

        public StringReader In { get; }

        public StringWriter Out { get; }

        public StringWriter Error { get; }
        public Stream OpenStandardOutput()
        {
            throw new NotImplementedException();
        }

        TextReader IConsole.In => In;

        TextWriter IConsole.Out => Out;

        TextWriter IConsole.Error => Error;
    }
}
