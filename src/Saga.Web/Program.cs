using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Saga.Core;
using Saga.Web;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

LoggerConfig.ConfigureLogger();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.HttpContext.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            context.ProblemDetails.Title = "Dokument jest za duży";
            context.ProblemDetails.Detail = UploadLimits.ExceededMessage;
        }
        else if (context.Exception is BadHttpRequestException)
        {
            // Żądanie, którego nie da się nawet związać z parametrami (np. bez formularza) — błąd klienta, nie usługi.
            context.ProblemDetails.Title = "Błędne żądanie";
            context.ProblemDetails.Detail =
                $"Wyślij dokument jako formularz multipart/form-data w polu documentFile (obsługiwane formaty: {ServiceInfo.SupportedFormats}).";
        }
    };
});

builder.Services.AddSagaOpenApi(builder.Environment);

builder.Services.AddHealthChecks();

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = UploadLimits.MaxBytes);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = UploadLimits.MaxBytes);

var app = builder.Build();

var forwardedHeaders = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

// Błąd wiązania żądania niesie własny kod (400, 413, 415) — usługa nie odpowiada na niego 500.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

app.UseStaticFiles();

app.MapOpenApi();
app.MapScalarApiReference(ServiceInfo.DocsPath, options => options
    .WithTitle("SAGA — dokumentacja API")
    .WithFavicon("/caise.png")
    .WithDotNetFlag(false));

app.MapLandingPage();
app.MapOperationsEndpoints();
app.MapConversionEndpoints();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
