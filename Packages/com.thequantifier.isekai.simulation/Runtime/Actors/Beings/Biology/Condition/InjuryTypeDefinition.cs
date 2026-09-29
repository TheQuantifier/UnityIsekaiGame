using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Condition
{
    [CreateAssetMenu(fileName = "InjuryType", menuName = "Unity Isekai Game/Beings/Biology/Injury Type")]
    public sealed class InjuryTypeDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string injuryTypeId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private DamageTypeDefinition[] compatibleDamageTypes;
        [SerializeField] private TagDefinition[] compatibleAnatomyTags;
        [SerializeField] private AnatomyStructuralCategory[] compatibleNodeCategories;
        [SerializeField] private StructureDamageState structuralImpact = StructureDamageState.Damaged;
        [SerializeField] private StructureFunctionalState functionalImpact = StructureFunctionalState.Reduced;
        [SerializeField, Min(0)] private int baseIntegrityDamage = 10;
        [SerializeField] private bool canCauseStructuralFailure = true;
        [SerializeField] private bool canCauseRuntimeAbsence;
        [SerializeField] private bool canAffectVitalStructures = true;
        [SerializeField] private bool causesBleeding;
        [SerializeField] private bool causesPain;
        [SerializeField] private bool supportsHealing;
        [SerializeField] private bool carriesInfectionRisk;
        [SerializeField] private TagDefinition[] tags;

        public string Id => injuryTypeId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public IReadOnlyList<DamageTypeDefinition> CompatibleDamageTypes => compatibleDamageTypes ?? Array.Empty<DamageTypeDefinition>();
        public IReadOnlyList<TagDefinition> CompatibleAnatomyTags => compatibleAnatomyTags ?? Array.Empty<TagDefinition>();
        public IReadOnlyList<AnatomyStructuralCategory> CompatibleNodeCategories => compatibleNodeCategories ?? Array.Empty<AnatomyStructuralCategory>();
        public StructureDamageState StructuralImpact => structuralImpact;
        public StructureFunctionalState FunctionalImpact => functionalImpact;
        public int BaseIntegrityDamage => Math.Max(0, baseIntegrityDamage);
        public bool CanCauseStructuralFailure => canCauseStructuralFailure;
        public bool CanCauseRuntimeAbsence => canCauseRuntimeAbsence;
        public bool CanAffectVitalStructures => canAffectVitalStructures;
        public bool CausesBleeding => causesBleeding;
        public bool CausesPain => causesPain;
        public bool SupportsHealing => supportsHealing;
        public bool CarriesInfectionRisk => carriesInfectionRisk;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();

        private void OnValidate()
        {
            injuryTypeId = injuryTypeId?.Trim();
            displayName = displayName?.Trim();
            baseIntegrityDamage = Math.Max(0, baseIntegrityDamage);
        }

        public bool IsCompatibleWith(AnatomyNodeSnapshot node, DamageTypeDefinition damageType)
        {
            if (node == null || !node.Present)
            {
                return false;
            }

            if (node.Vital && !CanAffectVitalStructures)
            {
                return false;
            }

            if (CompatibleNodeCategories.Count > 0 && !CompatibleNodeCategories.Contains(node.Category))
            {
                return false;
            }

            if (CompatibleAnatomyTags.Count > 0)
            {
                HashSet<string> nodeTags = new HashSet<string>(node.DamageTagIds ?? Array.Empty<string>(), StringComparer.Ordinal);
                if (!CompatibleAnatomyTags.Any(tag => tag != null && nodeTags.Contains(tag.Id)))
                {
                    return false;
                }
            }

            if (CompatibleDamageTypes.Count > 0)
            {
                if (damageType == null)
                {
                    return false;
                }

                bool compatibleDamage = CompatibleDamageTypes.Any(candidate => candidate != null && damageType.IsOrInheritsFrom(candidate));
                if (!compatibleDamage)
                {
                    return false;
                }
            }

            return true;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"InjuryTypeDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("injury.", StringComparison.Ordinal))
            {
                report.AddWarning($"InjuryTypeDefinition '{Id}' should use the 'injury.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"InjuryTypeDefinition '{Id}' is missing a display name.");
            }

            ValidateReferences(definitionsById, report);
            ValidateCanonicalAlphaSet(definitionsById, report);
        }

        private void ValidateReferences(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (DamageTypeDefinition damageType in CompatibleDamageTypes)
            {
                if (damageType == null)
                {
                    report.AddError($"InjuryTypeDefinition '{DisplayName}' has a missing compatible Damage Type.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(damageType.Id, out IGameDefinition found) || found is not DamageTypeDefinition))
                {
                    report.AddError($"InjuryTypeDefinition '{DisplayName}' references Damage Type '{damageType.Id}', which is not in the configured catalog.");
                }
            }

            foreach (TagDefinition tag in CompatibleAnatomyTags.Concat(Tags))
            {
                if (tag == null)
                {
                    report.AddError($"InjuryTypeDefinition '{DisplayName}' has a missing tag reference.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(tag.Id, out IGameDefinition found) || found is not TagDefinition))
                {
                    report.AddError($"InjuryTypeDefinition '{DisplayName}' references tag '{tag.Id}', which is not in the configured catalog.");
                }
            }
        }

        private void ValidateCanonicalAlphaSet(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || !definitionsById.ContainsKey("species.human"))
            {
                return;
            }

            InjuryTypeDefinition first = definitionsById.Values.OfType<InjuryTypeDefinition>().OrderBy(definition => definition.Id, StringComparer.Ordinal).FirstOrDefault();
            if (!ReferenceEquals(first, this))
            {
                return;
            }

            string[] required =
            {
                "injury.blunt-trauma",
                "injury.bruise",
                "injury.laceration",
                "injury.puncture",
                "injury.penetrating",
                "injury.fracture",
                "injury.crush",
                "injury.burn",
                "injury.structural-rupture",
                "injury.severing",
                "injury.organ-trauma",
                "injury.core-damage",
                "injury.incorporeal-disruption"
            };

            foreach (string id in required)
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not InjuryTypeDefinition)
                {
                    report.AddError($"Canonical InjuryTypeDefinition '{id}' must be registered in the alpha definition catalog.");
                }
            }
        }
    }
}
