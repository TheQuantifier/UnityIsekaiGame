using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Diplomacy
{
    [CreateAssetMenu(fileName = "DiplomaticRelationDefinition", menuName = "Unity Isekai Game/Diplomacy/Relation Definition")]
    public sealed class DiplomaticRelationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string relationDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private DiplomaticRelationCategory category = DiplomaticRelationCategory.Neutral;
        [SerializeField] private DiplomaticReciprocityPolicy reciprocityPolicy = DiplomaticReciprocityPolicy.MirrorOnCreate;
        [SerializeField] private DiplomaticVisibility defaultVisibility = DiplomaticVisibility.Public;
        [SerializeField] private bool requiresRecognition;
        [SerializeField] private bool createsMilitaryObligation;
        [SerializeField] private bool supportsWarState;
        [SerializeField] private string[] requiredAuthorityPermissionIds = Array.Empty<string>();
        [SerializeField] private string[] referencedSocialNormIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => relationDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public DiplomaticRelationCategory Category => category;
        public DiplomaticReciprocityPolicy ReciprocityPolicy => reciprocityPolicy;
        public DiplomaticVisibility DefaultVisibility => defaultVisibility;
        public bool RequiresRecognition => requiresRecognition;
        public bool CreatesMilitaryObligation => createsMilitaryObligation;
        public bool SupportsWarState => supportsWarState;
        public IReadOnlyList<string> RequiredAuthorityPermissionIds => DiplomacyModelUtility.Clean(requiredAuthorityPermissionIds);
        public IReadOnlyList<string> ReferencedSocialNormIds => DiplomacyModelUtility.Clean(referencedSocialNormIds);
        public IReadOnlyList<string> TagIds => DiplomacyModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, DiplomaticRelationCategory relationCategory, DiplomaticReciprocityPolicy reciprocity, DiplomaticVisibility visibility = DiplomaticVisibility.Public, bool recognitionRequired = false, bool militaryObligation = false, bool warState = false, IEnumerable<string> authorityPermissionIds = null, IEnumerable<string> socialNormIds = null, IEnumerable<string> tagIds = null)
        {
            relationDefinitionId = DiplomacyModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? relationDefinitionId : name.Trim();
            description = string.Empty;
            category = relationCategory;
            reciprocityPolicy = reciprocity;
            defaultVisibility = visibility;
            requiresRecognition = recognitionRequired;
            createsMilitaryObligation = militaryObligation;
            supportsWarState = warState;
            requiredAuthorityPermissionIds = DiplomacyModelUtility.Clean(authorityPermissionIds);
            referencedSocialNormIds = DiplomacyModelUtility.Clean(socialNormIds);
            tags = DiplomacyModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Diplomatic Relation definition has no stable ID.");
            else if (!Id.StartsWith("diplomatic-relation.", StringComparison.Ordinal)) report.AddWarning($"Diplomatic Relation definition '{DisplayName}' should use the 'diplomatic-relation.' namespace prefix.");
            ValidateEnum(category, DisplayName, "category", report);
            ValidateEnum(reciprocityPolicy, DisplayName, "reciprocity policy", report);
            ValidateEnum(defaultVisibility, DisplayName, "visibility", report);
            if (category == DiplomaticRelationCategory.AtWar && !supportsWarState) report.AddError($"Diplomatic Relation definition '{DisplayName}' is war category but does not support war state.");
            ValidateReferences(definitionsById, RequiredAuthorityPermissionIds, report, DisplayName, "authority permission");
            ValidateReferences(definitionsById, ReferencedSocialNormIds, report, DisplayName, "social norm");
        }

        private static void ValidateEnum<T>(T value, string name, string field, DefinitionValidationReport report) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value)) report.AddError($"Diplomatic Relation definition '{name}' has invalid {field}.");
        }

        private static void ValidateReferences(IReadOnlyDictionary<string, IGameDefinition> definitionsById, IEnumerable<string> ids, DefinitionValidationReport report, string name, string field)
        {
            if (definitionsById == null) return;
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (!definitionsById.ContainsKey(id)) report.AddError($"Diplomatic Relation definition '{name}' references missing {field} definition '{id}'.");
            }
        }
    }

    [CreateAssetMenu(fileName = "DiplomaticAgreementDefinition", menuName = "Unity Isekai Game/Diplomacy/Agreement Definition")]
    public class DiplomaticAgreementDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string agreementDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private DiplomaticAgreementCategory category = DiplomaticAgreementCategory.Cooperation;
        [SerializeField] private DiplomaticVisibility defaultVisibility = DiplomaticVisibility.Public;
        [SerializeField] private int minimumPrincipalParties = 2;
        [SerializeField] private bool requiresSignatures = true;
        [SerializeField] private bool requiresRatification;
        [SerializeField] private bool permitsSecretClauses;
        [SerializeField] private bool createsAutomaticMilitaryAssistance;
        [SerializeField] private string[] allowedClauseDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredAuthorityPermissionIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => agreementDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public DiplomaticAgreementCategory Category => category;
        public DiplomaticVisibility DefaultVisibility => defaultVisibility;
        public int MinimumPrincipalParties => Math.Max(1, minimumPrincipalParties);
        public bool RequiresSignatures => requiresSignatures;
        public bool RequiresRatification => requiresRatification;
        public bool PermitsSecretClauses => permitsSecretClauses;
        public bool CreatesAutomaticMilitaryAssistance => createsAutomaticMilitaryAssistance;
        public IReadOnlyList<string> AllowedClauseDefinitionIds => DiplomacyModelUtility.Clean(allowedClauseDefinitionIds);
        public IReadOnlyList<string> RequiredAuthorityPermissionIds => DiplomacyModelUtility.Clean(requiredAuthorityPermissionIds);
        public IReadOnlyList<string> TagIds => DiplomacyModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, DiplomaticAgreementCategory agreementCategory, DiplomaticVisibility visibility = DiplomaticVisibility.Public, int principalParties = 2, bool signatures = true, bool ratification = false, bool secretClauses = false, bool automaticMilitaryAid = false, IEnumerable<string> clauseIds = null, IEnumerable<string> permissionIds = null, IEnumerable<string> tagIds = null)
        {
            agreementDefinitionId = DiplomacyModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? agreementDefinitionId : name.Trim();
            description = string.Empty;
            category = agreementCategory;
            defaultVisibility = visibility;
            minimumPrincipalParties = Math.Max(1, principalParties);
            requiresSignatures = signatures;
            requiresRatification = ratification;
            permitsSecretClauses = secretClauses;
            createsAutomaticMilitaryAssistance = automaticMilitaryAid;
            allowedClauseDefinitionIds = DiplomacyModelUtility.Clean(clauseIds);
            requiredAuthorityPermissionIds = DiplomacyModelUtility.Clean(permissionIds);
            tags = DiplomacyModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Diplomatic Agreement definition has no stable ID.");
            else if (!Id.StartsWith("diplomatic-agreement.", StringComparison.Ordinal)) report.AddWarning($"Diplomatic Agreement definition '{DisplayName}' should use the 'diplomatic-agreement.' namespace prefix.");
            if (!Enum.IsDefined(typeof(DiplomaticAgreementCategory), category) || category == DiplomaticAgreementCategory.Unknown) report.AddError($"Diplomatic Agreement definition '{DisplayName}' has invalid category.");
            if (!Enum.IsDefined(typeof(DiplomaticVisibility), defaultVisibility)) report.AddError($"Diplomatic Agreement definition '{DisplayName}' has invalid visibility.");
            if (MinimumPrincipalParties < 1) report.AddError($"Diplomatic Agreement definition '{DisplayName}' has invalid minimum principal party count.");
            ValidateReferences<DiplomaticClauseDefinition>(definitionsById, AllowedClauseDefinitionIds, report, DisplayName, "clause");
            foreach (string id in RequiredAuthorityPermissionIds) if (definitionsById != null && !definitionsById.ContainsKey(id)) report.AddError($"Diplomatic Agreement definition '{DisplayName}' references missing authority permission definition '{id}'.");
        }

        private static void ValidateReferences<TDefinition>(IReadOnlyDictionary<string, IGameDefinition> definitionsById, IEnumerable<string> ids, DefinitionValidationReport report, string name, string field) where TDefinition : class, IGameDefinition
        {
            if (definitionsById == null) return;
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not TDefinition) report.AddError($"Diplomatic Agreement definition '{name}' references missing {field} definition '{id}'.");
            }
        }
    }

    [CreateAssetMenu(fileName = "DiplomaticClauseDefinition", menuName = "Unity Isekai Game/Diplomacy/Clause Definition")]
    public class DiplomaticClauseDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string clauseDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private DiplomaticClauseCategory category = DiplomaticClauseCategory.Custom;
        [SerializeField] private DiplomaticVisibility defaultVisibility = DiplomaticVisibility.Public;
        [SerializeField] private DiplomaticClauseParameterType[] allowedParameterTypes = Array.Empty<DiplomaticClauseParameterType>();
        [SerializeField] private bool breachTrackable = true;
        [SerializeField] private bool referencesExternalContract;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => clauseDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public DiplomaticClauseCategory Category => category;
        public DiplomaticVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<DiplomaticClauseParameterType> AllowedParameterTypes => allowedParameterTypes ?? Array.Empty<DiplomaticClauseParameterType>();
        public bool BreachTrackable => breachTrackable;
        public bool ReferencesExternalContract => referencesExternalContract;
        public IReadOnlyList<string> TagIds => DiplomacyModelUtility.Clean(tags);

        public void DevelopmentConfigure(string id, string name, DiplomaticClauseCategory clauseCategory, DiplomaticVisibility visibility = DiplomaticVisibility.Public, IEnumerable<DiplomaticClauseParameterType> parameterTypes = null, bool trackBreaches = true, bool externalContract = false, IEnumerable<string> tagIds = null)
        {
            clauseDefinitionId = DiplomacyModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? clauseDefinitionId : name.Trim();
            category = clauseCategory;
            defaultVisibility = visibility;
            allowedParameterTypes = (parameterTypes ?? Array.Empty<DiplomaticClauseParameterType>()).Where(item => item != DiplomaticClauseParameterType.Unknown).Distinct().ToArray();
            breachTrackable = trackBreaches;
            referencesExternalContract = externalContract;
            tags = DiplomacyModelUtility.Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Diplomatic Clause definition has no stable ID.");
            else if (!Id.StartsWith("diplomatic-clause.", StringComparison.Ordinal)) report.AddWarning($"Diplomatic Clause definition '{DisplayName}' should use the 'diplomatic-clause.' namespace prefix.");
            if (!Enum.IsDefined(typeof(DiplomaticClauseCategory), category) || category == DiplomaticClauseCategory.Unknown) report.AddError($"Diplomatic Clause definition '{DisplayName}' has invalid category.");
            if (!Enum.IsDefined(typeof(DiplomaticVisibility), defaultVisibility)) report.AddError($"Diplomatic Clause definition '{DisplayName}' has invalid visibility.");
            foreach (DiplomaticClauseParameterType type in AllowedParameterTypes)
            {
                if (!Enum.IsDefined(typeof(DiplomaticClauseParameterType), type) || type == DiplomaticClauseParameterType.Unknown) report.AddError($"Diplomatic Clause definition '{DisplayName}' has invalid parameter type.");
            }
        }
    }

    [CreateAssetMenu(fileName = "DiplomaticWarDefinition", menuName = "Unity Isekai Game/Diplomacy/War Definition")]
    public class DiplomaticWarDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string warDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private DiplomaticWarCategory category = DiplomaticWarCategory.FormalWar;
        [SerializeField] private DiplomaticVisibility defaultVisibility = DiplomaticVisibility.Public;
        [SerializeField] private bool requiresDeclaration = true;
        [SerializeField] private bool supportsFactionalParticipants = true;
        [SerializeField] private string[] requiredAuthorityPermissionIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => warDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public DiplomaticWarCategory Category => category;
        public DiplomaticVisibility DefaultVisibility => defaultVisibility;
        public bool RequiresDeclaration => requiresDeclaration;
        public bool SupportsFactionalParticipants => supportsFactionalParticipants;
        public IReadOnlyList<string> RequiredAuthorityPermissionIds => DiplomacyModelUtility.Clean(requiredAuthorityPermissionIds);
        public IReadOnlyList<string> TagIds => DiplomacyModelUtility.Clean(tags);

        public void DevelopmentConfigure(string id, string name, DiplomaticWarCategory warCategory, DiplomaticVisibility visibility = DiplomaticVisibility.Public, bool declarationRequired = true, bool factionsAllowed = true, IEnumerable<string> permissionIds = null, IEnumerable<string> tagIds = null)
        {
            warDefinitionId = DiplomacyModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? warDefinitionId : name.Trim();
            category = warCategory;
            defaultVisibility = visibility;
            requiresDeclaration = declarationRequired;
            supportsFactionalParticipants = factionsAllowed;
            requiredAuthorityPermissionIds = DiplomacyModelUtility.Clean(permissionIds);
            tags = DiplomacyModelUtility.Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Diplomatic War definition has no stable ID.");
            else if (!Id.StartsWith("diplomatic-war.", StringComparison.Ordinal)) report.AddWarning($"Diplomatic War definition '{DisplayName}' should use the 'diplomatic-war.' namespace prefix.");
            if (!Enum.IsDefined(typeof(DiplomaticWarCategory), category) || category == DiplomaticWarCategory.Unknown) report.AddError($"Diplomatic War definition '{DisplayName}' has invalid category.");
            if (!Enum.IsDefined(typeof(DiplomaticVisibility), defaultVisibility)) report.AddError($"Diplomatic War definition '{DisplayName}' has invalid visibility.");
            foreach (string id in RequiredAuthorityPermissionIds) if (definitionsById != null && !definitionsById.ContainsKey(id)) report.AddError($"Diplomatic War definition '{DisplayName}' references missing authority permission definition '{id}'.");
        }
    }
}
