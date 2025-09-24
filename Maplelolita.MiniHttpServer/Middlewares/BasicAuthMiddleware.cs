using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Maplelolita.MiniHttpServer.Middlewares
{
    public class BasicAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ApplicationInstance _applicationInstance;
        private readonly BasicAuthOptions _options;

        public BasicAuthMiddleware(RequestDelegate next, IOptions<BasicAuthOptions> options,ApplicationInstance applicationInstance)
        {
            _next = next;
            _applicationInstance = applicationInstance;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/login", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/logout", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var user = context.User.Identity.Name;
                var server = context.User.FindFirst("ServerVersion")?.Value;

                if (user == _options.Username && server == _applicationInstance.Id)
                {
                    await _next(context);
                    return;
                }

                //delete the cookie
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            var returnUrl = context.Request.Path + context.Request.QueryString;
            var redirectUrl = "/login" + "?returnUrl=" + Uri.EscapeDataString(returnUrl);
            context.Response.Redirect(redirectUrl);
        }
    }
}
