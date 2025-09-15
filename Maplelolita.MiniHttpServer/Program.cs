using Maplelolita.MiniHttpServer.Middlewares;
using Microsoft.AspNetCore.Http.Features;

namespace Maplelolita.MiniHttpServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var allowDirectoryBrowser = builder.Configuration.GetSection("UseDirectoryBrowser").Get<bool>();
            var usePathFilter = builder.Configuration.GetSection("UsePathFilter").Get<bool>();

            if (allowDirectoryBrowser)
            {
                builder.Services.AddDirectoryBrowser();
            }

            builder.Services.AddWindowsService(opt =>
            {
                opt.ServiceName = "Mini HTTP Server";
            });

            var app = builder.Build();

            if (usePathFilter)
            {
                app.UseMiddleware<SensitivePathFilterMiddleware>();
            }

            if (allowDirectoryBrowser)
            {
                app.UseDirectoryBrowser();
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream",
                HttpsCompression = HttpsCompressionMode.DoNotCompress,
            });

            app.UseMiddleware<NotFoundFallbackMiddleware>();

            app.Run();
        }
    }
}