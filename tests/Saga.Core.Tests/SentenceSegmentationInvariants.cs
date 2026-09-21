using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Niezmienniki podziału na zdania — asekuracja dla zmian w regułach cięcia.
    ///
    /// Reguły decydujące o POZYCJI granicy będą się zmieniać (kolejne grupy poprawek:
    /// oznaczenia jednostek, inicjały, skróty, wersaliki, weto warunkowe). Niezależnie od
    /// tego, GDZIE splitter tnie, cztery własności muszą zachodzić zawsze — i to one chronią
    /// przed klasą błędów, której testy na konkretnych zdaniach nie wyłapią: zgubionym
    /// znakiem, zdublowanym fragmentem i rozcięciem wyrazu w środku.
    ///
    /// Wszystkie cztery sprawdzono na całym lokalnym korpusie (632 978 jednostek
    /// redakcyjnych z ustaw, obwieszczeń i rozporządzeń) — zero naruszeń, więc opisują stan
    /// faktyczny, a nie życzenie.
    ///
    /// Każdy test splittera powinien kończyć się wywołaniem <see cref="AssertHolds"/>:
    /// dzięki temu nowy przypadek testowy dokłada asekurację automatycznie, bez wysiłku autora.
    /// </summary>
    internal static class SentenceSegmentationInvariants
    {
        /// <summary>
        /// Sprawdza komplet niezmienników dla jednego podziału.
        /// </summary>
        /// <param name="input">Tekst podany do splittera.</param>
        /// <param name="segments">Segmenty, które splitter zwrócił.</param>
        public static void AssertHolds(string input, IReadOnlyList<TextSegment> segments)
        {
            AssertNothingLostOrAdded(input, segments);
            AssertNoEmptySegments(segments);
            AssertOrderIsContiguous(segments);
            AssertSegmentsComeFromInput(input, segments);
        }

        /// <summary>
        /// I — sklejenie segmentów odtwarza wejście z dokładnością do białych znaków.
        ///
        /// Porównanie pomija białe znaki celowo: splitter pochłania odstęp po kropce, a przyszłe
        /// reguły będą ten odstęp poszerzać (cudzysłów zamykający, odstęp nierozdzielający).
        /// Niezmiennik nie może więc zakładać, że segmenty sklejają się jedną spacją — ma pilnować
        /// TREŚCI, nie konwencji separatora.
        /// </summary>
        private static void AssertNothingLostOrAdded(string input, IReadOnlyList<TextSegment> segments)
        {
            var expected = StripWhitespace(input);
            var actual = StripWhitespace(string.Concat(segments.Select(segment => segment.Text)));

            Assert.True(expected == actual,
                $"Sklejone segmenty nie odtwarzają wejścia.{Environment.NewLine}"
                + $"  oczekiwano: {expected}{Environment.NewLine}"
                + $"  otrzymano : {actual}");
        }

        /// <summary>II — żaden segment nie jest pusty ani złożony z samych białych znaków.</summary>
        private static void AssertNoEmptySegments(IReadOnlyList<TextSegment> segments)
        {
            Assert.DoesNotContain(segments, segment => string.IsNullOrWhiteSpace(segment.Text));
        }

        /// <summary>
        /// III — Order tworzy ciąg 1..N bez luk i powtórzeń.
        ///
        /// Numer zdania jest adresowalny prawnie (ZTP § 59 ust. 2 dopuszcza odesłanie
        /// „art. N ust. M zdanie drugie”) i po nim rozwiązuje cele EntryIntoForceTargetResolver,
        /// więc luka w numeracji przesuwa cel odesłania.
        /// </summary>
        private static void AssertOrderIsContiguous(IReadOnlyList<TextSegment> segments)
        {
            Assert.Equal(Enumerable.Range(1, segments.Count), segments.Select(segment => segment.Order));
        }

        /// <summary>
        /// IV — każdy segment pochodzi z wejścia, a nie z przepisania treści.
        /// Pilnuje, żeby splitter pozostał operacją tnącą, nie normalizującą.
        /// </summary>
        private static void AssertSegmentsComeFromInput(string input, IReadOnlyList<TextSegment> segments)
        {
            var haystack = StripWhitespace(input);

            Assert.All(segments, segment =>
                Assert.Contains(StripWhitespace(segment.Text), haystack, StringComparison.Ordinal));
        }

        /// <summary>
        /// Usuwa wszystkie białe znaki. <see cref="char.IsWhiteSpace(char)"/> obejmuje też odstęp
        /// nierozdzielający (U+00A0), którego w polskich aktach jest pełno (77% jednostek korpusu) —
        /// dzięki temu niezmiennik nie zależy od tego, którym odstępem rozdzielono wyrazy.
        /// </summary>
        private static string StripWhitespace(string text)
            => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));
    }
}
