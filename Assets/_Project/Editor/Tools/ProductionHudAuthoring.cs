using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.UI;

namespace UnityIsekaiGame.Editor
{
    public static class ProductionHudAuthoring
    {
        public const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [MenuItem("Tools/Unity Isekai Game/UI/Bake Production HUD")]
        public static void BakePrototypeSceneMenu() => BakePrototypeScene();

        public static void BakePrototypeScene()
        {
            Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            Canvas hudCanvas = FindAll<Canvas>(scene).FirstOrDefault(value => value.name == "HUD Canvas");
            Canvas promptCanvas = FindAll<Canvas>(scene).FirstOrDefault(value => value.name == "Interaction Prompt Canvas");
            if (hudCanvas == null || promptCanvas == null)
            {
                throw new InvalidOperationException("The prototype scene is missing its HUD or interaction prompt canvas.");
            }

            ConfigureCanvas(hudCanvas);
            ConfigureCanvas(promptCanvas);
            BakeVitals(scene, hudCanvas.transform);
            BakeNotifications(hudCanvas.transform);
            BakeHotbar(scene, hudCanvas.transform);
            BakeQuestTracker(scene, hudCanvas.transform);
            BakeTargetFrame(scene, hudCanvas.transform);
            BakeReticle(hudCanvas.transform);
            BakeInteractionPrompt(promptCanvas.transform);

            hudCanvas.GetComponent<GameUiThemeApplicator>()?.Refresh();
            promptCanvas.GetComponent<GameUiThemeApplicator>()?.Refresh();
            hudCanvas.GetComponent<GameUiSafeArea>()?.CaptureCurrentLayout();
            promptCanvas.GetComponent<GameUiSafeArea>()?.CaptureCurrentLayout();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save the production HUD in '{PrototypeScenePath}'.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Baked the production HUD into '{PrototypeScenePath}'.");
        }

        private static void ConfigureCanvas(Canvas canvas)
        {
            GetOrAdd<GameUiThemeApplicator>(canvas.gameObject);
            GameUiSafeArea safeArea = GetOrAdd<GameUiSafeArea>(canvas.gameObject);
            GameUiTheme.ConfigureCanvas(canvas);
            safeArea.ConfigureForCanvas(canvas);
        }

        private static void BakeVitals(Scene scene, Transform canvas)
        {
            PlayerVitalsHudView view = FindAll<PlayerVitalsHudView>(scene).FirstOrDefault();
            if (view == null) throw new InvalidOperationException("The prototype scene is missing PlayerVitalsHudView.");
            GameObject root = view.gameObject;
            root.name = "Player Vitals";
            root.transform.SetParent(canvas, false);
            SetRect(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(360f, 178f));
            Text legacy = root.GetComponent<Text>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
            Image panel = GetOrAdd<Image>(root);
            StyleHudPanel(root.transform, panel, GameUiTheme.Danger);

            Text heading = EnsureText(root.transform, "Vitals Heading", "ADVENTURER", 16, TextAnchor.MiddleLeft);
            SetRect(heading.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, -8f), new Vector2(-28f, 28f));
            GameUiTheme.StyleText(heading, GameUiTextRole.Title);

            HudResourceBarView healthBar = EnsureResourceBar(root.transform, "Health Bar", -43f);
            HudResourceBarView staminaBar = EnsureResourceBar(root.transform, "Stamina Bar", -84f);
            HudResourceBarView manaBar = EnsureResourceBar(root.transform, "Mana Bar", -125f);
            Text defeated = EnsureText(root.transform, "Defeated Label", string.Empty, 14, TextAnchor.MiddleCenter);
            SetRect(defeated.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(14f, 5f), new Vector2(-28f, 24f));

            view.Configure(
                FindAll<PlayerHealth>(scene).FirstOrDefault(),
                FindAll<PlayerStamina>(scene).FirstOrDefault(),
                FindAll<PlayerMana>(scene).FirstOrDefault(),
                healthBar, staminaBar, manaBar, defeated);
        }

        private static HudResourceBarView EnsureResourceBar(Transform parent, string name, float y)
        {
            GameObject root = EnsureChild(parent, name);
            SetRect(root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, y), new Vector2(-28f, 35f));
            Image track = GetOrAdd<Image>(root);
            GameUiTheme.StylePanel(track);
            track.color = GameUiTheme.SurfaceInset;
            track.raycastTarget = false;

