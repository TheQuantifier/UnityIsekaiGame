using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "SocialStatusDefinition", menuName = "Unity Isekai Game/Progression/Social Status Definition")]
    public sealed class SocialStatusDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string socialStatusId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private ProgressionPolicyPayload[] permissions;
        [SerializeField] private ProgressionPolicyPayload[] restrictions;
        [SerializeField] private ProgressionPolicyPayload[] accessRules;
        [SerializeField] private ProgressionPolicyPayload[] serviceEligibility;
        [SerializeField] private float priceModifier;
        [SerializeField] private float taxModifier;
        [SerializeField] private CalculatedStatModifierDefinition[] calculatedStatModifiers;
        [SerializeField] private ResistanceModifierDefinition[] resistanceModifiers;
        [SerializeField] private ProgressionAbilityReference[] grantedAbilities;
        [SerializeField] private ProgressionAbilityReference[] blockedAbilities;
        [SerializeField] private ProgressionPolicyPayload[] legalConsequences;
        [SerializeField] private ProgressionPolicyPayload[] relationshipEffects;
        [SerializeField] private ProgressionPolicyPayload[] hostilityEffects;
        [SerializeField] private SocialStatusContextKind defaultContextKind = SocialStatusContextKind.Global;
        [SerializeField] private bool allowMultipleContexts = true;
        [SerializeField] private bool allowGlobalApplication = true;
        [SerializeField] private bool retainHistory = true;
        [SerializeField] private string futureExpirationResolutionMetadata;

        public string SocialStatusId => socialStatusId;
        public string Id => socialStatusId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.SocialStatus;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public float PriceModifier => priceModifier;
        public float TaxModifier => taxModifier;
        public IReadOnlyList<CalculatedStatModifierDefinition> CalculatedStatModifiers => calculatedStatModifiers ?? System.Array.Empty<CalculatedStatModifierDefinition>();
        public IReadOnlyList<ResistanceModifierDefinition> ResistanceModifiers => resistanceModifiers ?? System.Array.Empty<ResistanceModifierDefinition>();
        public IReadOnlyList<ProgressionAbilityReference> GrantedAbilities => grantedAbilities ?? System.Array.Empty<ProgressionAbilityReference>();
        public IReadOnlyList<ProgressionAbilityReference> BlockedAbilities => blockedAbilities ?? System.Array.Empty<ProgressionAbilityReference>();
        public SocialStatusContextKind DefaultContextKind => defaultContextKind;
        public bool AllowMultipleContexts => allowMultipleContexts;
        public bool AllowGlobalApplication => allowGlobalApplication;
        public bool RetainHistory => retainHistory;

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Id.StartsWith("social-status."))
            {
                report.AddWarning($"Social status '{DisplayName}' should use the 'social-status.' namespace prefix.");
            }

            if (!System.Enum.IsDefined(typeof(SocialStatusContextKind), defaultContextKind))
            {
                report.AddError($"Social status '{DisplayName}' has an invalid default context kind.");
            }

            if (!allowGlobalApplication && defaultContextKind == SocialStatusContextKind.Global)
            {
                report.AddError($"Social status '{DisplayName}' disallows global application but defaults to Global.");
            }

            if (float.IsNaN(priceModifier) || float.IsInfinity(priceModifier) || priceModifier < -1f)
            {
                report.AddError($"Social status '{DisplayName}' has an invalid price modifier.");
            }

            if (float.IsNaN(taxModifier) || float.IsInfinity(taxModifier) || taxModifier < -1f)
            {
                report.AddError($"Social status '{DisplayName}' has an invalid tax modifier.");
            }

            RoleDefinition.ValidateStatModifiers("Social status", DisplayName, CalculatedStatModifiers, definitionsById, report);
            RoleDefinition.ValidateResistanceModifiers("Social status", DisplayName, ResistanceModifiers, definitionsById, report);
            RoleDefinition.ValidateAbilityReferences("Social status", DisplayName, "grant", GrantedAbilities, definitionsById, report);
            RoleDefinition.ValidateAbilityReferences("Social status", DisplayName, "block", BlockedAbilities, definitionsById, report);
        }
    }
}
