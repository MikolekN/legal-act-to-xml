using System.Collections.Generic;
using Saga.Model.EditorialUnits;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Obwieszczenie w sprawie ogłoszenia tekstu jednolitego: to, co stoi przed aktem ogłaszanym w załączniku —
    /// punkty obwieszczenia, przytoczone przepisy, podpis. Nie należy do struktury ogłaszanego aktu.
    /// Nagłówek obwieszczenia (rodzaj, organ, data, przedmiot) niesie <see cref="LegalDocument"/>;
    /// strefę tytułową ogłaszanego aktu — to obwieszczenie.
    /// </summary>
    public class Announcement
    {
        /// <summary>Skrót punktu obwieszczenia w eId (<c>pkt_1</c>).</summary>
        public const string PointEIdPrefix = "pkt";

        /// <summary>Skrót podpunktu obwieszczenia w eId (<c>pkt_1__ppkt_2</c>) — ADR-0032.</summary>
        public const string SubPointEIdPrefix = "ppkt";

        /// <summary>Wiersz otwierający ogłaszany akt („Załącznik do obwieszczenia … (Dz. U. poz. 1094)").</summary>
        public string AttachedTo { get; set; } = string.Empty;

        /// <summary>Wiersz rodzaju ogłaszanego aktu („USTAWA"); pusty, gdy nierozpoznany.</summary>
        public string ActKindLine { get; set; } = string.Empty;

        /// <summary>Wiersz daty ogłaszanego aktu („z dnia 11 sierpnia 2001 r."); pusty, gdy nierozpoznany.</summary>
        public string ActDateLine { get; set; } = string.Empty;

        /// <summary>Przedmiot ogłaszanego aktu („o odbudowie obiektów budowlanych"); pusty, gdy nierozpoznany.</summary>
        public string ActSubject { get; set; } = string.Empty;

        /// <summary>
        /// Punkty obwieszczenia („1. Na podstawie …") z podpunktami („1) ustawą …") i częścią wspólną podpunktów.
        /// Przytoczone przepisy i tabele stoją w dalszej treści jednostki, za którą się znajdują.
        /// </summary>
        public List<Paragraph> Points { get; set; } = new();

        /// <summary>Treść sprzed pierwszego punktu, poza nagłówkiem: akapity i tabele w kolejności dokumentu.</summary>
        public List<ContentBlock> Content { get; set; } = new();
    }
}
