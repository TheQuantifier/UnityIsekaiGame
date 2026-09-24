using UnityEngine;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayTimeTracker : MonoBehaviour
    {
        [SerializeField, Min(0f)] private double cumulativeSeconds;
        [SerializeField] private bool countWhileMenuOpen;

        private bool paused;
        private bool menuOpen;

        public double CumulativeSeconds => cumulativeSeconds;
        public bool CountWhileMenuOpen => countWhileMenuOpen;

        private void Update()
        {
            if (paused || (menuOpen && !countWhileMenuOpen))
            {
                return;
            }

            cumulativeSeconds += Time.unscaledDeltaTime;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            paused = pauseStatus;
        }

        public void Restore(double seconds)
        {
            cumulativeSeconds = System.Math.Max(0d, seconds);
        }

        public void SetMenuOpen(bool open)
        {
            menuOpen = open;
        }
    }
}
