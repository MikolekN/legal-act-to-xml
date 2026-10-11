using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Organy aktu w metadanych (<c>Organy</c>): kto akt wydał i kto go podpisał.
    ///
    /// Ustawę uchwala Sejm, a podpisuje Prezydent — dwa organy. Akt wykonawczy i obwieszczenie wydaje organ
    /// wymieniony w strefie tytułowej w dopełniaczu („MINISTRA FINANSÓW"); jego nazwę w mianowniku niesie
    /// formuła podpisu („Minister Finansów: …"), gdy organ podpisuje sam — wtedy to jeden organ wydający
    /// z osobą podpisującą, jak w przykładzie autorów schematu. Organ kolegialny podpisuje jego przewodniczący
    /// („Rady Ministrów" — „Prezes Rady Ministrów"): wydający i podpisujący to dwa organy (ADR-0041).
    /// </summary>
    internal static class AuthoritiesXmlConverter
    {
        /// <summary>Nazwa nieodczytanego organu wydającego — nazwana wprost, bez zmyślonego organu.</summary>
        internal const string Unknown = "UNKNOWN";

        // Wyraz główny nazwy organu w dopełniaczu (strefa tytułowa) i w mianowniku; dalsza część nazwy jest
        // przydawką i się nie odmienia („Ministra Finansów" — „Minister Finansów"). Dłuższe przed krótszymi.
        private static readonly (string Genitive, string Nominative)[] Heads =
        [
            ("Prokuratora Generalnego", "Prokurator Generalny"),
            ("Krajowej Rady", "Krajowa Rada"),
            ("Przewodniczącego", "Przewodniczący"),
            ("Prezydenta", "Prezydent"),
            ("Marszałka", "Marszałek"),
            ("Ministra", "Minister"),
            ("Prezesa", "Prezes"),
            ("Rady", "Rada"),
        ];

        // Wyrazy pisane w nazwie organu małą literą („Minister Spraw Wewnętrznych i Administracji"); „spraw"
        // tylko po „do" („Komitet do spraw Pożytku Publicznego").
        private static readonly HashSet<string> MinorWords = new(StringComparer.Ordinal) { "i", "oraz", "do", "w", "z", "na" };

        private static readonly Regex Word = new(@"[^\W\d_]+", RegexOptions.Compiled);

        // Formy tego samego wyrazu różnią się końcówką: mają wspólny początek, a za nim najwyżej tyle liter
        // („Ministr-a" i „Minist-er", „Przewodnicząc-ego" i „Przewodnicząc-y"; nie „Spraw" i „Sprawiedliwości").
        private const int CommonStem = 3;
        private const int LongestEnding = 3;

        internal static XElement ToXml(LegalDocument document)
        {
            var authorities = Element(XmlConverterConstants.Metadata.Authorities.Element);
            var signatures = document.Signature is { } signature
                ? new List<ActSignature> { signature }.Concat(document.CoSignatures).ToList()
                : [];

            // Organ wydający wynika z rodzaju aktu (ustawa — Sejm); podpisuje ją inny organ.
            if (ActKindXml.IssuingAuthority(document) is { } byKind)
            {
                authorities.Add(Issuing(byKind.Name, byKind.EliUri));
                authorities.Add(signatures.Select(SignatureXmlConverter.ToAuthority));
                return authorities;
            }

            // Organ ze strefy tytułowej podpisuje sam: jego nazwę w mianowniku niesie formuła podpisu. Akt wydany
            // wspólnie ma kilku takich podpisujących; kontrasygnata (Prezes Rady Ministrów pod aktem Prezydenta)
            // wydaniem nie jest.
            var issuer = FootnoteMarkers.Remove(document.IssuerLine).CollapseWhitespace().Trim();
            var issuing = signatures
                .Where(signed => IsTheSameAuthority(issuer, signed.Authority) || IsCoIssuer(issuer, signed.Authority))
                .ToHashSet();

            if (issuing.Count == 0)
                authorities.Add(Issuing(issuer.Length == 0 ? Unknown : Nominative(issuer), null));

            authorities.Add(signatures.Select(signed => SignatureXmlConverter.ToAuthority(signed, issuing.Contains(signed))));
            return authorities;
        }

        private static XElement Issuing(string name, string? eliUri)
        {
            var authority = Element(XmlConverterConstants.Metadata.Authorities.Authority.Element,
                new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.GUID, Guid.NewGuid().ToString()),
                new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.Role,
                    XmlConverterConstants.Metadata.Authorities.Authority.RoleIssuing));
            if (eliUri is not null)
                authority.Add(new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.URI, eliUri));
            authority.Add(Element(XmlConverterConstants.Metadata.Authorities.Authority.Name, name));
            return authority;
        }

        /// <summary>
        /// Nazwa organu ze strefy tytułowej w mianowniku: odmienia się tylko wyraz główny („RADY MINISTRÓW" —
        /// „Rada Ministrów"), a wielkość liter jest wielkością nazwy, nie zapisu strefy tytułowej (wersaliki).
        /// Nazwa o nieznanym wyrazie głównym zostaje w przypadku, w którym ją zapisano — odmiana zgadywana
        /// byłaby nazwą zmyśloną.
        /// </summary>
        internal static string Nominative(string issuer)
        {
            var name = InNameCase(issuer);
            foreach (var (genitive, nominative) in Heads)
            {
                if (name.StartsWith(genitive, StringComparison.Ordinal)
                    && (name.Length == genitive.Length || !char.IsLetter(name[genitive.Length])))
                    return nominative + name[genitive.Length..];
            }

            return name;
        }

        private static string InNameCase(string name)
        {
            var previous = string.Empty;
            return Word.Replace(name, match =>
            {
                var word = match.Value.ToLowerInvariant();
                var minor = MinorWords.Contains(word) || (word == "spraw" && previous == "do");
                previous = word;
                return minor ? word : char.ToUpperInvariant(word[0]) + word[1..];
            });
        }

        /// <summary>
        /// Czy organ ze strefy tytułowej (dopełniacz) i organ z formuły podpisu (mianownik) to ten sam organ:
        /// kolejne wyrazy obu nazw są formami tych samych wyrazów, do końca krótszej nazwy („Marszałka Sejmu
        /// Rzeczypospolitej Polskiej" i „Marszałek Sejmu"; nie „Rady Ministrów" i „Prezes Rady Ministrów").
        /// </summary>
        internal static bool IsTheSameAuthority(string issuer, string signer)
        {
            var titled = Words(issuer);
            var signed = Words(signer);
            var shared = Math.Min(titled.Count, signed.Count);
            return shared > 0 && Enumerable.Range(0, shared).All(i => AreForms(titled[i], signed[i]));
        }

        /// <summary>
        /// Czy organ z formuły podpisu jest wymieniony w strefie tytułowej dalej niż na początku — współwydający
        /// aktu wydanego wspólnie („MINISTRÓW INFRASTRUKTURY ORAZ SPRAW WEWNĘTRZNYCH I ADMINISTRACJI"). Wyraz
        /// główny („Minister") stoi tam raz, w liczbie mnogiej, więc szukany jest ciąg wyrazów za nim.
        /// </summary>
        private static bool IsCoIssuer(string issuer, string signer)
        {
            var titled = Words(issuer);
            var rest = Words(signer).Skip(1).ToList();
            if (rest.Count == 0)
                return false;

            return Enumerable.Range(1, Math.Max(0, titled.Count - rest.Count))
                .Any(start => Enumerable.Range(0, rest.Count).All(i => AreForms(titled[start + i], rest[i])));
        }

        private static List<string> Words(string name) =>
            Word.Matches(name).Select(match => match.Value.ToLowerInvariant()).ToList();

        private static bool AreForms(string first, string second)
        {
            var common = 0;
            while (common < first.Length && common < second.Length && first[common] == second[common])
                common++;

            if (common == first.Length && common == second.Length)
                return true;

            return common >= CommonStem
                && first.Length - common <= LongestEnding
                && second.Length - common <= LongestEnding;
        }
    }
}
