namespace Maplelolita.MiniHttpServer.Middlewares
{
    public class NotFoundFallbackMiddleware
    {
        private readonly RequestDelegate _next;

        public NotFoundFallbackMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);
            if (context.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("404 Not Found");
            }
        }
    }
}
