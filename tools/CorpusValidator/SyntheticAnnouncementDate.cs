using Saga.Core.Services.EntryIntoForce;
using Saga.Model;

namespace CorpusValidator;

/// <summary>
/// Awaryjna data ogłoszenia dla dokumentów, które JEJ NIE NIOSĄ.
///
/// Parser odczytuje datę ogłoszenia z nagłówka publikatora (JournalHeaderParser), ale szczotki RCL
/// takiego nagłówka nie mają — data powstaje dopiero przy publikacji w Dz.U., już po ich wytworzeniu.
/// Bez punktu odniesienia formuły względne § 45 ZTP zostają nierozstrzygnięte i narzędzie mierzyłoby
/// wyłącznie akty z jawną datą kalendarzową.
///
/// Data z wiersza poleceń jest SYNTETYCZNA — służy pomiarowi rozsyłania dat po drzewie i nie
/// twierdzi nic o rzeczywistym ogłoszeniu aktu. Dlatego NIE nadpisuje daty odczytanej z dokumentu:
/// prawdziwy nagłówek zawsze wygrywa.
/// </summary>
internal static class SyntheticAnnouncementDate
{
	/// <summary>
	/// Podstawia datę i przelicza wejście w życie od nowa, o ile dokument nie przyniósł własnej.
	/// Ponowny przebieg nadpisuje daty w całości, ale komunikaty walidacji tylko dopisuje — na encji
	/// zostaje wtedy nieaktualne „data nierozstrzygnięta" z pierwszego przebiegu (jedna zbędna linia
	/// w zrzucie diagnostycznym, świadomie tolerowana w narzędziu).
	/// </summary>
	internal static void ApplyIfMissing(LegalDocument document, DateTime? announcementDate)
	{
		if (announcementDate is null || document.AnnouncementDate is not null)
			return;

		document.AnnouncementDate = announcementDate;
		new EntryIntoForceService().Apply(document);
	}
}
