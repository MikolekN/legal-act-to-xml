namespace Saga.Web
{
    /// <summary>
    /// Limit rozmiaru przesyłanego dokumentu.
    /// </summary>
    internal static class UploadLimits
    {
        public const long MaxBytes = 64L * 1024 * 1024;

        public const int MaxMegabytes = (int)(MaxBytes / 1024 / 1024);

        public static readonly string MaxSizeText = $"{MaxMegabytes} MB";

        public static readonly string ExceededMessage = $"Dokument przekracza limit {MaxSizeText}.";
    }
}
