using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityIsekaiGame.Presentation
{
    public enum GameUiTextRole
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

    public enum GameUiButtonTone
    {
        Neutral,
        Primary,
        Positive,
        Danger
    }

    /// <summary>
    /// Production visual language for the game's uGUI and immediate-mode interfaces.
    /// The warm timber, leather, parchment, and brass palette is intentionally centralized
    /// so every screen reads as part of the same medieval tavern-inspired interface.
    /// </summary>
    public static class GameUiTheme
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public const float SpacingSmall = 6f;
        public const float SpacingMedium = 12f;
        public const float SpacingLarge = 20f;
        public const float StandardControlHeight = 42f;

        public static readonly Color Backdrop = Hex("160D08", 0.97f);
        public static readonly Color Panel = Hex("2A1A10", 0.97f);
        public static readonly Color PanelRaised = Hex("432C19", 0.98f);
        public static readonly Color PanelLight = Hex("624326", 1f);
        public static readonly Color Border = Hex("9B7134", 0.95f);
        public static readonly Color Accent = Hex("D9AA4E", 1f);
        public static readonly Color AccentBright = Hex("F0CD78", 1f);
        public static readonly Color AccentSoft = Hex("765125", 1f);
        public static readonly Color Secondary = Hex("B9783B", 1f);
        public static readonly Color TextPrimary = Hex("F3E5C3", 1f);
        public static readonly Color TextMuted = Hex("C2A77A", 1f);
        public static readonly Color Success = Hex("91A65C", 1f);
        public static readonly Color Warning = Hex("E1A13A", 1f);
        public static readonly Color Danger = Hex("B9503E", 1f);

        private static readonly Color PrimaryButton = Hex("8A5A25", 1f);
        private static readonly Color PositiveButton = Hex("5D6735", 1f);
        private static readonly Color DangerButton = Hex("79352C", 1f);
        private static readonly Color DisabledButton = Hex("3B3028", 0.72f);

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
        private static Sprite roundedSprite;

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
            DrawSolidRect(rect, Backdrop);
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
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        public static void StyleText(Text text, GameUiTextRole role = GameUiTextRole.Body)
        {
            if (text == null)
            {
                return;
            }

            text.color = TextColor(role);
            switch (role)
            {
                case GameUiTextRole.Title:
                    text.fontSize = Mathf.Max(text.fontSize, 22);
                    text.fontStyle = FontStyle.Bold;
                    break;
                case GameUiTextRole.Heading:
                    text.fontSize = Mathf.Max(text.fontSize, 16);
                    text.fontStyle = FontStyle.Bold;
                    break;
                case GameUiTextRole.Feedback:
                case GameUiTextRole.Success:
                case GameUiTextRole.Warning:
                case GameUiTextRole.Danger:
                    text.fontStyle = FontStyle.Bold;
                    break;
            }
        }

        public static void StyleButton(Button button, GameUiButtonTone tone = GameUiButtonTone.Neutral)
        {
            if (button == null)
            {
                return;
            }

            Color baseColor = tone switch
            {
                GameUiButtonTone.Primary => PrimaryButton,
                GameUiButtonTone.Positive => PositiveButton,
                GameUiButtonTone.Danger => DangerButton,
                _ => PanelRaised
            };

            if (button.targetGraphic is Image image)
            {
                ApplyRoundedSurface(image);
                image.color = baseColor;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Lighten(baseColor, 0.16f);
            colors.pressedColor = Darken(baseColor, 0.14f);
            colors.selectedColor = Lighten(baseColor, 0.12f);
            colors.disabledColor = DisabledButton;
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
                ApplyRoundedSurface(image);
                image.color = raised ? PanelRaised : Panel;
            }
        }

        public static void StyleInputField(InputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            Image background = inputField.targetGraphic as Image ?? inputField.GetComponent<Image>();
            if (background != null)
            {
                ApplyRoundedSurface(background);
                background.color = Backdrop;
            }

            ColorBlock colors = inputField.colors;
            colors.normalColor = Backdrop;
            colors.highlightedColor = PanelRaised;
            colors.selectedColor = PanelRaised;
            colors.pressedColor = PanelRaised;
            colors.disabledColor = DisabledButton;
            colors.fadeDuration = 0.08f;
            inputField.colors = colors;
            inputField.customCaretColor = true;
            inputField.caretColor = AccentBright;
            inputField.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.42f);

            StyleText(inputField.textComponent, GameUiTextRole.Body);
            if (inputField.placeholder is Text placeholder)
            {
                StyleText(placeholder, GameUiTextRole.Muted);
            }
        }

        public static void StyleToggle(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            if (toggle.targetGraphic is Image background)
            {
                ApplyRoundedSurface(background);
                background.color = PanelRaised;
            }

            if (toggle.graphic is Image checkmark)
            {
                ApplyRoundedSurface(checkmark);
                checkmark.color = Accent;
            }

            Text label = toggle.GetComponentInChildren<Text>(true);
            StyleText(label, GameUiTextRole.Body);
        }

        public static void StyleScrollbar(Scrollbar scrollbar)
        {
            if (scrollbar == null)
            {
                return;
            }

            if (scrollbar.GetComponent<Image>() is Image track)
            {
                ApplyRoundedSurface(track);
                track.color = Backdrop;
            }

            if (scrollbar.targetGraphic is Image handle)
            {
                ApplyRoundedSurface(handle);
                handle.color = AccentSoft;
            }

            ColorBlock colors = scrollbar.colors;
            colors.normalColor = AccentSoft;
            colors.highlightedColor = Accent;
            colors.pressedColor = AccentBright;
            colors.selectedColor = Accent;
            colors.disabledColor = DisabledButton;
            colors.fadeDuration = 0.08f;
            scrollbar.colors = colors;
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

        public static GameUiTextRole InferTextRole(string objectName)
        {
            string value = objectName?.ToLowerInvariant() ?? string.Empty;
            if (value.Contains("title")) return GameUiTextRole.Title;
            if (value.Contains("heading") || value.Contains("header") || value.Contains("speaker")) return GameUiTextRole.Heading;
            if (value.Contains("error")) return GameUiTextRole.Danger;
            if (value.Contains("warning")) return GameUiTextRole.Warning;
            if (value.Contains("feedback") || value.Contains("status")) return GameUiTextRole.Feedback;
            if (value.Contains("hint") || value.Contains("description") || value.Contains("detail")) return GameUiTextRole.Muted;
            return GameUiTextRole.Body;
        }

        public static GameUiButtonTone InferButtonTone(string objectName)
        {
            string value = objectName?.ToLowerInvariant() ?? string.Empty;
            if (value.Contains("delete") || value.Contains("abandon") || value.Contains("dissolve") || value.Contains("remove")) return GameUiButtonTone.Danger;
            if (value.Contains("accept") || value.Contains("claim") || value.Contains("create") || value.Contains("save")) return GameUiButtonTone.Positive;
            if (value.Contains("equip") || value.Contains("use") || value.Contains("execute") || value.Contains("assign")) return GameUiButtonTone.Primary;
            return GameUiButtonTone.Neutral;
        }

        private static Color TextColor(GameUiTextRole role)
        {
            return role switch
            {
                GameUiTextRole.Muted => TextMuted,
                GameUiTextRole.Title => Accent,
                GameUiTextRole.Feedback => Secondary,
                GameUiTextRole.Success => Success,
                GameUiTextRole.Warning => Warning,
                GameUiTextRole.Danger => Danger,
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

        private static Color Darken(Color color, float amount)
        {
            return new Color(
                Mathf.Lerp(color.r, 0f, amount),
                Mathf.Lerp(color.g, 0f, amount),
                Mathf.Lerp(color.b, 0f, amount),
                color.a);
        }

        private static Color Hex(string rgb, float alpha)
        {
            return ColorUtility.TryParseHtmlString($"#{rgb}", out Color color)
                ? new Color(color.r, color.g, color.b, alpha)
                : Color.magenta;
        }

        private static void ApplyRoundedSurface(Image image)
        {
            Sprite sprite = RoundedSprite;
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }

        private static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite == null)
                {
                    roundedSprite = Resources.Load<Sprite>("Theme/GameUiRoundedSurface");
                    if (roundedSprite == null)
                    {
                        roundedSprite = CreateRoundedSprite();
                    }
                }

                return roundedSprite;
            }
        }

        private static Sprite CreateRoundedSprite()
        {
            const int size = 32;
            const float radius = 8f;
            const int samplesPerAxis = 4;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Game UI Rounded Surface",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int coveredSamples = 0;
                    for (int sampleY = 0; sampleY < samplesPerAxis; sampleY++)
                    {
                        for (int sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                        {
                            float pointX = x + (sampleX + 0.5f) / samplesPerAxis;
                            float pointY = y + (sampleY + 0.5f) / samplesPerAxis;
                            if (IsInsideRoundedRectangle(pointX, pointY, size, radius))
                            {
                                coveredSamples++;
                            }
                        }
                    }

                    float alpha = coveredSamples / (float)(samplesPerAxis * samplesPerAxis);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "Game UI Rounded Surface";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool IsInsideRoundedRectangle(float x, float y, float size, float radius)
        {
            float nearestX = Mathf.Clamp(x, radius, size - radius);
            float nearestY = Mathf.Clamp(y, radius, size - radius);
            float deltaX = x - nearestX;
            float deltaY = y - nearestY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
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
