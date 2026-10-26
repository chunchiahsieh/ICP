using System.Net;
using System.Security.Cryptography;
using ICP.Infrastructure;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

if (args.Length != 1) throw new ArgumentException("Pass the actual user-guide DOCX path.");
var source = Path.GetFullPath(args[0]);
var expected = await File.ReadAllBytesAsync(source);
var fixture = Path.Combine(Path.GetTempPath(), "ICP-DocumentDownloadChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(fixture, "wwwroot"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = fixture,
    WebRootPath = Path.Combine(fixture, "wwwroot"),
    EnvironmentName = "Production"
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var app = builder.Build();
app.UsePathBase("/ICP");
app.UseRuntimeDocumentFiles(app.Environment);
app.Run(context => { context.Response.StatusCode = 404; return Task.CompletedTask; });
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}

try
{
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features
        .Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient { BaseAddress = new Uri(address) };
    Check((await client.GetAsync("/ICP/docs/ICP-User-Guide.docx")).StatusCode == HttpStatusCode.NotFound,
        "missing document is 404");

    // Create the directory and real document after the Production server has already started.
    var docs = Path.Combine(fixture, "wwwroot", "docs");
    Directory.CreateDirectory(docs);
    File.Copy(source, Path.Combine(docs, "ICP-User-Guide.docx"));
    using var response = await client.GetAsync("/ICP/docs/ICP-User-Guide.docx?v=1.5");
    Check(response.StatusCode == HttpStatusCode.OK, "runtime-added document works under /ICP with version query");
    var bytes = await response.Content.ReadAsByteArrayAsync();
    Check(SHA256.HashData(bytes).SequenceEqual(SHA256.HashData(expected)), "actual complete user-guide bytes match SHA-256");
    Check(response.Content.Headers.ContentType?.MediaType ==
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "DOCX has correct MIME type");
    Check(response.Headers.CacheControl?.NoCache == true, "manual is revalidated instead of immutable build caching");
    Check(response.Headers.ETag is not null, "document returns ETag");

    using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/ICP/docs/ICP-User-Guide.docx"));
    Check(head.StatusCode == HttpStatusCode.OK && head.Content.Headers.ContentLength == expected.Length,
        "HEAD returns actual document length");
    using var conditional = new HttpRequestMessage(HttpMethod.Get, "/ICP/docs/ICP-User-Guide.docx");
    conditional.Headers.IfNoneMatch.Add(response.Headers.ETag!);
    Check((await client.SendAsync(conditional)).StatusCode == HttpStatusCode.NotModified, "unchanged document supports conditional download");

    var replacement = expected.Concat(new byte[] { 1, 2, 3 }).ToArray();
    await File.WriteAllBytesAsync(Path.Combine(docs, "ICP-User-Guide.docx"), replacement);
    using var changed = await client.GetAsync("/ICP/docs/ICP-User-Guide.docx?v=1.5");
    Check((await changed.Content.ReadAsByteArrayAsync()).SequenceEqual(replacement), "same filename serves replacement without rebuilding or restarting");
    Check(changed.Headers.ETag?.Tag != response.Headers.ETag?.Tag, "replacement changes ETag");

    await File.WriteAllTextAsync(Path.Combine(docs, "ICP-Administrator-Guide.docx"), "admin fixture");
    Check((await client.GetStringAsync("/ICP/docs/ICP-Administrator-Guide.docx?v=2.4")) == "admin fixture",
        "administrator guide is still downloadable");
    Check((await client.GetAsync("/docs/ICP-User-Guide.docx")).StatusCode == HttpStatusCode.OK,
        "root-hosted website also works");
    await File.WriteAllTextAsync(Path.Combine(fixture, "wwwroot", "outside.txt"), "outside fixture");
    Check((await client.GetAsync("/outside.txt")).StatusCode == HttpStatusCode.NotFound,
        "runtime middleware does not expand serving to other web-root paths");
    Check((await client.GetAsync("/ICP/docs-other/outside.txt")).StatusCode == HttpStatusCode.NotFound,
        "docs prefix must be a whole path segment");
    Check((await client.GetAsync("/ICP/docs")).StatusCode == HttpStatusCode.NotFound, "directory listing is not enabled");
    Console.WriteLine($"{checks} checks passed; local ephemeral Production HTTP server and temporary files only. No database or SMTP.");
}
finally
{
    await app.StopAsync();
    var verifiedFixture = Path.GetFullPath(fixture);
    if (!verifiedFixture.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
        || !Path.GetFileName(verifiedFixture).StartsWith("ICP-DocumentDownloadChecks-", StringComparison.Ordinal))
        throw new InvalidOperationException("Unexpected fixture cleanup path.");
    Directory.Delete(verifiedFixture, recursive: true);
}
