using System;
using System.Collections.Generic;
using Saga.Model.EditorialUnits;

#nullable enable

namespace Saga.Model.SystematizingUnits
{
    /// <summary>
    /// Kształt hierarchii jednostek systematyzacyjnych w jednym miejscu (§ 60 ZTP): kolejność poziomów,
    /// prefiks eId (ADR-0014), tworzenie jednostki i jej dzieci. Oddział trzyma artykuły, pozostałe
    /// jednostki — jednostki poziomu niższego.
    /// </summary>
    public static class SystematizingHierarchy
    {
        /// <summary>Poziomy od najwyższego.</summary>
        public static IReadOnlyList<UnitType> Levels { get; } =
            [UnitType.Part, UnitType.Book, UnitType.Title, UnitType.Division, UnitType.Chapter, UnitType.Subchapter];

        /// <summary>Miejsce poziomu w hierarchii: 0 — część, 5 — oddział.</summary>
        public static int Rank(UnitType level)
        {
            var rank = IndexOf(level);
            return rank >= 0 ? rank : throw new ArgumentException($"Nie jest poziomem jednostki systematyzacyjnej: {level}", nameof(level));
        }

        public static int Rank(BaseEntity unit) => Rank(unit.UnitType);

        public static bool IsSystematizing(UnitType level) => IndexOf(level) >= 0;

        public static string EIdPrefix(UnitType level) => level switch
        {
            UnitType.Part => "czesc",
            UnitType.Book => "ksi",
            UnitType.Title => "tyt",
            UnitType.Division => "dzial",
            UnitType.Chapter => "rozdz",
            UnitType.Subchapter => "oddz",
            _ => throw new ArgumentException($"Nie jest poziomem jednostki systematyzacyjnej: {level}", nameof(level)),
        };

        /// <summary>Poziom z prefiksu segmentu eId („rozdz" → rozdział); null, gdy to nie jednostka systematyzacyjna.</summary>
        public static UnitType? LevelOfPrefix(string prefix)
        {
            foreach (var level in Levels)
                if (EIdPrefix(level) == prefix)
                    return level;
            return null;
        }

        /// <summary>Nowa jednostka poziomu — niejawna, dopóki tekst jej nie oznaczy.</summary>
        public static BaseEntity Create(UnitType level) => level switch
        {
            UnitType.Part => new Part(),
            UnitType.Book => new Book(),
            UnitType.Title => new Title(),
            UnitType.Division => new Division(),
            UnitType.Chapter => new Chapter(),
            UnitType.Subchapter => new Subchapter(),
            _ => throw new ArgumentException($"Nie jest poziomem jednostki systematyzacyjnej: {level}", nameof(level)),
        };

        /// <summary>Dzieci jednostki: jednostki poziomu niższego, a w oddziale — artykuły.</summary>
        public static IReadOnlyList<BaseEntity> Children(BaseEntity unit) => unit switch
        {
            Part part => part.Books,
            Book book => book.Titles,
            Title title => title.Divisions,
            Division division => division.Chapters,
            Chapter chapter => chapter.Subchapters,
            Subchapter subchapter => subchapter.Articles,
            _ => [],
        };

        /// <summary>Dołącza dziecko na koniec jednostki i ustawia mu rodzica.</summary>
        public static void AddChild(BaseEntity parent, BaseEntity child)
        {
            switch (parent)
            {
                case Part part: part.Books.Add((Book)child); break;
                case Book book: book.Titles.Add((Title)child); break;
                case Title title: title.Divisions.Add((Division)child); break;
                case Division division: division.Chapters.Add((Chapter)child); break;
                case Chapter chapter: chapter.Subchapters.Add((Subchapter)child); break;
                case Subchapter subchapter: subchapter.Articles.Add((Article)child); break;
                default: throw new ArgumentException($"Nie jest jednostką systematyzacyjną: {parent.GetType().Name}", nameof(parent));
            }
            child.Parent = parent;
        }

        private static int IndexOf(UnitType level)
        {
            for (var i = 0; i < Levels.Count; i++)
                if (Levels[i] == level)
                    return i;
            return -1;
        }
    }
}
