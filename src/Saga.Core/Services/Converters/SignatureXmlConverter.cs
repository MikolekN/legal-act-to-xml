using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Podpis aktu: formuła za ostatnim przepisem (<c>organ-podpisujacy</c> z osobą) i organ podpisujący
    /// w metadanych. Organ i osoba wychodzą tak, jak stoją w akcie — także „wz." i „z up." przed osobą.
    /// </summary>
    internal static class SignatureXmlConverter
    {
        /// <summary>„Prezydent Rzeczypospolitej Polskiej: A. Duda" jako tekst z wyróżnioną osobą.</summary>
        internal static XElement ToXml(ActSignature signature) =>
            Element(XmlConverterConstants.Signatures.SigningAuthority,
                signature.Authority + ": " + (signature.Capacity is { Length: > 0 } capacity ? capacity + " " : string.Empty),
                Person(XmlConverterConstants.Signatures.Person, signature,
                    new XAttribute(XmlConverterConstants.Signatures.Signing, true),
                    new XAttribute(XmlConverterConstants.Signatures.PersonGUID, signature.PersonGuid.ToString())));

        /// <summary>Organ podpisujący w <c>Organy</c>.</summary>
        internal static XElement ToAuthority(ActSignature signature)
        {
            var authority = Element(XmlConverterConstants.Metadata.Authorities.Authority.Element,
                new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.GUID, Guid.NewGuid().ToString()),
                new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.Role,
                    XmlConverterConstants.Metadata.Authorities.Authority.RoleSigning));

            if (ActKindXml.EliUriOf(signature.Authority) is { } uri)
                authority.Add(new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.URI, uri));

            authority.Add(
                Element(XmlConverterConstants.Metadata.Authorities.Authority.Name, signature.Authority),
                Element(XmlConverterConstants.Metadata.Authorities.Authority.SigningAuthority, signature.Authority),
                Person(XmlConverterConstants.Metadata.Authorities.Authority.SigningPerson, signature));
            return authority;
        }

        private static XElement Person(string name, ActSignature signature, params object[] attributes) =>
            Element(name,
                attributes,
                Element(XmlConverterConstants.Signatures.GivenName, signature.GivenName),
                " ",
                Element(XmlConverterConstants.Signatures.Surname, signature.Surname));
    }
}
