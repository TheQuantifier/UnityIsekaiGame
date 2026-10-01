using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Client;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI.Authentication
{
    [DisallowMultipleComponent]
    public sealed class AccountLoginScreenController : MonoBehaviour
    {
        private const string BackgroundResourcePath = "Login/backgroundimage";
        private static bool hooksInstalled;

        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly List<MonoBehaviour> disabledImmediateModePanels = new List<MonoBehaviour>();
        private LocalGameClient client;
        private PlayerInputReader input;
        private Canvas canvas;
        private RawImage background;
        private InputField usernameField;
        private InputField passwordField;
        private Button loginButton;
        private Button createAccountButton;
        private Text statusText;
        private Rect lastPixelRect;
        private bool enteringWorld;
        private float nextCanvasSuppressionAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHooks()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            hooksInstalled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHooks()
        {
            if (!hooksInstalled)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                hooksInstalled = true;
            }

            TryInstall();
        }

        private static void OnSceneLoaded(Scene _, LoadSceneMode __) => TryInstall();

        private static void TryInstall()
        {
            if (FindAnyObjectByType<AccountLoginScreenController>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            LocalGameClient localClient = FindAnyObjectByType<LocalGameClient>(FindObjectsInactive.Include);
            if (localClient == null || localClient.LocalPlayerActor != null)
            {
                return;
            }

            // A screen-space Canvas must be rooted on a RectTransform. Creating the object with a
            // plain Transform can leave runtime-built UI active but without a renderable canvas
            // geometry path on some player configurations.
            GameObject root = new GameObject(
                "Account Login Screen",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            root.SetActive(false);
            AccountLoginScreenController controller = root.AddComponent<AccountLoginScreenController>();
            controller.client = localClient;
            root.SetActive(true);
        }

        private void Awake()
        {
            client = client == null ? FindAnyObjectByType<LocalGameClient>(FindObjectsInactive.Include) : client;
            if (client == null)
            {
                Destroy(gameObject);
                return;
            }

            BuildInterface();
            HideGameplayCanvases();
            SuppressImmediateModePanels();
            input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            input?.SetMenuInputBlocked(this, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            client.StatusChanged += OnStatusChanged;
            client.AccountAuthenticationCompleted += OnAccountAuthenticationCompleted;
            client.LocalPlayerActorChanged += OnLocalPlayerActorChanged;
            ApplyStatus(client.Status);
        }

        private void Start()
        {
            if (usernameField != null)
            {
                usernameField.text = client.SuggestedAccountIdentifier;
                usernameField.Select();
                usernameField.ActivateInputField();
            }

            if (TryReadCommandLineValue("--capture-login-screen", out string screenshotPath))
            {
                StartCoroutine(CaptureLoginScreen(screenshotPath));
            }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextCanvasSuppressionAt)
            {
                HideGameplayCanvases();
                SuppressImmediateModePanels();
                nextCanvasSuppressionAt = Time.unscaledTime + 0.25f;
            }

            if (background == null || background.texture == null || canvas == null)
            {
                return;
            }

            Rect pixelRect = canvas.pixelRect;
            if (pixelRect == lastPixelRect)
            {
                return;
            }

            lastPixelRect = pixelRect;
            ApplyCoverCrop(background, background.texture, pixelRect.size);
        }

        private void OnDestroy()
        {
            if (client != null)
            {
                client.StatusChanged -= OnStatusChanged;
                client.AccountAuthenticationCompleted -= OnAccountAuthenticationCompleted;
                client.LocalPlayerActorChanged -= OnLocalPlayerActorChanged;
            }

            input?.SetMenuInputBlocked(this, false);
            RestoreGameplayCanvases();
            RestoreImmediateModePanels();
        }

        private void BuildInterface()
        {
            gameObject.layer = 5;
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
            canvas.targetDisplay = 0;
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
            GameUiTheme.ConfigureCanvas(canvas);

            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            background = CreateRawImage("Login Background", transform);
            Texture2D texture = Resources.Load<Texture2D>(BackgroundResourcePath);
            background.texture = texture;
            background.color = texture == null ? GameUiTheme.Backdrop : Color.white;
            Stretch(background.rectTransform);

            Image shade = CreateImage("Login Tint", transform, new Color(0.08f, 0.035f, 0.015f, 0.11f));
            Stretch(shade.rectTransform);

            RectTransform banner = CreateRect("Leather Login Banner", transform);
            banner.anchorMin = new Vector2(0.022f, 0f);
            banner.anchorMax = new Vector2(0.37f, 1f);
            banner.offsetMin = Vector2.zero;
            banner.offsetMax = Vector2.zero;
            Image bannerImage = banner.gameObject.AddComponent<Image>();
            bannerImage.color = new Color(0.17f, 0.075f, 0.03f, 0.97f);

            Image leftEdge = CreateImage("Leather Edge", banner, new Color(0.78f, 0.51f, 0.19f, 0.88f));
            RectTransform edgeRect = leftEdge.rectTransform;
            edgeRect.anchorMin = new Vector2(0f, 0f);
            edgeRect.anchorMax = new Vector2(0f, 1f);
            edgeRect.pivot = new Vector2(0f, 0.5f);
            edgeRect.sizeDelta = new Vector2(7f, 0f);
            edgeRect.anchoredPosition = Vector2.zero;

            Image innerLeather = CreateImage("Leather Inset", banner, new Color(0.31f, 0.16f, 0.07f, 0.9f));
            RectTransform innerRect = innerLeather.rectTransform;
            innerRect.anchorMin = new Vector2(0.06f, 0.08f);
            innerRect.anchorMax = new Vector2(0.94f, 0.92f);
            innerRect.offsetMin = Vector2.zero;
            innerRect.offsetMax = Vector2.zero;
            GameUiTheme.StylePanel(innerLeather, true);

            RectTransform form = CreateRect("Login Form", innerRect);
            form.anchorMin = new Vector2(0.1f, 0.5f);
            form.anchorMax = new Vector2(0.9f, 0.5f);
            form.pivot = new Vector2(0.5f, 0.5f);
            form.sizeDelta = new Vector2(0f, 300f);
            form.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup layout = form.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            Text title = CreateText("Login Title", form, "UNITY ISEKAI", 34, FontStyle.Bold, GameUiTheme.AccentBright);
            title.alignment = TextAnchor.MiddleCenter;
            AddLayout(title.gameObject, 44f);
            Text subtitle = CreateText("Login Subtitle", form, "ENTER THE WORLD", 16, FontStyle.Bold, GameUiTheme.TextMuted);
            subtitle.alignment = TextAnchor.MiddleCenter;
            AddLayout(subtitle.gameObject, 28f);

            usernameField = CreateInputField("Username Pill Input", form, "username or user ID...", false);
            passwordField = CreateInputField("Password Pill Input", form, "password...", true);
            usernameField.characterLimit = AccountAuthenticationProtocol.SecureUserIdLength;
            passwordField.characterLimit = LocalConnectionProtocol.MaximumPasswordLength;

            loginButton = CreateButton("Login Pill Button", form, "LOGIN", () => Submit(false), GameUiButtonTone.Neutral);
            createAccountButton = CreateButton("Register Account Pill Button", form, "CREATE ACCOUNT", () => Submit(true), GameUiButtonTone.Neutral);
            if (usernameField is ExplicitSubmitInputField usernameSubmit)
            {
                usernameSubmit.Submitted += InvokeLoginButton;
            }
            if (passwordField is ExplicitSubmitInputField passwordSubmit)
            {
                passwordSubmit.Submitted += InvokeLoginButton;
            }

            statusText = CreateText("Login Feedback", form, string.Empty, 15, FontStyle.Bold, GameUiTheme.TextMuted);
            statusText.alignment = TextAnchor.UpperCenter;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            AddLayout(statusText.gameObject, 42f);

            if (texture != null)
            {
                ApplyCoverCrop(background, texture, new Vector2(Screen.width, Screen.height));
            }
        }

        private void Submit(bool createAccount)
        {
            if (client == null || !client.IsConnected || enteringWorld)
            {
                return;
            }

            string username = usernameField.text?.Trim() ?? string.Empty;
            string password = passwordField.text ?? string.Empty;
            bool validUsername = username.Length >= LocalConnectionProtocol.MinimumAccountNameLength
                && username.Length <= LocalConnectionProtocol.MaximumAccountNameLength
                && LocalConnectionProtocol.IsValidIdentifier(username);
            bool validUserId = AccountAuthenticationProtocol.IsSecureUserId(username);
            if ((createAccount && !validUsername) || (!createAccount && !validUsername && !validUserId))
            {
                SetFeedback(
                    createAccount
                        ? $"Use {LocalConnectionProtocol.MinimumAccountNameLength}-{LocalConnectionProtocol.MaximumAccountNameLength} letters, numbers, periods, underscores, or hyphens."
                        : "Enter a valid username or user ID.",
                    GameUiTheme.Warning);
                return;
            }

            if (password.Length < LocalConnectionProtocol.MinimumPasswordLength
                || password.Length > LocalConnectionProtocol.MaximumPasswordLength)
            {
                SetFeedback(
                    $"Password must be {LocalConnectionProtocol.MinimumPasswordLength}-{LocalConnectionProtocol.MaximumPasswordLength} characters.",
                    GameUiTheme.Warning);
                return;
            }

            SetControlsInteractable(false);
            SetFeedback(createAccount ? "Creating account..." : "Verifying account...", GameUiTheme.TextMuted);
            if (!client.ConnectWithCredentials(username, password, createAccount))
            {
                passwordField.text = string.Empty;
                SetControlsInteractable(true);
            }
        }

        private void InvokeLoginButton()
        {
            if (loginButton != null && loginButton.IsActive() && loginButton.IsInteractable())
            {
                loginButton.onClick.Invoke();
            }
        }

        private void OnStatusChanged(LocalConnectionStatus status) => ApplyStatus(status);

        private void ApplyStatus(LocalConnectionStatus status)
        {
            switch (status.Phase)
            {
                case LocalConnectionPhase.Connecting:
                    SetControlsInteractable(false);
                    SetFeedback("Contacting the server...", GameUiTheme.TextMuted);
                    break;
                case LocalConnectionPhase.Connected:
                    enteringWorld = false;
                    SetControlsInteractable(true);
                    SetFeedback("App connection accepted. Enter your account credentials.", GameUiTheme.TextMuted);
                    break;
                case LocalConnectionPhase.Failed:
                    passwordField.text = string.Empty;
                    SetControlsInteractable(client != null && client.IsConnected);
                    SetFeedback(status.Message, GameUiTheme.Danger);
                    enteringWorld = false;
                    passwordField.Select();
                    passwordField.ActivateInputField();
                    break;
                case LocalConnectionPhase.Offline:
                    SetControlsInteractable(false);
                    if (!string.IsNullOrWhiteSpace(status.Message) && !status.Message.Contains("credentials"))
                    {
                        SetFeedback(status.Message, GameUiTheme.TextMuted);
                    }
                    break;
            }
        }

        private void OnAccountAuthenticationCompleted(AccountAuthenticationResponse response)
        {
            if (response.Succeeded)
            {
                enteringWorld = true;
                SetControlsInteractable(false);
                SetFeedback($"Welcome, {response.Username}. Preparing your character...", GameUiTheme.Success);
                return;
            }

            enteringWorld = false;
            passwordField.text = string.Empty;
            SetControlsInteractable(client != null && client.IsConnected);
            SetFeedback(response.Message, GameUiTheme.Danger);
            passwordField.Select();
            passwordField.ActivateInputField();
        }

        private void OnLocalPlayerActorChanged(NetworkPlayerActor actor)
        {
            if (actor == null || !actor.IsSpawned || !actor.HasIdentity)
            {
                return;
            }

            input?.SetMenuInputBlocked(this, false);
            RestoreGameplayCanvases();
            RestoreImmediateModePanels();
            Destroy(gameObject);
        }

        private void HideGameplayCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas candidate = canvases[index];
                if (candidate == null
                    || candidate == canvas
                    || candidate.transform.IsChildOf(transform)
                    || !candidate.enabled
                    || candidate.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                hiddenCanvases.Add(candidate);
                candidate.enabled = false;
            }
        }

        private void RestoreGameplayCanvases()
        {
            for (int index = 0; index < hiddenCanvases.Count; index++)
            {
                if (hiddenCanvases[index] != null)
                {
                    hiddenCanvases[index].enabled = true;
                }
            }

            hiddenCanvases.Clear();
        }

        private void SuppressImmediateModePanels()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour == this || !behaviour.enabled)
                {
                    continue;
                }

                MethodInfo onGui = behaviour.GetType().GetMethod(
                    "OnGUI",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (onGui == null)
                {
                    continue;
                }

                disabledImmediateModePanels.Add(behaviour);
                behaviour.enabled = false;
            }
        }

        private void RestoreImmediateModePanels()
        {
            for (int index = 0; index < disabledImmediateModePanels.Count; index++)
            {
                if (disabledImmediateModePanels[index] != null)
                {
                    disabledImmediateModePanels[index].enabled = true;
                }
            }

            disabledImmediateModePanels.Clear();
        }

        private void SetControlsInteractable(bool interactable)
        {
            if (usernameField != null) usernameField.interactable = interactable;
            if (passwordField != null) passwordField.interactable = interactable;
            if (loginButton != null) loginButton.interactable = interactable;
            if (createAccountButton != null) createAccountButton.interactable = interactable;
        }

        private void SetFeedback(string message, Color color)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message ?? string.Empty;
            statusText.color = color;
        }

        public static Rect CalculateCoverUv(Texture texture, Vector2 viewport)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0 || viewport.x <= 0f || viewport.y <= 0f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            float textureAspect = texture.width / (float)texture.height;
            float viewportAspect = viewport.x / viewport.y;
            if (viewportAspect > textureAspect)
            {
                // Keep the complete horizontal span. Crop equal horizontal bands from the
                // top and bottom so the background fills the viewport without distortion.
                float visibleHeight = textureAspect / viewportAspect;
                float verticalInset = (1f - visibleHeight) * 0.5f;
                return new Rect(0f, verticalInset, 1f, visibleHeight);
            }

            // Narrower aspect ratios cannot be filled without either letterboxing or trimming
            // the sides. Use a centered cover crop here; the image is never stretched.
            float visibleWidth = viewportAspect / textureAspect;
            return new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
        }

        private static void ApplyCoverCrop(RawImage image, Texture texture, Vector2 viewport)
        {
            image.uvRect = CalculateCoverUv(texture, viewport);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(string name, Transform parent)
        {
            RectTransform rect = CreateRect(name, parent);
            return rect.gameObject.AddComponent<RawImage>();
        }

        private static Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.supportRichText = false;
            return text;
        }

        private static InputField CreateInputField(string name, Transform parent, string placeholderValue, bool password)
        {
            Image backgroundImage = CreateImage(name, parent, GameUiTheme.SurfaceInset);
            RectTransform root = backgroundImage.rectTransform;
            AddLayout(root.gameObject, 30f);
            InputField field = root.gameObject.AddComponent<ExplicitSubmitInputField>();
            RectTransform textArea = CreateRect("Text Area", root);
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(16f, 3f);
            textArea.offsetMax = new Vector2(-16f, -3f);
            Text text = CreateText("Text", textArea, string.Empty, 15, FontStyle.Normal, GameUiTheme.TextPrimary);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleLeft;
            Text placeholder = CreateText("Placeholder", textArea, placeholderValue, 15, FontStyle.Italic, new Color(GameUiTheme.TextMuted.r, GameUiTheme.TextMuted.g, GameUiTheme.TextMuted.b, 0.62f));
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAnchor.MiddleLeft;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = backgroundImage;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.asteriskChar = '\u2022';
            GameUiTheme.StyleInputField(field);
            GameUiTheme.StylePillSurface(backgroundImage);
            backgroundImage.color = new Color(0.20f, 0.085f, 0.025f, 0.98f);
            ColorBlock fieldColors = field.colors;
            fieldColors.normalColor = Color.white;
            fieldColors.highlightedColor = new Color(1f, 0.94f, 0.82f, 1f);
            fieldColors.selectedColor = new Color(1f, 0.94f, 0.82f, 1f);
            fieldColors.pressedColor = Color.white;
            field.colors = fieldColors;
            return field;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Action clicked,
            GameUiButtonTone tone)
        {
            Image image = CreateImage(name, parent, GameUiTheme.PanelRaised);
            AddLayout(image.gameObject, 30f);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => clicked());
            Text text = CreateText("Label", image.transform, label, 15, FontStyle.Bold, GameUiTheme.AccentBright);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            GameUiTheme.StyleButton(button, tone);
            GameUiTheme.StylePillSurface(image);
            image.color = new Color(0.34f, 0.14f, 0.035f, 1f);
            ColorBlock buttonColors = button.colors;
            buttonColors.normalColor = Color.white;
            buttonColors.highlightedColor = new Color(1f, 0.92f, 0.75f, 1f);
            buttonColors.selectedColor = new Color(1f, 0.92f, 0.75f, 1f);
            buttonColors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            buttonColors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            button.colors = buttonColors;
            return button;
        }

        private static void AddLayout(GameObject target, float preferredHeight)
        {
            LayoutElement layout = target.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool TryReadCommandLineValue(string option, out string value)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string prefix = option + "=";
            for (int index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index] ?? string.Empty;
                if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = argument.Substring(prefix.Length);
                    return !string.IsNullOrWhiteSpace(value);
                }

                if (string.Equals(argument, option, StringComparison.OrdinalIgnoreCase)
                    && index + 1 < arguments.Length)
                {
                    value = arguments[index + 1];
                    return !string.IsNullOrWhiteSpace(value);
                }
            }

            value = string.Empty;
            return false;
        }

        private IEnumerator CaptureLoginScreen(string requestedPath)
        {
            // Player builds can need several rendered frames after the startup-scene transition
            // before the back buffer contains the newly-created overlay.
            yield return new WaitForSecondsRealtime(1f);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            string fullPath = Path.GetFullPath(requestedPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ScreenCapture.CaptureScreenshot(fullPath, 1);
            Debug.Log(
                $"[Login UI] Capturing login-screen validation image to '{fullPath}'. "
                + $"Active={gameObject.activeInHierarchy}, Canvas={canvas != null && canvas.enabled}, "
                + $"Children={transform.childCount}, Background={background != null && background.gameObject.activeInHierarchy}, "
                + $"Texture={background?.texture?.name ?? "missing"}, PixelRect={canvas?.pixelRect}.");
        }
    }
}
