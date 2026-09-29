using System;
using UnityEngine;

namespace UnityIsekaiGame.Equipment
{
    [Serializable]
    public sealed class EquipmentViewData
    {
        [SerializeField] private GameObject firstPersonPrefab;
        [SerializeField] private Vector3 firstPersonLocalPosition;
        [SerializeField] private Vector3 firstPersonLocalEulerAngles;
        [SerializeField] private Vector3 firstPersonLocalScale = Vector3.one;

        public GameObject FirstPersonPrefab => firstPersonPrefab;
        public Vector3 FirstPersonLocalPosition => firstPersonLocalPosition;
        public Vector3 FirstPersonLocalEulerAngles => firstPersonLocalEulerAngles;
        public Vector3 FirstPersonLocalScale => firstPersonLocalScale == Vector3.zero ? Vector3.one : firstPersonLocalScale;
        public bool HasFirstPersonPrefab => firstPersonPrefab != null;
    }
}
