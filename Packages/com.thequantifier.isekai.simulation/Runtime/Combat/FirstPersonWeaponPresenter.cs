using System.Collections;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Combat
{
    public sealed class FirstPersonWeaponPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private PlayerMeleeCombat combat;
        [SerializeField] private GameObject weaponRoot;
        [SerializeField] private Transform swingRoot;
        [SerializeField] private Vector3 swingEulerOffset = new Vector3(25f, -20f, 10f);
        [SerializeField, Min(0.01f)] private float swingDuration = 0.18f;

        private Coroutine swingRoutine;
        private Quaternion restingRotation;
        private GameObject generatedWeaponView;
        private ItemDefinition visibleItem;

        private void Awake()
        {
            if (equipment == null)
            {
                equipment = GetComponentInParent<PlayerEquipment>();
            }

            if (combat == null)
            {
                combat = GetComponentInParent<PlayerMeleeCombat>();
            }

            if (swingRoot == null && weaponRoot != null)
            {
                swingRoot = weaponRoot.transform;
            }

            restingRotation = swingRoot == null ? Quaternion.identity : swingRoot.localRotation;
            RefreshVisibility();
        }

        private void OnEnable()
        {
            if (equipment != null)
            {
                equipment.EquipmentChanged += RefreshVisibility;
            }

            if (combat != null)
            {
                combat.AttackResolved += OnAttackResolved;
            }

            RefreshVisibility();
        }

        private void OnDisable()
        {
            if (equipment != null)
            {
                equipment.EquipmentChanged -= RefreshVisibility;
            }

            if (combat != null)
            {
                combat.AttackResolved -= OnAttackResolved;
            }

            ClearGeneratedWeaponView();
            visibleItem = null;
        }

        private void RefreshVisibility()
        {
            if (weaponRoot == null)
            {
                return;
            }

            EquipmentSlotState mainHand = equipment == null ? null : equipment.GetSlot(EquipmentSlotType.MainHand);
            ItemDefinition item = mainHand == null ? null : mainHand.Item;
            bool shouldShow = item != null
                && item.IsEquippable
                && item.Equipment.SlotType == EquipmentSlotType.MainHand
                && IsWeapon(item);

            if (!shouldShow)
            {
                ClearGeneratedWeaponView();
                visibleItem = null;
                weaponRoot.SetActive(false);
                return;
            }

            EquipmentViewData itemView = item.Equipment.View;
            bool generatedViewMissing = itemView != null && itemView.HasFirstPersonPrefab && generatedWeaponView == null;
            if (visibleItem != item || generatedViewMissing)
            {
                RebuildGeneratedWeaponView(item);
                visibleItem = item;
            }

            weaponRoot.SetActive(shouldShow);
        }

        private static bool IsWeapon(ItemDefinition item)
        {
            return (item.Equipment.MeleeWeapon != null && item.Equipment.MeleeWeapon.IsWeapon)
                || (item.Equipment.RangedWeapon != null && item.Equipment.RangedWeapon.IsWeapon);
        }

        private void RebuildGeneratedWeaponView(ItemDefinition item)
        {
            ClearGeneratedWeaponView();

            EquipmentViewData view = item.Equipment.View;
            if (view == null || !view.HasFirstPersonPrefab)
            {
                SetExistingWeaponViewEnabled(true);
                return;
            }

            SetExistingWeaponViewEnabled(false);
            generatedWeaponView = Instantiate(view.FirstPersonPrefab, weaponRoot.transform);
            generatedWeaponView.name = $"{item.DisplayName} View";
            Transform viewTransform = generatedWeaponView.transform;
            viewTransform.localPosition = view.FirstPersonLocalPosition;
            viewTransform.localRotation = Quaternion.Euler(view.FirstPersonLocalEulerAngles);
            viewTransform.localScale = CompensateForMountScale(view.FirstPersonLocalScale);
            RemoveRuntimePhysics(generatedWeaponView);
            FlattenSingleChildVisualRoot();
        }

        private void ClearGeneratedWeaponView()
        {
            if (generatedWeaponView == null)
            {
                return;
            }

            DestroyViewObject(generatedWeaponView);
            generatedWeaponView = null;
        }

        private static void RemoveRuntimePhysics(GameObject viewObject)
        {
            foreach (Collider collider in viewObject.GetComponentsInChildren<Collider>())
            {
                DestroyViewObject(collider);
            }

            foreach (Rigidbody rigidbody in viewObject.GetComponentsInChildren<Rigidbody>())
            {
                DestroyViewObject(rigidbody);
            }
        }

        private void FlattenSingleChildVisualRoot()
        {
            if (generatedWeaponView == null || weaponRoot == null)
            {
                return;
            }

            Transform root = generatedWeaponView.transform;
            if (root.childCount != 1 || HasRuntimeComponentsBeyondTransform(generatedWeaponView))
            {
                return;
            }

            Transform visual = root.GetChild(0);
            visual.SetParent(weaponRoot.transform, worldPositionStays: true);
            DestroyViewObject(generatedWeaponView);
            generatedWeaponView = visual.gameObject;
        }

        private static bool HasRuntimeComponentsBeyondTransform(GameObject target)
        {
            Component[] components = target == null ? null : target.GetComponents<Component>();
            if (components == null)
            {
                return false;
            }

            foreach (Component component in components)
            {
                if (component != null && component is not Transform)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DestroyViewObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private Vector3 CompensateForMountScale(Vector3 authoredScale)
        {
            Vector3 mountScale = weaponRoot.transform.localScale;
            return new Vector3(
                Mathf.Approximately(mountScale.x, 0f) ? authoredScale.x : authoredScale.x / mountScale.x,
                Mathf.Approximately(mountScale.y, 0f) ? authoredScale.y : authoredScale.y / mountScale.y,
                Mathf.Approximately(mountScale.z, 0f) ? authoredScale.z : authoredScale.z / mountScale.z);
        }

        private void SetExistingWeaponViewEnabled(bool enabled)
        {
            foreach (Renderer renderer in weaponRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (generatedWeaponView != null && renderer.transform.IsChildOf(generatedWeaponView.transform))
                {
                    continue;
                }

                renderer.enabled = enabled;
            }

            foreach (Collider collider in weaponRoot.GetComponentsInChildren<Collider>(true))
            {
                if (generatedWeaponView != null && collider.transform.IsChildOf(generatedWeaponView.transform))
                {
                    continue;
                }

                collider.enabled = enabled;
            }
        }

        private void OnAttackResolved(MeleeAttackResult result)
        {
            if (!result.Started || weaponRoot == null || !weaponRoot.activeInHierarchy || swingRoot == null)
            {
                return;
            }

            if (swingRoutine != null)
            {
                StopCoroutine(swingRoutine);
            }

            swingRoutine = StartCoroutine(Swing());
        }

        private IEnumerator Swing()
        {
            Quaternion swingRotation = restingRotation * Quaternion.Euler(swingEulerOffset);
            float halfDuration = swingDuration * 0.5f;

            for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
            {
                swingRoot.localRotation = Quaternion.Slerp(restingRotation, swingRotation, elapsed / halfDuration);
                yield return null;
            }

            for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
            {
                swingRoot.localRotation = Quaternion.Slerp(swingRotation, restingRotation, elapsed / halfDuration);
                yield return null;
            }

            swingRoot.localRotation = restingRotation;
            swingRoutine = null;
        }
    }
}
