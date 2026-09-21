namespace Saga.Web
{
    /// <summary>Punkty utrzymaniowe usługi.</summary>
    internal static class OperationsEndpoints
    {
        public static void MapOperationsEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/health", () => Results.Text("ok"))
                .WithName("GetHealth")
                .WithTags(ServiceInfo.OperationsTag)
                .WithSummary("Sonda zdrowia dla platformy wdrożeniowej.")
                .WithDescription("Zwraca `ok`, jeśli proces przyjmuje żądania. Nie sprawdza zależności.")
                .Produces(StatusCodes.Status200OK, contentType: "text/plain")
                .WithResponseDescriptions((StatusCodes.Status200OK, "Usługa przyjmuje żądania."));
        }
    }
}
