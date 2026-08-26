using Saga.Core.Services.EntryIntoForce;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Rozwijanie przedziałów oznaczeń. Przedział niejednoznaczny MUSI dać null — wywołujący
	/// oflaguje wtedy cel zamiast zgadywać, które jednostki prawodawca miał na myśli.
	/// </summary>
	public class DesignationRangeTests
	{
		[Fact]
		public void NumericRange()
		{
			Assert.Equal(new[] { "19", "20", "21" }, DesignationRange.Expand("19", "21"));
		}

		[Fact]
		public void SuffixedRange_SharesNumericPart()
		{
			Assert.Equal(
				new[] { "85c", "85d", "85e", "85f", "85g", "85h", "85i", "85j" },
				DesignationRange.Expand("85c", "85j"));
		}

		[Fact]
		public void AlphabeticRange()
		{
			Assert.Equal(new[] { "a", "b", "c" }, DesignationRange.Expand("a", "c"));
		}

		[Fact]
		public void TwoLetterAlphabeticRange()
		{
			Assert.Equal(new[] { "aa", "ab", "ac" }, DesignationRange.Expand("aa", "ac"));
		}

		[Fact]
		public void OrdinalWordRange_IsExpandedNumerically()
		{
			// „tiret czwarte–szóste"
			Assert.Equal(new[] { "4", "5", "6" }, DesignationRange.Expand("czwarte", "szóste"));
		}

		[Fact]
		public void DifferentNumericParts_AreAmbiguous()
		{
			// § 89 ZTP nie definiuje następnika przez dwie różne części liczbowe.
			Assert.Null(DesignationRange.Expand("5a", "7b"));
		}

		[Fact]
		public void ReversedRange_IsRejected()
		{
			Assert.Null(DesignationRange.Expand("21", "19"));
			Assert.Null(DesignationRange.Expand("c", "a"));
		}

		[Fact]
		public void OversizedRange_IsRejected()
		{
			// Zabezpieczenie przed przedziałem z błędu OCR — ma być oflagowany, nie rozwinięty.
			Assert.Null(DesignationRange.Expand("1", "9999"));
		}

		[Fact]
		public void NonLatinLetters_AreRejected()
		{
			Assert.Null(DesignationRange.Expand("ą", "ć"));
		}

		[Fact]
		public void EmptyBounds_AreRejected()
		{
			Assert.Null(DesignationRange.Expand(null, "5"));
			Assert.Null(DesignationRange.Expand("5", ""));
		}
	}
}
