using System;
using System.Collections.Generic;
using Saga.Model.EditorialUnits;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Obwieszczenie: jego własna treść — punkty, przytoczone przepisy, podpis. Obwieszczenie w sprawie
    /// ogłoszenia tekstu jednolitego stoi przed aktem ogłaszanym w załączniku i nie należy do jego struktury;
    /// obwieszczenie, które aktu nie ogłasza (stawki, wskaźniki, wolne stanowiska), jest całym dokumentem.
    /// Nagłówek obwieszczenia (rodzaj, organ, data, przedmiot) niesie <see cref="LegalDocument"/>;
    /// strefę tytułową ogłaszanego aktu — to obwieszczenie.
    /// </summary>
    public class Announcement
    {
        /// <summary>Skrót punktu obwieszczenia w eId (<c>pkt_1</c>).</summary>
        public const string PointEIdPrefix = "pkt";

        /// <summary>Skrót podpunktu obwieszczenia w eId (<c>pkt_1__ppkt_2</c>) — ADR-0032.</summary>
        public const string SubPointEIdPrefix = "ppkt";

        /// <summary>
        /// Czy obwieszczenie ogłasza tekst jednolity („w sprawie ogłoszenia jednolitego tekstu …"), a nie akt
        /// innego rodzaju z załącznikiem. Tekst jednolity nie wchodzi w życie — jest ujednoliceniem.
        /// </summary>
        public bool IsConsolidatedText { get; set; }

        /// <summary>
        /// Czy obwieszczenie ogłasza akt w załączniku. Bez niego załączniki dokumentu są załącznikami samego
        /// obwieszczenia („Załącznik do obwieszczenia Ministra … — wykaz jednostek"), a nie aktem.
        /// </summary>
        public bool AnnouncesAct { get; set; } = true;

        /// <summary>Wiersz otwierający ogłaszany akt („Załącznik do obwieszczenia … (Dz. U. poz. 1094)").</summary>
        public string AttachedTo { get; set; } = string.Empty;

        /// <summary>Wiersz rodzaju ogłaszanego aktu („USTAWA"); pusty, gdy nierozpoznany.</summary>
        public string ActKindLine { get; set; } = string.Empty;

        /// <summary>Wiersz organu wydającego ogłaszany akt („MINISTRA FINANSÓW"); pusty dla ustawy.</summary>
        public string ActIssuerLine { get; set; } = string.Empty;

        /// <summary>Wiersz daty ogłaszanego aktu („z dnia 11 sierpnia 2001 r."); pusty, gdy nierozpoznany.</summary>
        public string ActDateLine { get; set; } = string.Empty;

        /// <summary>Przedmiot ogłaszanego aktu („o odbudowie obiektów budowlanych"); pusty, gdy nierozpoznany.</summary>
        public string ActSubject { get; set; } = string.Empty;

        /// <summary>
        /// Dzień stanu prawnego tekstu jednolitego podany w obwieszczeniu („z uwzględnieniem stanu prawnego na dzień
        /// 25 sierpnia 2026 r." — § 100a i § 104 ZTP); null, gdy obwieszczenie go nie podaje.
        /// </summary>
        public DateTime? LegalStateDate { get; set; }

        /// <summary>
        /// Punkty obwieszczenia („1. Na podstawie …") z podpunktami („1) ustawą …") i częścią wspólną podpunktów.
        /// Przytoczone przepisy i tabele stoją w dalszej treści jednostki, za którą się znajdują.
        /// </summary>
        public List<Paragraph> Points { get; set; } = new();

        /// <summary>
        /// Treść obwieszczenia bez numerowanych punktów, poza podstawą prawną: jeden przepis bez oznaczenia —
        /// tekst, wyliczenie (punkty, litery, tirety), część wspólna i dalsza treść (akapity, tabele).
        /// Null, gdy obwieszczenie ma punkty albo kończy się na podstawie prawnej.
        /// </summary>
        public Paragraph? Body { get; set; }

        /// <summary>Treść sprzed pierwszego punktu, poza nagłówkiem: akapity i tabele w kolejności dokumentu.</summary>
        public List<ContentBlock> Content { get; set; } = new();
    }
}
