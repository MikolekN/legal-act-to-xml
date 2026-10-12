using System.Xml.Linq;

namespace Saga.Core.Services.Converters
{
    /// <summary>Stałe nazwy elementów i atrybutów XML używane w konwerterach.</summary>
    internal static class XmlConverterConstants
    {
        internal const string RootElement = "akt-prawny";
        internal const string Namespace = "https://projects.task.gda.pl/caise/NLP/kisi/rcl/schemaPL/1.0";
        internal const string AmendmentNamespace = "https://projects.task.gda.pl/caise/NLP/kisi/rcl/schemaPL/now";

        /// <summary>Przestrzeń atrybutów GUID schematu (GUIDs.xsd) — prefiks „gd".</summary>
        internal static readonly XNamespace GuidNamespace = "https://projects.task.gda.pl/caise/NLP/kisi/rcl/GUIDs/1.0";
        internal const string MathMLNamespace = "http://www.w3.org/1998/Math/MathML";
        internal const string ELIUrl = "https://eli.gov.pl/";
        internal const string Act = "ustawa";

        /// <summary>Ustawa będąca kodeksem („Kodeks karny") — w schemacie osobny element z artykułami o paragrafach.</summary>
        internal const string Code = "kodeks";

        /// <summary>Akty oparte na paragrafach (§ 124 ZTP): korzeń według rodzaju, treść typu rozporządzenia.</summary>
        internal static class SectionBasedActs
        {
            internal const string Regulation = "rozporzadzenie";
            internal const string Resolution = "uchwala";
            internal const string Order = "zarzadzenie";

            /// <summary>Akt ogłaszany w obwieszczeniu, którego rodzaju nagłówek nie nazywa.</summary>
            internal const string OtherAct = "inny-akt-prawny";
            internal const string LegalBasis = "podstawa-prawna";
            internal const string Section = "paragraf";
        }

        /// <summary>Obwieszczenie ogłaszające akt w załączniku (tekst jednolity) — korzeń zamiast aktu.</summary>
        internal static class Announcements
        {
            internal const string Element = "obwieszczenie";
            internal const string Author = "autor-aktu";
            internal const string Point = "punkt-OTJ";
            internal const string SubPoint = "podpunkt-OTJ";
            internal const string SubPointCommonPart = "czesc-wspolna-podpunkt-OTJ";
        }

        /// <summary>Strefa tytułowa aktu: rodzaj, data i przedmiot — zaraz po metadanych.</summary>
        internal static class ActHeading
        {
            internal const string Kind = "rodzaj-aktu";
            internal const string Date = "data";
            internal const string Subject = "tytul";
        }
        internal const string ActEnclavesAttribute = "enclaves";

        /// <summary>Preambuła ustawy — między tytułem a pierwszą jednostką.</summary>
        internal const string Preamble = "preambula";

        /// <summary>Tabele (grupa-tabeli: tytul-tabeli? + tabela).</summary>
        internal static class Table
        {
            internal const string TitleElement = "tytul-tabeli";
            internal const string Element = "tabela";
            internal const string Body = "tbody";

            /// <summary>Legenda/objaśnienie drukowane pod tabelą.</summary>
            internal const string Description = "opis-tabeli";
            internal const string Row = "tr";
            internal const string Cell = "td";
            internal const string StyleAttribute = "style";
            internal const string WidthAttribute = "width";
            internal const string HeaderAttribute = "naglowek";
            internal const string HeaderValue = "tak";
            internal const string ColspanAttribute = "colspan";
            internal const string RowspanAttribute = "rowspan";

