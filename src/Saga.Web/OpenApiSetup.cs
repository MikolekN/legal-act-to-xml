using System.Globalization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace Saga.Web
{
    /// <summary>Opis API.</summary>
    internal static class OpenApiSetup
    {
        private const int FallbackPort = 8080;

        public static IServiceCollection AddSagaOpenApi(
            this IServiceCollection services,
            IWebHostEnvironment environment)
        {
            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info = new OpenApiInfo
                    {
                        Title = ServiceInfo.Name,
                        Version = BuildInfo.Version,
                        Description = BuildDescription(environment, context.ApplicationServices),
                    };

                    document.Servers?.Clear();
                    document.Tags = DescribeTags();

                    return Task.CompletedTask;
                });

                options.AddSchemaTransformer((schema, context, cancellationToken) =>
                {
                    if (context.JsonTypeInfo.Type == typeof(ProblemDetails))
                    {
                        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                        schema.Properties["traceId"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Description = "Identyfikator żądania — podaj go przy zgłaszaniu błędu.",
                        };
                    }

                    return Task.CompletedTask;
                });
            });

            return services;
        }

        public static TBuilder WithResponseDescriptions<TBuilder>(
            this TBuilder builder,
            params (int Status, string Description)[] descriptions)
            where TBuilder : IEndpointConventionBuilder =>
            builder.AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
            {
                foreach ((int status, string description) in descriptions)
                {
                    string key = status.ToString(CultureInfo.InvariantCulture);

                    if (operation.Responses?.TryGetValue(key, out var response) == true
                        && response is OpenApiResponse described)
                    {
                        described.Description = description;
                    }
                }

                return Task.CompletedTask;
            });

        private static HashSet<OpenApiTag> DescribeTags() =>
        [
            new OpenApiTag
            {
                Name = ServiceInfo.ConversionTag,
                Description = "Zamiana dokumentu źródłowego na XML aktu prawnego.",
            },
            new OpenApiTag
            {
                Name = ServiceInfo.OperationsTag,
                Description = "Sondy i punkty utrzymaniowe poza kontraktem wersjonowanym.",
            },
        ];

        private static string BuildDescription(IWebHostEnvironment environment, IServiceProvider services)
        {
            return string.Join('\n',
            [
                $"Konwersja polskich aktów prawnych ({ServiceInfo.SupportedFormats}) do XML.",
                "",
                $"- Wersja: `{BuildInfo.VersionWithCommit}`",
                $"- Środowisko: `{environment.EnvironmentName}`",
                $"- Runtime: `.NET {Environment.Version}`",
                $"- Port nasłuchu: `{ListeningPort(services)}`",
                $"- Limit rozmiaru dokumentu: `{UploadLimits.MaxSizeText}`",
                "",
                "Każdy błąd wraca jako `ProblemDetails` (`application/problem+json`) z polem `traceId`.",
            ]);
        }

        private static int ListeningPort(IServiceProvider services)
        {
            var addresses = services.GetService<IServer>()?
                .Features.Get<IServerAddressesFeature>()?.Addresses;

            if (addresses is null)
            {
                return FallbackPort;
            }

            foreach (string address in addresses)
            {
                string normalized = address.Replace("//+:", "//localhost:").Replace("//*:", "//localhost:");

                if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Port > 0)
                {
                    return uri.Port;
                }
            }

            return FallbackPort;
        }
    }
}
