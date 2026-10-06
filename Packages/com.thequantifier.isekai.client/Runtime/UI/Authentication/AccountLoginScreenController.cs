using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        private const string BackgroundResourcePath = "Login/backgroundimage-v2";
        private const string CrestResourcePath = "Login/isekai-reality-crest";
        private const string MedievalTitleFontResourcePath = "Login/Fonts/CinzelDecorative-Bold";
        private const string MedievalSubtitleFontResourcePath = "Login/Fonts/CinzelDecorative-Regular";
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
        private Button settingsButton;
        private GameObject settingsOverlay;
        private GameObject graphicsSettingsPage;
        private GameObject gameSettingsPage;
        private Button applyDisplayButton;
        private Button graphicsTabButton;
        private Button gameTabButton;
        private Dropdown resolutionDropdown;
        private Dropdown displayModeDropdown;
        private Text statusText;
        private readonly List<GameDisplayResolution> displayedResolutions = new List<GameDisplayResolution>();
        private Rect lastPixelRect;
        private bool enteringWorld;
        private bool submitAfterReconnect;
        private bool reconnectCreateAccount;
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

            if (TryReadCommandLineValue("--capture-login-settings-dropdown", out string dropdownScreenshotPath))
            {
                OpenSettings();
                StartCoroutine(CaptureLoginDropdown(dropdownScreenshotPath));
            }
            else if (TryReadCommandLineValue("--capture-login-game-settings", out string gameSettingsScreenshotPath))
            {
                OpenSettings();
                ShowSettingsPage(false);
                StartCoroutine(CaptureLoginScreen(gameSettingsScreenshotPath));
            }
            else if (TryReadCommandLineValue("--capture-login-settings", out string settingsScreenshotPath))
            {
                OpenSettings();
                StartCoroutine(CaptureLoginScreen(settingsScreenshotPath));
            }
            else if (TryReadCommandLineValue("--capture-login-screen", out string screenshotPath))
            {
                StartCoroutine(CaptureLoginScreen(screenshotPath));
            }
        }

        private void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ToggleSettings();
            }

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
            // Unity's runtime Dropdown creates its popup canvas at sorting order 30000.
            // Keep the login UI beneath that layer so option lists render above this modal.
            canvas.sortingOrder = 20000;
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
            banner.anchorMin = new Vector2(0.024f, 0f);
            banner.anchorMax = new Vector2(0.382f, 1f);
            banner.offsetMin = Vector2.zero;
            banner.offsetMax = Vector2.zero;
            LoginOrnamentGraphic bannerGraphic = banner.gameObject.AddComponent<LoginOrnamentGraphic>();
            bannerGraphic.Configure(LoginOrnamentGraphic.OrnamentStyle.Leather);

            Image leftEdge = CreateImage("Leather Edge", banner, new Color(0.78f, 0.51f, 0.19f, 0.88f));
            RectTransform edgeRect = leftEdge.rectTransform;
            edgeRect.anchorMin = new Vector2(0f, 0f);
            edgeRect.anchorMax = new Vector2(0f, 1f);
            edgeRect.pivot = new Vector2(0f, 0.5f);
            edgeRect.sizeDelta = new Vector2(5f, 0f);
            edgeRect.anchoredPosition = Vector2.zero;

            RectTransform innerRect = CreateRect("Ornate Gold Frame", banner);
            innerRect.anchorMin = new Vector2(0.055f, 0.045f);
            innerRect.anchorMax = new Vector2(0.945f, 0.955f);
            innerRect.offsetMin = Vector2.zero;
            innerRect.offsetMax = Vector2.zero;
            LoginOrnamentGraphic frame = innerRect.gameObject.AddComponent<LoginOrnamentGraphic>();
            frame.Configure(LoginOrnamentGraphic.OrnamentStyle.Frame);

            RectTransform form = CreateRect("Login Form", innerRect);
            form.anchorMin = new Vector2(0.08f, 0.04f);
            form.anchorMax = new Vector2(0.92f, 0.96f);
            form.offsetMin = Vector2.zero;
            form.offsetMax = Vector2.zero;

            RawImage crest = CreateRawImage("Isekai Reality Crest", form);
            crest.texture = Resources.Load<Texture2D>(CrestResourcePath);
            crest.color = crest.texture == null ? Color.clear : Color.white;
            crest.rectTransform.anchorMin = new Vector2(0.5f, 0.855f);
            crest.rectTransform.anchorMax = new Vector2(0.5f, 0.855f);
            crest.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crest.rectTransform.sizeDelta = new Vector2(165f, 165f);
            crest.rectTransform.anchoredPosition = Vector2.zero;
            crest.raycastTarget = false;

            Text title = CreateText("Login Title", form, "ISEKAI REALITY", 43, FontStyle.Bold, GameUiTheme.AccentBright);
            title.font = LoadLoginFont(MedievalTitleFontResourcePath, title.font);
            title.fontStyle = FontStyle.Normal;
            title.alignment = TextAnchor.MiddleCenter;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 27;
            title.resizeTextMaxSize = 43;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(title.rectTransform, new Vector2(0.02f, 0.655f), new Vector2(0.98f, 0.755f));
            title.gameObject.AddComponent<LoginEmbossedTextEffect>().Configure(3.5f, 0.9f, 4, 0.045f);

            Text subtitle = CreateText("Login Subtitle", form, "E N T E R   T H E   W O R L D", 14, FontStyle.Normal, GameUiTheme.AccentBright);
            subtitle.font = LoadLoginFont(MedievalSubtitleFontResourcePath, subtitle.font);
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.resizeTextForBestFit = true;
            subtitle.resizeTextMinSize = 10;
            subtitle.resizeTextMaxSize = 14;
            Place(subtitle.rectTransform, new Vector2(0.08f, 0.605f), new Vector2(0.92f, 0.655f));
            subtitle.gameObject.AddComponent<LoginEmbossedTextEffect>().Configure(1.75f, 0.5f, 2, 0.055f);

            CreateSeparator("Title Separator", form, new Vector2(0.08f, 0.575f), new Vector2(0.92f, 0.605f));

            usernameField = CreateInputField("Username Input", form, "Username or user ID", false);
            Place(usernameField.GetComponent<RectTransform>(), new Vector2(0.04f, 0.485f), new Vector2(0.96f, 0.555f));
            passwordField = CreateInputField("Password Input", form, "Password", true);
            Place(passwordField.GetComponent<RectTransform>(), new Vector2(0.04f, 0.385f), new Vector2(0.96f, 0.455f));
            usernameField.characterLimit = AccountAuthenticationProtocol.SecureUserIdLength;
            passwordField.characterLimit = LocalConnectionProtocol.MaximumPasswordLength;

            loginButton = CreateButton("Login Button", form, "LOGIN", () => Submit(false), GameUiButtonTone.Primary);
            ApplyMedievalButtonFont(loginButton);
            Place(loginButton.GetComponent<RectTransform>(), new Vector2(0.04f, 0.275f), new Vector2(0.96f, 0.35f));
            createAccountButton = CreateButton("Register Account Button", form, "CREATE ACCOUNT", () => Submit(true), GameUiButtonTone.Neutral);
            ApplyMedievalButtonFont(createAccountButton);
            Place(createAccountButton.GetComponent<RectTransform>(), new Vector2(0.04f, 0.185f), new Vector2(0.96f, 0.25f));
            if (usernameField is ExplicitSubmitInputField usernameSubmit)
            {
                usernameSubmit.Submitted += FocusPasswordField;
            }
            if (passwordField is ExplicitSubmitInputField passwordSubmit)
            {
                passwordSubmit.Submitted += InvokeLoginButton;
            }

            CreateSeparator("Status Separator", form, new Vector2(0.08f, 0.125f), new Vector2(0.92f, 0.155f));

            statusText = CreateText("Login Feedback", form, string.Empty, 14, FontStyle.Normal, GameUiTheme.TextMuted);
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Truncate;
            Place(statusText.rectTransform, new Vector2(0.07f, 0.025f), new Vector2(0.93f, 0.12f));

            if (texture != null)
            {
                ApplyCoverCrop(background, texture, new Vector2(Screen.width, Screen.height));
            }

            BuildSettingsInterface();
        }

        private void BuildSettingsInterface()
        {
            settingsButton = CreateIconButton("Display Settings Button", transform, OpenSettings);
            RectTransform settingsButtonRect = settingsButton.GetComponent<RectTransform>();
            settingsButtonRect.anchorMin = Vector2.one;
            settingsButtonRect.anchorMax = Vector2.one;
            settingsButtonRect.pivot = Vector2.one;
            settingsButtonRect.anchoredPosition = new Vector2(-26f, -26f);
            settingsButtonRect.sizeDelta = new Vector2(62f, 62f);

            settingsOverlay = CreateRect("Display Settings Overlay", transform).gameObject;
            RectTransform overlayRect = settingsOverlay.GetComponent<RectTransform>();
            Stretch(overlayRect);
            Image overlayShade = settingsOverlay.AddComponent<Image>();
            overlayShade.color = new Color(0.025f, 0.012f, 0.006f, 0.70f);
            overlayShade.raycastTarget = true;

            RectTransform panel = CreateRect("Settings Leather Banner", settingsOverlay.transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(720f, 520f);
            panel.anchoredPosition = Vector2.zero;
            LoginOrnamentGraphic leather = panel.gameObject.AddComponent<LoginOrnamentGraphic>();
            leather.Configure(LoginOrnamentGraphic.OrnamentStyle.Leather, true);

            RectTransform frameRect = CreateRect("Settings Ornate Frame", panel);
            Place(frameRect, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f));
            LoginOrnamentGraphic frame = frameRect.gameObject.AddComponent<LoginOrnamentGraphic>();
            frame.Configure(LoginOrnamentGraphic.OrnamentStyle.Frame);

            Text heading = CreateText("Settings Heading", frameRect, "SETTINGS", 31, FontStyle.Bold, GameUiTheme.AccentBright);
            heading.alignment = TextAnchor.MiddleCenter;
            Place(heading.rectTransform, new Vector2(0.08f, 0.845f), new Vector2(0.92f, 0.95f));
            GameUiTheme.EnsureTextShadow(heading, 1.6f);

            CreateSeparator("Settings Header Separator", frameRect, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.845f));

            gameTabButton = CreateSettingsTab(
                "Game Settings Group",
                frameRect,
                "GAME",
                LoginOrnamentGraphic.OrnamentStyle.ControllerIcon,
                0.40f,
                () => ShowSettingsPage(false));
            graphicsTabButton = CreateSettingsTab(
                "Graphics Settings Group",
                frameRect,
                "GRAPHICS",
                LoginOrnamentGraphic.OrnamentStyle.GearIcon,
                0.60f,
                () => ShowSettingsPage(true));

            RectTransform options = CreateRect("Graphics Options", frameRect);
            graphicsSettingsPage = options.gameObject;
            Place(options, new Vector2(0.12f, 0.155f), new Vector2(0.88f, 0.615f));

            Text resolutionLabel = CreateText("Window Size Label", options, "WINDOW SIZE", 16, FontStyle.Bold, GameUiTheme.AccentBright);
            resolutionLabel.alignment = TextAnchor.MiddleLeft;
            Place(resolutionLabel.rectTransform, new Vector2(0f, 0.72f), new Vector2(0.35f, 0.94f));
            resolutionDropdown = CreateDropdown("Window Size Dropdown", options);
            Place(resolutionDropdown.GetComponent<RectTransform>(), new Vector2(0.38f, 0.72f), new Vector2(1f, 0.94f));

            Text modeLabel = CreateText("Fullscreen Mode Label", options, "FULLSCREEN MODE", 16, FontStyle.Bold, GameUiTheme.AccentBright);
            modeLabel.alignment = TextAnchor.MiddleLeft;
            Place(modeLabel.rectTransform, new Vector2(0f, 0.37f), new Vector2(0.35f, 0.59f));
            displayModeDropdown = CreateDropdown("Fullscreen Mode Dropdown", options);
            Place(displayModeDropdown.GetComponent<RectTransform>(), new Vector2(0.38f, 0.37f), new Vector2(1f, 0.59f));
            displayModeDropdown.AddOptions(new List<string> { "Fullscreen", "Borderless", "Windowed" });

            Text hint = CreateText(
                "Settings Hint",
                options,
                "Borderless fills the current monitor and is recommended for fast multitasking.",
                13,
                FontStyle.Normal,
                GameUiTheme.TextMuted);
            hint.alignment = TextAnchor.MiddleCenter;
            Place(hint.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.22f));

            RectTransform gamePage = CreateRect("Game Options", frameRect);
            gameSettingsPage = gamePage.gameObject;
            Place(gamePage, new Vector2(0.12f, 0.155f), new Vector2(0.88f, 0.615f));
            Text gameHint = CreateText(
                "Game Settings Hint",
                gamePage,
                "Exit the client and return to your desktop.",
                15,
                FontStyle.Normal,
                GameUiTheme.TextMuted);
            gameHint.alignment = TextAnchor.MiddleCenter;
            Place(gameHint.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.83f));
            Button quitButton = CreateButton("Quit Game Button", gamePage, "QUIT GAME", QuitGame, GameUiButtonTone.Neutral);
            Place(quitButton.GetComponent<RectTransform>(), new Vector2(0.25f, 0.30f), new Vector2(0.75f, 0.54f));

            applyDisplayButton = CreateButton("Apply Display Settings", frameRect, "APPLY", ApplyDisplaySettings, GameUiButtonTone.Primary);
            Place(applyDisplayButton.GetComponent<RectTransform>(), new Vector2(0.18f, 0.055f), new Vector2(0.48f, 0.145f));
            Button closeButton = CreateButton("Close Display Settings", frameRect, "CLOSE", CloseSettings, GameUiButtonTone.Neutral);
            Place(closeButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0.055f), new Vector2(0.82f, 0.145f));

            ShowSettingsPage(false);
            settingsOverlay.SetActive(false);
        }

        private Button CreateSettingsTab(
            string name,
            Transform parent,
            string label,
            LoginOrnamentGraphic.OrnamentStyle iconStyle,
            float horizontalAnchor,
            Action clicked)
        {
            RectTransform root = CreateRect(name, parent);
            root.anchorMin = new Vector2(horizontalAnchor, 0.705f);
            root.anchorMax = new Vector2(horizontalAnchor, 0.705f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(130f, 86f);
            root.anchoredPosition = Vector2.zero;

            LoginOrnamentGraphic surface = root.gameObject.AddComponent<LoginOrnamentGraphic>();
            surface.Configure(LoginOrnamentGraphic.OrnamentStyle.SecondaryButton, true);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() => clicked());
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.90f, 0.65f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.72f, 0.55f, 0.34f, 1f);
            colors.disabledColor = new Color(1f, 0.77f, 0.34f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            RectTransform icon = CreateRect(label + " Icon", root);
            icon.anchorMin = new Vector2(0.5f, 0.58f);
            icon.anchorMax = new Vector2(0.5f, 0.58f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(39f, 39f);
            icon.anchoredPosition = Vector2.zero;
            LoginOrnamentGraphic iconGraphic = icon.gameObject.AddComponent<LoginOrnamentGraphic>();
            iconGraphic.Configure(iconStyle);

            Text groupLabel = CreateText(label + " Group Label", root, label, 13, FontStyle.Bold, GameUiTheme.AccentBright);
            groupLabel.alignment = TextAnchor.MiddleCenter;
            Place(groupLabel.rectTransform, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.30f));
            return button;
        }

        private void ShowSettingsPage(bool showGraphics)
        {
            if (graphicsSettingsPage != null)
            {
                graphicsSettingsPage.SetActive(showGraphics);
            }

            if (gameSettingsPage != null)
            {
                gameSettingsPage.SetActive(!showGraphics);
            }

            if (applyDisplayButton != null)
            {
                applyDisplayButton.gameObject.SetActive(showGraphics);
            }

            if (graphicsTabButton != null)
            {
                graphicsTabButton.interactable = !showGraphics;
            }

            if (gameTabButton != null)
            {
                gameTabButton.interactable = showGraphics;
            }
        }

        private static void QuitGame()
        {
            Debug.Log("[Login UI] Quit requested from the Game settings page.");
            Application.Quit();
        }

        private void OpenSettings()
        {
            ClientDisplaySettings settings = ClientDisplaySettings.Instance;
            if (settings == null || settingsOverlay == null)
            {
                return;
            }

            settings.RefreshForCurrentDisplay();
            displayedResolutions.Clear();
            displayedResolutions.AddRange(settings.AvailableResolutions);
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(displayedResolutions.ConvertAll(value => value.Label));
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, displayedResolutions.IndexOf(settings.SelectedResolution)));
            resolutionDropdown.RefreshShownValue();
            displayModeDropdown.SetValueWithoutNotify((int)settings.SelectedMode);
            displayModeDropdown.RefreshShownValue();
            ShowSettingsPage(false);
            settingsOverlay.SetActive(true);
            settingsOverlay.transform.SetAsLastSibling();
            SetControlsInteractable(false);
            EventSystem.current?.SetSelectedGameObject(resolutionDropdown.gameObject);
        }

        private void ToggleSettings()
        {
            if (settingsOverlay == null) return;
            if (settingsOverlay.activeSelf) CloseSettings();
            else OpenSettings();
        }

        private void ApplyDisplaySettings()
        {
            if (resolutionDropdown == null
                || displayModeDropdown == null
                || displayedResolutions.Count == 0)
            {
                return;
            }

            int resolutionIndex = Mathf.Clamp(resolutionDropdown.value, 0, displayedResolutions.Count - 1);
            int modeIndex = Mathf.Clamp(displayModeDropdown.value, 0, Enum.GetValues(typeof(GameDisplayMode)).Length - 1);
            ClientDisplaySettings.Instance?.Apply((GameDisplayMode)modeIndex, displayedResolutions[resolutionIndex]);
        }

        private void CloseSettings()
        {
            if (settingsOverlay == null)
            {
                return;
            }

            settingsOverlay.SetActive(false);
            SetControlsInteractable(!enteringWorld);
            EventSystem.current?.SetSelectedGameObject(usernameField != null ? usernameField.gameObject : null);
        }

        private void Submit(bool createAccount)
        {
            if (client == null || enteringWorld)
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

            if (!client.IsConnected)
            {
                submitAfterReconnect = true;
                reconnectCreateAccount = createAccount;
                SetControlsInteractable(false);
                SetFeedback("Reconnecting to the server...", GameUiTheme.TextMuted);
                if (!client.ReconnectApplication())
                {
                    submitAfterReconnect = false;
                    SetControlsInteractable(true);
                }
                return;
            }

            submitAfterReconnect = false;
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

        private void FocusPasswordField()
        {
            if (passwordField == null || enteringWorld || !passwordField.IsInteractable()) return;
            EventSystem.current?.SetSelectedGameObject(passwordField.gameObject);
            passwordField.Select();
            passwordField.ActivateInputField();
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
                    if (submitAfterReconnect)
                    {
                        bool createAccount = reconnectCreateAccount;
                        submitAfterReconnect = false;
                        Submit(createAccount);
                    }
                    break;
                case LocalConnectionPhase.Failed:
                    submitAfterReconnect = false;
                    SetControlsInteractable(true);
                    SetFeedback(status.Message, GameUiTheme.Danger);
                    enteringWorld = false;
                    passwordField.Select();
                    passwordField.ActivateInputField();
                    break;
                case LocalConnectionPhase.Offline:
                    SetControlsInteractable(true);
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
                passwordField.text = string.Empty;
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

        private static Font LoadLoginFont(string resourcePath, Font fallback)
        {
            Font font = Resources.Load<Font>(resourcePath);
            return font == null ? fallback : font;
        }

        private static void ApplyMedievalButtonFont(Button button)
        {
            Text label = button == null ? null : button.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                return;
            }

            label.font = LoadLoginFont(MedievalTitleFontResourcePath, label.font);
            label.fontStyle = FontStyle.Normal;
        }

        private static InputField CreateInputField(string name, Transform parent, string placeholderValue, bool password)
        {
            RectTransform root = CreateRect(name, parent);
            LoginOrnamentGraphic backgroundGraphic = root.gameObject.AddComponent<LoginOrnamentGraphic>();
            backgroundGraphic.Configure(LoginOrnamentGraphic.OrnamentStyle.Input, true);
            InputField field = root.gameObject.AddComponent<ExplicitSubmitInputField>();

            RectTransform iconWell = CreateRect(password ? "Lock Icon Well" : "User Icon Well", root);
            iconWell.anchorMin = Vector2.zero;
            iconWell.anchorMax = new Vector2(0.135f, 1f);
            iconWell.offsetMin = Vector2.zero;
            iconWell.offsetMax = Vector2.zero;
            LoginOrnamentGraphic icon = iconWell.gameObject.AddComponent<LoginOrnamentGraphic>();
            icon.Configure(password ? LoginOrnamentGraphic.OrnamentStyle.LockIcon : LoginOrnamentGraphic.OrnamentStyle.UserIcon);

            Image iconDivider = CreateImage("Icon Divider", root, new Color(0.70f, 0.43f, 0.12f, 0.48f));
            RectTransform dividerRect = iconDivider.rectTransform;
            dividerRect.anchorMin = new Vector2(0.135f, 0.13f);
            dividerRect.anchorMax = new Vector2(0.135f, 0.87f);
            dividerRect.sizeDelta = new Vector2(1f, 0f);
            dividerRect.anchoredPosition = Vector2.zero;

            RectTransform textArea = CreateRect("Text Area", root);
            textArea.anchorMin = new Vector2(0.135f, 0f);
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(18f, 4f);
            textArea.offsetMax = new Vector2(-18f, -4f);
            Text text = CreateText("Text", textArea, string.Empty, 17, FontStyle.Normal, GameUiTheme.TextPrimary);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleLeft;
            Text placeholder = CreateText("Placeholder", textArea, placeholderValue, 17, FontStyle.Normal, new Color(GameUiTheme.TextMuted.r, GameUiTheme.TextMuted.g, GameUiTheme.TextMuted.b, 0.78f));
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAnchor.MiddleLeft;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = backgroundGraphic;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.asteriskChar = '\u2022';
            GameUiTheme.StyleInputField(field);
            ColorBlock fieldColors = field.colors;
            fieldColors.normalColor = Color.white;
            fieldColors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
            fieldColors.selectedColor = new Color(1f, 0.92f, 0.72f, 1f);
            fieldColors.pressedColor = new Color(0.94f, 0.84f, 0.68f, 1f);
            fieldColors.disabledColor = new Color(0.48f, 0.43f, 0.38f, 0.82f);
            fieldColors.fadeDuration = 0.1f;
            field.colors = fieldColors;
            return field;
        }

        private static Button CreateIconButton(string name, Transform parent, Action clicked)
        {
            RectTransform root = CreateRect(name, parent);
            LoginOrnamentGraphic surface = root.gameObject.AddComponent<LoginOrnamentGraphic>();
            surface.Configure(LoginOrnamentGraphic.OrnamentStyle.SecondaryButton, true);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() => clicked());

            RectTransform iconRect = CreateRect("Gear Icon", root);
            Place(iconRect, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
            LoginOrnamentGraphic icon = iconRect.gameObject.AddComponent<LoginOrnamentGraphic>();
            icon.Configure(LoginOrnamentGraphic.OrnamentStyle.GearIcon);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.91f, 0.66f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.75f, 0.58f, 0.38f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        private static Dropdown CreateDropdown(string name, Transform parent)
        {
            RectTransform root = CreateRect(name, parent);
            LoginOrnamentGraphic surface = root.gameObject.AddComponent<LoginOrnamentGraphic>();
            surface.Configure(LoginOrnamentGraphic.OrnamentStyle.Input, true);
            Dropdown dropdown = root.gameObject.AddComponent<ToggleDropdown>();
            dropdown.targetGraphic = surface;

            Text caption = CreateText("Label", root, string.Empty, 16, FontStyle.Normal, GameUiTheme.TextPrimary);
            caption.alignment = TextAnchor.MiddleLeft;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 12;
            caption.resizeTextMaxSize = 16;
            Place(caption.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.84f, 0.92f));

            Text arrow = CreateText("Arrow", root, "v", 17, FontStyle.Bold, GameUiTheme.AccentBright);
            arrow.alignment = TextAnchor.MiddleCenter;
            Place(arrow.rectTransform, new Vector2(0.84f, 0.08f), new Vector2(0.96f, 0.92f));

            RectTransform template = CreateRect("Template", root);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -4f);
            template.sizeDelta = new Vector2(0f, 210f);
            template.gameObject.AddComponent<GameUiThemeOptOut>();
            template.gameObject.AddComponent<CanvasGroup>();
            Image templateBackground = template.gameObject.AddComponent<Image>();
            templateBackground.color = new Color(0.10f, 0.04f, 0.015f, 0.995f);
            GameUiTheme.StylePanel(templateBackground, true);
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform roundedClip = CreateRect("Rounded Dropdown Window", template);
            Stretch(roundedClip);
            roundedClip.offsetMin = new Vector2(2f, 2f);
            roundedClip.offsetMax = new Vector2(-2f, -2f);
            Image clipImage = roundedClip.gameObject.AddComponent<Image>();
            clipImage.color = Color.white;
            GameUiTheme.StylePanel(clipImage);
            Mask roundedMask = roundedClip.gameObject.AddComponent<Mask>();
            roundedMask.showMaskGraphic = false;

            RectTransform viewport = CreateRect("Viewport", roundedClip);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-23f, -4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 42f);

            RectTransform item = CreateRect("Item", content);
            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(1f, 0.5f);
            item.pivot = new Vector2(0.5f, 0.5f);
            item.sizeDelta = new Vector2(0f, 42f);
            item.anchoredPosition = Vector2.zero;
            Image itemBackground = item.gameObject.AddComponent<Image>();
            itemBackground.color = new Color(0.24f, 0.11f, 0.035f, 1f);
            itemBackground.sprite = null;
            itemBackground.type = Image.Type.Simple;
            Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemBackground;
            itemToggle.graphic = null;
            ColorBlock toggleColors = itemToggle.colors;
            toggleColors.normalColor = Color.white;
            toggleColors.highlightedColor = new Color(1f, 0.82f, 0.50f, 1f);
            toggleColors.selectedColor = toggleColors.highlightedColor;
            toggleColors.pressedColor = new Color(0.78f, 0.63f, 0.43f, 1f);
            itemToggle.colors = toggleColors;

            Text itemLabel = CreateText("Item Label", item, "Option", 15, FontStyle.Normal, GameUiTheme.TextPrimary);
            itemLabel.alignment = TextAnchor.MiddleLeft;
            Place(itemLabel.rectTransform, new Vector2(0.06f, 0.05f), new Vector2(0.96f, 0.95f));

            Image itemDivider = CreateImage("Item Divider", item, new Color(0.70f, 0.43f, 0.12f, 0.28f));
            RectTransform dividerRect = itemDivider.rectTransform;
            dividerRect.anchorMin = Vector2.zero;
            dividerRect.anchorMax = Vector2.right;
            dividerRect.pivot = new Vector2(0.5f, 0f);
            dividerRect.sizeDelta = new Vector2(0f, 1f);
            dividerRect.anchoredPosition = Vector2.zero;

            RectTransform scrollbarRoot = CreateRect("Scrollbar Vertical", roundedClip);
            scrollbarRoot.anchorMin = new Vector2(1f, 0f);
            scrollbarRoot.anchorMax = Vector2.one;
            scrollbarRoot.pivot = new Vector2(1f, 0.5f);
            scrollbarRoot.offsetMin = new Vector2(-17f, 5f);
            scrollbarRoot.offsetMax = new Vector2(-5f, -5f);
            Image scrollbarTrack = scrollbarRoot.gameObject.AddComponent<Image>();
            scrollbarTrack.color = new Color(0.055f, 0.018f, 0.006f, 0.96f);
            GameUiTheme.StylePillSurface(scrollbarTrack);
            Scrollbar scrollbar = scrollbarRoot.gameObject.AddComponent<Scrollbar>();

            RectTransform slidingArea = CreateRect("Sliding Area", scrollbarRoot);
            Stretch(slidingArea);
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);
            RectTransform handle = CreateRect("Handle", slidingArea);
            Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.86f, 0.57f, 0.17f, 0.96f);
            GameUiTheme.StylePillSurface(handleImage);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.vertical = true;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 4f;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.options = new List<Dropdown.OptionData>();
            template.gameObject.SetActive(false);

            ColorBlock dropdownColors = dropdown.colors;
            dropdownColors.normalColor = Color.white;
            dropdownColors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
            dropdownColors.selectedColor = dropdownColors.highlightedColor;
            dropdownColors.pressedColor = new Color(0.78f, 0.66f, 0.48f, 1f);
            dropdownColors.fadeDuration = 0.08f;
            dropdown.colors = dropdownColors;
            return dropdown;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Action clicked,
            GameUiButtonTone tone)
        {
            RectTransform root = CreateRect(name, parent);
            LoginOrnamentGraphic surface = root.gameObject.AddComponent<LoginOrnamentGraphic>();
            surface.Configure(
                tone == GameUiButtonTone.Primary
                    ? LoginOrnamentGraphic.OrnamentStyle.PrimaryButton
                    : LoginOrnamentGraphic.OrnamentStyle.SecondaryButton,
                true);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() => clicked());
            Color textColor = tone == GameUiButtonTone.Primary
                ? new Color(0.15f, 0.06f, 0.015f, 1f)
                : GameUiTheme.AccentBright;
            Text text = CreateText("Label", root, label, tone == GameUiButtonTone.Primary ? 21 : 18, FontStyle.Bold, textColor);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            GameUiTheme.EnsureTextShadow(text, tone == GameUiButtonTone.Primary ? 0.65f : 1.25f);
            ColorBlock buttonColors = button.colors;
            buttonColors.normalColor = Color.white;
            buttonColors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
            buttonColors.selectedColor = new Color(1f, 0.92f, 0.72f, 1f);
            buttonColors.pressedColor = new Color(0.78f, 0.66f, 0.48f, 1f);
            buttonColors.disabledColor = new Color(0.45f, 0.42f, 0.38f, 0.72f);
            buttonColors.fadeDuration = 0.08f;
            button.colors = buttonColors;
            return button;
        }

        private static void CreateSeparator(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = CreateRect(name, parent);
            Place(rect, anchorMin, anchorMax);
            LoginOrnamentGraphic separator = rect.gameObject.AddComponent<LoginOrnamentGraphic>();
            separator.Configure(LoginOrnamentGraphic.OrnamentStyle.Separator);
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        private IEnumerator CaptureLoginDropdown(string requestedPath)
        {
            ShowSettingsPage(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            resolutionDropdown?.Show();
            yield return CaptureLoginScreen(requestedPath);
        }
    }
}
