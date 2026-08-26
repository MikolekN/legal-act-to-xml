using System;
using Saga.Core;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;

namespace Saga.Core.Tests
{
	/// <summary>
	/// RUSZTOWANIE — TYMCZASOWE OBEJŚCIE BRAKU PARSOWANIA DATY OGŁOSZENIA.
	///
	/// Parser nie odczytuje jej jeszcze z nagłówka aktu, więc po parsowaniu
	/// <see cref="LegalDocument.AnnouncementDate"/> jest zawsze null i formuły względne § 45 ZTP
	/// („po upływie 14 dni od dnia ogłoszenia") nie mają punktu odniesienia. Bez tego szwu nie da
	/// się przetestować drogi: data ogłoszenia → formuła → data wejścia w życie → propagacja.
	///
	/// DO USUNIĘCIA wraz z feature'em ekstrakcji daty ogłoszenia (docs/backlog.md § 3b); wymusi to
	/// <see cref="AnnouncementDateExtractionTests"/>, który wtedy spadnie.
	/// </summary>
	internal static class AnnouncementDateScaffold
	{
		/// <summary>
		/// Zwraca dokument tak, jakby parser odczytał podaną datę ogłoszenia. Ponowny przebieg
		/// nadpisuje daty w całości, ale komunikaty walidacji tylko dopisuje — test asertujący ich
		/// zawartość dla klauzuli musi się liczyć z nieaktualnym „data nierozstrzygnięta".
		/// </summary>
		internal static LegalDocument AsAnnouncedOn(this LegalDocument document, DateTime? announcementDate)
		{
			if (announcementDate is null)
				return document;

			document.AnnouncementDate = announcementDate;
			new EntryIntoForceService().Apply(document);
			return document;
		}
	}
}
