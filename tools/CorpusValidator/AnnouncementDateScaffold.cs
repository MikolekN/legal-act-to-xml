using Saga.Core.Services.EntryIntoForce;
using Saga.Model;

namespace CorpusValidator;

/// <summary>
/// RUSZTOWANIE — TYMCZASOWE OBEJŚCIE BRAKU PARSOWANIA DATY OGŁOSZENIA.
///
/// Parser nie odczytuje jej jeszcze z nagłówka aktu, a szczotki RCL w ogóle jej nie niosą (jest
/// nadawana przy publikacji w Dz.U.). Bez podstawienia formuły względne § 45 ZTP zostają
/// nierozstrzygnięte i mierzylibyśmy tylko akty z jawną datą kalendarzową. Data podana z wiersza
/// poleceń jest SYNTETYCZNA — służy pomiarowi, nie twierdzi nic o rzeczywistym ogłoszeniu aktu.
///
/// DO USUNIĘCIA wraz z feature'em ekstrakcji daty ogłoszenia (docs/backlog.md § 3b).
/// </summary>
internal static class AnnouncementDateScaffold
{
	/// <summary>
	/// Ustawia datę ogłoszenia i przelicza wejście w życie od nowa. Ponowny przebieg nadpisuje daty
	/// w całości, ale komunikaty walidacji tylko dopisuje — na encji zostaje nieaktualne
	/// „data nierozstrzygnięta" z pierwszego przebiegu. W zrzucie diagnostycznym to jedna zbędna
	/// linia, świadomie zostawiona w kodzie, który i tak ma zniknąć.
	/// </summary>
	internal static void Reapply(LegalDocument document, DateTime? announcementDate)
	{
		if (announcementDate is null)
			return;

		document.AnnouncementDate = announcementDate;
		new EntryIntoForceService().Apply(document);
	}
}
