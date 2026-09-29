using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.Observation
{
    [CreateAssetMenu(fileName = "ExaminationMethodDefinition", menuName = "Unity Isekai Game/Knowledge/Examination Method")]
    public sealed class ExaminationMethodDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string methodId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ExaminationMethodCategory category;
        [SerializeField] private bool active;
        [SerializeField] private ObservationTargetType targetType = ObservationTargetType.Body;
        [SerializeField, Min(0f)] private float requiredProximity = 2f;
        [SerializeField] private bool requiresConsent = true;
        [SerializeField] private ObservationAccessLevel requiredAccess = ObservationAccessLevel.Medical;
        [SerializeField] private string requiredSkillId;
        [SerializeField] private string requiredToolTagId;
        [SerializeField, Range(0, 1000)] private int basePrecision = 650;
        [SerializeField] private bool exposesAnatomy;
        [SerializeField] private bool exposesInternalState;
        [SerializeField] private bool exposesBiologicalConditions;
        [SerializeField] private bool exposesPoisonOrToxin;
        [SerializeField] private bool detectsTransformation;
        [SerializeField] private KnowledgeVisibility privacyClassification = KnowledgeVisibility.Private;
        [SerializeField] private string[] resultingEvidenceCategories;
        [SerializeField] private string validationMetadata;

        public string Id => methodId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public ExaminationMethodCategory Category => category;
        public bool Active => active;
        public ObservationTargetType TargetType => targetType;
        public float RequiredProximity => Mathf.Max(0f, requiredProximity);
        public bool RequiresConsent => requiresConsent;
        public ObservationAccessLevel RequiredAccess => requiredAccess;
        public string RequiredSkillId => requiredSkillId ?? string.Empty;
        public string RequiredToolTagId => requiredToolTagId ?? string.Empty;
        public int BasePrecision => KnowledgeConfidence.Clamp(basePrecision);
        public bool ExposesAnatomy => exposesAnatomy;
        public bool ExposesInternalState => exposesInternalState;
        public bool ExposesBiologicalConditions => exposesBiologicalConditions;
        public bool ExposesPoisonOrToxin => exposesPoisonOrToxin;
        public bool DetectsTransformation => detectsTransformation;
        public KnowledgeVisibility PrivacyClassification => privacyClassification;
        public IReadOnlyList<string> ResultingEvidenceCategories => resultingEvidenceCategories ?? Array.Empty<string>();
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            methodId = methodId?.Trim();
            basePrecision = KnowledgeConfidence.Clamp(basePrecision);
            requiredProximity = Mathf.Max(0f, requiredProximity);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            MethodValidation.ValidateMethod(this, "Examination Method", "examination-method.", category != ExaminationMethodCategory.Unknown, BasePrecision, KnowledgeTrackingPolicy.PlayerMechanicalOnly, report);
            if (targetType == ObservationTargetType.Unknown)
            {
                report?.AddError($"Examination Method '{DisplayName}' must declare a concrete target type.");
            }

            MethodValidation.ValidateOptionalReference(RequiredSkillId, definitionsById, "skill", Id, report);
        }
    }
}
