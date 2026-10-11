using Saga.Core.Helpers;
using Saga.Core.Services.Classify;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;
using Serilog;

namespace Saga.Core.Services.Parsing.Builders
{
    /// <summary>
    /// Wejście dla budowania treści nowelizacji z zebranych akapitów. <paramref name="SectionIsBasicUnit"/>
    /// mówi, że akt zmieniający jest oparty na paragrafach (ADR-0034) — cytowane „§ N." jest wtedy
    /// jednostką podstawową, nie paragrafem kodeksu.
    /// </summary>
    public sealed record AmendmentBuildInput(
        IReadOnlyList<CollectedAmendmentParagraph> Paragraphs,
        StructuralAmendmentReference? Target,
        AmendmentOperationType OperationType,
        bool SectionIsBasicUnit = false);

    /// <summary>
    /// Typ klasyfikacji akapitu w treści nowelizacji.
    /// Mapuje AmendmentTargetKind (ze stylu) lub wzorzec tekstowy (fallback)
    /// na rodzaj encji do zbudowania.
    /// </summary>
    public enum AmendmentEntityKind
    {
        Article,
        Paragraph,
        Point,
        Letter,
        Tiret,
        CommonPart,
        PlainText,
        Unknown
    }

    /// <summary>
    /// Builder treści nowelizacji. Przetwarza bufor akapitów zebranych przez AmendmentCollector
    /// i buduje hierarchiczną strukturę AmendmentContent, analogicznie do głównego orkiestratora.
    ///
    /// Klasyfikacja wewnętrzna opiera się na AmendmentStyleInfo.TargetKind (z dekodera stylów),
    /// z fallbackiem na wzorce tekstowe (regex) gdy styl jest nieznany.
    ///
    /// Hierarchia budowania: ART → UST → PKT → LIT → TIR.
    /// Encje zagnieżdżone trafiają do odpowiednich kolekcji rodziców,
    /// a encje najwyższego poziomu trafiają bezpośrednio do AmendmentContent.
    /// </summary>
    public sealed class AmendmentBuilder : IEntityBuilder<AmendmentBuildInput, AmendmentContent>
    {
        private static readonly EntityNumberService NumberService = new();

        // Bieżący stan hierarchii podczas budowania
        private Article? _currentArticle;
        private Paragraph? _currentParagraph;
        private Point? _currentPoint;
        private Letter? _currentLetter;
        private int _currentTiretIndex;

        // Otwarte tirety cytatu od poziomu pierwszego — tiret zagnieżdżony („– –") trafia do ostatniego.
        private readonly List<Tiret> _openTirets = new();

        // Otwarte jednostki systematyzacyjne cytatu, od najwyższej — artykuł trafia do najgłębszej.
        private readonly List<BaseEntity> _openUnits = new();
        private ISystematizingUnit? _pendingHeading;

        // Akt zmieniający oparty na paragrafach: cytowane „§ N." to jednostka podstawowa (ADR-0034).
        private bool _sectionIsBasicUnit;

        /// <summary>
        /// Buduje AmendmentContent z listy zebranych akapitów nowelizacji.
        /// Klasyfikuje każdy akapit, buduje encje i organizuje je hierarchicznie.
        /// </summary>
        public AmendmentContent Build(AmendmentBuildInput input)
        {
            var content = new AmendmentContent();

            if (input.Paragraphs.Count == 0)
            {
                content.ObjectType = AmendmentObjectType.None;
                Log.Debug("AmendmentBuilder: brak akapitów do przetworzenia");
                return content;
            }

            ResetState();
            _sectionIsBasicUnit = input.SectionIsBasicUnit;

            content.ObjectType = DetermineObjectType(input.Paragraphs, _sectionIsBasicUnit);

            foreach (var para in input.Paragraphs)
            {
                ProcessAmendmentParagraph(para, content);
            }

            Log.Information(
                "AmendmentBuilder: zbudowano treść nowelizacji ({ObjectType}): {Content}",
                content.ObjectType, content);

            return content;
        }

        // ============================================================
        // Klasyfikacja akapitów nowelizacji
        // ============================================================