            // Typowana treść komórki (mapowana ze stylu akapitu komórki)
            internal const string ColumnTitle = "tytul-kolumny";
            internal const string CenteredText = "tekst-centr";
            internal const string Number = "liczba";
            internal const string Enumeration1 = "wyliczenie-1";
            internal const string Enumeration2 = "wyliczenie-2";
            internal const string Enumeration3 = "wyliczenie-3";
            internal const string Enumeration4 = "wyliczenie-4";
            internal const string PlainText = "tekst-zwykly";
            internal const string Text1 = "tekst-1";
            internal const string Text2 = "tekst-2";
            internal const string Text3 = "tekst-3";
        }

        /// <summary>Grafika rastrowa (odwołanie/URI danych + metadane).</summary>
        internal static class Graphic
        {
            internal const string Element = "grafika";
            internal const string HrefAttribute = "href";
            internal const string WidthAttribute = "width";
            internal const string HeightAttribute = "height";
            internal const string AltAttribute = "alt";
        }

        /// <summary>Wzór matematyczny (wzor-mat: mml:math + opisy symboli).</summary>
        internal static class Formula
        {
            internal const string Element = "wzor-mat";
            internal const string Description = "opis";
            internal const string SymbolText = "symbol-txt";
            internal const string SymbolMath = "symbol-mat";
            internal const string Explanation = "objasnienie";
            internal const string GraphicAttribute = "grafika";
            internal const string DigestAttribute = "skrot";
        }

        /// <summary>Załączniki (grupa zalaczniki).</summary>
        internal static class Attachments
        {
            internal const string Element = "zalacznik";
            internal const string AttachedTo = "zalacznik-do";
            internal const string NumberElement = "zalacznik-nr";
            internal const string TitleElement = "tytul-zalacznika";
            internal const string Segment = "segment-zalacznika";
            internal const string OrientationAttribute = "orientacja";
            internal const string OrientationPortrait = "pionowa";
            internal const string OrientationLandscape = "pozioma";
        }

        /// <summary>Stałe odnoszące się do metadanych aktu.</summary>
        internal static class Metadata
        {
            internal const string Element = "metadane";
            internal static class Eli
            {
                internal const string Element = "ELIaktu";
                internal const string ELI = "ELI";
                internal const string ELIDL = "ELIDL";
                internal const string ELIDLrezultatProjektu = "ELIDLrezultatProjektu";
                internal const string ELIDLdotyczyAktu = "ELIDLdotyczyAktu";
                internal const string ELIDLproces = "ELIDLproces";
            }
            internal static readonly XName VersionGUID = GuidNamespace + "GUIDwersji";
            internal const string ISAPAdressElement = "AdresISAP";
            internal const string PublisherElement = "Wydawnictwo";
            internal const string YearElement = "Rok";
            internal const string VolumeElement = "NrDziennika";
            internal const string PositionElement = "Pozycja";
            internal const string DisplayAddressElement = "AdresWyswietlany";
            internal const string TitleElement = "Tytul";
            internal const string TypeElement = "Rodzaj";
            internal const string PreviousTitlesElement = "TytulyPoprzednie";
            internal const string AnnouncementDateElement = "DataOgloszenia";
            internal static class Date
            {
                internal const string Element = "DataAktu";
                internal const string Project = "DataProjektu";
                internal const string Publication = "DataWydania";
            }
            /// <summary>Dane czasowe aktu: cykl życia jako zdarzenia (ADR-0022).</summary>
            internal static class Temporal
            {
                internal const string Element = "DaneCzasowe";
                internal const string ActLifecycle = "CyklZyciaAktu";
                internal const string Event = "Zdarzenie";
                internal const string Id = "id";
                internal const string Kind = "rodzaj";
                internal const string KindEntryIntoForce = "wejscie-w-zycie";
                internal const string KindRetroactivity = "stosowanie-z-moca-wsteczna";
                internal const string KindConsolidation = "ujednolicenie";
                internal const string Origin = "pochodzenie";
                internal const string OriginComputed = "domyslne-z-aktu";
                internal const string OriginRead = "odczytane";
                internal const string Date = "data";
                internal const string Condition = "warunek";
                internal const string Basis = "podstawa";
                internal const string Source = "zrodlo";
                internal const string Evidence = "dowod";
                internal const string Unit = "JednostkaAktu";
                internal const string UnitInScope = "JednostkaWZakresie";
                internal const string SubjectScope = "ZakresMerytorycznyZdarzenia";
                internal const string SubjectScopeText = "ZakresMerytoryczny";
                internal const string Group = "GrupaCzasowa";
                internal const string GroupIsMain = "glowna";
                internal const string Interval = "Przedzial";
                internal const string IntervalInForce = "obowiazywanie";
                internal const string IntervalFrom = "od";
                internal const string IntervalTo = "do";
                internal const string Period = "okres";
            }
            internal const string EntryIntoForcePartialsElement = "DatyWejsciaWZycieCzesciowe";
            internal const string EntryIntoForcePartialElement = "DataWejsciaWZycieCzesciowa";
            internal static class PartialDate
            {
                internal const string Unit = "Jednostka";
                internal const string Date = "Data";
                internal const string UnitInScope = "Jednostka-w-zakresie";
                internal const string EditorialNotes = "Uwagi-redakcyjne";
            }
            internal const string RepealDateElement = "DataUchylenia";
            internal const string RepealDatePartialElement = "DataUchyleniaCzesciowa";
            internal const string ExpirationDateElement = "DataWygasniecia";
            internal const string LegalStatusDateElement = "DataStanuPrawnegoTJ";
            internal const string ChangeDateElement = "DataOstatniejZmiany";
            internal const string StatusElement = "Status";
            internal const string InForceElement = "CzyObowiazuje";
            internal static class Authorities
            {
                internal const string Element = "Organy";
                internal static class Authority
                {
                    internal const string Element = "Organ";
                    internal const string Name = "Nazwa";
                    internal static readonly XName GUID = GuidNamespace + "GUID";
                    internal const string Role = "rola";
                    internal const string URI = "eliURI";
                    internal const string SigningAuthority = "OrganPodpisujacy";
                    internal const string SigningPerson = "OsobaPodpisujaca";
                    internal const string RoleIssuing = "wydajacy";
                    internal const string RoleSigning = "podpisujacy";
                }
            }

