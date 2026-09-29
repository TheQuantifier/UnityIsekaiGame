using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Justice
{
    public sealed class JusticeValidationReport
    {
        public JusticeValidationReport(IEnumerable<string> errors)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }

    public sealed class JusticeRuntimeValidationService
    {
        public JusticeValidationReport Validate(JusticeRuntimeSaveData saveData, DefinitionRegistry definitions, GovernmentRuntime governments, LegalRuntime laws, OrganizationRuntime organizations, OrganizationAuthorityRuntime authority, CrimeRuntime crimes, string expectedWorldId, IEnumerable<string> knownPersons, IEnumerable<string> knownPlaces)
        {
            return JusticeRuntime.ValidateSaveData(saveData, definitions, governments, laws, organizations, authority, crimes, expectedWorldId, knownPersons, knownPlaces, out string failure)
                ? new JusticeValidationReport(Array.Empty<string>())
                : new JusticeValidationReport(new[] { failure });
        }
    }
}