        /// <summary>
        /// Klasyfikuje pojedynczy akapit nowelizacji na podstawie AmendmentStyleInfo
        /// (priorytet) lub wzorców tekstowych (fallback).
        /// </summary>
        internal static AmendmentEntityKind ClassifyAmendmentParagraph(
            CollectedAmendmentParagraph para, bool sectionIsBasicUnit = false)
            => ClassifyWithStyleOverride(para, sectionIsBasicUnit).Kind;

        /// <summary>Klasyfikacja akapitu nowelizacji wraz z rodzajem odczytanym ze stylu.</summary>
        private static (AmendmentEntityKind Kind, AmendmentEntityKind? OverriddenStyleKind) ClassifyWithStyleOverride(
            CollectedAmendmentParagraph para, bool sectionIsBasicUnit)
        {
            var byText = ClassifyByText(para.Text, sectionIsBasicUnit);

            // Priorytet 1: dekodowanie z metadanych stylu
            if (para.StyleInfo != null)
            {
                var byStyle = MapTargetKindToEntityKind(para.StyleInfo.TargetKind);

                if (IsStructural(byStyle) && IsStructural(byText) && byText != byStyle)
                    return (byText, byStyle);

                // Artykuł wymaga oznaczenia w treści — sam styl go nie czyni, tak jak poza cytatem: akapit
                // w stylu artykułu bez „Art." (wzór, dalszy akapit) jest tekstem, nie artykułem bez numeru.
                if (byStyle == AmendmentEntityKind.Article && byText == AmendmentEntityKind.Unknown)
                    return (AmendmentEntityKind.Unknown, null);

                return (byStyle, null);
            }

            // Priorytet 2: klasyfikacja tekstowa (te same regexy co ParagraphClassifier)
            return (byText, null);
        }

        private static bool IsStructural(AmendmentEntityKind kind) =>
            kind is AmendmentEntityKind.Article
                 or AmendmentEntityKind.Paragraph
                 or AmendmentEntityKind.Point
                 or AmendmentEntityKind.Letter
                 or AmendmentEntityKind.Tiret;

        /// <summary>
        /// Klasyfikacja akapitu wyłącznie na podstawie wzorców tekstowych (regex). W akcie opartym na
        /// paragrafach „§ N." otwiera jednostkę podstawową — tak samo jak poza cytatem (ADR-0034).
        /// </summary>
        private static AmendmentEntityKind ClassifyByText(string text, bool sectionIsBasicUnit)
        {
            if (ParagraphClassifier.IsArticleByText(text))
                return AmendmentEntityKind.Article;
            if (sectionIsBasicUnit && ParagraphClassifier.SectionUnitPattern.IsMatch(text))
                return AmendmentEntityKind.Article;
            if (ParagraphClassifier.IsParagraphByText(text))
                return AmendmentEntityKind.Paragraph;
            if (ParagraphClassifier.IsPointByText(text))
                return AmendmentEntityKind.Point;
            if (ParagraphClassifier.IsLetterByText(text))
                return AmendmentEntityKind.Letter;
            if (ParagraphClassifier.IsTiretByText(text))
                return AmendmentEntityKind.Tiret;

            return AmendmentEntityKind.Unknown;
        }

        /// <summary>
        /// Mapuje AmendmentTargetKind (z dekodera stylów) na AmendmentEntityKind
        /// (wewnętrzny typ klasyfikacji do budowania).
        /// </summary>
        internal static AmendmentEntityKind MapTargetKindToEntityKind(AmendmentTargetKind targetKind)
        {
            return targetKind switch
            {
                AmendmentTargetKind.Article => AmendmentEntityKind.Article,
                AmendmentTargetKind.Paragraph => AmendmentEntityKind.Paragraph,
                AmendmentTargetKind.Point => AmendmentEntityKind.Point,
                AmendmentTargetKind.Letter => AmendmentEntityKind.Letter,
                AmendmentTargetKind.Tiret => AmendmentEntityKind.Tiret,
                AmendmentTargetKind.DoubleTiret => AmendmentEntityKind.Tiret,
                AmendmentTargetKind.CommonPart => AmendmentEntityKind.CommonPart,
                AmendmentTargetKind.Fragment => AmendmentEntityKind.PlainText,
                AmendmentTargetKind.Citation => AmendmentEntityKind.PlainText,
                AmendmentTargetKind.PenalSanction => AmendmentEntityKind.PlainText,
                AmendmentTargetKind.NonArticleText => AmendmentEntityKind.PlainText,
                _ => AmendmentEntityKind.Unknown
            };
        }

