using System.Collections.Generic;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Requirements
{
    public sealed class RequirementEvaluationContext
    {
        public CharacterAttributes Attributes { get; set; }
        public CalculatedStatCollection CalculatedStats { get; set; }
        public CharacterResourceCollection Resources { get; set; }
        public CharacterSkillCollection Skills { get; set; }
        public CharacterTraitCollection Traits { get; set; }
        public PlayerIdentityProgression Identity { get; set; }
        public PlayerInventory Inventory { get; set; }
        public PlayerEquipment Equipment { get; set; }
        public StatusEffectController Statuses { get; set; }
        public HashSet<string> OwnedAbilityIds { get; } = new HashSet<string>();
        public Dictionary<string, string> ContextIds { get; } = new Dictionary<string, string>();
        public bool TestLabDiagnostics { get; set; }
    }
}
