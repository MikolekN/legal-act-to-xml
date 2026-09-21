using System.Reflection;

namespace Saga.Web
{
    /// <summary>Dane wydania.</summary>
    internal static class BuildInfo
    {
        private const int ShortCommitLength = 8;

        private const string UnknownVersion = "nieznana";

        static BuildInfo()
        {
            string informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? string.Empty;

            int separator = informational.IndexOf('+');

            string version = separator < 0 ? informational : informational[..separator];
            Version = string.IsNullOrWhiteSpace(version) ? UnknownVersion : version;

            string commit = separator < 0 ? string.Empty : informational[(separator + 1)..];
            Commit = string.IsNullOrWhiteSpace(commit)
                ? null
                : commit[..Math.Min(ShortCommitLength, commit.Length)];
        }

        /// <summary>Wersja semantyczna wydania (bez identyfikatora commita).</summary>
        public static string Version { get; }

        /// <summary>Skrócony identyfikator commita albo null, gdy build go nie wstrzyknął.</summary>
        public static string? Commit { get; }

        /// <summary>Wersja z commitem w nawiasie.</summary>
        public static string VersionWithCommit => Commit is null ? Version : $"{Version} ({Commit})";
    }
}