        /// <summary>
        /// Wyznacza typ obiektu nowelizacji (ObjectType) na podstawie pierwszego
        /// sklasyfikowanego akapitu w buforze.
        /// </summary>
        internal static AmendmentObjectType DetermineObjectType(
            IReadOnlyList<CollectedAmendmentParagraph> paragraphs, bool sectionIsBasicUnit = false)
        {
            // Tabela nie rozstrzyga rodzaju cytowanej jednostki — rozstrzyga pierwszy akapit.
            foreach (var para in paragraphs.Where(p => p.Table is null))
            {
                var kind = ClassifyAmendmentParagraph(para, sectionIsBasicUnit);
                return kind switch
                {
                    AmendmentEntityKind.Article => AmendmentObjectType.Article,
                    AmendmentEntityKind.Paragraph => AmendmentObjectType.Paragraph,
                    AmendmentEntityKind.Point => AmendmentObjectType.Point,
                    AmendmentEntityKind.Letter => AmendmentObjectType.Letter,
                    AmendmentEntityKind.Tiret => AmendmentObjectType.Tiret,
                    AmendmentEntityKind.CommonPart => AmendmentObjectType.CommonPart,
                    _ => AmendmentObjectType.None
                };
            }
            return AmendmentObjectType.None;
        }

        // ============================================================
        // Przetwarzanie pojedynczego akapitu
        // ============================================================

        private void ProcessAmendmentParagraph(CollectedAmendmentParagraph para, AmendmentContent content)
        {
            if (para.Table is { } table)
            {
                AttachTable(table, content);
                return;
            }

            if (TryEnterSystematizingUnit(para.Text, content))
                return;

            var (entityKind, overriddenStyleKind) = ClassifyWithStyleOverride(para, _sectionIsBasicUnit);

            // Tytuł jednostki (§ 62 ust. 2 ZTP): pierwszy akapit po oznaczeniu, jeśli nie jest jednostką redakcyjną.
            if (_pendingHeading is { } titled)
            {
                _pendingHeading = null;
                if (!IsStructural(entityKind) && entityKind != AmendmentEntityKind.CommonPart)
                {
                    titled.Heading = para.Text.Trim();
                    return;
                }
            }

            BuildByKind(entityKind, para, content);

            if (overriddenStyleKind is { } styleKind)
                ReportStyleOverride(LastBuiltEntity(entityKind, content), styleKind, entityKind, para.StyleId);
        }

        /// <summary>
        /// Tabela w treści cytowanej: dalsza treść jednostki, za którą stoi (ADR-0029), a gdy cytat nie ma
        /// jeszcze żadnej jednostki — tabela cytowana wprost.
        /// </summary>
        private void AttachTable(ContentTable table, AmendmentContent content)
        {
            var unit = (BaseEntity?)_currentLetter ?? (BaseEntity?)_currentPoint ?? _currentParagraph;
            if (unit is null)
                content.Tables.Add(table);
            else
                unit.Continuation.Add(new ContentBlock { Table = table });
        }

        /// <summary>
        /// Encja zbudowana przez ostatnie wywołanie <see cref="BuildByKind"/> dla danego rodzaju.
        /// </summary>
        private BaseEntity? LastBuiltEntity(AmendmentEntityKind kind, AmendmentContent content) => kind switch
        {
            AmendmentEntityKind.Article => _currentArticle,
            AmendmentEntityKind.Paragraph => _currentParagraph,
            AmendmentEntityKind.Point => _currentPoint,
            AmendmentEntityKind.Letter => _currentLetter,
            AmendmentEntityKind.Tiret => _openTirets.LastOrDefault(),
            _ => null,
        };

