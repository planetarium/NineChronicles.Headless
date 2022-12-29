using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Json.Schema;
using Libplanet;

namespace NineChronicles.Headless.Executable;

public static class JsonSchemaExtensions
{
    public static async ValueTask<JsonSchema> FromUri(Uri uri, bool cache = true)
    {
        if (cache)
        {
            try
            {
                using FileStream fileStream = File.OpenRead(GetCachePath(uri));
                return await JsonSchema.FromStream(fileStream);
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                // The case that the cache file does not exist; simply ignore.
            }
        }

        using var client = new System.Net.Http.HttpClient();
        var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        using Stream stream = await response.Content.ReadAsStreamAsync();

        if (cache)
        {
            using FileStream fileStream = File.OpenWrite(GetCachePath(uri));
            await stream.CopyToAsync(fileStream);
            stream.Seek(0, SeekOrigin.Begin);
        }

        return await JsonSchema.FromStream(stream);
    }

    public static ValueTask<JsonSchema> FromUri(string uri, bool cache = true) =>
        FromUri(new Uri(uri), cache);

    public static EvaluationResults EvaluateFile(
        this JsonSchema schema,
        string filePath,
        out JsonDocument document,
        EvaluationOptions? options = null)
    {
        using FileStream fileStream = File.OpenRead(filePath);
        document = JsonDocument.Parse(fileStream);
        return schema.Evaluate(document, options);
    }

    public static EvaluationResults EvaluateFile(
        this JsonSchema schema,
        string filePath,
        EvaluationOptions? options = null
    ) =>
        schema.EvaluateFile(filePath, out _, options);

    private static string GetCachePath(Uri uri)
    {
        Assembly asm = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        string appName = asm.GetName().Name ??
                         $"{nameof(NineChronicles)}.{nameof(Headless)}.{nameof(Executable)}";
        string root = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
        string dir = Path.Combine(root, appName, ".json-schema-cache", uri.Host);
        Directory.CreateDirectory(dir);
        HashDigest<SHA1> sha1 = HashDigest<SHA1>.DeriveFrom(Encoding.UTF8.GetBytes(uri.ToString()));
        return Path.Combine(dir, $"{sha1}.json");
    }
}
