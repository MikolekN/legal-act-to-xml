using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Core.Services.Classify;
using Saga.Model;
using Saga.Model.EditorialUnits;
using DtoArticle = Saga.Model.EditorialUnits.Article;
using DtoLetter = Saga.Model.EditorialUnits.Letter;
using DtoParagraph = Saga.Model.EditorialUnits.Paragraph;
using DtoPoint = Saga.Model.EditorialUnits.Point;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Fabryki pomocnicze dla parsowania: tworzenie encji, parsowanie numerow,
    /// usuwanie prefiksow numeracji i podzial tekstu na segmenty (zdania).
    /// </summary>
    public static class ParsingFactories
    {
        private static readonly EntityNumberService _numberService = new();
        private static readonly Regex WrapUpPrefixPattern = new(@"^(\u2013|-)\s*", RegexOptions.Compiled);
        private static readonly Regex CodeParagraphMark = new(
            $@"^{ParagraphClassifier.OptionalQuotePrefix}\u00a7", RegexOptions.Compiled);

        /// <summary>
        /// Oznacza paragraf kodeksu (§): rola "par", prefiks eId "par", etykieta "§".
        /// </summary>
        public static void ApplyCodeParagraphRole(DtoParagraph paragraph, string? sourceText)
        {
            if (sourceText == null || !CodeParagraphMark.IsMatch(sourceText.TrimStart()))
                return;

            paragraph.Role = DtoParagraph.CodeParagraphRole;
            paragraph.EIdPrefix = DtoParagraph.CodeParagraphRole;
            paragraph.DisplayLabel = "§";
        }

        /// <summary>
        /// Usuwa prefiks numeru ustepu (np. "1. ") z tekstu.
        /// </summary>
        public static string StripParagraphPrefix(string text)
            => ParagraphClassifier.ParagraphPattern.Replace(text.Trim(), "", 1);

        /// <summary>
        /// Usuwa prefiks numeru punktu (np. "1) ") z tekstu.
        /// </summary>
        public static string StripPointPrefix(string text)
            => ParagraphClassifier.PointPattern.Replace(text.Trim(), "", 1);

        /// <summary>
        /// Usuwa prefiks litery (np. "a) ") z tekstu.
        /// </summary>
        public static string StripLetterPrefix(string text)
            => ParagraphClassifier.LetterStripPattern.Replace(text.Trim(), "", 1);

        /// <summary>
        /// Usuwa prefiks tiretu (np. "– ") z tekstu.
        /// </summary>
        public static string StripTiretPrefix(string text)
            => ParagraphClassifier.TiretStripPattern.Replace(text.Trim(), "", 1);

        /// <summary>
        /// Usuwa prefiks wrapUp (polpauza lub myślnik) z tekstu.
        /// </summary>
        public static string StripWrapUpPrefix(string text)
            => WrapUpPrefixPattern.Replace(text.Trim(), "", 1);

        /// <summary>
        /// Dzieli tekst na segmenty (zdania). Podział następuje w miejscu,
        /// gdzie po kropce i spacji pojawia się wielka litera, z wyjątkami:
        /// - "RRRR r." (rok) - nie jest punktem podziału
        /// - oznaczenie dziennika urzędowego (§ 162 ust. 2 ZTP: "Dz. U.", "Dz. Urz. UE",
		///   "Dz. Urz. Woj. …", "M.P.", "M.S.G." itd.) - nie jest punktem podziału
        /// </summary>
        public static List<TextSegment> SplitIntoSentences(string text)
        {
            var segments = new List<TextSegment>();
            if (string.IsNullOrWhiteSpace(text))
                return segments;

            var sentences = SplitBySentenceWithExceptions(text);
            for (int i = 0; i < sentences.Count; i++)
            {
                var sentence = sentences[i].Trim();
                if (!string.IsNullOrEmpty(sentence))
                {
                    segments.Add(new TextSegment
                    {
                        Type = TextSegmentType.Sentence,
                        Text = sentence,
                        Order = i + 1
                    });
                }
            }
            return segments;
        }

        /// <summary>
        /// Dzieli tekst na zdania z uwzględnieniem wyjątków:
        /// - Nie dzieli po "RRRR r." (rok)
        /// - Nie dzieli wewnątrz oznaczenia dziennika urzędowego ("Dz. U.", "Dz. Urz. …", "M.P.", "M.S.G.")
        /// </summary>
        private static List<string> SplitBySentenceWithExceptions(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            var journalDesignations = JournalDesignationRecognizer.FindAll(text);

            int startIndex = 0;
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (!IsSentenceBoundary(text, i, startIndex, journalDesignations, out int sentenceEnd, out int nextStart))
                {
                    continue;
                }

                result.Add(text.Substring(startIndex, sentenceEnd - startIndex + 1));
                startIndex = nextStart;
            }

            // Dodaj pozostałą część tekstu jako ostatnie zdanie
            if (startIndex < text.Length)
            {
                result.Add(text.Substring(startIndex));
            }

            return result;
        }

        /// <summary>
        /// Rozstrzyga, czy znak na pozycji <paramref name="terminatorIndex"/> kończy zdanie.
        /// </summary>
        private static bool IsSentenceBoundary(
            string text,
            int terminatorIndex,
            int sentenceStart,
            MatchCollection journalDesignations,
            out int sentenceEnd,
            out int nextStart)
        {
            int dotIndex = terminatorIndex;
            sentenceEnd = dotIndex;
            nextStart = dotIndex + 2;

            if (!IsSentenceTerminator(text[dotIndex]))
            {
                return false;
            }

            // 1. Kształt granicy: znak kończący, ewentualny cudzysłów domykający cytat, odstęp.
            int afterDot = dotIndex + 1;
            while (afterDot < text.Length && IsClosingQuote(text[afterDot]))
            {
                afterDot++;
            }

            if (afterDot >= text.Length || !char.IsWhiteSpace(text[afterDot]))
            {
                return false;
            }

            sentenceEnd = afterDot - 1;

            // 2. Prawy kontekst: pierwszy niebiały znak musi być wielką literą.
            int nextCharIndex = afterDot;
            while (nextCharIndex < text.Length && char.IsWhiteSpace(text[nextCharIndex]))
            {
                nextCharIndex++;
            }

            if (nextCharIndex >= text.Length || !char.IsUpper(text[nextCharIndex]))
            {
                return false;
            }

            // Następne zdanie zaczyna się za całym ciągiem odstępu, nie za jednym znakiem.
            nextStart = nextCharIndex;

            // 3. Weta lewego kontekstu — kropka należy do skrótu, nie kończy zdania.
            if (text[dotIndex] != '.')
            {
                return true;
            }

            if (IsRokException(text, dotIndex, sentenceStart))
            {
                return false;
            }

            if (JournalDesignationRecognizer.Covers(journalDesignations, dotIndex)
                && !JournalDesignationRecognizer.EndsDesignationBeforeSentence(journalDesignations, dotIndex, text, nextCharIndex))
            {
                return false;
            }

            if (AbbreviationRecognizer.EndsAbbreviation(text, dotIndex))
            {
                return false;
            }

            if (EndsPersonInitial(text, dotIndex, nextCharIndex))
            {
                return false;
            }

            if (IsStandaloneUnitDesignation(text, sentenceStart, dotIndex))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Oznaczenie jednostki otwierające treść cytowaną nowelizacji — „„Art. 59a. 1.", „„§ 2." —
        /// stojące SAMODZIELNIE na początku zdania. Kropka kończąca oznaczenie nie kończy zdania:
        /// oznaczenie należy do zdania, które po nim następuje. Tylko CAŁE zdanie dotąd złożone
        /// z oznaczenia — „…o którym mowa w art. 4a. Minister…" pozostaje granicą.
        ///
        /// Cudzysłów otwierający jest warunkiem celowo: samotne oznaczenie BEZ niego na początku
        /// treści jednostki („1. Zajęcia…", „5.6. Przekroczenie…") to ślad defektu wyżej w potoku —
        /// niezdjęty prefiks albo nierozpoznany numer (eId bez numeru). Weto zamaskowałoby go
        /// w pomiarze podziału na zdania, zostawiając zepsuty eId i treść.
        /// </summary>
        private static readonly Regex StandaloneUnitDesignation = new(
            @"^\s*[„""]\s*(?:Art\.\s*\d+[a-z]*\.\s*)?(?:§\s*\d+[a-z]*\.\s*)?(?:\d+[a-z]*\.\s*)*"
            + @"(?:[IVXLC]{1,6}(?:\.\d+[a-z]*)*\.)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ContainsDesignationNumber = new(@"[0-9IVXLC]", RegexOptions.Compiled);

        private static bool IsStandaloneUnitDesignation(string text, int sentenceStart, int dotIndex)
        {
            var sentenceSoFar = text.AsSpan(sentenceStart, dotIndex - sentenceStart + 1);
            return StandaloneUnitDesignation.IsMatch(sentenceSoFar)
                && ContainsDesignationNumber.IsMatch(sentenceSoFar);
        }

        /// <summary>
        /// Tytuły stojące przed imieniem (inicjałem) — „im. J. Babińskiego", „prof. A. Nowak".
        /// </summary>
        private static readonly Regex PersonTitleBeforeInitial = new(
            @"(?<!\p{L})(?:im|dr|prof|ks|gen|mgr|inż|św|płk|hab)\.\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Poprzedni inicjał łańcucha „J. K. Nowak".</summary>
        private static readonly Regex PrecedingInitial = new(
            @"(?<!\p{L})\p{Lu}\.$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Po inicjale: nazwisko (wielka litera, dalej małe) albo kolejny inicjał.</summary>
        private static readonly Regex SurnameOrInitial = new(
            @"^\p{Lu}(?:\p{Ll}|\.)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Inicjał imienia — pojedyncza wielka litera z kropką przed nazwiskiem („A. Duda").
        /// Sam kształt nie wystarcza: „…określa tabela A. Opłatę pobiera się…" kończy zdanie.
        /// Inicjał uznajemy więc tylko w kontekście osoby: po dwukropku formuły podpisu
        /// („Prezydent Rzeczypospolitej Polskiej: A. Duda"), po tytule („im. J. Babińskiego")
        /// albo na końcu łańcucha inicjałów, przed którym stoi jedno z nich („Marszałek Sejmu: J. K. Nowak").
        /// Łańcuch wymaga odstępów między inicjałami — „…Papierów Wartościowych S.A. Przepis…" kończy zdanie.
        /// </summary>
        private static bool EndsPersonInitial(string text, int dotIndex, int nextCharIndex)
        {
            if (dotIndex < 1 || !char.IsUpper(text[dotIndex - 1]))
                return false;

            if (dotIndex >= 2 && char.IsLetter(text[dotIndex - 2]))
                return false;

            if (!SurnameOrInitial.IsMatch(text.AsSpan(nextCharIndex)))
                return false;

            var initialStart = dotIndex - 1;
            while (true)
            {
                var before = text.AsSpan(0, initialStart);
                var trimmed = before.TrimEnd();
                if ((trimmed.Length > 0 && trimmed[^1] == ':') || PersonTitleBeforeInitial.IsMatch(trimmed))
                    return true;

                if (trimmed.Length == before.Length || !PrecedingInitial.IsMatch(trimmed))
                    return false;

                initialStart = trimmed.Length - 2;
            }
        }

        /// <summary>
        /// Znak kończący zdanie. Poza kropką także znak zapytania i wykrzyknik — występują
        /// w ankietach i formularzach stanowiących załączniki do aktów („Czy posiada Pani
        /// zaległości podatkowe? Proszę zakreślić właściwe pole.").
        /// </summary>
        private static bool IsSentenceTerminator(char character)
            => character is '.' or '?' or '!';

        /// <summary>
        /// Cudzysłów domykający brzmienie. Kanał DOCX daje „…”, kanał TXT/PDF proste "…"
        /// </summary>
        private static bool IsClosingQuote(char character)
            => character is '”' or '"' or '»';

        private const string YearOrLongDate =
            @"(?:\d{1,2}\s+(?:" + PolishDates.MonthAlternation + @")\s+)?\d{4}\s+r";

        private static readonly Regex FrontedDatePattern = new(
            @"^(?:W|Od|Do|Z|Za|Przez|Począwszy\s+od|Najpóźniej\s+do|Nie\s+później\s+niż)\s+"
            + @"(?:terminie\s+do\s+|okresie\s+od\s+|roku\s+)?(?:dnia\s+)?"
            + YearOrLongDate
            // Daty: „W 2020 r. i 2021 r.", „Od dnia 1 marca 2023 r. do dnia 31 maja 2023 r."
            + @"(?:\s*\.\s+(?:i|oraz|do|a\s+także)\s+(?:dnia\s+)?" + YearOrLongDate + @")*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Data w oznaczeniu aktu: „…ustawy z dnia 18 października 2006 r. o…",
        /// „…decyzją Komisji Europejskiej z dnia 13 lipca 2012 r.".
        /// </summary>
        private static readonly Regex ActDesignationDatePattern = new(
            @"(?<!\p{L})z\s+dnia\s+" + YearOrLongDate + "$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static bool IsRokException(string text, int dotIndex, int sentenceStart)
        {
            if (!EndsWithYearAbbreviation(text, dotIndex))
            {
                return false;
            }

            var sentenceSoFar = text.AsSpan(sentenceStart, dotIndex - sentenceStart).Trim();

            return FrontedDatePattern.IsMatch(sentenceSoFar)
                || ActDesignationDatePattern.IsMatch(sentenceSoFar);
        }

        private static bool EndsWithYearAbbreviation(string text, int dotIndex)
        {
            // Szukamy wzorca: cyfra cyfra cyfra cyfra spacja r. tj. "RRRR r."
            if (dotIndex < 7) return false;

            // Sprawdź czy jest " r." przed dotIndex
            if ((text[dotIndex - 1] != 'r' && text[dotIndex - 1] != 'R') || !char.IsWhiteSpace(text[dotIndex - 2]))
                return false;

            // Sprawdź czy poprzedzające to 4 cyfry (rok)
            // text[dotIndex - 3] powinno być spacją lub cyfrą
            int checkPos = dotIndex - 3;
            int digitCount = 0;
            while (checkPos >= 0 && char.IsDigit(text[checkPos]))
            {
                digitCount++;
                checkPos--;
            }

            // Powinna być dokładnie 4 cyfry na rok
            return digitCount == 4;
        }

        /// <summary>
        /// Ustawia ContentText (bez numeru) i TextSegments na encji implementujacej IHasTextSegments.
        /// </summary>
        public static void SetContentAndSegments(BaseEntity entity, string contentWithoutNumber)
        {
            entity.ContentText = contentWithoutNumber;
            if (entity is IHasTextSegments hasSegments)
            {
                hasSegments.TextSegments = SplitIntoSentences(contentWithoutNumber);
            }
        }
        public static DtoParagraph CreateImplicitParagraph(DtoArticle article)
        {
            return new DtoParagraph
            {
                Parent = article,
                Article = article,
                ContentText = string.Empty,
                IsImplicit = true
            };
        }

        public static DtoParagraph CreateParagraphFromArticleTail(DtoArticle article, string tail)
        {
            var normalizedTail = tail?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedTail))
            {
                return CreateImplicitParagraph(article);
            }

            var match = ParagraphClassifier.ParagraphNumberCapture.Match(normalizedTail);
            if (match.Success)
            {
                var contentText = StripParagraphPrefix(normalizedTail);
                var paragraph = new DtoParagraph
                {
                    Parent = article,
                    Article = article,
                    Number = _numberService.Parse(match.Groups[1].Value),
                    IsImplicit = false
                };
                ApplyCodeParagraphRole(paragraph, normalizedTail);
                SetContentAndSegments(paragraph, contentText);
                return paragraph;
            }

            var implicitParagraph = new DtoParagraph
            {
                Parent = article,
                Article = article,
                IsImplicit = true
            };
            SetContentAndSegments(implicitParagraph, normalizedTail);
            return implicitParagraph;
        }

        public static DtoPoint CreateImplicitPoint(DtoParagraph? paragraph, DtoArticle article)
        {
            return new DtoPoint
            {
                Parent = paragraph,
                Article = article,
                Paragraph = paragraph,
                ContentText = string.Empty
            };
        }

        public static DtoLetter CreateImplicitLetter(DtoPoint point, DtoParagraph? paragraph, DtoArticle article)
        {
            return new DtoLetter
            {
                Parent = point,
                Article = article,
                Paragraph = paragraph,
                Point = point,
                ContentText = string.Empty
            };
        }

        public static EntityNumber? ParseArticleNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = ParagraphClassifier.ArticleNumberCapture.Match(text);
            if (match.Success)
            {
                return _numberService.Parse(match.Groups[1].Value);
            }

            return null;
        }

        public static EntityNumber? ParseParagraphNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = ParagraphClassifier.ParagraphNumberCapture.Match(text.Trim());
            if (match.Success)
            {
                return _numberService.Parse(match.Groups[1].Value);
            }

            return null;
        }

        public static EntityNumber? ParsePointNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = ParagraphClassifier.PointNumberCapture.Match(text.Trim());
            if (match.Success)
            {
                return _numberService.Parse(match.Groups[1].Value);
            }

            return null;
        }

        public static EntityNumber? ParseLetterNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = ParagraphClassifier.LetterNumberCapture.Match(text.Trim());
            return match.Success ? _numberService.Parse(match.Groups[1].Value) : null;
        }

        public static string GetArticleTail(string text)
        {
            var match = ParagraphClassifier.ArticleTailCapture.Match(text.Trim());
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// Tworzy CommonPart typu Intro i wiaze go z odpowiednim segmentem tekstu rodzica.
        /// Logika:
        ///   - 1 segment  -> caly segment jest Intro
        ///   - N segmentow -> ostatni segment jest Intro
        /// Warunki wstepne: rodzic implementuje IHasCommonParts i IHasTextSegments,
        /// posiada segmenty tekstu i nie ma jeszcze CommonPart Intro.
        /// </summary>
        public static void AttachIntroCommonPart(BaseEntity parent)
        {
            if (parent is not IHasCommonParts hasCommonParts) return;
            if (parent is not IHasTextSegments hasTextSegments) return;

            var segments = hasTextSegments.TextSegments;
            if (segments.Count == 0) return;

            // Nie dodawaj ponownie jesli intro juz istnieje
            if (hasCommonParts.CommonParts.Any(cp => cp.Type == CommonPartType.Intro))
                return;

            // 1 segment -> caly jest intro; wiele segmentow -> ostatni jest intro
            var introSegment = segments.Count == 1 ? segments[0] : segments[^1];
            introSegment.Role = "ListIntro";

            var commonPart = new CommonPart(
                CommonPartType.Intro,
                parent.Id,
                introSegment.Text,
                introSegment.Order)
            {
                Parent = parent
            };

            hasCommonParts.CommonParts.Add(commonPart);
        }

        /// <summary>
        /// Tworzy CommonPart typu WrapUp na podstawie akapitu po liscie.
        /// Warunki wstepne: rodzic implementuje IHasCommonParts i nie ma jeszcze WrapUp.
        /// Zwraca true, gdy dodano CommonPart.
        /// </summary>
        public static bool AttachWrapUpCommonPart(BaseEntity parent, string text)
        {
            if (parent is not IHasCommonParts hasCommonParts) return false;
            if (string.IsNullOrWhiteSpace(text)) return false;

            if (hasCommonParts.CommonParts.Any(cp => cp.Type == CommonPartType.WrapUp))
                return false;

            var contentText = StripWrapUpPrefix(text);
            if (string.IsNullOrWhiteSpace(contentText))
                return false;

            var commonPart = new CommonPart(
                CommonPartType.WrapUp,
                parent.Id,
                contentText)
            {
                Parent = parent
            };

            hasCommonParts.CommonParts.Add(commonPart);
            return true;
        }
    }
}
