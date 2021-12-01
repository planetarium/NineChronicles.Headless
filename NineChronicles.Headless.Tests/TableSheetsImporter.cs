// Copy from Lib9c.Tests
namespace NineChronicles.Headless.Tests
{
    using System.Collections.Generic;
    using System.IO;

    public static class TableSheetsImporter
    {
        public static Dictionary<string, string> ImportSheets(
            string? dir = null)
        {
            var path = Path.Combine("..", "..", "..", "..", "Lib9c", "Lib9c", "TableCSV");
            var files = Directory.GetFiles(path, "*.csv", SearchOption.AllDirectories);
            var sheets = new Dictionary<string, string>();
            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                if (fileName.EndsWith(".csv"))
                {
                    fileName = fileName.Split(".csv")[0];
                }

                sheets[fileName] = File.ReadAllText(filePath);
            }

            return sheets;
        }
    }
}
