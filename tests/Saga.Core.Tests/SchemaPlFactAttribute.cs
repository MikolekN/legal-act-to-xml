using System.Runtime.CompilerServices;
using Saga.Core.Services.Converters;

namespace Saga.Core.Tests
{
    /// <summary>
    /// [Fact] dla testu walidującego eksport wobec schemaPL.xsd. Schemat jest lokalny (katalog
    /// <c>schema/</c> jest gitignorowany), więc gdy go nie ma albo się nie kompiluje, test jest
    /// POMIJANY z powodem — jak <see cref="DocRepoFactAttribute"/>.
    /// </summary>
    public sealed class SchemaPlFactAttribute : FactAttribute
    {
        public SchemaPlFactAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            Skip = SchemaPl.SkipReason;
        }
    }

    /// <summary>Walidator schemaPL dla testów — ładowany raz, ze schematu w korzeniu repozytorium.</summary>
    internal static class SchemaPl
    {
        private static readonly Lazy<(SchemaPlValidator? Validator, string? SkipReason)> Loaded = new(() =>
        {
            var validator = SchemaPlValidator.TryLoad(TestFiles.GetRepositoryRoot(), out var reason);
            return (validator, validator is null ? reason : null);
        });

        public static string? SkipReason => Loaded.Value.SkipReason;

        public static SchemaPlValidator Validator =>
            Loaded.Value.Validator ?? throw new InvalidOperationException(SkipReason);
    }
}
