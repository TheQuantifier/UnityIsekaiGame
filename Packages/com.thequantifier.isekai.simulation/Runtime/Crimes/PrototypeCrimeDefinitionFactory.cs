using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Laws;

namespace UnityIsekaiGame.Crimes
{
    public static class PrototypeCrimeDefinitionFactory
    {
        public const string UnlawfulPhysicalAttackOffenseId = "legal-offense.prototype.unlawful-physical-attack";
        public const string UnlawfulKillingOffenseId = "legal-offense.prototype.unlawful-killing";
        public const string ThreatCoercionOffenseId = "legal-offense.prototype.threat-coercion";
        public const string TheftOffenseId = "legal-offense.prototype.theft";
        public const string PropertyDamageOffenseId = "legal-offense.prototype.property-damage";
        public const string UnlawfulEntryOffenseId = "legal-offense.prototype.unlawful-entry";
        public const string FraudFalseRepresentationOffenseId = "legal-offense.prototype.fraud-false-representation";
        public const string MisuseOrganizationFundsOffenseId = "legal-offense.prototype.misuse-organization-funds";
        public const string ConfidentialityBreachOffenseId = "legal-offense.prototype.confidentiality-breach";
        public const string UnlawfulExerciseOfOfficeOffenseId = "legal-offense.prototype.unlawful-exercise-office";
        public const string MilitaryInternalViolationOffenseId = "legal-offense.prototype.military-internal-violation";
        public const string RegulatoryViolationOffenseId = "legal-offense.prototype.regulatory-violation";
        public const string AttemptOffenseId = "legal-offense.prototype.attempt";
        public const string AssistanceOffenseId = "legal-offense.prototype.assistance";
        public const string ArrestWarrantDefinitionId = "warrant.prototype.arrest";
        public const string SearchWarrantDefinitionId = "warrant.prototype.search";
        public const string SeizureWarrantDefinitionId = "warrant.prototype.seizure";
        public const string QuestioningWarrantDefinitionId = "warrant.prototype.questioning";
        public const string WantedForArrestDefinitionId = "wanted-status.prototype.arrest";
        public const string WantedForQuestioningDefinitionId = "wanted-status.prototype.questioning";
        public const string WantedForLocationDefinitionId = "wanted-status.prototype.locate";
        public const string MilitaryApprehensionWantedDefinitionId = "wanted-status.prototype.military-apprehension";
        public const string MissingPersonNoticeDefinitionId = "wanted-status.prototype.missing-person";

