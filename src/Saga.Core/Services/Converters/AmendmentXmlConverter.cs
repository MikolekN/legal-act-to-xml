using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Amendment na XML.
    /// </summary>
    internal static class AmendmentXmlConverter
    {
        /// <summary>Komenda nowelizacji: cel i GUID, którym wiąże się z modelem.</summary>
        private sealed class Command(StructuralAmendmentReference? target, Guid guid)
        {
            private bool _guidTaken;

            internal StructuralAmendmentReference? Target { get; } = target;

            /// <summary>
            /// Adres celu w akcie zmienianym („art_5__ust_2", „zal_2"); null — komenda bez celu (ADR-0125), tytuł aktu
            /// (ADR-0101) albo cel, z którego nie powstał żaden segment adresu.
            /// </summary>
            internal string? Address =>
                Target is { Structure.ActTitle: false } target && target.Structure.ToString() is { Length: > 0 } address ? address : null;

            /// <summary>Instrukcja ręczna: cel, którego eId nie zaadresuje (ADR-0100), albo cel bez adresu (ADR-0101, ADR-0125).</summary>
            internal bool IsManual => Target is { IsManual: true } || Address is null;

            /// <summary>
            /// GUID kolejnego elementu komendy. Pierwszy niesie GUID komendy, następne własne — jedna komenda
            /// bywa kilkoma cytatami (tekstowy i strukturalny), a schemat wymaga unikalnego nowGUID.
            /// </summary>
            internal Guid NextGuid()
            {
                if (_guidTaken)
                    return Guid.NewGuid();

                _guidTaken = true;
                return guid;
            }
        }

        internal static void ToXml(Amendment? amendment, XNode? sentence = null)
        {
            if (amendment is null)
                return;

            var commands = Commands(amendment);

            AddContentWrapInstruction(amendment, sentence);
            AddAnnexInstructions(amendment, sentence);

            if (amendment.OperationType == AmendmentOperationType.Repeal)
            {
                AddRepealInstructions(amendment, commands, sentence);
                AddTextChangeInstructions(amendment, commands, sentence);
                return;
            }

            AddTextChangeInstructions(amendment, commands, sentence);

            if (amendment.Content is null)
                return;

            var units = AmendmentTargetPairing.ContentUnits(amendment.Content);

            // Tekst cytowany przy kilku celach powtarza się przy każdym; tabela cytowana jest jedna, przy pierwszym.
            if (units.Count == 0 && amendment.Content.Tables.Count == 0 && commands.Count > 1)
            {
                foreach (var command in commands)
                {
                    var copy = new List<XElement>();
                    AddPlainTextContent(amendment, command, sentence, copy);
                    AddCommonParts(amendment, command, sentence, copy);
                    ApplyQuotationMarks(amendment, copy);
                }
            }
            else
            {
                var paired = commands.Count > 1 ? AmendmentTargetPairing.Pair(amendment.Targets, units) : null;
                if (paired is null)
                {
                    commands = commands.Take(1).ToList();
                    paired = [units];
                }

                var blocks = new List<XElement>();
                for (var i = 0; i < commands.Count; i++)
                {
                    if (i == 0)
                    {
                        AddPlainTextContent(amendment, commands[i], sentence, blocks, AmendmentTargetPairing.IntroductionOwner(amendment));
                        AddQuotedFootnote(amendment, commands[i], sentence, blocks);
                    }

                    AddStructuredContentItems(amendment, commands[i], paired[i], sentence, blocks);
                    if (i == 0)
                        AddTables(amendment, commands[i], sentence, blocks);
                    if (i == commands.Count - 1)
                        AddCommonParts(amendment, commands[i], sentence, blocks);
                }

                ApplyQuotationMarks(amendment, blocks);
            }

            // Interpunkcja po cytacie należy do zdania nowelizującego, nie do cytowanej treści.
            if (amendment.TrailingPunctuation is { Length: > 0 } trailing && sentence is XElement sentenceElement)
            {
                sentenceElement.Add(trailing);
            }
        }

        /// <summary>
        /// Nowelizacje komend z wcześniejszych zdań jednostki — każda w zdaniu, w którym stoi jej komenda (ADR-0122).
        /// Elementy zdań stoją w kolejności segmentów tekstu jednostki; gdy ich liczba się nie zgadza, nowelizacja idzie
        /// do ostatniego zdania, jak komenda jednostki.
        /// </summary>
        internal static void ToXml(IReadOnlyList<Amendment> earlier, IReadOnlyList<TextSegment> segments, XElement textSection)
        {
            if (earlier.Count == 0)
                return;

            var sentences = textSection.Elements().ToList();
            foreach (var amendment in earlier)
            {
                var index = -1;
                for (var i = 0; i < segments.Count && sentences.Count == segments.Count; i++)
                {
                    if (segments[i].Order == amendment.CommandSentence)
                        index = i;
                }

                ToXml(amendment, index >= 0 ? sentences[index] : textSection.LastNode);
            }
        }

        private static List<Command> Commands(Amendment amendment)
        {
            var first = amendment.Guid != Guid.Empty ? amendment.Guid : Guid.NewGuid();
            var targets = amendment.Targets ?? [];
            // Nowelizacja bez celu to jedna komenda bez celu: instrukcja ręczna na samym akcie (ADR-0125).
            if (targets.Count == 0)
                return [new Command(null, first)];

            return targets
                .Select((target, index) => new Command(target, index == 0 ? first : Guid.NewGuid()))
                .ToList();
        }

        /// <summary>
        /// Rozdziela cudzysłowy cytatu pomiędzy jego bloki: otwierający trafia do pierwszego, zamykający
        /// do ostatniego, środkowe nie mają żadnego. Cudzysłowy niesie wyłącznie atrybut — treść cytatu,
        /// także tekstowego, jest bez nich (jak w przykładach autorów schematu).
        /// </summary>
        private static void ApplyQuotationMarks(Amendment amendment, List<XElement> blocks)
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                var opens = i == 0 && amendment.HasOpeningQuote;
                var closes = i == blocks.Count - 1 && amendment.HasClosingQuote;
                blocks[i].SetAttributeValue(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute, QuotationMarks(opens, closes));
            }
        }

        internal static string QuotationMarks(bool opens, bool closes) => (opens, closes) switch
        {
            (true, true) => XmlConverterConstants.Contents.Citation.Quotation.QuotationBoth,
            (true, false) => XmlConverterConstants.Contents.Citation.Quotation.QuotationOpening,
            (false, true) => XmlConverterConstants.Contents.Citation.Quotation.QuotationClosing,
            _ => XmlConverterConstants.Contents.Citation.Quotation.QuotationNone,
        };

        private static string BuildActUri(Amendment amendment) =>
            XmlConverterConstants.ELIUrl + "eli/" + (amendment.TargetLegalAct.GetELIStrings().FirstOrDefault() ?? string.Empty);

        /// <param name="function">Funkcja inna niż wynikająca z rodzaju operacji.</param>
        /// <param name="address">Adres inny niż cel komendy — gdy cytat zastępuje jednostkę pod celem.</param>
        private static void ApplyAttributes(XElement element, Amendment amendment, Command command, bool isStructural,
            string? function = null, string? address = null)
        {
            XNamespace now = XmlConverterConstants.AmendmentNamespace;

            element.Add(new XAttribute(now + XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute,
                XmlConverterConstants.Contents.Citation.Quotation.QuotationBoth));

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, command.NextGuid().ToString()));

            // if (amendment.RefNowGuid != Guid.Empty)
            // element.Add(new XAttribute(XmlConverterConstants.QuotationRefGUIDAttribute, "UNKNOWN"));

            var explicitAddress = !string.IsNullOrEmpty(address);
            if (address is { Length: > 0 })
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, address));
            else
                AddTargetAttributes(element, amendment, command);

            var manual = command.Target is { IsManual: true } || (!explicitAddress && command.Address is null);
            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.FunctionAttribute,
                manual ? ManualFunction(amendment.OperationType) : function ?? GetFunctionType(amendment.OperationType, isStructural)));

            // element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.SentenceAttribute, sentence.ReferenceSentenceNumber));

            // string appearance = string.IsNullOrWhiteSpace(amendment.Appearance) ? "wszystkie" : amendment.Appearance;
            // element.Add(new XAttribute(XmlConverterConstants.QuotationAppearanceAttribute, "UNKNOWN"));
        }

        private static string GetFunctionType(AmendmentOperationType operation, bool isStructural) =>
            operation switch
            {
                AmendmentOperationType.Repeal => XmlConverterConstants.Contents.Citation.Quotation.Function.RepealText,
                AmendmentOperationType.Insertion => XmlConverterConstants.Contents.Citation.Quotation.Function.AddAfter,
                AmendmentOperationType.Modification => isStructural
                    ? XmlConverterConstants.Contents.Citation.Quotation.Function.Edit
                    : XmlConverterConstants.Contents.Citation.Quotation.Function.EditText,
                AmendmentOperationType.Error => XmlConverterConstants.Contents.Citation.Quotation.Function.NewText,
                _ => XmlConverterConstants.Contents.Citation.Quotation.Function.NewText
            };

        /// <summary>
        /// Instrukcja ręczna dla celu, którego <c>eId</c> nie zaadresuje — adres wskazuje wtedy najgłębszą adresowalną
        /// jednostkę (ADR-0100).
        /// </summary>
        private static string ManualFunction(AmendmentOperationType operation) => operation switch
        {
            AmendmentOperationType.Insertion => XmlConverterConstants.Contents.Citation.Quotation.Function.AddManually,
            AmendmentOperationType.Repeal => XmlConverterConstants.Contents.Citation.Quotation.Function.RepealManually,
            _ => XmlConverterConstants.Contents.Citation.Quotation.Function.EditManually,
        };

        /// <summary>Przedmiot uchylenia z tekstu przepisu: „w załączniku nr 2 uchyla się lp. 7;" → „lp. 7".</summary>
        private static readonly Regex RepealedObject = new(
            @"\b(?:uchyla|skreśla)\s+się\s+(?<object>.+?)[\s;,.:]*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Cytaty instrukcji ręcznych różniące się tylko <c>nowGUID</c> — cele zwinięte do jednego adresu (ADR-0100), np.
        /// „uchyla się pkt 8, 9 i 26” w załączniku — to jedna zmiana do wprowadzenia ręcznie: zostaje pierwszy.
        /// </summary>
        private static List<XElement> Distinct(List<XElement> manual)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return manual.Where(citation => seen.Add(WithoutGuid(citation))).ToList();

            static string WithoutGuid(XElement citation)
            {
                var copy = new XElement(citation);
                copy.Attribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute)?.Remove();
                return copy.ToString(SaveOptions.DisableFormatting);
            }
        }

        /// <summary>
        /// Uchylenie celu bez adresu: tekstu do uchylenia akt zmieniający nie przytacza, więc cytat niesie przedmiot uchylenia
        /// tak, jak nazywa go przepis („lp. 7"), bez cudzysłowów — to wskazanie dla tego, kto zmianę wprowadzi ręcznie.
        /// Komenda bez celu bierze przedmiot z tekstu zdania (ADR-0125).
        /// </summary>
        private static XElement ManualRepeal(Amendment amendment, Command command, XElement sentence)
        {
            var raw = (command.Target?.RawText ?? string.Concat(sentence.Nodes().OfType<XText>().Select(text => text.Value))).Trim();
            var repealed = RepealedObject.Match(raw) is { Success: true } match ? match.Groups["object"].Value : raw;

            var citation = Element(XmlConverterConstants.Contents.Citation.Text.Element,
                Element(XmlConverterConstants.Contents.Citation.Text.Content, repealed));
            ApplyAttributes(citation, amendment, command, isStructural: false);
            citation.SetAttributeValue(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute,
                XmlConverterConstants.Contents.Citation.Quotation.QuotationNone);
            return citation;
        }

        /// <summary>
        /// Zmiana w tekście celu bez adresu: <c>now:zamien-tekst</c> nie ma funkcji ręcznej, więc wychodzi cytat
        /// z nowymi wyrazami i funkcją <c>zastap-recznie</c> — przy skreśleniu ze skreślanymi wyrazami i <c>uchyl-recznie</c>.
        /// </summary>
        private static XElement ManualTextChange(Amendment amendment, Command command, TextChange change)
        {
            var repeal = change.NewText.Length == 0;
            var citation = Element(XmlConverterConstants.Contents.Citation.Text.Element,
                Element(XmlConverterConstants.Contents.Citation.Text.Content, repeal ? change.OldText : change.NewText));
            ApplyAttributes(citation, amendment, command, isStructural: false);
            citation.SetAttributeValue(XmlConverterConstants.Contents.Citation.Quotation.FunctionAttribute, repeal
                ? XmlConverterConstants.Contents.Citation.Quotation.Function.RepealManually
                : AddsWithoutAPlace(change)
                    ? XmlConverterConstants.Contents.Citation.Quotation.Function.AddManually
                    : XmlConverterConstants.Contents.Citation.Quotation.Function.EditManually);
            return citation;
        }

        /// <summary>
        /// Dodanie bez wskazanego miejsca w tekście („w pkt 4 dodaje się przecinek"): ostatniego słowa jednostki akt
        /// zmieniający nie podaje, więc nie ma czego zastąpić — instrukcja ręczna ze znakiem (ADR-0104).
        /// </summary>
        private static bool AddsWithoutAPlace(TextChange change) => change.OldText.Length == 0 && change.NewText.Length > 0;

        /// <summary>
        /// Adres celu; cel bez adresu — tytuł aktu (ADR-0101) albo komenda bez celu (ADR-0125) — ma za adres sam akt
        /// („DU/2021/200"), a instrukcja jest wtedy ręczna. Aktu nieodczytanego się nie zgaduje: bez adresu.
        /// </summary>
        private static void AddTargetAttributes(XElement element, Amendment amendment, Command command)
        {
            var start = command.Address ?? amendment.TargetLegalAct.GetELIStrings().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(start))
                return;

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, start));

            var end = command.Address is null ? null : command.Target?.RangeEnd?.ToString();
            if (!string.IsNullOrWhiteSpace(end) && end != start)
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementDoAttribute, end));
        }

        /// <summary>
        /// Uchylenie: <c>now:uchyl</c> na każdy cel, przed tekstem zdania jak każda instrukcja <c>now:</c> (ADR-0123),
        /// w kolejności celów; cel bez adresu — cytat <c>uchyl-recznie</c> za tekstem (ADR-0100, ADR-0125).
        /// </summary>
        private static void AddRepealInstructions(Amendment amendment, List<Command> commands, XNode? sentence)
        {
            if (sentence is not XElement zdanie)
                return;

            XNamespace now = XmlConverterConstants.AmendmentNamespace;
            var instructions = new List<XElement>();
            var manual = new List<XElement>();

            foreach (var command in commands)
            {
                if (command.IsManual)
                {
                    manual.Add(ManualRepeal(amendment, command, zdanie));
                    continue;
                }

                var uchyl = new XElement(now + XmlConverterConstants.Contents.Instruction.Repeal);
                uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));
                AddTargetAttributes(uchyl, amendment, command);
                uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, command.NextGuid().ToString()));
                instructions.Add(uchyl);
            }

            zdanie.AddFirst(instructions);
            zdanie.Add(Distinct(manual));
        }

        /// <summary>
        /// Obudowanie dotychczasowej treści jednostki ustępem albo paragrafem („dotychczasową treść oznacza się jako
        /// ust. 1") — instrukcja <c>now:dodaj-ustep</c> / <c>now:dodaj-paragraf</c> na jednostce, w której powstaje
        /// nowy poziom (ADR-0056). Schemat wymaga jej adresu, więc bez niego instrukcji nie ma.
        /// </summary>
        private static void AddContentWrapInstruction(Amendment amendment, XNode? sentence)
        {
            if (amendment.WrapsContentAs == ContentWrap.None || sentence is not XElement zdanie
                || amendment.WrappedUnit?.Structure.ToString() is not { Length: > 0 } unit)
            {
                return;
            }

            XNamespace now = XmlConverterConstants.AmendmentNamespace;
            var name = amendment.WrapsContentAs == ContentWrap.CodeParagraph
                ? XmlConverterConstants.Contents.Instruction.WrapInSection
                : XmlConverterConstants.Contents.Instruction.WrapInParagraph;

            zdanie.AddFirst(new XElement(now + name,
                new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, unit),
                new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)),
                new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, Guid.NewGuid().ToString())));
        }

        /// <summary>
        /// Załączniki zmieniane odesłaniem do załączników aktu zmieniającego: <c>now:zastap</c> na każdy zastępowany
        /// załącznik, <c>now:dodaj-po</c> na dodawane — z adresem załącznika aktu zmieniającego, który niesie nowe
        /// brzmienie (ADR-0058). Akt zmieniający i prefiksy adresów uzupełnia <see cref="ActPrefixXml"/>.
        /// </summary>
        private static void AddAnnexInstructions(Amendment amendment, XNode? sentence)
        {
            if (amendment.AnnexInstructions.Count == 0 || sentence is not XElement zdanie)
                return;

            XNamespace now = XmlConverterConstants.AmendmentNamespace;
            static string Annex(string number) => $"{XmlConverterConstants.Contents.Instruction.AnnexPrefix}_{number}";

            var instructions = new List<XElement>();
            var manual = new List<XElement>();
            foreach (var annex in amendment.AnnexInstructions)
            {
                if (annex.Part is { } part)
                {
                    manual.Add(ManualAnnexPart(amendment, annex, part, Annex));
                    continue;
                }

                var instruction = new XElement(now + (annex.Adds
                        ? XmlConverterConstants.Contents.Instruction.AddAfter
                        : XmlConverterConstants.Contents.Instruction.ReplaceUnits),
                    new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));

                if (annex.Target is { } target)
                    instruction.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, Annex(target)));

                instruction.Add(new XAttribute(XmlConverterConstants.Contents.Instruction.AmendingActAttribute,
                    XmlConverterConstants.ELIUrl + "eli/"));
                if (annex.Source is { } source)
                    instruction.Add(new XAttribute(XmlConverterConstants.Contents.Instruction.AmendingUnitAttribute, Annex(source)));
                if (annex is { Adds: true, SourceTo: { } sourceTo })
                    instruction.Add(new XAttribute(XmlConverterConstants.Contents.Instruction.AmendingUnitToAttribute, Annex(sourceTo)));

                instruction.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, Guid.NewGuid().ToString()));
                instructions.Add(instruction);
            }

            zdanie.AddFirst(instructions);
            zdanie.Add(Distinct(manual));
        }

        /// <summary>
        /// Część załącznika zmieniana albo dodawana odesłaniem („tabela C otrzymuje brzmienie określone w załączniku nr 1
        /// do niniejszego rozporządzenia"): części nie zaadresuje <c>eId</c>, a instrukcja ręczna nie ma atrybutów
        /// załącznika aktu zmieniającego. Cytat niesie część tak, jak ją nazywa przepis, bez cudzysłowów — jak przy
        /// uchyleniu celu bez adresu; odesłanie zostaje w zdaniu przepisu (ADR-0108).
        /// </summary>
        private static XElement ManualAnnexPart(Amendment amendment, AnnexInstruction annex, string part, Func<string, string> address)
        {
            XNamespace now = XmlConverterConstants.AmendmentNamespace;
            var citation = Element(XmlConverterConstants.Contents.Citation.Text.Element,
                Element(XmlConverterConstants.Contents.Citation.Text.Content, part));
            citation.Add(
                new XAttribute(now + XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)),
                new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute,
                    XmlConverterConstants.Contents.Citation.Quotation.QuotationNone),
                new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, Guid.NewGuid().ToString()));
            if (annex.Target is { } target)
                citation.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, address(target)));
            citation.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.FunctionAttribute, annex.Adds
                ? XmlConverterConstants.Contents.Citation.Quotation.Function.AddManually
                : XmlConverterConstants.Contents.Citation.Quotation.Function.EditManually));
            return citation;
        }

        /// <summary>
        /// Zmiana wyrazów nie przytacza brzmienia jednostki, więc nie jest cytatem: wychodzi instrukcją
        /// <c>now:zamien-tekst</c> — po jednej na każdy cel i każdą zmianę — przed tekstem zdania, jak
        /// w przykładach autorów schematu (ADR-0052).
        /// </summary>
        private static void AddTextChangeInstructions(Amendment amendment, List<Command> commands, XNode? sentence)
        {
            if (amendment.TextChanges.Count == 0 || sentence is not XElement zdanie)
                return;

            XNamespace now = XmlConverterConstants.AmendmentNamespace;
            var instructions = new List<XElement>();
            var manual = new List<XElement>();

            foreach (var change in amendment.TextChanges)
            {
                // Zmiana z własnym celem (znak kończący jednostkę) nie dzieli go z resztą nowelizacji.
                var targets = change.Targets.Count > 0
                    ? change.Targets.Select(target => new Command(target, Guid.NewGuid())).ToList()
                    : commands;

                foreach (var command in targets)
                {
                    if (command.IsManual || AddsWithoutAPlace(change))
                    {
                        manual.Add(ManualTextChange(amendment, command, change));
                        continue;
                    }

                    var instruction = new XElement(now + XmlConverterConstants.Contents.Instruction.ReplaceText,
                        new XAttribute(XmlConverterConstants.Contents.Instruction.OldTextAttribute, change.OldText),
                        new XAttribute(XmlConverterConstants.Contents.Instruction.NewTextAttribute, change.NewText));
                    AddTargetAttributes(instruction, amendment, command);
                    instruction.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));
                    if (change.Occurrence == TextOccurrence.Last)
                    {
                        instruction.Add(new XAttribute(now + XmlConverterConstants.Contents.Citation.Quotation.AppearanceAttribute,
                            XmlConverterConstants.Contents.Instruction.LastOccurrence));
                    }

                    instruction.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, command.NextGuid().ToString()));
                    instructions.Add(instruction);
                }
            }

            zdanie.AddFirst(instructions);
            zdanie.Add(Distinct(manual));
        }

        /// <summary>
        /// Odnośnik będący treścią nowelizacji: cytat strukturalny z <c>odnosnik-cyt</c> pod adresem <c>odn_N</c>.
        /// Znacznik stoi w treści indeksem górnym — schemat nie wydziela numeru cytowanego odnośnika (ADR-0059).
        /// </summary>
        private static void AddQuotedFootnote(Amendment amendment, Command command, XNode? sentence, List<XElement> blocks)
        {
            if (amendment.Content?.Footnote is not { } footnote)
                return;

            var citation = Element(XmlConverterConstants.Contents.Citation.StructElement);
            ApplyAttributes(citation, amendment, command, isStructural: true);
            citation.Add(FootnoteContentXml.Quoted(footnote));

            if (sentence is XElement zdanie)
                zdanie.Add(citation);
            blocks.Add(citation);
        }

        /// <param name="address">Adres inny niż cel komendy — wprowadzenia do wyliczenia zmienianego razem z pozycjami.</param>
        private static void AddPlainTextContent(Amendment amendment, Command command, XNode? sentence, List<XElement> blocks, string? address = null)
        {
            var plainText = amendment.Content?.PlainText;
            if (plainText == null) return;
            AddTextCitation(amendment, command, sentence, plainText, blocks, address);
        }

        private static void AddCommonParts(Amendment amendment, Command command, XNode? sentence, List<XElement> blocks)
        {
            var commonParts = amendment.Content?.CommonParts;
            if (commonParts == null) return;

            // Część wspólna jest elementem struktury, nie fragmentem tekstu: cytat strukturalny z `czesc-wspolna-*`.
            // Dodawana staje W jednostce wskazanej komendą; przy zmianie brzmienia komenda wskazuje ją samą.
            foreach (var commonPart in commonParts)
            {
                var replaces = amendment.OperationType == AmendmentOperationType.Modification;
                var citation = Element(XmlConverterConstants.Contents.Citation.StructElement);
                ApplyAttributes(citation, amendment, command, isStructural: true,
                    function: replaces ? null : XmlConverterConstants.Contents.Citation.Quotation.Function.AddInside,
                    address: replaces && commonPart.AddressAnchor.Length > 0 ? commonPart.Id : null);
                citation.Add(CommonPartXmlConverter.Quoted(commonPart));
                if (sentence is XElement zdanie)
                    zdanie.Add(citation);
                blocks.Add(citation);
            }
        }

        // Tabela cytowana wprost (wiersze dodawane do tabeli aktu zmienianego) to cytat strukturalny z „tabela".
        private static void AddTables(Amendment amendment, Command command, XNode? sentence, List<XElement> blocks)
        {
            var tables = (amendment.Content?.Tables ?? []).Select(TableXmlConverter.ToXml).OfType<XElement>().ToList();
            if (tables.Count == 0)
                return;

            var citation = Element(XmlConverterConstants.Contents.Citation.StructElement);
            ApplyAttributes(citation, amendment, command, isStructural: true);
            citation.Add(tables);
            if (sentence is XElement zdanie)
                zdanie.Add(citation);
            blocks.Add(citation);
        }

        private static void AddTextCitation(Amendment amendment, Command command, XNode? sentence, string text, List<XElement> blocks,
            string? address = null)
        {
            // Zdanie albo wprowadzenie do wyliczenia przytoczone w całości wychodzi jako zdanie, nie jako tekst (ADR-0124),
            // z datami nowelizacji — czyli zdania, które je przytacza (ADR-0121).
            var citation = Element(XmlConverterConstants.Contents.Citation.Text.Element);
            if (amendment.Content is { PlainTextIsSentences: true })
            {
                var number = 0;
                foreach (var sentenceText in ParsingFactories.SplitIntoSentences(text))
                    citation.Add(SentenceContext.ToXml(text: sentenceText.Text, number: ++number, dated: amendment));
            }

            if (!citation.HasElements)
                citation.Add(Element(XmlConverterConstants.Contents.Citation.Text.Content, text));

            ApplyAttributes(citation, amendment, command, isStructural: false, address: address);
            if (sentence is XElement zdanie)
                zdanie.Add(citation);
            blocks.Add(citation);
        }

        private static void AddStructuredContentItems(
            Amendment amendment, Command command, IReadOnlyList<BaseEntity> units, XNode? sentence, List<XElement> blocks)
        {
            foreach (var group in units.GroupBy(e => e.GetType()))
            {
                var elements = group
                    .Select(BuildContentElement)
                    .Where(element => element is not null)
                    .Select(element => element!)
                    .ToList();

                // Jednostka cytowana stoi przy jednostce, nie przy miejscu w niej: dodawana W jednostce — pod nią samą, nie
                // pod jej zdaniem („w ust. 8 dodaje się zdanie drugie w brzmieniu: „…: 1) …”", ADR-0055, ADR-0102); po
                // jednostce, której część wspólna się zmienia — po niej („w pkt 24 w części wspólnej kropkę zastępuje się
                // średnikiem i dodaje się pkt 25", ADR-0127).
                var inside = AddsInside(amendment, command, group.First());
                var structure = command.Target?.Structure;
                var citation = Element(XmlConverterConstants.Contents.Citation.StructElement);
                ApplyAttributes(citation, amendment, command, isStructural: true,
                    function: inside ? XmlConverterConstants.Contents.Citation.Quotation.Function.AddInside : null,
                    address: structure is { CommonPart: true } || (inside && structure is { Sentence: not null })
                        ? structure.HostUnit().ToString()
                        : null);
                citation.Add(elements);
                if (sentence is XElement zdanie)
                    zdanie.Add(citation);
                blocks.Add(citation);
            }
        }

        /// <summary>
        /// Czy jednostka jest dodawana W jednostce wskazanej komendą („w art. 13 dodaje się ust. 6" — cel to art. 13),
        /// a nie PO niej („po ust. 5 dodaje się ust. 5a" — cel to ust. 5). Rozstrzyga poziom: cel, który nie sięga
        /// poziomu jednostki dodawanej, jest jednostką nadrzędną (ADR-0055).
        /// </summary>
        private static bool AddsInside(Amendment amendment, Command command, BaseEntity unit)
        {
            if (amendment.OperationType != AmendmentOperationType.Insertion || command.Target?.Structure is not { } target)
                return false;

            if (unit is ISystematizingUnit)
            {
                var path = (target.Systematizing ?? string.Empty).Split(EIdSegment.Separator, StringSplitOptions.RemoveEmptyEntries);
                return path.Length > 0 && target.Article is null && path.All(segment =>
                    SystematizingHierarchy.LevelOfPrefix(segment.Split('_')[0]) is { } level
                    && SystematizingHierarchy.Rank(level) < SystematizingHierarchy.Rank(unit));
            }

            return unit switch
            {
                Article => target.Article is null && !string.IsNullOrEmpty(target.Systematizing),
                Paragraph => target is { Article: not null, Paragraph: null, Point: null, Letter: null, Tiret: null },
                Point => target is { Point: null, Letter: null, Tiret: null } && (target.Article ?? target.Paragraph) is not null,
                Letter => target is { Letter: null, Tiret: null } && (target.Article ?? target.Paragraph ?? target.Point) is not null,
                Tiret => target.Tiret is null && (target.Article ?? target.Paragraph ?? target.Point ?? target.Letter) is not null,
                _ => false,
            };
        }

        private static XElement? BuildContentElement(BaseEntity entity) =>
            entity switch
            {
                Article article => ArticleXmlConverter.ToXml(article),
                Paragraph paragraph => ParagraphXmlConverter.ToXml(paragraph),
                Point point => PointXmlConverter.ToXml(point),
                Letter letter => LetterXmlConverter.ToXml(letter),
                Tiret tiret => TiretXmlConverter.ToXml(tiret),
                // Cytat strukturalny przyjmuje te same jednostki co akt, więc i ich nazwy z poziomu aktu.
                ISystematizingUnit => SystematizingUnitXmlConverter.ToXml(entity, actLevel: true),
                _ => throw new InvalidOperationException($"Unsupported entity type: {entity.GetType().Name}")
            };
    }
}
