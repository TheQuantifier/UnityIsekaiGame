using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityIsekaiGame.Presentation
{
    public enum PrototypeUiTextRole
    {
        Body,
        Muted,
        Heading,
        Title,
        Feedback,
        Success,
        Warning,
        Danger
    }

    public enum PrototypeUiButtonTone
    {
        Neutral,
        Primary,
        Positive,
        Danger
    }

    /// <summary>
    /// Shared visual language for the prototype's uGUI and immediate-mode interfaces.
    /// Keeping the palette and interaction states here prevents each gameplay panel
    /// from slowly developing its own unrelated set of colors and spacing rules.
    /// </summary>
    public static class PrototypeUiTheme
    {
        public static readonly Color Backdrop = new Color(0.025f, 0.035f, 0.045f, 0.96f);
        public static readonly Color Panel = new Color(0.055f, 0.075f, 0.09f, 0.97f);
        public static readonly Color PanelRaised = new Color(0.085f, 0.11f, 0.13f, 0.98f);
        public static readonly Color Border = new Color(0.34f, 0.43f, 0.47f, 0.9f);
        public static readonly Color Accent = new Color(0.95f, 0.68f, 0.22f, 1f);
        public static readonly Color AccentSoft = new Color(0.36f, 0.27f, 0.13f, 1f);
        public static readonly Color Secondary = new Color(0.22f, 0.62f, 0.78f, 1f);
        public static readonly Color TextPrimary = new Color(0.94f, 0.96f, 0.97f, 1f);
        public static readonly Color TextMuted = new Color(0.67f, 0.73f, 0.76f, 1f);
        public static readonly Color Success = new Color(0.35f, 0.82f, 0.52f, 1f);
        public static readonly Color Warning = new Color(1f, 0.72f, 0.25f, 1f);
        public static readonly Color Danger = new Color(0.92f, 0.31f, 0.28f, 1f);

        private static GUISkin cachedSkin;
        private static GUIStyle windowStyle;
        private static GUIStyle cardStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle headingStyle;
        private static GUIStyle bodyStyle;
        private static GUIStyle mutedStyle;
        private static GUIStyle statusStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle primaryButtonStyle;
        private static GUIStyle dangerButtonStyle;
        private static GUIStyle centeredStyle;

        public static GUIStyle WindowStyle { get { EnsureGuiStyles(); return windowStyle; } }
        public static GUIStyle CardStyle { get { EnsureGuiStyles(); return cardStyle; } }
        public static GUIStyle TitleStyle { get { EnsureGuiStyles(); return titleStyle; } }
        public static GUIStyle HeadingStyle { get { EnsureGuiStyles(); return headingStyle; } }
        public static GUIStyle BodyStyle { get { EnsureGuiStyles(); return bodyStyle; } }
        public static GUIStyle MutedStyle { get { EnsureGuiStyles(); return mutedStyle; } }
        public static GUIStyle StatusStyle { get { EnsureGuiStyles(); return statusStyle; } }
        public static GUIStyle ButtonStyle { get { EnsureGuiStyles(); return buttonStyle; } }
        public static GUIStyle PrimaryButtonStyle { get { EnsureGuiStyles(); return primaryButtonStyle; } }
        public static GUIStyle DangerButtonStyle { get { EnsureGuiStyles(); return dangerButtonStyle; } }
        public static GUIStyle CenteredStyle { get { EnsureGuiStyles(); return centeredStyle; } }

        public static void DrawModalBackdrop()
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void DrawPanelFrame(Rect rect, bool modal = false)
        {
            if (modal)
            {
                DrawModalBackdrop();
            }

            DrawSolidRect(rect, Backdrop);
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 3f), Accent);
            DrawBorder(rect, Border, 1f);
        }

        public static void DrawProgress(Rect rect, float normalized, string label)
        {
            DrawSolidRect(rect, new Color(0.02f, 0.03f, 0.04f, 1f));
            Rect fill = new Rect(rect.x + 2f, rect.y + 2f, Mathf.Max(0f, (rect.width - 4f) * Mathf.Clamp01(normalized)), Mathf.Max(0f, rect.height - 4f));
            DrawSolidRect(fill, Secondary);
            DrawBorder(rect, Border, 1f);
            GUI.Label(rect, label ?? string.Empty, CenteredStyle);
        }

        public static void ConfigureCanvas(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                return;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        public static void StyleText(Text text, PrototypeUiTextRole role = PrototypeUiTextRole.Body)
        {
            if (text == null)
            {
                return;
            }

            text.color = TextColor(role);
            switch (role)
            {
                case PrototypeUiTextRole.Title:
                    text.fontSize = Mathf.Max(text.fontSize, 22);
                    text.fontStyle = FontStyle.Bold;
                    break;
                case PrototypeUiTextRole.Heading:
                    text.fontSize = Mathf.Max(text.fontSize, 16);
                    text.fontStyle = FontStyle.Bold;
                    break;
                case PrototypeUiTextRole.Feedback:
                case PrototypeUiTextRole.Success:
                case PrototypeUiTextRole.Warning:
                case PrototypeUiTextRole.Danger:
                    text.fontStyle = FontStyle.Bold;
                    break;
            }
        }

        public static void StyleButton(Button button, PrototypeUiButtonTone tone = PrototypeUiButtonTone.Neutral)
        {
            if (button == null)
            {
                return;
            }

            Color baseColor = tone switch
            {
                PrototypeUiButtonTone.Primary => new Color(0.18f, 0.43f, 0.54f, 1f),
                PrototypeUiButtonTone.Positive => new Color(0.16f, 0.42f, 0.27f, 1f),
                PrototypeUiButtonTone.Danger => new Color(0.48f, 0.16f, 0.15f, 1f),
                _ => PanelRaised
            };

            if (button.targetGraphic is Image image)
            {
                image.color = baseColor;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Lighten(baseColor, 0.14f);
            colors.pressedColor = Lighten(baseColor, 0.22f);
            colors.selectedColor = Lighten(baseColor, 0.12f);
            colors.disabledColor = new Color(0.14f, 0.16f, 0.17f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = TextPrimary;
                label.fontStyle = FontStyle.Bold;
            }
        }

        public static void StylePanel(Image image, bool raised = false)
        {
            if (image != null)
            {
                image.color = raised ? PanelRaised : Panel;
            }
        }

        public static void EnsureTextShadow(Text text, float distance = 1.5f)
        {
            if (text == null)
            {
                return;
            }

            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(distance, -distance);
            shadow.useGraphicAlpha = true;
        }

        public static PrototypeUiTextRole InferTextRole(string objectName)
        {
            string value = objectName?.ToLowerInvariant() ?? string.Empty;
            if (value.Contains("title")) return PrototypeUiTextRole.Title;
            if (value.Contains("heading") || value.Contains("header") || value.Contains("speaker")) return PrototypeUiTextRole.Heading;
            if (value.Contains("error")) return PrototypeUiTextRole.Danger;
            if (value.Contains("warning")) return PrototypeUiTextRole.Warning;
            if (value.Contains("feedback") || value.Contains("status")) return PrototypeUiTextRole.Feedback;
            if (value.Contains("hint") || value.Contains("description") || value.Contains("detail")) return PrototypeUiTextRole.Muted;
            return PrototypeUiTextRole.Body;
        }

        public static PrototypeUiButtonTone InferButtonTone(string objectName)
        {
            string value = objectName?.ToLowerInvariant() ?? string.Empty;
            if (value.Contains("delete") || value.Contains("abandon") || value.Contains("dissolve") || value.Contains("remove")) return PrototypeUiButtonTone.Danger;
            if (value.Contains("accept") || value.Contains("claim") || value.Contains("create") || value.Contains("save")) return PrototypeUiButtonTone.Positive;
            if (value.Contains("equip") || value.Contains("use") || value.Contains("execute") || value.Contains("assign")) return PrototypeUiButtonTone.Primary;
            return PrototypeUiButtonTone.Neutral;
        }

        private static Color TextColor(PrototypeUiTextRole role)
        {
            return role switch
            {
                PrototypeUiTextRole.Muted => TextMuted,
                PrototypeUiTextRole.Title => Accent,
                PrototypeUiTextRole.Feedback => Secondary,
                PrototypeUiTextRole.Success => Success,
                PrototypeUiTextRole.Warning => Warning,
                PrototypeUiTextRole.Danger => Danger,
                _ => TextPrimary
            };
        }

        private static Color Lighten(Color color, float amount)
        {
            return new Color(
                Mathf.Lerp(color.r, 1f, amount),
                Mathf.Lerp(color.g, 1f, amount),
                Mathf.Lerp(color.b, 1f, amount),
                color.a);
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void EnsureGuiStyles()
        {
            if (GUI.skin == null)
            {
                return;
            }

            if (cachedSkin == GUI.skin && windowStyle != null)
            {
                return;
            }

            cachedSkin = GUI.skin;
            windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(16, 16, 18, 14),
                normal = { textColor = TextPrimary }
            };
            cardStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(2, 2, 4, 4),
                normal = { textColor = TextPrimary }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Accent }
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = TextPrimary }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = TextPrimary }
            };
            mutedStyle = new GUIStyle(bodyStyle) { normal = { textColor = TextMuted } };
            statusStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 7, 7),
                normal = { textColor = Secondary }
            };
            buttonStyle = CreateButtonStyle(GUI.skin.button, TextPrimary);
            primaryButtonStyle = CreateButtonStyle(GUI.skin.button, Accent);
            dangerButtonStyle = CreateButtonStyle(GUI.skin.button, Danger);
            centeredStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        private static GUIStyle CreateButtonStyle(GUIStyle source, Color textColor)
        {
            GUIStyle style = new GUIStyle(source)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                padding = new RectOffset(10, 10, 7, 7),
                wordWrap = true
            };
            style.normal.textColor = textColor;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            return style;
        }
    }
}