            internal const string KeywordsElement = "SlowaKluczowe";
            internal const string ProperNamesElement = "NazwyWlasne";
            internal const string RelationsElement = "Relacje";
            internal const string DirectivesElement = "DyrektywyUE";
            internal const string TextsElement = "TekstyAktu";
            internal const string PrintsElement = "DrukiSejmowe";
            internal const string CommentsElement = "Komentarze";
        }

        // RODZAJ AKTU
        internal static class ActType
        {
            internal const string Element = "rodzaj-aktu";
        }

        // DATA
        internal static class Date
        {
            internal const string Element = "data";
        }

        // TYTUŁ
        internal static class Title
        {
            internal const string Element = "tytul";
        }

        // PODPISY (grupa podpisy — elementy bez wspólnego rodzica)
        internal static class Signatures
        {
            internal const string Element = "podpisy";
            internal const string IssuingAuthority = "organ-wydajacy";
            internal const string SigningAuthority = "organ-podpisujacy";
            internal const string Person = "osoba";
            internal const string GivenName = "imie";
            internal const string Surname = "nazwisko";
            internal const string Signing = "podpisujacy";
            internal static readonly XName PersonGUID = GuidNamespace + "osobaGUID";
            internal const string TextInAgreement = "tekst-w-porozumieniu";
            internal const string AuthorityInAgreement = "organ-w-porozumieniu";
        }

