using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace UnityIsekaiGame.UI.Startup
{
    [DisallowMultipleComponent]
    public sealed class StartupSequenceController : MonoBehaviour
    {
        private const string DefaultVideoRelativePath = "Startup/startup_scene.mp4";
        private const string DefaultNextScene = "PrototypeScene";
        private const float PreparationTimeoutSeconds = 12f;
        private const float MinimumSkipDelaySeconds = 0.75f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private string videoRelativePath = DefaultVideoRelativePath;
        [SerializeField] private string nextSceneName = DefaultNextScene;
        [SerializeField] private bool allowSkip = true;

        private VideoPlayer videoPlayer;
        private float startedAt;
        private bool transitioning;

        private void Awake()
        {
            Application.runInBackground = true;
            targetCamera = targetCamera == null ? Camera.main : targetCamera;
            if (targetCamera == null)
            {
                targetCamera = new GameObject("Startup Camera").AddComponent<Camera>();
                targetCamera.tag = "MainCamera";
            }

            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = Color.black;
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.isLooping = false;
            videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
            videoPlayer.targetCamera = targetCamera;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.loopPointReached += OnVideoCompleted;
            videoPlayer.errorReceived += OnVideoError;
        }

        private IEnumerator Start()
        {
            startedAt = Time.unscaledTime;
            if (HasCommandLineFlag("--skip-startup-video"))
            {
                ContinueToLogin();
                yield break;
            }

            string path = Path.Combine(Application.streamingAssetsPath, videoRelativePath);
            videoPlayer.url = path;
            videoPlayer.Prepare();

            float deadline = Time.realtimeSinceStartup + PreparationTimeoutSeconds;
            while (!videoPlayer.isPrepared && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (transitioning)
            {
                yield break;
            }

            if (!videoPlayer.isPrepared)
            {
                Debug.LogWarning($"[Startup] The startup movie at '{path}' did not prepare in time. Continuing to login.", this);
                ContinueToLogin();
                yield break;
            }

            Debug.Log($"[Startup] Playing startup movie '{path}'.", this);
            videoPlayer.Play();
        }

        private void Update()
        {
            if (!allowSkip || transitioning || Time.unscaledTime - startedAt < MinimumSkipDelaySeconds)
            {
                return;
            }

            bool keyboardSkip = Keyboard.current?.anyKey.wasPressedThisFrame == true;
            bool pointerSkip = Mouse.current?.leftButton.wasPressedThisFrame == true
                || Gamepad.current?.startButton.wasPressedThisFrame == true;
            if (keyboardSkip || pointerSkip)
            {
                ContinueToLogin();
            }
        }

        private void OnDestroy()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.loopPointReached -= OnVideoCompleted;
            videoPlayer.errorReceived -= OnVideoError;
        }

        private void OnVideoCompleted(VideoPlayer _)
        {
            Debug.Log("[Startup] Startup movie completed; opening account login.", this);
            ContinueToLogin();
        }

        private void OnVideoError(VideoPlayer _, string message)
        {
            Debug.LogWarning($"[Startup] The startup movie could not be played: {message}. Continuing to login.", this);
            ContinueToLogin();
        }

        private void ContinueToLogin()
        {
            if (transitioning)
            {
                return;
            }

            transitioning = true;
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }

            SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }

        private static bool HasCommandLineFlag(string flag)
        {
            return Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, flag, StringComparison.OrdinalIgnoreCase));
        }
    }
}
