using Microsoft.AspNetCore.Http.Features;

namespace Maplelolita.MiniHttpServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDirectoryBrowser();

            var app = builder.Build();

            app.UseDirectoryBrowser();

            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream",
                HttpsCompression = HttpsCompressionMode.DoNotCompress,
            });

            app.Run();
        }
    }
}