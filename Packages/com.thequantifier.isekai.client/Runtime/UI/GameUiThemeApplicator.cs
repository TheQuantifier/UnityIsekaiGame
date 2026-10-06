using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    /// <summary>
    /// Prevents the automatic theme pass from overwriting deliberately styled child controls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameUiThemeOptOut : MonoBehaviour
    {
    }

    /// <summary>
    /// Applies the shared theme to authored and runtime-created screen-space UI.
    /// Components are styled once so screen-specific selected states remain in control.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameUiThemeApplicator : MonoBehaviour
    {
        private const float SafetyRefreshIntervalSeconds = 30f;

        private readonly HashSet<Button> styledButtons = new HashSet<Button>();
        private readonly HashSet<Text> styledTexts = new HashSet<Text>();
        private readonly HashSet<Image> styledPanels = new HashSet<Image>();
        private readonly HashSet<InputField> styledInputFields = new HashSet<InputField>();
        private readonly HashSet<Toggle> styledToggles = new HashSet<Toggle>();
        private readonly HashSet<Scrollbar> styledScrollbars = new HashSet<Scrollbar>();
        private readonly List<Button> buttonBuffer = new List<Button>();
        private readonly List<Text> textBuffer = new List<Text>();
        private readonly List<Image> imageBuffer = new List<Image>();
        private readonly List<InputField> inputFieldBuffer = new List<InputField>();
        private readonly List<Toggle> toggleBuffer = new List<Toggle>();
        private readonly List<Scrollbar> scrollbarBuffer = new List<Scrollbar>();
        private Canvas targetCanvas;
        private GameUiSafeArea safeArea;
        private float nextRefreshAt;
        private bool refreshRequested;
        private int lastScreenWidth;
        private int lastScreenHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHooks()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToAllCanvases();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToAllCanvases();
        }

        private static void ApplyToAllCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (FindRootScreenSpaceCanvas(canvas) != canvas)
                {
                    continue;
                }

                GameUiThemeApplicator applicator = canvas.GetComponent<GameUiThemeApplicator>();
                if (applicator == null)
                {
                    applicator = canvas.gameObject.AddComponent<GameUiThemeApplicator>();
                }
                applicator.Refresh();
            }
        }

        private void Awake()
        {
            targetCanvas = GetComponent<Canvas>();
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnTransformChildrenChanged()
        {
            refreshRequested = true;
        }

        private void LateUpdate()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                refreshRequested = true;
            }

            if (!refreshRequested && Time.unscaledTime < nextRefreshAt)
            {
                return;
            }

            Refresh();
        }

        public void RequestRefresh()
        {
            refreshRequested = true;
        }

        public void Refresh()
        {
            if (targetCanvas == null)
            {
                targetCanvas = GetComponent<Canvas>();
            }
            if (targetCanvas == null || targetCanvas.renderMode == RenderMode.WorldSpace)
            {
                return;
            }

            GameUiTheme.ConfigureCanvas(targetCanvas);
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            if (safeArea == null)
            {
                safeArea = GetComponent<GameUiSafeArea>();
                if (safeArea == null)
                {
                    safeArea = gameObject.AddComponent<GameUiSafeArea>();
                }
            }
            safeArea.ConfigureForCanvas(targetCanvas);
            StyleNewButtons();
            StyleNewTexts();
            StyleNewPanels();
            StyleNewInputFields();
            StyleNewToggles();
            StyleNewScrollbars();
            refreshRequested = false;
            nextRefreshAt = Time.unscaledTime + SafetyRefreshIntervalSeconds;
        }

        public static Canvas FindRootScreenSpaceCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            Canvas root = canvas;
            Transform current = canvas.transform.parent;
            while (current != null)
            {
                Canvas parentCanvas = current.GetComponent<Canvas>();
                if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)
                {
                    root = parentCanvas;
                }
                current = current.parent;
            }

            return root;
        }

        private void StyleNewButtons()
        {
            styledButtons.RemoveWhere(value => value == null);
            buttonBuffer.Clear();
            GetComponentsInChildren(true, buttonBuffer);
            for (int i = 0; i < buttonBuffer.Count; i++)
            {
                Button button = buttonBuffer[i];
                if (button != null && styledButtons.Add(button))
                {
                    GameUiTheme.StyleButton(button, GameUiTheme.InferButtonTone(button.name));
                    if (button.name.IndexOf("pill", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && button.targetGraphic is Image pillImage)
                    {
                        GameUiTheme.StylePillSurface(pillImage);
                    }
                }
            }
        }

        private void StyleNewTexts()
        {
            styledTexts.RemoveWhere(value => value == null);
            textBuffer.Clear();
            GetComponentsInChildren(true, textBuffer);
            for (int i = 0; i < textBuffer.Count; i++)
            {
                Text text = textBuffer[i];
                if (text != null && styledTexts.Add(text))
                {
                    GameUiTheme.StyleText(text, GameUiTheme.InferTextRole(text.name));
                }
            }
        }

        private void StyleNewPanels()
        {
            styledPanels.RemoveWhere(value => value == null);
            imageBuffer.Clear();
            GetComponentsInChildren(true, imageBuffer);
            for (int i = 0; i < imageBuffer.Count; i++)
            {
                Image image = imageBuffer[i];
                if (image == null || image.GetComponent<Button>() != null || image.sprite != null || !IsPanelName(image.name)
                    || !styledPanels.Add(image))
                {
                    continue;
                }

                GameUiTheme.StylePanel(image, image.name.ToLowerInvariant().Contains("detail"));
            }
        }

        private void StyleNewInputFields()
        {
            styledInputFields.RemoveWhere(value => value == null);
            inputFieldBuffer.Clear();
            GetComponentsInChildren(true, inputFieldBuffer);
            for (int i = 0; i < inputFieldBuffer.Count; i++)
            {
                InputField inputField = inputFieldBuffer[i];
                if (inputField != null && styledInputFields.Add(inputField))
                {
                    GameUiTheme.StyleInputField(inputField);
                    if (inputField.name.IndexOf("pill", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        GameUiTheme.StylePillSurface(inputField.targetGraphic as Image ?? inputField.GetComponent<Image>());
                    }
                }
            }
        }

        private void StyleNewToggles()
        {
            styledToggles.RemoveWhere(value => value == null);
            toggleBuffer.Clear();
            GetComponentsInChildren(true, toggleBuffer);
            for (int i = 0; i < toggleBuffer.Count; i++)
            {
                Toggle toggle = toggleBuffer[i];
                if (toggle != null
                    && toggle.GetComponentInParent<GameUiThemeOptOut>(true) == null
                    && styledToggles.Add(toggle))
                {
                    GameUiTheme.StyleToggle(toggle);
                }
            }
        }

        private void StyleNewScrollbars()
        {
            styledScrollbars.RemoveWhere(value => value == null);
            scrollbarBuffer.Clear();
            GetComponentsInChildren(true, scrollbarBuffer);
            for (int i = 0; i < scrollbarBuffer.Count; i++)
            {
                Scrollbar scrollbar = scrollbarBuffer[i];
                if (scrollbar != null
                    && scrollbar.GetComponentInParent<GameUiThemeOptOut>(true) == null
                    && styledScrollbars.Add(scrollbar))
                {
                    GameUiTheme.StyleScrollbar(scrollbar);
                }
            }
        }

        private static bool IsPanelName(string objectName)
        {
            string value = objectName?.ToLowerInvariant() ?? string.Empty;
            return value.Contains("panel") || value.Contains("background") || value.EndsWith(" content");
        }
    }
}
