using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Client;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    /// <summary>
    /// Client-local pause overlay. It blocks this client's gameplay input without changing
    /// the simulation clock or pausing the authoritative server/world simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        public static readonly Vector2 PauseWindowSize = new Vector2(430f, 250f);

        private static PauseMenuController instance;
        private static bool hooksInstalled;

        private LocalGameClient client;
        private PlayerInputReader input;
        private Canvas canvas;
        private GameObject overlayRoot;
        private RectTransform windowRoot;
        private Button unpauseButton;
        private Button quitButton;
        private GameObject previouslySelectedObject;
        private NetworkPlayerActor participationActor;
        private bool isOpen;

        public bool IsOpen => isOpen;
        public RectTransform WindowRoot { get { EnsureInitialized(); return windowRoot; } }
        public Button UnpauseButton { get { EnsureInitialized(); return unpauseButton; } }
        public Button QuitButton { get { EnsureInitialized(); return quitButton; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHooks()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
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
            PauseMenuController existing = FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return;
            }

            if (FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include) == null)
            {
                return;
            }

            GameObject root = new GameObject(
                "Pause Menu Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            root.AddComponent<PauseMenuController>();
        }

        /// <summary>
        /// Handles the gameplay pause key. Returns false before a player has authenticated and spawned.
        /// </summary>
        public static bool TryToggleForGameplay()
        {
            TryInstall();
            if (instance == null || !instance.CanPauseGameplay())
            {
                return false;
            }

            if (instance.isOpen) instance.Unpause();
            else instance.Open();
            return true;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (overlayRoot != null)
            {
                return;
            }

            if (instance != null && instance != this)
            {
                return;
            }

            instance = this;
            client = FindAnyObjectByType<LocalGameClient>(FindObjectsInactive.Include);
            input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            BuildInterface();
        }

        private void OnEnable()
        {
            if (client != null)
            {
                client.LocalPlayerActorChanged += OnLocalPlayerActorChanged;
                BindParticipationActor(client.LocalPlayerActor);
            }
        }

        private void OnDisable()
        {
            SetOpen(false);

            if (client != null)
            {
                client.LocalPlayerActorChanged -= OnLocalPlayerActorChanged;
            }

            BindParticipationActor(null);
        }

        private void OnDestroy()
        {
            if (object.ReferenceEquals(instance, this))
            {
                instance = null;
            }

            input?.SetMenuInputBlocked(this, false);
        }

        public void Open()
        {
            EnsureInitialized();
            SetOpen(true);
        }

        public void Unpause() => SetOpen(false);

        public void QuitGame()
        {
            SetOpen(false);
            client?.Disconnect();
            Application.Quit();
        }

        private bool CanPauseGameplay()
        {
            if (client == null)
            {
                client = FindAnyObjectByType<LocalGameClient>(FindObjectsInactive.Include);
            }

            return client != null && client.LocalPlayerActor != null;
        }

        private void OnLocalPlayerActorChanged(NetworkPlayerActor actor)
        {
            BindParticipationActor(actor);
            if (actor == null)
            {
                SetOpen(false);
            }
        }

        private void BindParticipationActor(NetworkPlayerActor actor)
        {
            if (participationActor == actor)
            {
                return;
            }

            if (participationActor != null)
            {
                participationActor.WorldParticipationStateChanged -= OnWorldParticipationStateChanged;
            }

            participationActor = actor;
            if (participationActor != null)
            {
                participationActor.WorldParticipationStateChanged += OnWorldParticipationStateChanged;
                SetOpen(participationActor.IsPausedProtected, false);
            }
        }

        private void OnWorldParticipationStateChanged(
            NetworkPlayerActor _,
            NetworkPlayerWorldParticipationState __,
            NetworkPlayerWorldParticipationState current)
        {
            SetOpen(current == NetworkPlayerWorldParticipationState.PausedProtected, false);
        }

        private void SetOpen(bool open, bool requestAuthority = true)
        {
            if (isOpen == open)
            {
                return;
            }

            isOpen = open;
            if (requestAuthority)
            {
                NetworkPlayerActor actor = participationActor != null
                    ? participationActor
                    : client?.LocalPlayerActor;
                actor?.RequestPausedProtected(open);
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(open);
            }

            if (input == null)
            {
                input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            }

            input?.SetMenuInputBlocked(this, open, open ? Unpause : null);
            if (open)
            {
                previouslySelectedObject = EventSystem.current == null
                    ? null
                    : EventSystem.current.currentSelectedGameObject;
                if (EventSystem.current != null && unpauseButton != null)
                {
                    EventSystem.current.SetSelectedGameObject(unpauseButton.gameObject);
                }
            }
            else if (EventSystem.current != null)
            {
                GameObject selection = previouslySelectedObject != null && previouslySelectedObject.activeInHierarchy
                    ? previouslySelectedObject
                    : null;
                EventSystem.current.SetSelectedGameObject(selection);
                previouslySelectedObject = null;
            }
        }

        private void BuildInterface()
        {
            gameObject.layer = 5;
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue - 1;
            canvas.targetDisplay = 0;
            GameUiTheme.ConfigureCanvas(canvas);

            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            RectTransform overlay = CreateRect("Pause Overlay", transform);
            Stretch(overlay);
            overlayRoot = overlay.gameObject;
            Image backdrop = overlay.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.04f, 0.025f, 0.015f, 0.44f);
            backdrop.raycastTarget = true;

            windowRoot = CreateRect("Pause Window", overlay);
            windowRoot.anchorMin = new Vector2(0.5f, 0.5f);
            windowRoot.anchorMax = new Vector2(0.5f, 0.5f);
            windowRoot.pivot = new Vector2(0.5f, 0.5f);
            windowRoot.sizeDelta = PauseWindowSize;
            windowRoot.anchoredPosition = Vector2.zero;
            Image windowImage = windowRoot.gameObject.AddComponent<Image>();
            GameUiTheme.StylePanel(windowImage, true);
            windowImage.color = new Color(GameUiTheme.PanelRaised.r, GameUiTheme.PanelRaised.g, GameUiTheme.PanelRaised.b, 0.99f);

            RectTransform accent = CreateRect("Top Gold Accent", windowRoot);
            accent.anchorMin = new Vector2(0f, 1f);
            accent.anchorMax = new Vector2(1f, 1f);
            accent.pivot = new Vector2(0.5f, 1f);
            accent.offsetMin = new Vector2(10f, -4f);
            accent.offsetMax = new Vector2(-10f, 0f);
            Image accentImage = accent.gameObject.AddComponent<Image>();
            accentImage.color = GameUiTheme.Accent;
            accentImage.raycastTarget = false;

            Text heading = CreateText("Pause Heading", windowRoot, "You are Paused", 27, FontStyle.Bold);
            SetRect(heading.rectTransform, new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.91f));
            heading.alignment = TextAnchor.MiddleCenter;
            heading.color = GameUiTheme.AccentBright;
            GameUiTheme.EnsureTextShadow(heading, 1f);

            unpauseButton = CreateButton("Unpause Button", windowRoot, "Unpause", GameUiButtonTone.Primary);
            SetCenteredControl(unpauseButton.GetComponent<RectTransform>(), 22f);
            unpauseButton.onClick.AddListener(Unpause);

            quitButton = CreateButton("Quit Game Button", windowRoot, "Quit Game", GameUiButtonTone.Danger);
            SetCenteredControl(quitButton.GetComponent<RectTransform>(), -37f);
            quitButton.onClick.AddListener(QuitGame);

            overlayRoot.SetActive(false);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.layer = 5;
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = GameUiTheme.TextPrimary;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, GameUiButtonTone tone)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Label", rect, label, 16, FontStyle.Bold);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            GameUiTheme.StyleButton(button, tone);
            GameUiTheme.StylePillSurface(image);
            return button;
        }

        private static void SetCenteredControl(RectTransform rect, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(250f, 42f);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
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
    }
}
