using System.Collections.Generic;
using System.IO;

namespace NineChronicles.Headless.Tests
{
    public static class TableSheetsImporter
    {
        public static Dictionary<string, string> ImportSheets() =>
            Lib9c.Tests.TableSheetsImporter.ImportSheets(Path
                .GetFullPath("../../")
                .Replace(
                    Path.Combine("NineChronicles.Headless.Tests", "bin"),
                    Path.Combine("Lib9c", "Lib9c", "TableCSV")));
    }
}
