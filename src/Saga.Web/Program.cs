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
    };
});

builder.Services.AddSagaOpenApi(builder.Environment);

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = UploadLimits.MaxBytes);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = UploadLimits.MaxBytes);

var app = builder.Build();

var forwardedHeaders = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseExceptionHandler();
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
