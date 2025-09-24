using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Maplelolita.MiniHttpServer.Middlewares
{
    public class BasicDirectoryFormatter : IDirectoryFormatter
    {
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 500;

        public async Task GenerateContentAsync(HttpContext context, IEnumerable<IFileInfo> contents)
        {
            context.Response.ContentType = "text/html; charset=utf-8";

            var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
            // ensure path ends with '/'
            var basePath = path.EndsWith('/') ? path : path + "/";

            // pagination
            var query = context.Request.Query;
            var page = 1;
            var pageSize = DefaultPageSize;
            if (query.TryGetValue("page", out var pVal) && int.TryParse(pVal.FirstOrDefault(), out var p) && p > 0) page = p;
            if (query.TryGetValue("pageSize", out var sVal) && int.TryParse(sVal.FirstOrDefault(), out var s) && s > 0)
            {
                pageSize = Math.Min(s, MaxPageSize);
            }

            var ordered = contents
                .OrderBy(f => f.IsDirectory ? 0 : 1)
                .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var totalItems = ordered.Length;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var skip = (page - 1) * pageSize;
            var pageItems = ordered.Skip(skip).Take(pageSize).ToArray();

            string BuildPageLink(int targetPage)
            {
                var sbq = new StringBuilder();
                sbq.Append(context.Request.Path.Value);
                sbq.Append('?');
                sbq.Append("page=");
                sbq.Append(targetPage);
                sbq.Append("&pageSize=");
                sbq.Append(pageSize);
                return WebUtility.HtmlEncode(sbq.ToString());
            }

            // build breadcrumb segments
            var segments = GetPathSegments(path);
            string BuildSegmentLink(int index)
            {
                if (index == 0) return "/"; // root
                var segPath = string.Join("/", segments.Take(index + 1));
                if (!segPath.StartsWith("/")) segPath = "/" + segPath;
                if (!segPath.EndsWith("/")) segPath += "/";
                return segPath;
            }

            var parentPath = GetParentPath(path);
            var returnUrl = WebUtility.UrlEncode(path + context.Request.QueryString);

            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"utf-8\" />");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\" />");
            sb.AppendLine("  <title>Index of " + WebUtility.HtmlEncode(path) + "</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    :root { --card-bg: #fff; --bg: #f3f4f6; --accent: #2563eb; --muted: #6b7280; }");
            sb.AppendLine("    html,body { height:100%; margin:0; }");
            sb.AppendLine("    body { background:var(--bg); font-family:\"Microsoft YaHei\", \"Segoe UI\", Arial, Helvetica, sans-serif; color:#111; font-size:16px; }");
            sb.AppendLine("    .container { max-width:1100px; margin:28px auto; padding:18px; box-sizing:border-box; }");
            sb.AppendLine("    .card { background:var(--card-bg); padding:14px; border-radius:10px; box-shadow:0 6px 18px rgba(15,23,42,0.06); }");
            sb.AppendLine("    .header { display:flex; justify-content:space-between; align-items:flex-start; gap:12px; margin-bottom:12px; flex-wrap:wrap }");
            sb.AppendLine("    .breadcrumb { font-size:1rem; color:var(--muted); }");
            sb.AppendLine("    .breadcrumb a { color:var(--accent); text-decoration:none; margin-right:6px; }");
            sb.AppendLine("    .breadcrumb span.sep { color: #9CA3AF; margin-right:6px; }");
            sb.AppendLine("    .controls { display:flex; align-items:center; gap:8px; }");
            sb.AppendLine("    .auth { font-size:1rem; color:var(--muted); }");
            sb.AppendLine("    .auth a { margin-left:10px; color:var(--muted); text-decoration:none; padding:6px 10px; background:#f8fafc; border-radius:8px; border:1px solid #eef2f7; }");
            sb.AppendLine("    .table-wrap { overflow:auto; }");
            sb.AppendLine("    table { width:100%; border-collapse:collapse; font-size:1rem; }");
            sb.AppendLine("    thead th { text-align:left; padding:10px 12px; color:#374151; font-weight:600; border-bottom:1px solid #eef2f6; }");
            sb.AppendLine("    tbody td { padding:10px 12px; border-bottom:1px solid #f1f5f9; }");
            sb.AppendLine("    a.name { color:var(--accent); text-decoration:none; }");
            sb.AppendLine("    a.name:hover { text-decoration:underline; }");
            sb.AppendLine("    .pager { margin-top:12px; display:flex; gap:8px; align-items:center; flex-wrap:wrap }");
            sb.AppendLine("    .pager a, .pager span { padding:6px 10px; border-radius:6px; text-decoration:none; color:var(--muted); background:#f8fafc; border:1px solid #eef2f7; }");
            sb.AppendLine("    .pager .current { background:var(--accent); color:#fff; border-color:var(--accent); }");
            sb.AppendLine("    @media (max-width:720px) { .container { margin:12px; } thead th, tbody td { padding:8px; } }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class=\"container\">");
            sb.AppendLine("    <div class=\"card\">");
            sb.AppendLine("      <div class=\"header\">");

            // breadcrumb (no title)
            sb.Append("        <div class=\"breadcrumb\">");
            for (int i = 0; i < segments.Length; i++)
            {
                var segName = segments[i].Length == 0 ? "Home" : WebUtility.HtmlEncode(segments[i]);
                if (i < segments.Length - 1)
                {
                    // link
                    sb.Append($"<a href=\"{WebUtility.HtmlEncode(BuildSegmentLink(i))}\">{segName}</a>");
                    sb.Append("<span class=\"sep\">/</span>");
                }
                else
                {
                    // current (no link)
                    sb.Append($"<span>{segName}</span>");
                }
            }
            sb.AppendLine("</div>");

            // controls: only auth (no Up button)
            sb.AppendLine("        <div class=\"controls\">");
            var username = context.User?.Identity?.Name ?? string.Empty;
            var displayUser = string.IsNullOrEmpty(username) ? "User" : WebUtility.HtmlEncode(username);
            sb.AppendLine($"          <div class=\"auth\">{displayUser}<a href=\"/logout?returnUrl={returnUrl}\">Logout</a></div>");
            sb.AppendLine("        </div>");

            sb.AppendLine("      </div>"); // header end

            sb.AppendLine("      <div class=\"table-wrap\">");
            sb.AppendLine("        <table>");
            sb.AppendLine("          <thead><tr><th>Name</th><th>Size</th><th>Last modified</th></tr></thead>");
            sb.AppendLine("          <tbody>");

            // show ".." entry for non-root to return to parent
            if (!string.Equals(path, "/", StringComparison.Ordinal))
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"  <td><a class=\"name\" href=\"{WebUtility.HtmlEncode(parentPath)}\">..</a></td><td></td><td></td>");
                sb.AppendLine("</tr>");
            }

            foreach (var item in pageItems)
            {
                var name = item.Name;
                var displayName = WebUtility.HtmlEncode(name) + (item.IsDirectory ? "/" : "");
                var href = basePath + Uri.EscapeDataString(name) + (item.IsDirectory ? "/" : "");
                var size = item.IsDirectory ? "-" : FormatSize(item.Length);
                var lastModified = item.IsDirectory ? "" : item.LastModified.ToString("yyyy-MM-dd HH:mm:ss");

                sb.AppendLine("<tr>");
                sb.AppendLine($"  <td><a class=\"name\" href=\"{href}\">{displayName}</a></td>");
                sb.AppendLine($"  <td>{WebUtility.HtmlEncode(size)}</td>");
                sb.AppendLine($"  <td>{WebUtility.HtmlEncode(lastModified)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("          </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("      </div>");

            // pager
            sb.AppendLine("      <div class=\"pager\" role=\"navigation\" aria-label=\"Pagination\">");
            if (page > 1)
            {
                sb.AppendLine($"        <a href=\"{BuildPageLink(page - 1)}\">Previous</a>");
            }
            else
            {
                sb.AppendLine("        <span aria-disabled=\"true\">Previous</span>");
            }

            var start = Math.Max(1, page - 5);
            var end = Math.Min(totalPages, page + 4);
            if (start > 1)
            {
                sb.AppendLine($"        <a href=\"{BuildPageLink(1)}\">1</a>");
                if (start > 2) sb.AppendLine("        <span>...</span>");
            }

            for (int i = start; i <= end; i++)
            {
                if (i == page)
                    sb.AppendLine($"        <span class=\"current\">{i}</span>");
                else
                    sb.AppendLine($"        <a href=\"{BuildPageLink(i)}\">{i}</a>");
            }

            if (end < totalPages)
            {
                if (end < totalPages - 1) sb.AppendLine("        <span>...</span>");
                sb.AppendLine($"        <a href=\"{BuildPageLink(totalPages)}\">{totalPages}</a>");
            }

            if (page < totalPages)
            {
                sb.AppendLine($"        <a href=\"{BuildPageLink(page + 1)}\">Next</a>");
            }
            else
            {
                sb.AppendLine("        <span aria-disabled=\"true\">Next</span>");
            }

            sb.AppendLine($"        <span style=\"margin-left:8px;color:var(--muted);\">Page {page} of {totalPages}, {totalItems} items</span>");
            sb.AppendLine("      </div>");

            sb.AppendLine("    </div>"); // card
            sb.AppendLine("  </div>"); // container
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            await context.Response.WriteAsync(sb.ToString());
        }

        private static string[] GetPathSegments(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return new[] { string.Empty }; // Home
            var trimmed = path.Trim('/');
            var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // build array with leading empty string representing Home
            var result = new string[parts.Length + 1];
            result[0] = string.Empty; // Home
            for (int i = 0; i < parts.Length; i++) result[i + 1] = parts[i];
            return result;
        }

        private static string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return "/";
            var trimmed = path.TrimEnd('/');
            var idx = trimmed.LastIndexOf('/');
            if (idx <= 0) return "/";
            return trimmed.Substring(0, idx + 1);
        }

        private static string FormatSize(long length)
        {
            if (length < 0) return "-";
            if (length < 1024) return length + " B";
            if (length < 1024 * 1024) return (length / 1024.0).ToString("0.0") + " KB";
            if (length < 1024 * 1024 * 1024) return (length / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            return (length / (1024.0 * 1024.0 * 1024.0)).ToString("0.0") + " GB";
        }
    }
}