        /// <summary>
        /// Rejestruje rozstrzygnięcie konfliktu styl/treść na zbudowanej encji. Brzmienie jak w ścieżce
        /// ustawy matki (<see cref="ParagraphClassifier"/>), bo to ta sama decyzja naprawcza.
        /// </summary>
        private static void ReportStyleOverride(
            BaseEntity? entity, AmendmentEntityKind byStyle, AmendmentEntityKind byText, string? styleId)
        {
            Log.Debug("AmendmentBuilder: konflikt styl={StyleKind} ({StyleId}), treść={TextKind} — przyjęto treść",
                byStyle, styleId, byText);

            if (entity != null)
            {
                ValidationReporter.AddValidationMessage(entity, ValidationLevel.Warning,
                    $"Nowelizacja: Konflikt: styl={byStyle}, regex={byText}; rozstrzygnięto: {byText}");
            }
        }

        private void BuildByKind(AmendmentEntityKind entityKind, CollectedAmendmentParagraph para, AmendmentContent content)
        {
            switch (entityKind)
            {
                case AmendmentEntityKind.Article:
                    BuildAmendmentArticle(para.Text, content);
                    break;
                case AmendmentEntityKind.Paragraph:
                    BuildAmendmentParagraph(para.Text, content);
                    break;
                case AmendmentEntityKind.Point:
                    BuildAmendmentPoint(para.Text, content);
                    break;
                case AmendmentEntityKind.Letter:
                    BuildAmendmentLetter(para.Text, content);
                    break;
                case AmendmentEntityKind.Tiret:
                    if (!TryBuildStylelessCommonPart(para, content))
                        BuildAmendmentTiret(para, content);
                    break;
                case AmendmentEntityKind.CommonPart:
                    BuildAmendmentCommonPart(para, content);
                    break;
                default:
                    if (para.StyleInfo?.TargetKind == AmendmentTargetKind.PenalSanction && TryContinueWithSanction(para.Text))
                        break;

                    AppendPlainText(content, para.Text);
                    Log.Debug("AmendmentBuilder: akapit niesklasyfikowany, dodano jako tekst: {Text}",
                        para.Text.Length > 60 ? para.Text[..60] + "..." : para.Text);
                    break;
            }
        }

        // ============================================================
        // Budowanie poszczególnych typów encji
        // ============================================================

        private void BuildAmendmentArticle(string text, AmendmentContent content)
        {
            var article = new Article
            {
                ContentText = text,
                Number = ParsingFactories.ParseArticleNumber(text),
                IsSection = ParagraphClassifier.SectionUnitPattern.IsMatch(text),
            };

            var articleTail = ParsingFactories.GetArticleTail(text);
            var paragraph = ParsingFactories.CreateParagraphFromArticleTail(article, articleTail);
            article.Paragraphs.Add(paragraph);

            if (_openUnits.Count > 0)
            {
                var subchapter = OpenSubchapter();
                article.Parent = subchapter;
                subchapter.Articles.Add(article);
            }
            else
            {
                content.Articles.Add(article);
            }

            _currentArticle = article;
            _currentParagraph = paragraph;
            _currentPoint = null;
            _currentLetter = null;
            ResetTirets();

            Log.Debug("AmendmentBuilder: artykuł {Number}", article.Number?.Value ?? "?");
        }

        private void BuildAmendmentParagraph(string text, AmendmentContent content)
        {
            var contentText = ParsingFactories.StripParagraphPrefix(text);
            var number = ParsingFactories.ParseParagraphNumber(text);

            if (_currentArticle != null)
            {
                // Sprawdź czy mozna ponownie użyć niejawnego pierwszego ustępu
                var first = _currentArticle.Paragraphs.Count == 1
                    ? _currentArticle.Paragraphs[0]
                    : null;

                if (first is { IsImplicit: true }
                    && string.IsNullOrWhiteSpace(first.ContentText)
                    && first.Points.Count == 0)
                {
                    first.Number = number;
                    first.IsImplicit = false;
                    ParsingFactories.ApplyCodeParagraphRole(first, text);
                    ParsingFactories.SetContentAndSegments(first, contentText);
                    _currentParagraph = first;
                }
                else
                {
                    var paragraph = new Paragraph
                    {
                        Parent = _currentArticle,
                        Article = _currentArticle,
                        Number = number,
                        IsImplicit = false
                    };
                    ParsingFactories.ApplyCodeParagraphRole(paragraph, text);
                    ParsingFactories.SetContentAndSegments(paragraph, contentText);
                    _currentArticle.Paragraphs.Add(paragraph);
                    _currentParagraph = paragraph;
                }
            }
            else
            {
                // Ustęp najwyższego poziomu (bez kontekstu artykułu)
                var paragraph = new Paragraph
                {
                    Number = number,
                    IsImplicit = false
                };
                ParsingFactories.ApplyCodeParagraphRole(paragraph, text);
                ParsingFactories.SetContentAndSegments(paragraph, contentText);
                content.Paragraphs.Add(paragraph);
                _currentParagraph = paragraph;
            }

            _currentPoint = null;
            _currentLetter = null;
            ResetTirets();

            Log.Debug("AmendmentBuilder: ustęp {Number}", _currentParagraph.Number?.Value ?? "?");
        }

