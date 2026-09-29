using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Laws
{
    public sealed class LegalValidationReport
    {
        public LegalValidationReport(IEnumerable<string> errors)
        {
            Errors = (errors ?? Array.Empty<string>())
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Select(error => error.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(error => error, StringComparer.Ordinal)
                .ToArray();
        }

        public bool IsValid => Errors.Count == 0;
        public IReadOnlyList<string> Errors { get; }
    }

    /// <summary>
    /// Performs read-only validation of the complete legal persistence graph.
    /// Persistence and tooling share this facade so validation cannot drift.
    /// </summary>
    public sealed class LegalRuntimeValidationService
    {
        public LegalValidationReport Validate(
            LegalRuntime runtime,
            DefinitionRegistry definitions,
            GovernmentRuntime governments,
            OrganizationRuntime organizations,
            OrganizationAuthorityRuntime authority,
            OrganizationDecisionRuntime decisions,
            DiplomacyRuntime diplomacy,
            PropertyRuntime properties,
            string expectedWorldId,
            IEnumerable<string> knownPersonIds,
            IEnumerable<string> knownPlaceIds)
        {
            if (runtime == null)
            {
                return new LegalValidationReport(new[] { "Legal runtime is missing." });
            }

            return Validate(runtime.CreateSaveData(), definitions, governments, organizations, authority, decisions, diplomacy, properties, expectedWorldId, knownPersonIds, knownPlaceIds);
        }

        public LegalValidationReport Validate(
            LegalRuntimeSaveData saveData,
            DefinitionRegistry definitions,
            GovernmentRuntime governments,
            OrganizationRuntime organizations,
            OrganizationAuthorityRuntime authority,
            OrganizationDecisionRuntime decisions,
            DiplomacyRuntime diplomacy,
            PropertyRuntime properties,
            string expectedWorldId,
            IEnumerable<string> knownPersonIds,
            IEnumerable<string> knownPlaceIds)
        {
            return LegalRuntime.ValidateSaveData(saveData, definitions, governments, organizations, authority, decisions, diplomacy, properties, expectedWorldId, knownPersonIds, knownPlaceIds, out string failure)
                ? new LegalValidationReport(Array.Empty<string>())
                : new LegalValidationReport(new[] { failure });
        }
    }
}
