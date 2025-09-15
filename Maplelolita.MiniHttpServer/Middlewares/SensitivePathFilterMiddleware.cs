using System.Text.RegularExpressions;

namespace Maplelolita.MiniHttpServer.Middlewares
{
    public class SensitivePathFilterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string[] _filters;
        public SensitivePathFilterMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _filters = configuration.GetSection("PathFilter").Get<string[]>() ?? [];
        }
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;

            if ( _filters.Length == 0 || string.IsNullOrEmpty(path))
            {
                await _next(context);
                return;
            }

            //use partten to match the path
            if (_filters.Any(filter => Regex.IsMatch(path, filter, RegexOptions.IgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Access to this resource is forbidden.");
                return;
            }

            // Continue processing the request
            await _next(context);
        }

    }
}
