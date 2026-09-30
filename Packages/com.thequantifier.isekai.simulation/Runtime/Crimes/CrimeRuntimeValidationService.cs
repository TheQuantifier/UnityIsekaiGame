using System;
using System.Collections.Generic;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Crimes
{
    public sealed class CrimeValidationReport
    {
        public CrimeValidationReport(IEnumerable<string> errors)
        {
            Errors = new List<string>(errors ?? Array.Empty<string>()).AsReadOnly();
        }

        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }

    public sealed class CrimeRuntimeValidationService
    {
        public CrimeValidationReport Validate(CrimeRuntime runtime, DefinitionRegistry definitions, GovernmentRuntime governments, LegalRuntime laws, OrganizationAuthorityRuntime authority, DiplomacyRuntime diplomacy, string expectedWorldId, IEnumerable<string> knownPersons, IEnumerable<string> knownPlaces)
        {
            if (runtime == null)
            {
                return new CrimeValidationReport(new[] { "Crime runtime is missing." });
            }

            return Validate(runtime.CreateSaveData(), definitions, governments, laws, authority, diplomacy, expectedWorldId, knownPersons, knownPlaces);
        }

        public CrimeValidationReport Validate(CrimeRuntimeSaveData saveData, DefinitionRegistry definitions, GovernmentRuntime governments, LegalRuntime laws, OrganizationAuthorityRuntime authority, DiplomacyRuntime diplomacy, string expectedWorldId, IEnumerable<string> knownPersons, IEnumerable<string> knownPlaces)
        {
            return CrimeRuntime.ValidateSaveData(saveData, definitions, governments, laws, authority, diplomacy, expectedWorldId, knownPersons, knownPlaces, out string failure)
                ? new CrimeValidationReport(Array.Empty<string>())
                : new CrimeValidationReport(new[] { failure });
        }
    }
}
