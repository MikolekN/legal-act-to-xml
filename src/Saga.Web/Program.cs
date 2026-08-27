using System.Reflection;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using Saga.Core;
using Saga.Core.Exceptions;
using Saga.Core.Services;
using Scalar.AspNetCore;
using Serilog;

// Limit rozmiaru przesyłanego dokumentu.
const long MaxUploadBytes = 64L * 1024 * 1024;
const int MaxUploadMegabytes = (int)(MaxUploadBytes / 1024 / 1024);

var builder = WebApplication.CreateBuilder(args);

LoggerConfig.ConfigureLogger();

string buildVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? "nieznana";

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.HttpContext.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            context.ProblemDetails.Title = "Dokument jest za duży";
            context.ProblemDetails.Detail = $"Limit rozmiaru przesyłanego dokumentu to {MaxUploadMegabytes} MB.";
        }
    };
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "SAGA — konwerter aktów prawnych do XML",
            Version = buildVersion,
            Description = string.Join('\n',
            [
                "Konwersja aktów prawnych DOCX do XML.",
                "",
                $"- Wersja (build): `{buildVersion}`",
                $"- Środowisko: `{builder.Environment.EnvironmentName}`",
                $"- Runtime: `.NET {Environment.Version}`, kontener nasłuchuje na porcie 8080",
            ]),
        };

        document.Servers?.Clear();

        return Task.CompletedTask;
    });
});

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUploadBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxUploadBytes);

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto,
    KnownIPNetworks = { },
    KnownProxies = { },
});

app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

static IResult UploadError(string message, int statusCode) =>
    Results.Problem(detail: message, statusCode: statusCode, title: "Nie udało się przetworzyć dokumentu");

static string XmlFileName(string uploadedName)
{
    string stem = Path.GetFileNameWithoutExtension(uploadedName);
    string safe = new(stem.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    return string.IsNullOrEmpty(safe) ? "document.xml" : $"{safe}.xml";
}

async Task<IResult> ConvertToXmlAsync(IFormFile? documentFile)
{
    if (documentFile == null || documentFile.Length == 0)
    {
        return UploadError("Nie wybrano pliku (obsługiwane formaty: DOCX, PDF, TXT).",
            StatusCodes.Status400BadRequest);
    }

    try
    {
        using var buffer = new MemoryStream(capacity: (int)documentFile.Length);
        await documentFile.CopyToAsync(buffer);
        buffer.Position = 0;

        ParseResult result = LegalDocumentParser.Parse(buffer, documentFile.FileName,
            new ParseOptions { Policy = ParsePolicy.AlwaysParse });

        if (result.Document is null)
        {
            return UploadError("Nie udało się zbudować modelu dokumentu — nie ma czego wyeksportować.",
                StatusCodes.Status422UnprocessableEntity);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(LegalDocumentConverter.Convert(result.Document).ToString());

        return Results.File(bytes, "application/xml; charset=utf-8", XmlFileName(documentFile.FileName));
    }
    catch (ScannedPdfException ex)
    {
        Log.Warning(ex, "Odrzucono PDF bez uzytecznej warstwy tekstowej.");
        return UploadError(ex.Message, StatusCodes.Status422UnprocessableEntity);
    }
    catch (UnsupportedDocumentFormatException ex)
    {
        Log.Warning(ex, "Odrzucono dokument o nieobslugiwanym formacie.");
        return UploadError(ex.Message, StatusCodes.Status415UnsupportedMediaType);
    }
    catch (OpenXmlPackageException ex)
    {
        Log.Warning(ex, "Uszkodzony pakiet DOCX.");
        return UploadError("Plik nie jest poprawnym dokumentem Word (DOCX).", StatusCodes.Status400BadRequest);
    }
    catch (ParsingException ex)
    {
        Log.Warning(ex, "Nie udalo sie przetworzyc dokumentu.");
        return UploadError($"Nie udało się przetworzyć dokumentu: {ex.Message}",
            StatusCodes.Status422UnprocessableEntity);
    }
    catch (IOException ioEx)
    {
        Log.Error(ioEx, "Blad wejscia/wyjscia podczas przetwarzania pliku.");
        return UploadError("Nie udalo sie odczytac pliku. Sprobuj ponownie.",
            StatusCodes.Status500InternalServerError);
    }
}

app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapGet("/health", () => Results.Text("ok"))
    .WithName("GetHealth")
    .WithTags("Operacje")
    .WithSummary("Sonda zdrowia dla platformy wdrożeniowej.");

app.MapPost("/v1/convert", ConvertToXmlAsync)
    .DisableAntiforgery()
    .WithName("ConvertDocumentToXml")
    .WithTags("Konwersja")
    .WithSummary("Konwertuje przesłany dokument (DOCX, PDF, TXT) do XML aktu prawnego.")
    .WithDescription(
        "Odpowiedź 200 to plik XML (`application/xml`) z nagłówkiem `Content-Disposition: attachment`. "
        + $"Maksymalny rozmiar przesyłanego pliku to {MaxUploadMegabytes} MB.")
    .Produces(StatusCodes.Status200OK, contentType: "application/xml")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

app.MapPost("/parse_xml", ConvertToXmlAsync)
    .DisableAntiforgery()
    .WithName("ConvertDocumentToXmlLegacy")
    .WithTags("Konwersja")
    .WithSummary("PRZESTARZAŁE — używaj POST /v1/convert.")
    .WithDescription("Zachowuje się identycznie jak `POST /v1/convert`.")
    .Produces(StatusCodes.Status200OK, contentType: "application/xml")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
    .WithMetadata(new ObsoleteAttribute("Uzywaj POST /v1/convert."));

app.Run();
