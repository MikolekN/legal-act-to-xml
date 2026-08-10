using System;
using System.Linq;
using System.Xml.Linq;
using Saga.Core.Services.Converters;
using Saga.Model;
using Saga.Model.SystematizingUnits;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Konwertery jednostek systematyzacyjnych nie są dziś osiągalne z
	/// <c>LegalDocumentConverter.Convert</c> (iteruje po <c>document.Articles</c>), więc ich defekty
	/// nie ujawniają się w eksporcie. Test sięga do nich wprost — po podpięciu jednostek
	/// systematyzacyjnych do eksportu taki defekt byłby awarią całej konwersji, nie drobiazgiem.
	/// </summary>
	public class SystematizingUnitXmlConverterTests
	{
		private static readonly DateTime Effective = new(2026, 1, 1);

		private static T Dated<T>(T unit) where T : BaseEntity, ISystematizingUnit
		{
			unit.Number = new EntityNumber { Value = "I", NumericPart = 1 };
			unit.Heading = "Przepisy ogólne";
			unit.IsImplicit = false;
			unit.EffectiveDate = Effective;
			return unit;
		}

		[Fact]
		public void DatedUnit_EmitsExactlyOneEntryIntoForceAttribute()
		{
			// Dopóki data wejścia w życie była zawsze domyślna, podwójne wywołanie UnitLifecycleXml.Apply
			// w konwerterze było niewidocznym no-opem. Odkąd każda jednostka nosi datę, drugie wywołanie
			// dokłada atrybut o tej samej nazwie — a LINQ to XML rzuca wtedy „Duplicate attribute".
			var elements = new (string Label, Func<XElement> Convert)[]
			{
				("czesc",    () => PartXmlConverter.ToXml(Dated(new Part()))),
				("ksiega",   () => BookXmlConverter.ToXml(Dated(new Book()))),
				("tytul",    () => TitleXmlConverter.ToXml(Dated(new Title()))),
				("dzial",    () => DivisionXmlConverter.ToXml(Dated(new Division()))),
				("rozdzial", () => ChapterXmlConverter.ToXml(Dated(new Chapter()))),
				("oddzial",  () => SubchapterXmlConverter.ToXml(Dated(new Subchapter()))),
			};

			foreach (var (label, convert) in elements)
			{
				var element = convert();

				var dates = element.Attributes()
					.Where(a => a.Name.LocalName == XmlConverterConstants.Contents.EntryIntoForce)
					.ToList();

				Assert.True(dates.Count == 1,
					$"{label}: oczekiwano jednego atrybutu wejscie-w-zycie, jest {dates.Count}");
				Assert.Equal("2026-01-01", dates[0].Value);
			}
		}
	}
}
