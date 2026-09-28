using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    [DisallowMultipleComponent]
    public sealed class GameHudNotificationView : MonoBehaviour
    {
        private enum DisplayPhase { Hidden, FadeIn, Hold, FadeOut }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image panelImage;
        [SerializeField] private Image accentImage;
        [SerializeField] private Text label;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.18f;
        [SerializeField, Min(1)] private int maximumQueuedMessages = 5;

        private readonly Queue<GameHudMessage> pending = new Queue<GameHudMessage>();
        private DisplayPhase phase;
        private GameHudMessage current;
        private float phaseEndsAt;

        public string CurrentMessage => phase == DisplayPhase.Hidden ? string.Empty : current.Text;
        public int PendingCount => pending.Count;

        private void Awake()
        {
            ResolveComponents();
            ApplyTheme();
            HideImmediate();
        }

        private void OnEnable()
        {
            GameHudMessageBus.MessageRequested += Enqueue;
        }

        private void OnDisable()
        {
            GameHudMessageBus.MessageRequested -= Enqueue;
            pending.Clear();
            HideImmediate();
        }

        private void Update()
        {
            if (phase == DisplayPhase.Hidden)
            {
                TryShowNext();
                return;
            }

            float now = Time.unscaledTime;
            switch (phase)
            {
                case DisplayPhase.FadeIn:
                    canvasGroup.alpha = 1f - Mathf.Clamp01((phaseEndsAt - now) / fadeDuration);
                    if (now >= phaseEndsAt) BeginPhase(DisplayPhase.Hold, current.Duration);
                    break;
                case DisplayPhase.Hold:
                    if (now >= phaseEndsAt) BeginPhase(DisplayPhase.FadeOut, fadeDuration);
                    break;
                case DisplayPhase.FadeOut:
                    canvasGroup.alpha = Mathf.Clamp01((phaseEndsAt - now) / fadeDuration);
                    if (now >= phaseEndsAt)
                    {
                        HideImmediate();
                        TryShowNext();
                    }
                    break;
            }
        }

        public void Configure(CanvasGroup group, Image panel, Image accent, Text messageLabel)
        {
            canvasGroup = group;
            panelImage = panel;
            accentImage = accent;
            label = messageLabel;
            ResolveComponents();
            ApplyTheme();
            HideImmediate();
        }

        public void Enqueue(GameHudMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                return;
            }

            if (phase != DisplayPhase.Hidden && string.Equals(current.Text, message.Text, System.StringComparison.Ordinal))
            {
                current = message;
                BeginPhase(DisplayPhase.Hold, current.Duration);
                return;
            }

            while (pending.Count >= maximumQueuedMessages)
            {
                pending.Dequeue();
            }
            pending.Enqueue(message);
            TryShowNext();
        }

        private void TryShowNext()
        {
            if (phase != DisplayPhase.Hidden || pending.Count == 0)
            {
                return;
            }

            current = pending.Dequeue();
            if (label != null) label.text = current.Text;
            if (accentImage != null) accentImage.color = ToneColor(current.Tone);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            BeginPhase(DisplayPhase.FadeIn, fadeDuration);
        }

        private void BeginPhase(DisplayPhase next, float duration)
        {
            phase = next;
            phaseEndsAt = Time.unscaledTime + Mathf.Max(0.01f, duration);
            if (next == DisplayPhase.Hold && canvasGroup != null) canvasGroup.alpha = 1f;
        }

        private void HideImmediate()
        {
            phase = DisplayPhase.Hidden;
            current = default;
            if (label != null) label.text = string.Empty;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        private void ResolveComponents()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            label ??= GetComponentInChildren<Text>(true);
            panelImage ??= GetComponent<Image>();
        }

        private void ApplyTheme()
        {
            GameUiTheme.StyleText(label, GameUiTextRole.Feedback);
            GameUiTheme.EnsureTextShadow(label, 1f);
            GameUiTheme.StylePanel(panelImage, raised: true);
            if (label != null) label.raycastTarget = false;
            if (panelImage != null) panelImage.raycastTarget = false;
            if (accentImage != null) accentImage.raycastTarget = false;
        }

        public static Color ToneColor(GameHudMessageTone tone)
        {
            return tone switch
            {
                GameHudMessageTone.Success => GameUiTheme.Success,
                GameHudMessageTone.Warning => GameUiTheme.Warning,
                GameHudMessageTone.Danger => GameUiTheme.Danger,
                _ => GameUiTheme.Accent
            };
        }
    }
}
