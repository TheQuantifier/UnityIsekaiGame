using System;
using UnityEngine;

namespace UnityIsekaiGame.Gameplay
{
    public enum GameHudMessageTone
    {
        Information,
        Success,
        Warning,
        Danger
    }

    public readonly struct GameHudMessage
    {
        public GameHudMessage(string text, GameHudMessageTone tone, float duration)
        {
            Text = text ?? string.Empty;
            Tone = tone;
            Duration = Mathf.Max(0.5f, duration);
        }

        public string Text { get; }
        public GameHudMessageTone Tone { get; }
        public float Duration { get; }
    }

    public static class GameHudMessageBus
    {
        public static event Action<GameHudMessage> MessageRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            MessageRequested = null;
        }

        public static void Show(string message, GameHudMessageTone tone = GameHudMessageTone.Information, float duration = 2.75f)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                MessageRequested?.Invoke(new GameHudMessage(message.Trim(), tone, duration));
            }
        }
    }
}
