using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Saga.Web
{
    /// <summary>Punkty utrzymaniowe usługi.</summary>
    internal static class OperationsEndpoints
    {
        public static void MapOperationsEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/health", CheckReadinessAsync)
                .WithName("GetHealth")
                .WithTags(ServiceInfo.OperationsTag)
                .WithSummary("Sonda gotowości dla platformy wdrożeniowej.")
                .WithDescription(
                    "Uruchamia wszystkie zarejestrowane kontrole zależności i zwraca ich stan.")
                .Produces<HealthResponse>(StatusCodes.Status200OK)
                .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable)
                .WithResponseDescriptions(
                    (StatusCodes.Status200OK,
                        "Usługa jest gotowa — żadna zależność nie zgłosiła niesprawności."),
                    (StatusCodes.Status503ServiceUnavailable,
                        "Usługa nie jest gotowa — niesprawną zależność wskazuje pole `dependencies`."));
        }

        private static async Task<IResult> CheckReadinessAsync(
            HealthCheckService healthChecks,
            CancellationToken cancellationToken)
        {
            HealthReport report = await healthChecks.CheckHealthAsync(cancellationToken);
            var body = HealthResponse.From(report);
            return report.Status == HealthStatus.Unhealthy
                ? Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(body);
        }
    }
}
