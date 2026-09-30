using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    public readonly struct DamagePacket
    {
        private readonly IReadOnlyList<DamageComponent> components;

        public DamagePacket(GameObject source, UnityEngine.Object ability, UnityEngine.Object sourceItem, IReadOnlyList<DamageComponent> components)
        {
            Source = source;
            Ability = ability;
            SourceItem = sourceItem;
            this.components = components ?? Array.Empty<DamageComponent>();
        }

        public GameObject Source { get; }
        public UnityEngine.Object Ability { get; }
        public UnityEngine.Object SourceItem { get; }
        public IReadOnlyList<DamageComponent> Components => components ?? Array.Empty<DamageComponent>();
        public bool HasComponents => Components.Count > 0;

        public static DamagePacket Single(GameObject source, DamageComponent component)
        {
            return new DamagePacket(source, null, null, new[] { component });
        }
    }
}