            Image fill = EnsureImage(root.transform, "Fill");
            SetRect(fill.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, -4f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;

            Text resourceName = EnsureText(root.transform, "Name", name.Replace(" Bar", string.Empty).ToUpperInvariant(), 13, TextAnchor.MiddleLeft);
            SetRect(resourceName.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, 0f));
            Text value = EnsureText(root.transform, "Value", "-- / --", 13, TextAnchor.MiddleRight);
            SetRect(value.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-8f, 0f), new Vector2(-16f, 0f));

            HudResourceBarView view = GetOrAdd<HudResourceBarView>(root);
            view.Configure(fill, resourceName, value);
            return view;
        }

        private static void BakeNotifications(Transform canvas)
        {
            GameHudNotificationView view = canvas.GetComponentInChildren<GameHudNotificationView>(true);
            if (view == null)
            {
                view = GetOrAdd<GameHudNotificationView>(EnsureChild(canvas, "HUD Notifications"));
            }
            GameObject root = view.gameObject;
            root.name = "HUD Notifications";
            root.transform.SetParent(canvas, false);
            SetRect(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(620f, 60f));
            CanvasGroup group = GetOrAdd<CanvasGroup>(root);
            Text label = MigrateRootText(root, "Message");
            Image panel = GetOrAdd<Image>(root);
            StyleHudPanel(root.transform, panel, GameUiTheme.Secondary);
            SetRect(label.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(-28f, 0f));
            label.text = string.Empty;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 18;
            Image accent = EnsureImage(root.transform, "Accent");
            SetRect(accent.gameObject, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(5f, 0f));
            view.Configure(group, panel, accent, label);
        }

        private static void BakeHotbar(Scene scene, Transform canvas)
        {
            SpellLoadoutHudView loadoutView = FindAll<SpellLoadoutHudView>(scene).FirstOrDefault();
            if (loadoutView == null) throw new InvalidOperationException("The prototype scene is missing SpellLoadoutHudView.");
            GameObject root = loadoutView.gameObject;
            root.transform.SetParent(canvas, false);
            SetRect(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(520f, 90f));
            Image panel = GetOrAdd<Image>(root);
            StyleHudPanel(root.transform, panel, GameUiTheme.Accent);

            SpellQuickSlotView[] slots = root.GetComponentsInChildren<SpellQuickSlotView>(true).OrderBy(value => value.name).ToArray();
            for (int i = 0; i < slots.Length; i++)
            {
                SpellQuickSlotView slot = slots[i];
                SetRect(slot.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 127f, 0f), new Vector2(116f, 70f));
                Image background = GetOrAdd<Image>(slot.gameObject);
                Image selected = EnsureImage(slot.transform, "Selected Frame");
                SetRect(selected.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f));
                Text shortcut = EnsureText(slot.transform, "Shortcut", (i + 1).ToString(), 17, TextAnchor.UpperLeft);
                SetRect(shortcut.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, -4f), new Vector2(-16f, -8f));
                Text spellName = EnsureText(slot.transform, "Spell Name", "Empty", 14, TextAnchor.MiddleCenter);
                SetRect(spellName.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(-20f, -16f));
                Text cost = EnsureText(slot.transform, "Cost", string.Empty, 11, TextAnchor.LowerCenter);
                SetRect(cost.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(-16f, -10f));
                slot.Configure(background, selected, shortcut, spellName, cost);
            }
            loadoutView.Configure(FindAll<PlayerSpellLoadout>(scene).FirstOrDefault(), slots);
        }

        private static void BakeQuestTracker(Scene scene, Transform canvas)
        {
            GameObject root = EnsureChild(canvas, "Quest Tracker");
            SetRect(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(360f, 132f));
            CanvasGroup group = GetOrAdd<CanvasGroup>(root);
            Image panel = GetOrAdd<Image>(root);
            StyleHudPanel(root.transform, panel, GameUiTheme.Accent);
            Text title = EnsureText(root.transform, "Quest Title", "QUEST", 18, TextAnchor.UpperLeft);
            SetRect(title.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(14f, -10f), new Vector2(-28f, -20f));
            Text objective = EnsureText(root.transform, "Quest Objective", string.Empty, 14, TextAnchor.UpperLeft);
            SetRect(objective.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(14f, -46f), new Vector2(-28f, -58f));
            GetOrAdd<QuestTrackerHudView>(root).Configure(FindAll<PrototypePersistenceServiceBehaviour>(scene).FirstOrDefault(), group, panel, title, objective);
        }

        private static void BakeTargetFrame(Scene scene, Transform canvas)
        {
            GameObject root = EnsureChild(canvas, "Combat Target");
            SetRect(root, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 76f));
            CanvasGroup group = GetOrAdd<CanvasGroup>(root);
            Image panel = GetOrAdd<Image>(root);
            StyleHudPanel(root.transform, panel, GameUiTheme.Danger);
            Text targetName = EnsureText(root.transform, "Target Name", "TARGET", 16, TextAnchor.UpperCenter);
            SetRect(targetName.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(-24f, -2f));
            Image track = EnsureImage(root.transform, "Health Track");
            SetRect(track.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0f), new Vector2(14f, 10f), new Vector2(-28f, -2f));
            GameUiTheme.StylePanel(track);
            track.color = GameUiTheme.SurfaceInset;
            Image fill = EnsureImage(track.transform, "Fill");
            SetRect(fill.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(2f, 2f), new Vector2(-4f, -4f));
            Text value = EnsureText(track.transform, "Health Value", string.Empty, 12, TextAnchor.MiddleCenter);
            SetRect(value.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            PlayerHealth player = FindAll<PlayerHealth>(scene).FirstOrDefault();
            GetOrAdd<CombatTargetHudView>(root).Configure(Camera.main, player == null ? null : player.transform.root, group, panel, fill, targetName, value);
        }

        private static void BakeReticle(Transform canvas)
        {
            Transform reticle = FindDescendant(canvas, "Spell Aim Reticle");
            Text label = reticle == null ? null : reticle.GetComponent<Text>();
            if (label == null) return;
            label.text = "+";
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = GameUiTheme.AccentBright;
            GameUiTheme.EnsureTextShadow(label, 1f);
        }

        private static void BakeInteractionPrompt(Transform canvas)
        {
            InteractionPromptView view = canvas.GetComponentInChildren<InteractionPromptView>(true);
            if (view == null) throw new InvalidOperationException("The prototype scene is missing InteractionPromptView.");
            GameObject root = view.gameObject;
            SetRect(root, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(570f, 62f));
            CanvasGroup group = GetOrAdd<CanvasGroup>(root);
            Text prompt = MigrateRootText(root, "Prompt Text");
            Image panel = GetOrAdd<Image>(root);
            StyleHudPanel(root.transform, panel, GameUiTheme.Accent);
            SetRect(prompt.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(-100f, 0f));
            prompt.alignment = TextAnchor.MiddleCenter;
            prompt.fontSize = 17;
            GameObject badge = EnsureChild(root.transform, "Input Badge");
            SetRect(badge, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(64f, -16f));
            Image badgeImage = GetOrAdd<Image>(badge);
            GameUiTheme.StylePanel(badgeImage, raised: true);
            badgeImage.color = GameUiTheme.AccentSoft;
            Transform existingKey = FindDirectChild(root.transform, "Input Hint");
            if (existingKey != null) existingKey.SetParent(badge.transform, false);
            Text key = EnsureText(badge.transform, "Input Hint", "[E]", 18, TextAnchor.MiddleCenter);
            SetRect(key.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            view.Configure(group, panel, key, prompt);
        }

        private static void StyleHudPanel(Transform root, Image panel, Color accent)
        {
            GameUiTheme.StylePanel(panel, raised: true);
            Image band = EnsureImage(root, "Top Accent");
            SetRect(band.gameObject, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f));
            band.color = accent;
            band.raycastTarget = false;
            band.transform.SetAsLastSibling();
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T GetOrAdd<T>(GameObject target) where T : Component => target.GetComponent<T>() ?? target.AddComponent<T>();

        private static GameObject EnsureChild(Transform parent, string name)
        {
            Transform existing = FindDirectChild(parent, name);
            if (existing != null) return existing.gameObject;
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Image EnsureImage(Transform parent, string name) => GetOrAdd<Image>(EnsureChild(parent, name));

        private static Text EnsureText(Transform parent, string name, string content, int size, TextAnchor alignment)
        {
            Text text = GetOrAdd<Text>(EnsureChild(parent, name));
            text.font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Text MigrateRootText(GameObject root, string childName)
        {
            Text existingChild = FindDirectChild(root.transform, childName)?.GetComponent<Text>();
            Text rootText = root.GetComponent<Text>();
            if (existingChild != null)
            {
                if (rootText != null) UnityEngine.Object.DestroyImmediate(rootText);
                return existingChild;
            }

            GameObject child = EnsureChild(root.transform, childName);
            Text migrated = GetOrAdd<Text>(child);
            if (rootText != null)
            {
                EditorUtility.CopySerialized(rootText, migrated);
                UnityEngine.Object.DestroyImmediate(rootText);
            }
            migrated.font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            migrated.raycastTarget = false;
            return migrated;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++) if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDescendant(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetRect(GameObject target, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 sizeDelta)
        {
            RectTransform rect = GetOrAdd<RectTransform>(target);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
        }
    }
}
