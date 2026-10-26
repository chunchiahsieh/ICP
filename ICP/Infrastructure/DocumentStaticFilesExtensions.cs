using Microsoft.Extensions.FileProviders;

namespace ICP.Infrastructure;

public static class DocumentStaticFilesExtensions
{
    /// <summary>Read replaceable manuals from disk instead of the build-time asset manifest.</summary>
    public static IApplicationBuilder UseRuntimeDocumentFiles(
        this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        if (!Directory.Exists(webRoot))
        {
            return app;
        }

        var provider = new PhysicalFileProvider(webRoot);
        app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStopped.Register(provider.Dispose);

        // Limit this runtime path to public documentation. Other assets keep MapStaticAssets.
        return app.UseWhen(context => context.Request.Path.StartsWithSegments("/docs"), branch =>
            branch.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = provider,
                OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
            }));
    }
}
