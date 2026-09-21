namespace Saga.Web
{
    /// <summary>
    /// Nazwy, ścieżki i formaty informacyjne pojawiające się więcej niż jednen raz .
    /// </summary>
    internal static class ServiceInfo
    {
        public const string Name = "SAGA — konwerter aktów prawnych do XML";

        public const string DocsPath = "/scalar";

        public const string ConvertPath = "/v1/convert";

        public const string LegacyConvertPath = "/parse_xml";

        public const string SupportedFormats = "DOCX, PDF, TXT";

        public const string ConversionTag = "Konwersja";

        public const string OperationsTag = "Operacje";
    }
}
