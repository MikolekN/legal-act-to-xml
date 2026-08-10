namespace Saga.Core.Services.Converters
{
    /// <summary>Stałe nazwy elementów i atrybutów XML używane w konwerterach.</summary>
    internal static class XmlConverterConstants
    {
        internal const string RootElement = "akt-prawny";
        internal const string Namespace = "https://projects.task.gda.pl/caise/NLP/kisi/rcl/schemaPL/1.0";
        internal const string AmendmentNamespace = "https://projects.task.gda.pl/caise/NLP/kisi/rcl/schemaPL/now";
        internal const string MathMLNamespace = "http://www.w3.org/1998/Math/MathML";
        internal const string ELIUrl = "https://eli.gov.pl/";
        internal const string Act = "ustawa";
        internal const string ActEnclavesAttribute = "enclaves";

        /// <summary>Tabele (grupa-tabeli: tytul-tabeli? + tabela).</summary>
        internal static class Table
        {
            internal const string TitleElement = "tytul-tabeli";
            internal const string Element = "tabela";
            internal const string Body = "tbody";
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
            internal const string VersionGUID = "GUIDwersji";
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
            internal const string EntryIntoForceElement = "DataWejsciaWZycie";
            internal const string EntryIntoForcePartialsElement = "DatyWejsciaWZycieCzesciowe";
            internal const string EntryIntoForcePartialElement = "DataWejsciaWZycieCzesciowa";
            internal static class PartialDate
            {
                internal const string Unit = "Jednostka";
                internal const string Date = "Data";
                internal const string UnitInScope = "Jednostka-w-zakresie";
                internal const string EditorialNotes = "Uwagi-redakcyjne";
            }
            internal const string ValidFromElement = "DataObowiazywaniaOd";
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
                    internal const string GUID = "GUID";
                    internal const string Role = "rola";
                    internal const string URI = "eliURI";
                    internal const string SigningAuthority = "OrganPodpisujacy";
                    internal const string SigningPerson = "OsobaPodpisujaca";
                    internal const string RoleIssuing = "wydający";
                    internal const string RoleSigning = "podpisujący";
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
            internal const string SigningAuthority = "organ-podpisujący";
            internal const string TextInAgreement = "tekst-w-porozumieniu";
            internal const string AuthorityInAgreement = "organ-w-porozumieniu";
        }

        /// <summary>Stałe odnoszące się do treści aktu.</summary>
        internal static class Contents {
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
            internal const string PatoArticle = "pato-artykul";
            internal const string Paragraph = "ustep";
            internal const string PatoParagraph = "pato-ustep";
            internal const string ParagraphImplicit = "ustep-n";
            internal const string PatoParagraphImplicit = "pato-ustep-n";
            internal const string CodeParagraph = "paragraf-kodeksu";
            internal const string PatoCodeParagraph = "pato-paragraf-kodeksu";
            internal const string CodeParagraphImplicit = "paragraf-kodeksu-n";
            internal const string PatoCodeParagraphImplicit = "pato-paragraf-kodeksu-n";
            internal const string Point = "punkt";
            internal const string PatoPoint = "pato-punkt";
            internal const string Letter = "litera";
            internal const string PatoLetter = "pato-litera";
            internal const string Tiret = "tiret";
            internal const string PatoTiret = "pato-tiret";
            internal const string DoubleTiret = "podw-tiret";
            internal const string PatoDoubleTiret = "pato-podw-tiret";
            internal const string TripleTiret = "potr-tiret";
            internal const string QuadrupleTiret = "poczw-tiret";
            internal const string QuintupleTiret = "piec-tiret";
            internal static class Sentence
            {
                internal const string Element = "zdanie";
                internal const string ReferenceElement = "zdanie-ref";
                internal const string Number = "nr";
                internal const string ReferenceNumber = "ref-nr";
                internal const string GUID = "zdGUID";
                internal const string ReferenceGUID = "ref-zdGUID";
                internal const string Fragment = "fragment";
                internal const string FragmentGUID = "fragmentGUID";
            }
            internal static class CommonPart
            {
                internal const string Point = "czesc-wspolna-punkt";
                internal const string Letter = "czesc-wspolna-litera";
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
                internal const string EmptyStructElement = "cytat-strukt-pusty";
                internal const string BlocElement = "cytat-blok";
                internal static class Quotation
                {
                    internal const string QuotationAttribute = "cudzyslowy";
                    internal const string ActAttribute = "akt";
                    internal const string GUIDAttribute = "nowGUID";
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
            internal const string eId = "eId";
            internal const string GUID = "GUID";

            internal const string EntryIntoForce = "wejscie-w-zycie";
            internal const string UnitStatus = "status-jednostki";
            internal const string Opinions = "opinie";
            internal const string Formatting = "akapit";
            internal const string MarginLeft = "margin-left";
            internal const string MarginRight = "margin-right";
        }
    }
}
