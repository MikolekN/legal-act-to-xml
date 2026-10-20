using Saga.Model;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Numeracja i hierarchia jednostek systematyzacyjnych sprawdzane na gotowym drzewie. Wynik to
    /// komunikaty na jednostkach — struktura zostaje taka, jak ją zbudowano.
    /// </summary>
    internal static class SystematizingUnitValidator
    {
        internal static void Validate(LegalDocument document)
        {
            CheckNumbering(document.Parts);
            foreach (var part in document.Parts)
                Visit(part);
        }

        private static void Visit(BaseEntity unit)
        {
            var children = Children(unit).ToList();
            CheckNumbering(children);

            foreach (var child in children)
            {
                if (child is not ISystematizingUnit { IsImplicit: false } explicitUnit)
                {
                    Visit(child);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(explicitUnit.Heading) && !IsNamed(child))
                    ValidationReporter.AddValidationMessage(child, ValidationLevel.Info,
                        "Jednostka bez tytułu (§ 62 ust. 2 ZTP).");

                if (child is Subchapter && unit is Chapter { IsImplicit: true })
                    ValidationReporter.AddValidationMessage(child, ValidationLevel.Warning,
                        "Oddział poza rozdziałem — oddział dzieli rozdział.");

                Visit(child);
            }
        }

        /// <summary>Kolejne jawne jednostki rodzeństwa: ten sam numer z innym sufiksem albo następny.</summary>
        private static void CheckNumbering(IEnumerable<BaseEntity> siblings)
        {
            EntityNumber? previous = null;
            foreach (var unit in siblings)
            {
                if (unit is not ISystematizingUnit { IsImplicit: false } || unit.Number is not { } number || IsNamed(unit))
                    continue;

                if (previous != null)
                {
                    if (number.Designation == previous.Designation)
                        ValidationReporter.AddValidationMessage(unit, ValidationLevel.Warning,
                            $"Powtórzone oznaczenie jednostki ({number.RawValue ?? number.Value}).");
                    else if (number.NumericPart < previous.NumericPart)
                        ValidationReporter.AddValidationMessage(unit, ValidationLevel.Warning,
                            $"Numeracja maleje: {previous.RawValue ?? previous.Value} → {number.RawValue ?? number.Value}.");
                    else if (number.NumericPart > previous.NumericPart + 1)
                        ValidationReporter.AddValidationMessage(unit, ValidationLevel.Warning,
                            $"Luka w numeracji: {previous.RawValue ?? previous.Value} → {number.RawValue ?? number.Value}.");
                }

                previous = number;
            }
        }

        private static bool IsNamed(BaseEntity unit) => unit.Number?.IsName == true;

        private static IEnumerable<BaseEntity> Children(BaseEntity unit) =>
            SystematizingHierarchy.Children(unit).Where(child => child is ISystematizingUnit);
    }
}