        private void BuildAmendmentPoint(string text, AmendmentContent content)
        {
            var contentText = ParsingFactories.StripPointPrefix(text);
            var point = new Point
            {
                Parent = _currentParagraph,
                Article = _currentArticle,
                Paragraph = _currentParagraph,
                Number = ParsingFactories.ParsePointNumber(text)
            };
            ParsingFactories.SetContentAndSegments(point, contentText);

            if (_currentParagraph != null)
            {
                // Wiąż intro przed pierwszym punktem
                if (_currentParagraph.Points.Count == 0)
                    ParsingFactories.AttachIntroCommonPart(_currentParagraph);

                CommonPartPlacement.BeforeNextItem(_currentParagraph, _currentParagraph.Points);
                _currentParagraph.Points.Add(point);
            }
            else
            {
                // Punkt najwyższego poziomu
                content.Points.Add(point);
            }

            _currentPoint = point;
            _currentLetter = null;
            ResetTirets();

            Log.Debug("AmendmentBuilder: punkt {Number}", point.Number?.Value ?? "?");
        }

        private void BuildAmendmentLetter(string text, AmendmentContent content)
        {
            var contentText = ParsingFactories.StripLetterPrefix(text);
            var letter = new Letter
            {
                Parent = _currentPoint,
                Article = _currentArticle,
                Paragraph = _currentParagraph,
                Point = _currentPoint,
                Number = ParsingFactories.ParseLetterNumber(text)
            };
            ParsingFactories.SetContentAndSegments(letter, contentText);

            if (_currentPoint != null)
            {
                if (_currentPoint.Letters.Count == 0)
                    ParsingFactories.AttachIntroCommonPart(_currentPoint);

                CommonPartPlacement.BeforeNextItem(_currentPoint, _currentPoint.Letters);
                _currentPoint.Letters.Add(letter);
            }
            else
            {
                content.Letters.Add(letter);
            }

            _currentLetter = letter;
            ResetTirets();

            Log.Debug("AmendmentBuilder: litera {Number}", letter.Number?.Value ?? "?");
        }

        private void BuildAmendmentTiret(CollectedAmendmentParagraph para, AmendmentContent content)
        {
            var contentText = ParsingFactories.StripTiretPrefix(para.Text);

            // Zagnieżdżenie zapisane znacznikiem („– –") albo stylem; bez tiretu nadrzędnego zostaje poziom 1.
            var depth = Math.Min(TiretDepth(para), _openTirets.Count + 1);
            _openTirets.RemoveRange(depth - 1, _openTirets.Count - (depth - 1));
            var parentTiret = _openTirets.LastOrDefault();

            var tiret = new Tiret
            {
                Parent = (BaseEntity?)parentTiret ?? _currentLetter,
                Article = _currentArticle,
                Paragraph = _currentParagraph,
                Point = _currentPoint,
                Letter = _currentLetter,
                Number = NumberService.Create(numericPart: parentTiret != null ? parentTiret.Tirets.Count + 1 : ++_currentTiretIndex)
            };
            ParsingFactories.SetContentAndSegments(tiret, contentText);

            if (parentTiret != null)
            {
                if (parentTiret.Tirets.Count == 0)
                    ParsingFactories.AttachIntroCommonPart(parentTiret);

                parentTiret.Tirets.Add(tiret);
            }
            else if (_currentLetter != null)
            {
                if (_currentLetter.Tirets.Count == 0)
                    ParsingFactories.AttachIntroCommonPart(_currentLetter);

                _currentLetter.Tirets.Add(tiret);
            }
            else
            {
                content.Tirets.Add(tiret);
            }

            _openTirets.Add(tiret);
            Log.Debug("AmendmentBuilder: tiret {Number}", tiret.Number?.Value ?? "?");
        }

