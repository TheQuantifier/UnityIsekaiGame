using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge
{
    [CreateAssetMenu(fileName = "KnowledgeFactDefinition", menuName = "Unity Isekai Game/Knowledge/Fact Definition")]
    public sealed class KnowledgeFactDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string factId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private KnowledgeDomain domain = KnowledgeDomain.Unknown;
        [SerializeField] private KnowledgePropositionType propositionType = KnowledgePropositionType.Unknown;
        [SerializeField] private KnowledgeSubjectType subjectType = KnowledgeSubjectType.Unknown;
        [SerializeField] private KnowledgeSubjectType objectType = KnowledgeSubjectType.Unknown;
        [SerializeField] private KnowledgeValueType valueType = KnowledgeValueType.StableId;
        [SerializeField] private KnowledgeVisibility defaultVisibility = KnowledgeVisibility.Public;
        [SerializeField] private bool defaultDiscoverable = true;
        [SerializeField, Range(0, 1000)] private int certaintyThreshold = 700;
        [SerializeField, Min(1)] private int requiredEvidenceCount = 1;
        [SerializeField] private KnowledgeContradictionPolicy contradictionPolicy = KnowledgeContradictionPolicy.KeepBoth;
        [SerializeField] private KnowledgeStalenessPolicy stalenessPolicy = KnowledgeStalenessPolicy.NeverStale;
        [SerializeField] private KnowledgeForgettingPolicy forgettingPolicy = KnowledgeForgettingPolicy.NeverForget;
        [SerializeField] private bool shareable = true;
        [SerializeField] private string[] tags;
        [SerializeField] private string validationMetadata;

        public string Id => factId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public KnowledgeDomain Domain => domain;
        public KnowledgePropositionType PropositionType => propositionType;
        public KnowledgeSubjectType SubjectType => subjectType;
        public KnowledgeSubjectType ObjectType => objectType;
        public KnowledgeValueType ValueType => valueType;
        public KnowledgeVisibility DefaultVisibility => defaultVisibility;
        public bool DefaultDiscoverable => defaultDiscoverable;
        public int CertaintyThreshold => KnowledgeConfidence.Clamp(certaintyThreshold);
        public int RequiredEvidenceCount => Math.Max(1, requiredEvidenceCount);
        public KnowledgeContradictionPolicy ContradictionPolicy => contradictionPolicy;
        public KnowledgeStalenessPolicy StalenessPolicy => stalenessPolicy;
        public KnowledgeForgettingPolicy ForgettingPolicy => forgettingPolicy;
        public bool Shareable => shareable;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            factId = factId?.Trim();
            certaintyThreshold = KnowledgeConfidence.Clamp(certaintyThreshold);
            requiredEvidenceCount = Math.Max(1, requiredEvidenceCount);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Knowledge Fact '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("fact.", StringComparison.Ordinal))
            {
                report.AddWarning($"Knowledge Fact '{Id}' should use the 'fact.' namespace prefix.");
            }

            ValidateEnum(domain, nameof(KnowledgeDomain), report);
            ValidateEnum(propositionType, nameof(KnowledgePropositionType), report);
            ValidateEnum(subjectType, nameof(KnowledgeSubjectType), report);
            ValidateEnum(objectType, nameof(KnowledgeSubjectType), report);
            ValidateEnum(valueType, nameof(KnowledgeValueType), report);
            ValidateEnum(defaultVisibility, nameof(KnowledgeVisibility), report);
            ValidateEnum(contradictionPolicy, nameof(KnowledgeContradictionPolicy), report);
            ValidateEnum(stalenessPolicy, nameof(KnowledgeStalenessPolicy), report);
            ValidateEnum(forgettingPolicy, nameof(KnowledgeForgettingPolicy), report);

            if (domain == KnowledgeDomain.Unknown)
            {
                report.AddError($"Knowledge Fact '{DisplayName}' must declare a concrete domain.");
            }

            if (propositionType == KnowledgePropositionType.Unknown)
            {
                report.AddError($"Knowledge Fact '{DisplayName}' must declare a concrete proposition type.");
            }

            if (subjectType == KnowledgeSubjectType.Unknown)
            {
                report.AddError($"Knowledge Fact '{DisplayName}' must declare a concrete subject type.");
            }

            if (certaintyThreshold < KnowledgeConfidence.Minimum || certaintyThreshold > KnowledgeConfidence.Maximum)
            {
                report.AddError($"Knowledge Fact '{DisplayName}' has an invalid certainty threshold.");
            }
        }

        private void ValidateEnum<T>(T value, string enumName, DefinitionValidationReport report)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                report.AddError($"Knowledge Fact '{DisplayName}' has an invalid {enumName} value.");
            }
        }
    }
}
