using Saga.Model;
using Saga.Core.Services.EntryIntoForce;

namespace CorpusValidator;

/// <summary>
/// Ile jednostek i zdań realnie dostało datę wejścia w życie. Chodzi TYM SAMYM obchodem
/// (<see cref="EditorialUnitWalker"/>), którym parser rozsyła daty — własny obchód mierzyłby co
/// innego niż to, co odwiedza produkcja, i cicho rozjechałby się przy pierwszej zmianie modelu.
/// </summary>
internal sealed class PropagationCounters
{
	internal int Units { get; private set; }
	internal int UnitsDated { get; private set; }
	internal int Sentences { get; private set; }
	internal int SentencesDated { get; private set; }

	/// <summary>Jednostki i zdania z datą INNĄ niż reguła ogólna — czyli realny efekt wyjątków.</summary>
	internal int DifferentFromGeneral { get; private set; }

	internal static PropagationCounters Measure(LegalDocument document, DateTime? generalDate)
	{
		var counters = new PropagationCounters();

		foreach (var unit in EditorialUnitWalker.SelfAndDescendants(document.RootPart, includeAmendmentContent: true))
		{
			counters.Units++;
			if (unit.EffectiveDate != default)
			{
				counters.UnitsDated++;
				if (unit.EffectiveDate != generalDate)
					counters.DifferentFromGeneral++;
			}

			if (unit is not IHasTextSegments holder)
				continue;

			foreach (var segment in holder.TextSegments)
			{
				counters.Sentences++;
				if (segment.EffectiveDate == default)
					continue;

				counters.SentencesDated++;
				if (segment.EffectiveDate != generalDate)
					counters.DifferentFromGeneral++;
			}
		}

		return counters;
	}

	internal void Add(PropagationCounters other)
	{
		Units += other.Units;
		UnitsDated += other.UnitsDated;
		Sentences += other.Sentences;
		SentencesDated += other.SentencesDated;
		DifferentFromGeneral += other.DifferentFromGeneral;
	}
}