        public static DefinitionRegistry AddMissingPrototypeCrimeDefinitions(DefinitionRegistry source)
        {
            List<IGameDefinition> all = source?.DefinitionsById.Values.Where(item => item != null).ToList() ?? new List<IGameDefinition>();
            HashSet<string> ids = new HashSet<string>(all.Select(item => item.Id), StringComparer.Ordinal);
            void Add(IGameDefinition definition) { if (definition != null && ids.Add(definition.Id)) all.Add(definition); }

            Add(Offense(UnlawfulPhysicalAttackOffenseId, "Unlawful Physical Attack", OffenseCategory.ViolenceAgainstPerson, OffenseSeverityCategory.Serious, "crime.attack", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(UnlawfulKillingOffenseId, "Unlawful Killing", OffenseCategory.ViolenceAgainstPerson, OffenseSeverityCategory.Grave, "crime.killing", EvidenceSufficiencyState.Substantial, EvidenceSufficiencyState.ThresholdMet));
            Add(Offense(ThreatCoercionOffenseId, "Threat or Coercion", OffenseCategory.ThreatOrCoercion, OffenseSeverityCategory.Moderate, "crime.threat", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(TheftOffenseId, "Theft", OffenseCategory.TheftOrUnlawfulTaking, OffenseSeverityCategory.Moderate, "crime.theft", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(PropertyDamageOffenseId, "Property Damage", OffenseCategory.PropertyDamage, OffenseSeverityCategory.Moderate, "crime.property-damage", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(UnlawfulEntryOffenseId, "Unlawful Entry", OffenseCategory.TrespassOrUnauthorizedEntry, OffenseSeverityCategory.Minor, "crime.unlawful-entry", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Partial));
            Add(Offense(FraudFalseRepresentationOffenseId, "Fraud or False Representation", OffenseCategory.FraudOrDeception, OffenseSeverityCategory.Serious, "crime.fraud", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(MisuseOrganizationFundsOffenseId, "Misuse of Organization Funds", OffenseCategory.MisuseOfOffice, OffenseSeverityCategory.Serious, "crime.misuse-funds", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(ConfidentialityBreachOffenseId, "Confidentiality Breach", OffenseCategory.ConfidentialityOrInformationViolation, OffenseSeverityCategory.Moderate, "crime.confidentiality-breach", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(UnlawfulExerciseOfOfficeOffenseId, "Unlawful Exercise of Office", OffenseCategory.MisuseOfOffice, OffenseSeverityCategory.Serious, "crime.unlawful-office", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(MilitaryInternalViolationOffenseId, "Military Internal Violation", OffenseCategory.MilitaryInternalViolation, OffenseSeverityCategory.InternalDiscipline, "crime.military-violation", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial));
            Add(Offense(RegulatoryViolationOffenseId, "Regulatory Violation", OffenseCategory.RegulatoryViolation, OffenseSeverityCategory.Regulatory, "crime.regulatory-violation", EvidenceSufficiencyState.Weak, EvidenceSufficiencyState.Partial));
            Add(Offense(AttemptOffenseId, "Attempted Offense", OffenseCategory.Attempt, OffenseSeverityCategory.Moderate, "crime.attempt", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial, new[] { OffenseStage.Attempted, OffenseStage.Interrupted }));
            Add(Offense(AssistanceOffenseId, "Assistance or Participation", OffenseCategory.AssistanceOrParticipation, OffenseSeverityCategory.Moderate, "crime.assistance", EvidenceSufficiencyState.Partial, EvidenceSufficiencyState.Substantial, participation: new[] { ParticipationCategory.Assistant, ParticipationCategory.Organizer, ParticipationCategory.Instigator, ParticipationCategory.Beneficiary }));

            Add(Warrant(ArrestWarrantDefinitionId, "Arrest Warrant", WarrantCategory.Arrest, new[] { WarrantScopeKind.Person }, EvidenceSufficiencyState.Substantial, createsWanted: true));
            Add(Warrant(SearchWarrantDefinitionId, "Search Warrant", WarrantCategory.Search, new[] { WarrantScopeKind.Place, WarrantScopeKind.Inventory, WarrantScopeKind.Record }, EvidenceSufficiencyState.Substantial));
            Add(Warrant(SeizureWarrantDefinitionId, "Seizure Warrant", WarrantCategory.Seizure, new[] { WarrantScopeKind.Property, WarrantScopeKind.Item }, EvidenceSufficiencyState.Substantial));
            Add(Warrant(QuestioningWarrantDefinitionId, "Questioning Warrant", WarrantCategory.Questioning, new[] { WarrantScopeKind.Person }, EvidenceSufficiencyState.Partial, createsWanted: true));

            Add(Wanted(WantedForArrestDefinitionId, "Wanted for Arrest", WantedPurposeCategory.Arrest));
            Add(Wanted(WantedForQuestioningDefinitionId, "Wanted for Questioning", WantedPurposeCategory.Questioning));
            Add(Wanted(WantedForLocationDefinitionId, "Wanted for Location", WantedPurposeCategory.Locate));
            Add(Wanted(MilitaryApprehensionWantedDefinitionId, "Military Apprehension Wanted Status", WantedPurposeCategory.MilitaryApprehension));
            Add(Wanted(MissingPersonNoticeDefinitionId, "Missing Person Notice", WantedPurposeCategory.MissingPerson));
            return new DefinitionRegistry(all);
        }

        private static LegalOffenseDefinition Offense(string id, string name, OffenseCategory category, OffenseSeverityCategory severity, string actionId, EvidenceSufficiencyState charge, EvidenceSufficiencyState warrant, IEnumerable<OffenseStage> stages = null, IEnumerable<ParticipationCategory> participation = null)
        {
            LegalOffenseDefinition value = ScriptableObject.CreateInstance<LegalOffenseDefinition>();
            value.DevelopmentConfigure(
                id,
                name,
                category,
                severity,
                new[] { LegalEffectCategory.Prohibition },
                new[] { OffenseElementDefinitionData.Create(OffenseElementKind.ActorConduct, "conduct", actionId) },
                CrimeMentalState.Intentional,
                stages ?? new[] { OffenseStage.Attempted, OffenseStage.Completed },
                participation ?? new[] { ParticipationCategory.PrincipalActor, ParticipationCategory.CoActor },
                canWarrant: warrant != EvidenceSufficiencyState.None,
                threshold: warrant,
                chargeThreshold: charge,
                actionId: actionId);
            return value;
        }

        private static WarrantDefinition Warrant(string id, string name, WarrantCategory category, IEnumerable<WarrantScopeKind> scopes, EvidenceSufficiencyState threshold, bool createsWanted = false)
        {
            WarrantDefinition value = ScriptableObject.CreateInstance<WarrantDefinition>();
            value.DevelopmentConfigure(id, name, category, scopes, threshold, createsWanted);
            return value;
        }

        private static WantedStatusDefinition Wanted(string id, string name, WantedPurposeCategory purpose)
        {
            WantedStatusDefinition value = ScriptableObject.CreateInstance<WantedStatusDefinition>();
            value.DevelopmentConfigure(id, name, purpose, publicAllowed: purpose != WantedPurposeCategory.InternalOrganizationProcess);
            return value;
        }
    }
}
