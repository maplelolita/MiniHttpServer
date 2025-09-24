using Maplelolita.MiniHttpServer.Middlewares;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Maplelolita.MiniHttpServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var appInstance = new ApplicationInstance();
            builder.Services.AddSingleton(appInstance);

            var allowDirectoryBrowser = builder.Configuration.GetSection("UseDirectoryBrowser").Get<bool>();
            var usePathFilter = builder.Configuration.GetSection("UsePathFilter").Get<bool>();
            var useBasicAuth = builder.Configuration.GetSection("UseBasicAuth").Get<bool>();

            if (allowDirectoryBrowser)
            {
                builder.Services.AddDirectoryBrowser();
            }

            if (useBasicAuth)
            {
                builder.Services.AddRazorPages();

                builder.Services.Configure<BasicAuthOptions>(builder.Configuration.GetSection("BasicAuth"));

                builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(options =>
                    {
                        options.LoginPath = "/login";
                        options.LogoutPath = "/logout";
                        options.Cookie.Name = BasicAuthOptions.AuthCookieName;
                        options.Cookie.HttpOnly = true;
                        options.Cookie.SameSite = SameSiteMode.Lax;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                        options.SlidingExpiration = true;
                        options.ExpireTimeSpan = TimeSpan.FromMinutes(BasicAuthOptions.TimeoutMinutes);
                    });

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

            if (useBasicAuth)
            {
                app.UseAuthentication();

                app.UseMiddleware<BasicAuthMiddleware>();
            }

            if (allowDirectoryBrowser)
            {
                if (useBasicAuth)
                {
                    app.UseDirectoryBrowser(new DirectoryBrowserOptions
                    {
                        Formatter = new BasicDirectoryFormatter()
                    });
                }
                else
                {
                    app.UseDirectoryBrowser();
                }
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream",
                HttpsCompression = HttpsCompressionMode.DoNotCompress,
            });

            // 登录页（GET /login）和登录提交（POST /login）
            if (useBasicAuth)
            {
                var authOptions = app.Services.GetRequiredService<IOptions<BasicAuthOptions>>().Value;
                var configuredUser = authOptions.Username;
                var configuredPassword = authOptions.Password;

                app.MapGet("/login", async (HttpContext ctx) =>
                {
                    var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/";

                    if (ctx.User?.Identity?.IsAuthenticated == true)
                    {
                        ctx.Response.Redirect(returnUrl);
                        return;
                    }

                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    await ctx.Response.WriteAsync(RenderLoginPage(returnUrl));
                });

                app.MapPost("/login", async (HttpContext ctx) =>
                {
                    var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
                    var form = await ctx.Request.ReadFormAsync();
                    var username = form["username"].ToString();
                    var password = form["password"].ToString();

                    if (!string.IsNullOrEmpty(configuredUser) && username == configuredUser && password == configuredPassword)
                    {
                        var claims = new[] {
                            new Claim(ClaimTypes.Name, username),
                            new Claim("ServerVersion", appInstance.Id)
                            };

                        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var principal = new ClaimsPrincipal(identity);

                        var authProperties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                        {
                            IsPersistent = true,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                        };

                        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
                        ctx.Response.Redirect(returnUrl);
                        return;
                    }

                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    await ctx.Response.WriteAsync("<p>登录失败</p><a href='/login'>返回</a>");
                });

                app.MapGet("/logout", async (HttpContext ctx) =>
                {
                    var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
                    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    ctx.Response.Redirect(returnUrl);
                });

                app.UseAuthorization();
            }

            app.UseMiddleware<NotFoundFallbackMiddleware>();

            app.Run();
        }

        // helper: render login page HTML (modern centered card) — all visible strings in English
        static string RenderLoginPage(string returnUrl, string error = "", string username = "")
        {
            var escReturn = WebUtility.HtmlEncode(returnUrl);
            var escUser = WebUtility.HtmlEncode(username ?? "");
            var escError = string.IsNullOrEmpty(error) ? "" : $"<div class=\"error\">{WebUtility.HtmlEncode(error)}</div>";
            var escResource = WebUtility.HtmlEncode(WebUtility.UrlDecode(returnUrl ?? "/"));

            var sb = new StringBuilder();

            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"utf-8\"/>");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>");
            sb.AppendLine("  <title>Login</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    :root { --card-bg: #fff; --bg: #f3f4f6; --accent: #2563eb; --muted: #6b7280; }");
            sb.AppendLine("    html,body { height:100%; margin:0; }");
            sb.AppendLine("    body { display:flex; align-items:center; justify-content:center; background:var(--bg); font-family:Segoe UI,Arial,Helvetica,sans-serif; color:#111; }");
            sb.AppendLine("    .card { width:100%; max-width:420px; background:var(--card-bg); padding:22px; border-radius:10px; box-shadow:0 6px 18px rgba(15,23,42,0.08); box-sizing:border-box; }");
            sb.AppendLine("    h1 { margin:0 0 8px; font-size:20px; }");
            sb.AppendLine("    .subtitle { margin:0 0 8px; color:var(--muted); font-size:13px }");
            sb.AppendLine("    .resource { margin:0 0 12px; color:var(--muted); font-size:13px; word-break:break-all }");
            sb.AppendLine("    .error { margin:0 0 12px; color:#b91c1c; background:#fff7f7; padding:8px; border-radius:6px; border:1px solid #fecaca; font-size:13px; }");
            sb.AppendLine("    .form-row { margin-bottom:12px; }");
            sb.AppendLine("    label { display:block; font-size:13px; color:var(--muted); margin-bottom:6px; }");
            sb.AppendLine("    input[type=\"text\"],input[type=\"password\"] { width:100%; padding:10px; border:1px solid #e6e6e6; border-radius:8px; font-size:14px; box-sizing:border-box; }");
            sb.AppendLine("    .actions-top { margin-bottom:10px; }");
            sb.AppendLine("    .actions { margin-top:6px; }");
            sb.AppendLine("    button { width:100%; background:var(--accent); color:#fff; border:none; padding:10px 14px; border-radius:8px; font-weight:600; cursor:pointer; }");
            sb.AppendLine("    a.return { color:var(--muted); font-size:13px; text-decoration:none }");
            sb.AppendLine("    @media (max-width:420px) { .card { margin:16px; } }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class=\"card\" role=\"main\" aria-labelledby=\"login-title\">");
            sb.AppendLine("    <h1 id=\"login-title\">Sign in</h1>");
            sb.AppendLine("    <p class=\"subtitle\">Enter your credentials to access the resource</p>");
            sb.AppendLine("    <div class=\"resource\">Resource: " + escResource + "</div>");
            if (!string.IsNullOrEmpty(escError))
            {
                sb.AppendLine("    " + escError);
            }
            sb.AppendLine("    <form method=\"post\" action=\"/login?returnUrl=" + escReturn + "\" autocomplete=\"off\">");
            sb.AppendLine("      <div class=\"form-row\">");
            sb.AppendLine("        <label for=\"username\">Username</label>");
            sb.AppendLine("        <input id=\"username\" name=\"username\" type=\"text\" value=\"" + escUser + "\" required />");
            sb.AppendLine("      </div>");
            sb.AppendLine("      <div class=\"form-row\">");
            sb.AppendLine("        <label for=\"password\">Password</label>");
            sb.AppendLine("        <input id=\"password\" name=\"password\" type=\"password\" required />");
            sb.AppendLine("      </div>");
            sb.AppendLine("      <div class=\"actions-top\"><a class=\"return\" href=\"/\">Back</a></div>");
            sb.AppendLine("      <div class=\"actions\">");
            sb.AppendLine("        <button type=\"submit\">Sign in</button>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </form>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}