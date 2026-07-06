using Serilog;
using WordParserCore;
using WordParserWeb;

var builder = WebApplication.CreateBuilder(args);

LoggerConfig.ConfigureLogger();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

HtmlTemplateStore.Initialize(app.Environment.ContentRootPath);

app.MapGet("/", () => Results.Content(HtmlDocumentRenderer.RenderUploadForm(null), "text/html; charset=utf-8"));

app.MapPost("/parse", async (IFormFile docxFile) =>
{
    if (docxFile == null || docxFile.Length == 0)
    {
        return Results.Content(HtmlDocumentRenderer.RenderUploadForm("Nie wybrano pliku DOCX."), "text/html; charset=utf-8");
    }

    string tempFilePath = Path.Combine(Path.GetTempPath(), $"wordparser_{Guid.NewGuid():N}.docx");

    try
    {
        await using (var stream = File.Create(tempFilePath))
        {
            await docxFile.CopyToAsync(stream);
        }

        var document = LegalDocumentParser.Parse(tempFilePath);
        string html = HtmlDocumentRenderer.RenderDocument(document, docxFile.FileName);
        return Results.Content(html, "text/html; charset=utf-8");
    }
    catch (IOException ioEx)
    {
        Log.Error(ioEx, "Blad wejscia/wyjscia podczas przetwarzania pliku.");
        return Results.Content(HtmlDocumentRenderer.RenderUploadForm("Nie udalo sie odczytac pliku. Sprobuj ponownie."), "text/html; charset=utf-8");
    }
    finally
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch (IOException ioEx)
        {
            Log.Warning(ioEx, "Nie udalo sie usunac pliku tymczasowego.");
        }
    }
}).DisableAntiforgery();

app.MapPost("/parse_xml", async (IFormFile docxFile) =>
{
    if (docxFile == null || docxFile.Length == 0)
    {
        return Results.Content(HtmlDocumentRenderer.RenderUploadForm("Nie wybrano pliku DOCX."), "text/html; charset=utf-8");
    }

    string tempFilePath = Path.Combine(Path.GetTempPath(), $"wordparser_{Guid.NewGuid():N}.docx");

    try
    {
        await using (var stream = File.Create(tempFilePath))
        {
            await docxFile.CopyToAsync(stream);
        }

        var document = LegalDocumentParser.Parse(tempFilePath);
        var xml = LegalDocumentConverter.Convert(document);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml.ToString());
        return Results.File(bytes, "application/xml", "document.xml");
    }
    catch (IOException ioEx)
    {
        Log.Error(ioEx, "Blad wejscia/wyjscia podczas przetwarzania pliku.");
        return Results.Content(HtmlDocumentRenderer.RenderUploadForm("Nie udalo sie odczytac pliku. Sprobuj ponownie."), "text/html; charset=utf-8");
    }
    finally
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch (IOException ioEx)
        {
            Log.Warning(ioEx, "Nie udalo sie usunac pliku tymczasowego.");
        }
    }
}).DisableAntiforgery();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();
