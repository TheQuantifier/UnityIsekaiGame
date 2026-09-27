using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    /// <summary>
    /// Applies the shared theme to authored and runtime-created screen-space UI.
    /// Components are styled once so screen-specific selected states remain in control.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeUiThemeApplicator : MonoBehaviour
    {
        private const float SafetyRefreshIntervalSeconds = 5f;

        private readonly HashSet<Button> styledButtons = new HashSet<Button>();
        private readonly HashSet<Text> styledTexts = new HashSet<Text>();
        private readonly HashSet<Image> styledPanels = new HashSet<Image>();
        private readonly List<Button> buttonBuffer = new List<Button>();
        private readonly List<Text> textBuffer = new List<Text>();
        private readonly List<Image> imageBuffer = new List<Image>();
        private Canvas targetCanvas;
        private float nextRefreshAt;
        private bool refreshRequested;

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

                PrototypeUiThemeApplicator applicator = canvas.GetComponent<PrototypeUiThemeApplicator>();
                if (applicator == null)
                {
                    applicator = canvas.gameObject.AddComponent<PrototypeUiThemeApplicator>();
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

            PrototypeUiTheme.ConfigureCanvas(targetCanvas);
            StyleNewButtons();
            StyleNewTexts();
            StyleNewPanels();
            refreshRequested = false;
            nextRefreshAt = Time.unscaledTime + SafetyRefreshIntervalSeconds;
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
                    PrototypeUiTheme.StyleButton(button, PrototypeUiTheme.InferButtonTone(button.name));
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
                    PrototypeUiTheme.StyleText(text, PrototypeUiTheme.InferTextRole(text.name));
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

                PrototypeUiTheme.StylePanel(image, image.name.ToLowerInvariant().Contains("detail"));
            }
        }

        private static bool IsPanelName(string objectName)
        {
            string value = objectName?.ToLowerInvariant() ?? string.Empty;
            return value.Contains("panel") || value.Contains("background") || value.EndsWith(" content");
        }
    }
}