        private static int TiretDepth(CollectedAmendmentParagraph para)
        {
            var written = TiretMarker.Depth(para.Text);
            if (written > 1)
                return written;

            return para.StyleInfo?.TargetKind == AmendmentTargetKind.DoubleTiret ? 2 : 1;
        }

        private void ResetTirets()
        {
            _currentTiretIndex = 0;
            _openTirets.Clear();
        }

        /// <summary>
        /// Sankcja karna cytowanego przepisu („Kto …, || podlega karze…") domyka jego zdanie, a po wyliczeniu
        /// jest jego częścią wspólną — tak samo jak w treści aktu. Bez otwartego przepisu („sankcja otrzymuje
        /// brzmienie:") zostaje tekstem cytatu.
        /// </summary>
        private bool TryContinueWithSanction(string text)
        {
            if (_currentParagraph is not { } paragraph)
                return false;

            var sanction = text.Trim();
            if (_currentPoint is not null)
            {
                paragraph.CommonParts.Add(new CommonPart(CommonPartType.WrapUp, paragraph.Id, sanction)
                {
                    Parent = paragraph,
                    ListItemType = UnitType.Point,
                });
            }
            else
            {
                ParsingFactories.SetContentAndSegments(paragraph, $"{paragraph.ContentText.TrimEnd()} {sanction}");
            }

            return true;
        }

        /// <summary>
        /// Część wspólna w treści cytowanej bez stylu — ta sama reguła co w treści aktu
        /// (<c>StructureProcessor.TryHandleStylelessWrapUp</c>): myślnik tuż po punkcie albo literze, które nie
        /// mają żadnego znaku końcowego, zamyka wyliczenie (§ 57 ust. 3 ZTP). Po tirecie i po elemencie
        /// zakończonym znakiem myślnik zostaje tiretem.
        /// </summary>
        private bool TryBuildStylelessCommonPart(CollectedAmendmentParagraph para, AmendmentContent content)
        {
            if (para.StyleInfo != null || ParagraphClassifier.GetStyleType(para.StyleId) == "TIR")
                return false;

            BaseEntity lastItem;
            BaseEntity? parent;
            UnitType itemType;
            if (_currentLetter != null)
            {
                if (_currentLetter.Tirets.Count > 0)
                    return false;
                (lastItem, parent, itemType) = (_currentLetter, _currentPoint, UnitType.Letter);
            }
            else if (_currentPoint != null && _currentPoint.Letters.Count == 0)
            {
                (lastItem, parent, itemType) = (_currentPoint, _currentParagraph, UnitType.Point);
            }
            else
            {
                return false;
            }

            if (!CommonPartPlacement.LeavesEnumerationOpen(lastItem.ContentText))
                return false;

            var commonPart = new CommonPart(CommonPartType.WrapUp, parent?.Id ?? string.Empty, para.Text)
            {
                Parent = parent,
                ListItemType = itemType,
            };

            if (parent is IHasCommonParts host)
                host.CommonParts.Add(commonPart);
            else
                content.CommonParts.Add(commonPart);

            if (parent != null)
                ValidationReporter.AddValidationMessage(parent, ValidationLevel.Info,
                    "Część wspólna rozpoznana bez stylu: ostatni element wyliczenia nie ma znaku końcowego (§ 57 ust. 3 ZTP).");
            return true;
        }

