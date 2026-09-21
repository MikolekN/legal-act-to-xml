using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Saga.Web
{
    /// <summary>Treść odpowiedzi sondy gotowości.</summary>
    internal sealed record HealthResponse(
        string Status,
        IReadOnlyDictionary<string, HealthDependencyStatus> Dependencies)
    {
        public static HealthResponse From(HealthReport report) => new(
            report.Status.ToString(),
            report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthDependencyStatus(entry.Value.Status.ToString(), entry.Value.Description)));
    }

    /// <summary>Stan pojedynczej zależności.</summary>
    internal sealed record HealthDependencyStatus(string Status, string? Description);
}
