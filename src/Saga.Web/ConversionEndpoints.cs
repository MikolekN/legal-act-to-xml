using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Saga.Core;
using Saga.Core.Exceptions;
using Saga.Core.Services;
using Serilog;

namespace Saga.Web
{
    /// <summary>Endpointy konwersji dokumentu na XML aktu prawnego.</summary>
    internal static class ConversionEndpoints
    {
        private const string XmlContentType = "application/xml";
        private const string DefaultErrorTitle = "Nie udało się przetworzyć dokumentu";

        private const string LegacyNotice = $"Używaj POST {ServiceInfo.ConvertPath}.";

        /// <summary>Opisy odpowiedzi — bez nich generator wstawia gołe frazy HTTP („Bad Request").</summary>
        private static readonly (int Status, string Description)[] ResponseDescriptions =
        [
            (StatusCodes.Status200OK,
                "XML aktu prawnego w załączniku (`Content-Disposition: attachment`)."),
            (StatusCodes.Status400BadRequest,
                "Nie przesłano pliku, dokument jest uszkodzony albo dane ogłoszenia są błędne."),
            (StatusCodes.Status413PayloadTooLarge,
                UploadLimits.ExceededMessage),
            (StatusCodes.Status415UnsupportedMediaType,
                $"Format pliku nie jest obsługiwany — przyjmowane są: {ServiceInfo.SupportedFormats}."),
            (StatusCodes.Status422UnprocessableEntity,
                "Dokument odczytano, ale nie dało się z niego zbudować modelu aktu prawnego."),
            (StatusCodes.Status500InternalServerError,
                "Nieoczekiwany błąd usługi."),
        ];

        public static void MapConversionEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost(ServiceInfo.ConvertPath, Convert)
                .WithName("ConvertDocumentToXml")
                .WithSummary(
                    $"Konwertuje przesłany dokument aktu prawnego ({ServiceInfo.SupportedFormats}) do XML.")
                .WithConversionContract();

            app.MapPost(ServiceInfo.LegacyConvertPath, Convert)
                .WithName("ConvertDocumentToXmlLegacy")
                .WithSummary(LegacyNotice)
                .WithConversionContract()
                .WithMetadata(new ObsoleteAttribute(LegacyNotice))
                .AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
                {
                    operation.Deprecated = true;
                    return Task.CompletedTask;
                });
        }

        private static RouteHandlerBuilder WithConversionContract(this RouteHandlerBuilder builder) =>
            builder
                .DisableAntiforgery()
                .WithTags(ServiceInfo.ConversionTag)
                .Produces(StatusCodes.Status200OK, contentType: XmlContentType)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
                .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithResponseDescriptions(ResponseDescriptions)
                .AddOpenApiOperationTransformer(DescribeRequestBody);

        private static Task DescribeRequestBody(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            if (operation.RequestBody is OpenApiRequestBody body)
            {
                body.Description =
                    $"Dokument źródłowy w polu `documentFile` ({ServiceInfo.SupportedFormats}; "
                    + $"do {UploadLimits.MaxSizeText}). Opcjonalnie dane ogłoszenia dokumentu, który ich nie niesie "
                    + "(szczotka przed publikacją, akt bez nagłówka publikatora): `publisher` (DU albo MP), `year`, "
                    + "`position`, `announcementDate` (RRRR-MM-DD). Uzupełniają tylko braki — dane z dokumentu wygrywają.";
                body.Required = true;
            }

            return Task.CompletedTask;
        }

        private static IResult Convert(
            IFormFile? documentFile,
            [FromForm] string? publisher,
            [FromForm] string? year,
            [FromForm] string? position,
            [FromForm] string? announcementDate)
        {
            if (documentFile is null || documentFile.Length == 0)
            {
                return Failure($"Nie wybrano pliku (obsługiwane formaty: {ServiceInfo.SupportedFormats}).",
                    StatusCodes.Status400BadRequest);
            }

            if (!PublicationData.TryCreate(publisher, year, position, announcementDate, out var publication, out var error))
                return Failure(error!, StatusCodes.Status400BadRequest, title: "Błędne dane ogłoszenia");

            try
            {
                using Stream document = documentFile.OpenReadStream();

                ParseResult result = LegalDocumentParser.Parse(document, documentFile.FileName,
                    new ParseOptions { Policy = ParsePolicy.AlwaysParse, Publication = publication });

                if (result.Document is null)
                {
                    return Failure("Nie udało się zbudować modelu dokumentu — nie ma czego wyeksportować.",
                        StatusCodes.Status422UnprocessableEntity);
                }

                byte[] xml = Encoding.UTF8.GetBytes(LegalDocumentConverter.Convert(result.Document).ToString());

                return Results.File(xml, $"{XmlContentType}; charset=utf-8", XmlFileName(documentFile.FileName));
            }
            catch (ScannedPdfException ex)
            {
                Log.Warning(ex, "Odrzucono PDF bez użytecznej warstwy tekstowej.");
                return Failure(ex.Message, StatusCodes.Status422UnprocessableEntity);
            }
            catch (UnsupportedDocumentFormatException ex)
            {
                Log.Warning(ex, "Odrzucono dokument o nieobsługiwanym formacie.");
                return Failure(ex.Message, StatusCodes.Status415UnsupportedMediaType);
            }
            catch (OpenXmlPackageException ex)
            {
                Log.Warning(ex, "Uszkodzony pakiet DOCX.");
                return Failure("Plik nie jest poprawnym dokumentem Word (DOCX).",
                    StatusCodes.Status400BadRequest);
            }
            catch (ParsingException ex)
            {
                Log.Warning(ex, "Nie udało się przetworzyć dokumentu.");
                return Failure($"Nie udało się przetworzyć dokumentu: {ex.Message}",
                    StatusCodes.Status422UnprocessableEntity);
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Błąd wejścia/wyjścia podczas przetwarzania pliku.");
                return Failure("Nie udało się odczytać pliku. Spróbuj ponownie.",
                    StatusCodes.Status500InternalServerError, title: "Błąd usługi");
            }
        }

        private static IResult Failure(string message, int statusCode, string title = DefaultErrorTitle) =>
            Results.Problem(detail: message, statusCode: statusCode, title: title);

        /// <summary>
        /// Nazwa pliku wynikowego zbudowana z nazwy wejściowej: zostają litery, cyfry, „-" i „_",
        /// a pozostałe znaki zwijają się w pojedynczy „-".
        /// </summary>
        private static string XmlFileName(string uploadedName)
        {
            string stem = Path.GetFileNameWithoutExtension(uploadedName);
            var safe = new StringBuilder(stem.Length);

            foreach (char character in stem)
            {
                if (char.IsLetterOrDigit(character) || character is '-' or '_')
                {
                    safe.Append(character);
                }
                else if (safe.Length > 0 && safe[^1] != '-')
                {
                    safe.Append('-');
                }
            }

            string name = safe.ToString().Trim('-');

            return name.Length == 0 ? "dokument.xml" : $"{name}.xml";
        }
    }
}
