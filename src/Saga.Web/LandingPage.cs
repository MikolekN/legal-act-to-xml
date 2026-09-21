using System.Text.Encodings.Web;
using System.Text.Unicode;
using Serilog;

namespace Saga.Web
{
    /// <summary>Strona powitalna - wizytówka usługi z odnośnikiem do dokumentacji API.</summary>
    internal static class LandingPage
    {
        private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

        public static void MapLandingPage(this WebApplication app)
        {
            IWebHostEnvironment environment = app.Environment;

            if (environment.IsDevelopment())
            {
                app.MapGet("/", () => Serve(Render(environment))).ExcludeFromDescription();
                return;
            }

            string? page = Render(environment);

            app.MapGet("/", () => Serve(page)).ExcludeFromDescription();
        }

        private static IResult Serve(string? page) =>
            page is null
                ? Results.Redirect(ServiceInfo.DocsPath)
                : Results.Content(page, "text/html; charset=utf-8");

        private static string? Render(IWebHostEnvironment environment)
        {
            string path = Path.Combine(environment.ContentRootPath, "Pages", "index.html");

            if (!File.Exists(path))
            {
                Log.Error("Nie znaleziono szablonu strony powitalnej: {Path}.", path);
                return null;
            }

            return File.ReadAllText(path)
                .Replace("{{service_name}}", Encoder.Encode(ServiceInfo.Name))
                .Replace("{{version}}", Encoder.Encode(BuildInfo.VersionWithCommit))
                .Replace("{{docs_path}}", Encoder.Encode(ServiceInfo.DocsPath));
        }
    }
}
