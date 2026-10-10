using Saga.Model;

namespace Saga.Core.Services.Classify
{
    /// <summary>
    /// Podpowiedź numeracyjna obliczana przez orkiestrator przed klasyfikacją.
    /// Informuje klasyfikator, czy numer encji w akapicie jest oczekiwanym następnikiem
    /// poprzedniej encji tego samego poziomu.
    /// </summary>
    public sealed class NumberingHint
    {
        /// <summary>
        /// Typ jednostki, dla której hint jest aktualny.
        /// Klasyfikator stosuje karę tylko gdy rozpoznany Kind == ExpectedKind.
        /// </summary>
        public ParagraphKind ExpectedKind { get; init; }

        /// <summary>
        /// Oczekiwany numer następnej encji; null = pierwsza encja danego poziomu (brak kary).
        /// </summary>
        public EntityNumber? ExpectedNumber { get; init; }

        /// <summary>
        /// Zwraca true gdy <paramref name="actual"/> jest oczekiwanym następnikiem.
        /// </summary>
        public bool IsContinuous(EntityNumber actual)
        {
            if (ExpectedNumber == null)
                return true;

            // Litery (NumericPart == 0): ciągłość alfabetyczna LexicalPart
            if (actual.NumericPart == 0)
                return IsExpectedNextLetter(ExpectedNumber, actual);

            return IsExpectedNextNumeric(ExpectedNumber, actual);
        }

        /// <summary>
        /// Oblicza następną wartość litery (a → b, z → aa, ab → ac, az → ba).
        /// Analogicznie do nazewnictwa kolumn w arkuszach kalkulacyjnych.
        /// </summary>
        internal static string? GetNextLetterValue(string? currentLetter)
        {
            if (string.IsNullOrEmpty(currentLetter))
                return null;

            var chars = currentLetter.ToLowerInvariant().ToCharArray();
            int carry = 1;

            for (int i = chars.Length - 1; i >= 0 && carry > 0; i--)
            {
                int val = chars[i] - 'a' + carry;
                if (val > 25)
                {
                    chars[i] = 'a';
                    carry = 1;
                }
                else
                {
                    chars[i] = (char)('a' + val);
                    carry = 0;
                }
            }

            return carry > 0
                ? "a" + new string(chars) // overflow: z → aa
                : new string(chars);
        }

        private static bool IsExpectedNextNumeric(EntityNumber previous, EntityNumber current)
        {
            // Ten sam NumericPart — wariant z sufiksem / indeksem górnym (2 → 2a)
            if (current.NumericPart == previous.NumericPart)
                return true;

            // Kolejny numer (2a → 3, 5 → 6)
            if (current.NumericPart == previous.NumericPart + 1)
                return true;

            return false;
        }

        private static bool IsExpectedNextLetter(EntityNumber previous, EntityNumber current)
        {
            // Polska litera stoi za bazową (l → ł → m), więc w porównaniu liczy się jak ona.
            var previousLetters = DesignationAlphabet.Fold(previous.LexicalPart);
            var currentLetters = DesignationAlphabet.Fold(current.LexicalPart);

            // Ten sam LexicalPart — wariant z indeksem (a → a^1) albo polska litera po bazowej (l → ł)
            if (currentLetters == previousLetters)
                return true;

            // Następna litera alfabetu (a → b, z → aa)
            var expectedNext = GetNextLetterValue(previousLetters);
            return !string.IsNullOrEmpty(expectedNext) && currentLetters == expectedNext;
        }
    }
}
