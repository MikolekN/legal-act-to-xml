using DocumentFormat.OpenXml.Packaging;
using Saga.Core;
using Saga.Core.Exceptions;
using Saga.Core.Services;
using Serilog;

namespace Saga.Cli
{
    /// <summary>
    /// Konwerter wsadowy: dokument (albo katalog dokumentów) → XML aktu prawnego.
    ///
    /// Kody wyjścia: 0 = wszystko przekonwertowane; 1 = błąd wywołania lub wejścia/wyjścia;
    /// 2 = przebieg zakończony, ale część plików pominięto lub nie udało się przetworzyć.
    /// </summary>
    internal static class Program
    {
        private const string DefaultPattern = "*.docx";

        private static int Main(string[] args)
        {
            LoggerConfig.ConfigureLogger();

            try
            {
                return Run(args);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static int Run(string[] args)
        {
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                PrintUsage();
                return args.Length == 0 ? 1 : 0;
            }

            string input = args[0];
            string outputDirectory = Directory.GetCurrentDirectory();

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--output" or "-o":
                        if (++i >= args.Length)
                        {
                            Console.Error.WriteLine("Brak ścieżki po --output.");
                            return 1;
                        }
                        outputDirectory = args[i];
                        break;

                    default:
                        Console.Error.WriteLine($"Nieznany argument: {args[i]}");
                        PrintUsage();
                        return 1;
                }
            }

            return Convert(input, outputDirectory);
        }

        private static int Convert(string input, string outputDirectory)
        {
            string[] files;

            if (File.Exists(input))
            {
                files = [input];
            }
            else if (Directory.Exists(input))
            {
                files = Directory.GetFiles(input, DefaultPattern);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                Console.Error.WriteLine($"Nie znaleziono pliku ani katalogu: {input}");
                return 1;
            }

            if (files.Length == 0)
            {
                Console.Error.WriteLine($"Katalog {input} nie zawiera plików.");
                return 1;
            }

            try
            {
                Directory.CreateDirectory(outputDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Nie udało się utworzyć katalogu wynikowego {outputDirectory}: {ex.Message}");
                return 1;
            }

            int converted = 0;
            int skipped = 0;
            int failed = 0;

            foreach (string filePath in files)
            {
                string name = Path.GetFileName(filePath);

                try
                {
                    ParseResult result = LegalDocumentParser.Parse(filePath);

                    if (result.Document is null)
                    {
                        Console.WriteLine($"POMINIĘTO  {name} — nie rozpoznano aktu prawnego "
                            + $"(pewność {result.Classification.Confidence}/100)");
                        skipped++;
                        continue;
                    }

                    string outputPath = Path.Combine(outputDirectory,
                        Path.GetFileNameWithoutExtension(filePath) + ".xml");

                    LegalDocumentConverter.Convert(result.Document).Save(outputPath);

                    Console.WriteLine($"OK         {name} → {outputPath}");
                    converted++;
                }
                catch (Exception ex) when (ex is ParsingException or OpenXmlPackageException
                    or IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"BŁĄD       {name}: {ex.Message}");
                    failed++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Przekonwertowane: {converted} | pominięte: {skipped} | błędy: {failed}");

            return skipped + failed == 0 ? 0 : 2;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Konwersja aktów prawnych do XML.");
            Console.WriteLine();
            Console.WriteLine("Użycie:");
            Console.WriteLine("  saga <plik|katalog> [--output <katalog>]");
            Console.WriteLine();
            Console.WriteLine("Opcje:");
            Console.WriteLine($"  -o, --output   katalog wynikowy (domyślnie: bieżący katalog)");
            Console.WriteLine();
            Console.WriteLine("Przykłady:");
            Console.WriteLine("  saga ustawa.docx --output wynik");
            Console.WriteLine(@"  saga C:\ustawy\2026 --output C:\xml\2026");
            Console.WriteLine();
            Console.WriteLine("Kody wyjścia: 0 = wszystko przekonwertowane; 1 = błąd wywołania lub wejścia/wyjścia;");
            Console.WriteLine("              2 = część plików pominięto lub nie udało się przetworzyć.");
        }
    }
}
