using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.UI.Development
{
    internal sealed class VisualGameplayCaptureHarness : MonoBehaviour
    {
        private const string EnableFlag = "--visual-audit-capture";
        private const string OutputFlag = "--visual-audit-output";

        private string outputDirectory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (!Array.Exists(arguments, value => string.Equals(value, EnableFlag, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var root = new GameObject(nameof(VisualGameplayCaptureHarness));
            DontDestroyOnLoad(root);
            root.AddComponent<VisualGameplayCaptureHarness>();
        }

        private void Awake()
        {
            outputDirectory = ReadArgument(OutputFlag);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.Combine(Application.persistentDataPath, "VisualAudit");
            }

            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            StartCoroutine(CaptureSupportedViews());
        }

        private IEnumerator CaptureSupportedViews()
        {
            InventoryScreenController controller = null;
            InventoryScreenView view = null;
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((controller == null || view == null) && Time.realtimeSinceStartup < deadline)
            {
                controller = FindAnyObjectByType<InventoryScreenController>(FindObjectsInactive.Include);
                view = FindAnyObjectByType<InventoryScreenView>(FindObjectsInactive.Include);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("hud-world");

            if (controller == null || view == null)
            {
                Debug.LogError("[Visual Audit] Inventory runtime UI was not available for capture.", this);
                yield break;
            }

            deadline = Time.realtimeSinceStartup + 20f;
            while (!HasPopulatedInventory(controller) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!HasPopulatedInventory(controller))
            {
                Debug.LogError("[Visual Audit] Authoritative inventory did not populate before the capture deadline.", this);
                yield break;
            }

            Invoke(controller, "SetOpen", true);
            Invoke(view, "ShowInventorySection");
            Invoke(controller, "Refresh");
            int populatedSlotIndex = FindPopulatedSlot(controller, requireEquippable: false);
            Invoke(controller, "SelectSlot", populatedSlotIndex);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("inventory-item-details");

            int equippableSlotIndex = FindPopulatedSlot(controller, requireEquippable: true);
            Invoke(controller, "SelectSlot", equippableSlotIndex >= 0 ? equippableSlotIndex : populatedSlotIndex);
            Invoke(controller, "HoverPrimaryAction", true);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("inventory-equipment-comparison");
            Invoke(controller, "HoverPrimaryAction", false);

            yield return CaptureMenu(view, "ShowCharacterSection", "menu-character");
            yield return CaptureMenu(view, "ShowSpellsSection", "menu-spells");
            yield return CaptureMenu(view, "ShowContractsSection", "menu-contracts");
            yield return CaptureMenu(view, "ShowSaveLoadSection", "menu-save-load");
            yield return CaptureMenu(view, "ShowInventorySection", "menu-inventory-return");

            Debug.Log($"[Visual Audit] Captured supported runtime views at {Screen.width}x{Screen.height} in '{outputDirectory}'.", this);
        }

        private static bool HasPopulatedInventory(InventoryScreenController controller)
        {
            return FindPopulatedSlot(controller, requireEquippable: false) >= 0;
        }

        private static int FindPopulatedSlot(InventoryScreenController controller, bool requireEquippable)
        {
            if (controller?.Inventory?.Slots == null)
            {
                return -1;
            }

            for (int i = 0; i < controller.Inventory.Slots.Count; i++)
            {
                var slot = controller.Inventory.Slots[i];
                if (slot == null || slot.IsEmpty || slot.Item == null)
                {
                    continue;
                }

                if (!requireEquippable || slot.Item.IsEquippable)
                {
                    return i;
                }
            }

            return -1;
        }

        private IEnumerator CaptureMenu(InventoryScreenView view, string methodName, string fileName)
        {
            Invoke(view, methodName);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture(fileName);
        }

        private IEnumerator Capture(string fileName)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outputDirectory, $"{fileName}-{Screen.width}x{Screen.height}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            method.Invoke(target, arguments);
        }

        private static string ReadArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[i + 1];
                }
            }

            return string.Empty;
        }
    }
}
