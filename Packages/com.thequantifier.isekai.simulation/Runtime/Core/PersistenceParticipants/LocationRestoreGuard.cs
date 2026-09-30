using System;

namespace UnityIsekaiGame.Persistence
{
    public static class LocationRestoreGuard
    {
        private static int suppressionDepth;

        public static bool IsRestoringLocation => suppressionDepth > 0;

        public static event Action<bool> SuppressionChanged;

        public static IDisposable Enter()
        {
            suppressionDepth++;
            if (suppressionDepth == 1)
            {
                SuppressionChanged?.Invoke(true);
            }

            return new Scope();
        }

        private static void Exit()
        {
            if (suppressionDepth <= 0)
            {
                suppressionDepth = 0;
                return;
            }

            suppressionDepth--;
            if (suppressionDepth == 0)
            {
                SuppressionChanged?.Invoke(false);
            }
        }

        private sealed class Scope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Exit();
            }
        }
    }
}
