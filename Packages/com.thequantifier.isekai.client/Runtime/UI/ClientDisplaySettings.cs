using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityIsekaiGame.UI
{
    public enum GameDisplayMode
    {
        ExclusiveFullscreen = 0,
        BorderlessFullscreen = 1,
        Windowed = 2
    }

    public readonly struct GameDisplayResolution : IEquatable<GameDisplayResolution>
    {
        public GameDisplayResolution(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
        }

        public int Width { get; }
        public int Height { get; }
        public string Label => $"{Width} × {Height}";

        public bool Equals(GameDisplayResolution other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is GameDisplayResolution other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Width, Height);
        public override string ToString() => Label;
    }

    /// <summary>
    /// Client-wide display configuration. Settings are applied before scene presentation begins,
    /// persist across launches, and are rebuilt when the game window moves to another monitor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientDisplaySettings : MonoBehaviour
    {
        private const string ModePreference = "display.mode";
        private const string WidthPreference = "display.width";
        private const string HeightPreference = "display.height";
        private const float DisplayPollIntervalSeconds = 1f;

        private static ClientDisplaySettings instance;
        private static bool applicationQuitting;

        private readonly List<GameDisplayResolution> availableResolutions = new List<GameDisplayResolution>();
        private Vector2Int currentDisplaySize;
        private GameDisplayResolution selectedResolution;
        private GameDisplayMode selectedMode;
        private float nextDisplayPollAt;
        private bool initialized;

        public static ClientDisplaySettings Instance
        {
            get
            {
                if (instance == null && !applicationQuitting)
                {
                    Install();
                }

                return instance;
            }
        }

        public IReadOnlyList<GameDisplayResolution> AvailableResolutions => availableResolutions;
        public GameDisplayResolution SelectedResolution => selectedResolution;
        public GameDisplayMode SelectedMode => selectedMode;
        public Vector2Int CurrentDisplaySize => currentDisplaySize;

        public event Action OptionsChanged;
        public event Action SettingsApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            applicationQuitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (applicationQuitting || instance != null)
            {
                return;
            }

            ClientDisplaySettings existing = FindAnyObjectByType<ClientDisplaySettings>(FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                instance.EnsureInitialized();
                return;
            }

            var root = new GameObject("Client Display Settings");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<ClientDisplaySettings>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        private void Update()
        {
            if (!initialized || Time.unscaledTime < nextDisplayPollAt)
            {
                return;
            }

            nextDisplayPollAt = Time.unscaledTime + DisplayPollIntervalSeconds;
            Vector2Int detected = DetectCurrentDisplaySize();
            if (detected == currentDisplaySize)
            {
                return;
            }

            currentDisplaySize = detected;
            RebuildAvailableResolutions();
            if (selectedMode == GameDisplayMode.BorderlessFullscreen)
            {
                selectedResolution = NativeDisplayResolution;
                ApplyDisplayMode(selectedMode, selectedResolution);
            }
            else
            {
                selectedResolution = FindClosestResolution(selectedResolution, availableResolutions);
                OptionsChanged?.Invoke();
            }
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        public void RefreshForCurrentDisplay()
        {
            EnsureInitialized();
            currentDisplaySize = DetectCurrentDisplaySize();
            RebuildAvailableResolutions();
        }

        public void Apply(GameDisplayMode mode, GameDisplayResolution resolution)
        {
            EnsureInitialized();
            selectedMode = Enum.IsDefined(typeof(GameDisplayMode), mode)
                ? mode
                : GameDisplayMode.BorderlessFullscreen;

            selectedResolution = FindClosestResolution(resolution, availableResolutions);

            ApplyDisplayMode(selectedMode, selectedResolution);

            PlayerPrefs.SetInt(ModePreference, (int)selectedMode);
            PlayerPrefs.SetInt(WidthPreference, selectedResolution.Width);
            PlayerPrefs.SetInt(HeightPreference, selectedResolution.Height);
            PlayerPrefs.Save();
            SettingsApplied?.Invoke();
        }

        public static IReadOnlyList<GameDisplayResolution> BuildResolutionOptions(
            IEnumerable<GameDisplayResolution> candidates,
            int displayWidth,
            int displayHeight)
        {
            int maximumWidth = Math.Max(640, displayWidth);
            int maximumHeight = Math.Max(360, displayHeight);
            var unique = new HashSet<GameDisplayResolution>();
            if (candidates != null)
            {
                foreach (GameDisplayResolution candidate in candidates)
                {
                    if (candidate.Width < 640
                        || candidate.Height < 360
                        || candidate.Width > maximumWidth
                        || candidate.Height > maximumHeight)
                    {
                        continue;
                    }

                    unique.Add(candidate);
                }
            }

            unique.Add(new GameDisplayResolution(maximumWidth, maximumHeight));
            return unique
                .OrderByDescending(value => (long)value.Width * value.Height)
                .ThenByDescending(value => value.Width)
                .ThenByDescending(value => value.Height)
                .ToArray();
        }

        public static GameDisplayResolution FindClosestResolution(
            GameDisplayResolution requested,
            IReadOnlyList<GameDisplayResolution> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return requested;
            }

            GameDisplayResolution closest = candidates[0];
            long closestDistance = ResolutionDistance(requested, closest);
            for (int i = 1; i < candidates.Count; i++)
            {
                long distance = ResolutionDistance(requested, candidates[i]);
                if (distance >= closestDistance)
                {
                    continue;
                }

                closest = candidates[i];
                closestDistance = distance;
            }

            return closest;
        }

        public static FullScreenMode ToUnityMode(GameDisplayMode mode)
        {
            return mode switch
            {
                GameDisplayMode.ExclusiveFullscreen => FullScreenMode.ExclusiveFullScreen,
                GameDisplayMode.BorderlessFullscreen => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.Windowed
            };
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            currentDisplaySize = DetectCurrentDisplaySize();
            RebuildAvailableResolutions();
            selectedMode = ReadSavedMode();
            int defaultWidth = Mathf.Max(640, currentDisplaySize.x);
            int defaultHeight = Mathf.Max(360, currentDisplaySize.y);
            selectedResolution = FindClosestResolution(
                new GameDisplayResolution(
                    PlayerPrefs.GetInt(WidthPreference, defaultWidth),
                    PlayerPrefs.GetInt(HeightPreference, defaultHeight)),
                availableResolutions);

            if (!Application.isEditor)
            {
                Apply(selectedMode, selectedResolution);
            }
        }

        private void RebuildAvailableResolutions()
        {
            IEnumerable<GameDisplayResolution> reported = Screen.resolutions
                .Select(value => new GameDisplayResolution(value.width, value.height));
            availableResolutions.Clear();
            availableResolutions.AddRange(BuildResolutionOptions(
                reported,
                currentDisplaySize.x,
                currentDisplaySize.y));
            selectedResolution = FindClosestResolution(selectedResolution, availableResolutions);
            OptionsChanged?.Invoke();
        }

        private GameDisplayMode ReadSavedMode()
        {
            int fallback = (int)GameDisplayMode.BorderlessFullscreen;
            int raw = PlayerPrefs.GetInt(ModePreference, fallback);
            return Enum.IsDefined(typeof(GameDisplayMode), raw)
                ? (GameDisplayMode)raw
                : GameDisplayMode.BorderlessFullscreen;
        }

        private GameDisplayResolution NativeDisplayResolution => new GameDisplayResolution(
            Mathf.Max(640, currentDisplaySize.x),
            Mathf.Max(360, currentDisplaySize.y));

        private static Vector2Int DetectCurrentDisplaySize()
        {
            DisplayInfo display = Screen.mainWindowDisplayInfo;
            int width = display.width > 0 ? display.width : Screen.currentResolution.width;
            int height = display.height > 0 ? display.height : Screen.currentResolution.height;
            if (width <= 0 || height <= 0)
            {
                width = Mathf.Max(640, Screen.width);
                height = Mathf.Max(360, Screen.height);
            }

            return new Vector2Int(width, height);
        }

        private static void ApplyDisplayMode(GameDisplayMode mode, GameDisplayResolution resolution)
        {
            // FullScreenWindow always fills the active monitor without borders. Exclusive mode
            // hands display ownership to the player; Windowed keeps normal OS window chrome.
            Screen.SetResolution(resolution.Width, resolution.Height, ToUnityMode(mode));
        }

        private static long ResolutionDistance(GameDisplayResolution left, GameDisplayResolution right)
        {
            long width = left.Width - right.Width;
            long height = left.Height - right.Height;
            return width * width + height * height;
        }
    }
}
