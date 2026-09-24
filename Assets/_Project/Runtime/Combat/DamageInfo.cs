using System.Linq;
using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    public readonly struct DamageInfo
    {
        public DamageInfo(DamagePacket damagePacket, Vector3 hitPoint, Vector3 hitDirection)
        {
            DamagePacket = damagePacket;
            RawAmount = damagePacket.Components.Sum(component => Mathf.Max(0f, component.Amount));
            Source = damagePacket.Source;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
        }

        public float RawAmount { get; }
        public GameObject Source { get; }
        public Vector3 HitPoint { get; }
        public Vector3 HitDirection { get; }
        public DamagePacket DamagePacket { get; }
    }
}
