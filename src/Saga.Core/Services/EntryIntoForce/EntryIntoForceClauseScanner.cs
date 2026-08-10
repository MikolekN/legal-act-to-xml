using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Services.EntryIntoForce
{
	/// <summary>Rola klauzuli znalezionej w jednostce.</summary>
	internal enum ClauseRole
	{
		/// <summary>Klauzula główna aktu („Ustawa wchodzi w życie …").</summary>
		Main,

		/// <summary>Wyjątek w treści klauzuli głównej („…, z wyjątkiem art. 2, który wchodzi w życie …").</summary>
		InlineException,

		/// <summary>Pozycja listy wyjątków pod klauzulą kończącą się „z wyjątkiem:".</summary>
		ExceptionListItem,

		/// <summary>Wystąpienie odrzucone przez filtry albo nadmiarowe — zapisywane, nigdy porzucane po cichu.</summary>
		Stray,
	}

	/// <summary>Kandydat na klauzulę wejścia w życie.</summary>
	internal sealed record ClauseCandidate(BaseEntity Owner, string Text, string Tail, ClauseRole Role)
	{
		internal string? RejectionReason { get; init; }

		/// <summary>Czy klauzula główna kończy się zapowiedzią listy wyjątków („z wyjątkiem:").</summary>
		internal bool OpensExceptionList { get; init; }
	}

	/// <summary>Przeszukuje dokument w poszukiwaniu klauzul wejścia w życie.</summary>
	internal sealed class EntryIntoForceClauseScanner
	{
		/// <summary>
		/// Zbiera kandydatów w kolejności dokumentu. Zaczyna od ustępów, bo artykuł nie niesie własnej treści.
        /// Przytoczona treść nowelizacji jest pomijana — znalezione klauzule należą do aktu zmienianego.
		/// </summary>
		internal IReadOnlyList<ClauseCandidate> Scan(LegalDocument document)
		{
			var candidates = new List<ClauseCandidate>();

			foreach (var article in document.Articles)
			{
				foreach (var paragraph in article.Paragraphs)
				foreach (var unit in EditorialUnitWalker.SelfAndDescendants(paragraph, includeAmendmentContent: false))
					Inspect(unit, article, candidates);
			}

			return candidates;
		}

		/// <summary>Bada treść jednej jednostki i dopisuje znalezione klauzule.</summary>
		private void Inspect(BaseEntity entity, Article article, List<ClauseCandidate> candidates)
		{
			var text = entity.ContentText?.Trim();
			if (string.IsNullOrEmpty(text) || !EntryIntoForcePatterns.AnyClauseVerb.IsMatch(text))
				return;

			var mainMatch = EntryIntoForcePatterns.MainClause.Match(text);
			var relativeMatch = EntryIntoForcePatterns.RelativeClause.Match(text);

			if (!mainMatch.Success && !relativeMatch.Success)
			{
				if (EntryIntoForcePatterns.NounPhraseMention.IsMatch(text))
					return;

				var reason = EntryIntoForcePatterns.ForeignActSubject.IsMatch(text)
					? "podmiotem klauzuli jest inny akt"
					: "czasownik klauzuli bez rozpoznanego podmiotu";

				candidates.Add(new ClauseCandidate(entity, text, string.Empty, ClauseRole.Stray)
				{
					RejectionReason = reason,
				});
				return;
			}

			if (mainMatch.Success)
			{
				var reason = RejectMain(text, mainMatch.Index, article);
				var tail = mainMatch.Groups["tail"].Value.Trim();

				candidates.Add(new ClauseCandidate(
					entity, text, tail, reason is null ? ClauseRole.Main : ClauseRole.Stray)
				{
					RejectionReason = reason,
					OpensExceptionList = OpensList(tail),
				});
				return;
			}

			candidates.Add(new ClauseCandidate(
				entity, text, relativeMatch.Groups["tail"].Value.Trim(), ClauseRole.ExceptionListItem));
		}

		/// <summary>Zwraca powód odrzucenia kandydata na klauzulę główną.</summary>
		private static string? RejectMain(string text, int matchIndex, Article article)
		{
			if (IsInsideQuote(text, matchIndex))
				return "klauzula wewnątrz cytatu";

			if (article.IsAmending)
				return "klauzula w artykule nowelizującym";

			return null;
		}

		/// <summary>Czy pozycja leży wewnątrz cytatu.</summary>
		private static bool IsInsideQuote(string text, int index)
		{
			var depth = 0;
			for (var i = 0; i < index && i < text.Length; i++)
			{
				if (text[i] == '„') depth++;
				else if (text[i] == '”' && depth > 0) depth--;
			}
			return depth > 0;
		}

		/// <summary>Czy ogon klauzuli zapowiada listę wyjątków („…, z wyjątkiem:").</summary>
		private static bool OpensList(string tail)
		{
			var match = EntryIntoForcePatterns.ExceptionMarker.Match(tail);
			if (!match.Success || !match.Groups["colon"].Success)
				return false;
			return match.Index + match.Length >= tail.TrimEnd().Length;
		}
	}
}
