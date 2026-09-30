#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UnityIsekaiGame.Development
{
    public static class PrototypeTestLabServiceLocator
    {
        public static PrototypeTestLabService ActiveService { get; private set; }

        public static void Register(PrototypeTestLabService service)
        {
            if (service != null)
            {
                ActiveService = service;
            }
        }

        public static void Unregister(PrototypeTestLabService service)
        {
            if (ReferenceEquals(ActiveService, service))
            {
                ActiveService = null;
            }
        }
    }
}
#endif