        private void BuildAmendmentCommonPart(CollectedAmendmentParagraph para, AmendmentContent content)
        {
            var commonPartOf = para.StyleInfo?.CommonPartOf;

            // Ustal rodzica na podstawie CommonPartOf — część wspólna
            // jest rodzeństwem elementów wyliczeniowych
            BaseEntity? parent = commonPartOf switch
            {
                AmendmentTargetKind.Point => _currentParagraph,
                AmendmentTargetKind.Letter => _currentPoint,
                AmendmentTargetKind.Tiret => _currentLetter,
                _ => _currentParagraph
            };

            var commonPart = new CommonPart(
                CommonPartType.WrapUp,
                parent?.Id ?? string.Empty,
                para.Text)
            {
                Parent = parent,
                ListItemType = commonPartOf switch
                {
                    AmendmentTargetKind.Letter => UnitType.Letter,
                    AmendmentTargetKind.Tiret => UnitType.Tiret,
                    _ => UnitType.Point,
                }
            };

            if (parent is IHasCommonParts hasCommonParts)
            {
                hasCommonParts.CommonParts.Add(commonPart);
            }
            else
            {
                content.CommonParts.Add(commonPart);
            }

            Log.Debug("AmendmentBuilder: część wspólna ({Type}) dla {ParentId}",
                commonPart.Type, commonPart.ParentEId);
        }

        // ============================================================
        // Jednostki systematyzacyjne w cytacie
        // ============================================================

        /// <summary>
        /// Oznaczenie jednostki („Rozdział 9a”) otwiera ją w cytacie: najwyższa trafia do treści nowelizacji,
        /// niższa — pod otwartą jednostkę wyższą, z poziomami niejawnymi pomiędzy.
        /// </summary>
        private bool TryEnterSystematizingUnit(string text, AmendmentContent content)
        {
            var designation = text.Trim();
            if (ParagraphClassifier.MatchSystematizingRegex(designation) is not { } kind
                || ParagraphClassifier.ParseSystematizingNumber(kind, designation) is not { } number)
                return false;

            var unit = SystematizingHierarchy.Create(ParagraphClassifier.LevelOf(kind)!.Value);
            unit.Number = number;
            ((ISystematizingUnit)unit).IsImplicit = false;

            while (_openUnits.Count > 0 && SystematizingHierarchy.Rank(_openUnits[^1]) >= SystematizingHierarchy.Rank(unit))
                _openUnits.RemoveAt(_openUnits.Count - 1);

            if (_openUnits.Count == 0)
                content.SystematizingUnits.Add(unit);
            else
                Attach(_openUnits[^1], unit);

            _openUnits.Add(unit);
            _pendingHeading = (ISystematizingUnit)unit;
            _currentArticle = null;
            _currentParagraph = null;
            _currentPoint = null;
            _currentLetter = null;
            ResetTirets();

            Log.Debug("AmendmentBuilder: jednostka systematyzacyjna {Kind} {Number}", kind, number.Value);
            return true;
        }

        /// <summary>Oddział, do którego trafia artykuł — pod najgłębszą otwartą jednostką, w razie potrzeby niejawny.</summary>
        private Subchapter OpenSubchapter()
        {
            var current = _openUnits[^1];
            while (current is not Subchapter)
            {
                if (SystematizingHierarchy.Children(current) is [.., ISystematizingUnit { IsImplicit: true } last])
                {
                    current = (BaseEntity)last;
                    continue;
                }

                var created = Below(current);
                SystematizingHierarchy.AddChild(current, created);
                current = created;
            }
            return (Subchapter)current;
        }

        private static void Attach(BaseEntity parent, BaseEntity child)
        {
            var current = parent;
            while (SystematizingHierarchy.Rank(current) + 1 < SystematizingHierarchy.Rank(child))
            {
                var implicitUnit = Below(current);
                SystematizingHierarchy.AddChild(current, implicitUnit);
                current = implicitUnit;
            }
            SystematizingHierarchy.AddChild(current, child);
        }

        /// <summary>Nowa jednostka niejawna poziomu tuż pod daną.</summary>
        private static BaseEntity Below(BaseEntity unit) =>
            SystematizingHierarchy.Create(SystematizingHierarchy.Levels[SystematizingHierarchy.Rank(unit) + 1]);

        // ============================================================
        // Helpers
        // ============================================================

        private static void AppendPlainText(AmendmentContent content, string text)
        {
            content.PlainText = string.IsNullOrEmpty(content.PlainText)
                ? text
                : content.PlainText + Environment.NewLine + text;
        }

        private void ResetState()
        {
            _currentArticle = null;
            _currentParagraph = null;
            _currentPoint = null;
            _currentLetter = null;
            ResetTirets();
            _openUnits.Clear();
            _pendingHeading = null;
        }
    }
}