        /// <summary>Stałe odnoszące się do treści aktu.</summary>
        internal static class Contents
        {
            internal static class Part
            {
                internal const string Element = "czesc";
                internal const string ElementImplicit = "czesc-n";
                internal const string Header = "naglowek-czesci";
            }
            internal static class Book
            {
                internal const string Element = "ksiega";
                internal const string ElementImplicit = "ksiega-n";
                internal const string Header = "naglowek-ksiegi";
            }
            internal static class Title
            {
                internal const string Element = "tytul";
                internal const string ElementImplicit = "tytul-n";
                internal const string ActLevelElement = "tytul-jednostka";
                internal const string ActLevelElementImplicit = "tytul-n-jednostka";
                internal const string Header = "naglowek-tytulu";
            }
            internal static class Division
            {
                internal const string Element = "dzial";
                internal const string ElementImplicit = "dzial-n";
                internal const string Header = "naglowek-dzialu";
            }
            internal static class Chapter
            {
                internal const string Element = "rozdzial";
                internal const string ElementImplicit = "rozdzial-n";
                internal const string Header = "naglowek-rozdzialu";
            }
            internal static class Subchapter
            {
                internal const string Element = "oddzial";
                internal const string ElementImplicit = "oddzial-n";
                internal const string Header = "naglowek-oddzialu";
            }
            internal const string Article = "artykul";
            internal const string CodeArticle = "artykul-kodeksu";
            internal const string NonZtpArticle = "nie-ztp-artykul";
            internal const string Paragraph = "ustep";
            internal const string NonZtpParagraph = "nie-ztp-ustep";
            internal const string ParagraphImplicit = "ustep-n";
            internal const string NonZtpParagraphImplicit = "nie-ztp-ustep-n";
            internal const string CodeParagraph = "paragraf-kodeksu";
            internal const string NonZtpCodeParagraph = "nie-ztp-paragraf-kodeksu";
            internal const string CodeParagraphImplicit = "paragraf-kodeksu-n";
            internal const string NonZtpCodeParagraphImplicit = "nie-ztp-paragraf-kodeksu-n";
            internal const string Point = "punkt";
            internal const string NonZtpPoint = "nie-ztp-punkt";
            internal const string Letter = "litera";
            internal const string NonZtpLetter = "nie-ztp-litera";
            internal const string Tiret = "tiret";
            internal const string NonZtpTiret = "nie-ztp-tiret";
            internal const string DoubleTiret = "podw-tiret";
            internal const string NonZtpDoubleTiret = "nie-ztp-podw-tiret";
            internal const string TripleTiret = "potr-tiret";
            internal const string QuadrupleTiret = "poczw-tiret";
            internal const string QuintupleTiret = "piec-tiret";
            internal static class Sentence
            {
                internal const string Element = "zdanie";
                internal const string ReferenceElement = "zdanie-ref";
                internal const string Number = "nr";
                internal const string ReferenceNumber = "ref-nr";
                internal static readonly XName GUID = GuidNamespace + "zdGUID";
                internal const string ReferenceGUID = "ref-zdGUID";
                internal const string Fragment = "fragment";
                internal static readonly XName FragmentGUID = GuidNamespace + "fragmentGUID";
            }
            internal static class CommonPart
            {
                internal const string Point = "czesc-wspolna-punkt";
                internal const string Letter = "czesc-wspolna-litera";
                internal const string InterruptingPoint = "nie-ztp-czesc-wspolna-punkt";
                internal const string InterruptingLetter = "nie-ztp-czesc-wspolna-litera";
                internal const string Tiret = "czesc-wspolna-tiret";
                internal const string DoubleTiret = "czesc-wspolna-podw-tiret";
                internal const string TripleTiret = "czesc-wspolna-potr-tiret";
                internal const string QuadrupleTiret = "czesc-wspolna-poczw-tiret";
                internal const string QuintupleTiret = "czesc-wspolna-piec-tiret";
            }
            internal static class Citation
            {
                internal static class Text
                {
                    internal const string Element = "cytat-w-tekscie";

                    internal const string Content = "tekst";
                }

                internal const string StructElement = "cytat-strukt";

