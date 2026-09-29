using UnityEngine;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayTimeTracker : MonoBehaviour
    {
        [SerializeField, Min(0f)] private double cumulativeSeconds;
        [SerializeField, HideInInspector] private bool countWhileMenuOpen = true;

        public double CumulativeSeconds => cumulativeSeconds;
        public bool CountWhileMenuOpen => countWhileMenuOpen;

        private void Awake()
        {
            // Older scenes may have serialized this as false. Menus are client presentation and
            // must never pause player or server time, so normalize the compatibility value.
            countWhileMenuOpen = true;
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        public void Advance(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0d) return;
            cumulativeSeconds += elapsedSeconds;
        }

        public void Restore(double seconds)
        {
            cumulativeSeconds = System.Math.Max(0d, seconds);
        }

        public void SetMenuOpen(bool open)
        {
            // Retained for save/UI compatibility. Player UI never pauses the authoritative world clock.
            countWhileMenuOpen = true;
        }
    }
}
