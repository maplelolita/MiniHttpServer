namespace Maplelolita.MiniHttpServer.Middlewares
{
    public class BasicAuthOptions
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public static int TimeoutMinutes { get; set; } = 60;

        public static string AuthCookieName => "MiniAuth";

    }
}