                /// <summary>Przytoczenie przepisów innego aktu jako jednostki — bez instrukcji nowelizującej.</summary>
                internal const string QuotedStructElement = "tresc-strukt";
                internal const string EmptyStructElement = "cytat-strukt-pusty";
                internal const string BlocElement = "cytat-blok";
                internal static class Quotation
                {
                    internal const string QuotationAttribute = "cudzyslowy";
                    internal const string QuotationNone = "brak";
                    internal const string QuotationOpening = "otwierajacy";
                    internal const string QuotationClosing = "zamykajacy";
                    internal const string QuotationBoth = "oba";
                    internal const string ActAttribute = "akt";
                    internal static readonly XName GUIDAttribute = GuidNamespace + "nowGUID";
                    internal const string RefGUIDAttribute = "ref-nowGUID";
                    internal const string ElementAttribute = "element-eId";
                    internal const string ElementDoAttribute = "element-do-eId";
                    internal const string FunctionAttribute = "funkcja";
                    internal static class Function
                    {
                        internal const string AddInside = "dodaj-w";
                        internal const string AddAfter = "dodaj-po";
                        internal const string Edit = "zastap";
                        internal const string AddAfterText = "dodaj-po-tekscie";
                        internal const string EditText = "zastap-tekst";
                        internal const string NewText = "nowy-tekst";
                        internal const string RepealText = "uchyl-tekst";
                        internal const string AddManually = "dodaj-recznie";
                        internal const string EditManually = "zastap-recznie";
                        internal const string RepealManually = "uchyl-recznie";
                    }
                    internal const string SentenceAttribute = "zdanie";
                    internal const string AppearanceAttribute = "wystapienie";
                }
            }
            internal static class Instruction
            {
                internal const string Repeal = "uchyl";
                internal const string RepealAct = "uchyl-akt";
                internal const string WrapInParagraph = "dodaj-ustep";
                internal const string WrapInSection = "dodaj-paragraf";
                internal const string FlattenParagraph = "usun-ustep";
                internal const string ReplaceUnits = "zastap";
                internal const string ReplaceNumber = "zastap-numer";
                internal const string ReplaceText = "zamien-tekst";
                internal const string OldTextAttribute = "stary-tekst";
                internal const string NewTextAttribute = "nowy-tekst";
                internal const string LastOccurrence = "ostatnie";
            }

            internal static class Odnosnik
            {
                internal const string Element = "odnosnik";
                internal const string ReferenceElement = "odsylacz-do-odnosnika";
                internal const string IdAttribute = "id-odnosnika";
                internal const string SymbolAttribute = "symbol";
            }

            internal const string Num = "nr";
            internal const string Label = "label";
            internal const string TextSection = "p";
            internal const string ContinuationFragment = "fragment";

            /// <summary>Wzór matematyczny w osobnym akapicie.</summary>
            internal static class Formula
            {
                internal const string Element = "wzor-mat";
                internal const string Expression = "wzor";
                internal const string MathMlPrefix = "mml";
                internal const string LegendIntroduction = "wprowadzenie-do-opisu";
                internal const string Description = "opis";
                internal const string SymbolAsMath = "symbol-mat";
                internal const string SymbolAsText = "symbol-txt";
                internal const string Explanation = "objasnienie";
            }
            internal const string Superscript = "sup";
            internal const string eId = "eId";
            internal static readonly XName GUID = GuidNamespace + "GUID";
            internal const string MissingEId = "brak-eid";
            internal const string UnmarkedUnit = "jednostka-nieoznaczona";
            internal const string RepeatedCommonPart = "nie-ztp-wielokrotna-czesc-wspolna";

            internal const string EntryIntoForce = "wejscie-w-zycie";
            internal const string UnitStatus = "status-jednostki";
            internal const string Opinions = "opinie";
            internal const string Formatting = "akapit";
            internal const string MarginLeft = "margin-left";
            internal const string MarginRight = "margin-right";
        }
    }
}
