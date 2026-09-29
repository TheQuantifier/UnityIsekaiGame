#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.Development
{
    public sealed class PrototypeTestLabMenuExtension : MonoBehaviour, IInventoryMenuExtension
    {
        private const string ExtensionKey = "development.test-lab";

        [SerializeField] private InventoryScreenView menuView;
        [SerializeField] private InventoryScreenController menuController;

        private PrototypeTestLabService service;
        private PrototypeTestLabView testLabView;
        private bool registered;

        public string ExtensionId => ExtensionKey;
        public string DisplayName => "Test Lab";
        public int Order => 100;
        public bool IsAvailable => Debug.isDebugBuild || Application.isEditor;
        public bool SuppressFeedbackText => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneRegistration()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterLoadedScene()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToInventoryMenus();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachToInventoryMenus();
        }

        private static void AttachToInventoryMenus()
        {
            InventoryScreenView[] views = Object.FindObjectsByType<InventoryScreenView>(FindObjectsInactive.Include);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] == null || views[i].GetComponent<PrototypeTestLabMenuExtension>() != null)
                {
                    continue;
                }

                PrototypeTestLabMenuExtension extension = views[i].gameObject.AddComponent<PrototypeTestLabMenuExtension>();
                extension.menuView = views[i];
            }
        }

        private void OnEnable()
        {
            ResolveMenuReferences();

            if (menuView != null && !registered)
            {
                registered = menuView.RegisterMenuExtension(this);
            }
        }

        private void OnDisable()
        {
            PrototypeTestLabServiceLocator.Unregister(service);
            service?.UnregisterAutomationHost();
            if (menuView != null && registered)
            {
                menuView.UnregisterMenuExtension(this);
            }

            registered = false;
        }

        public void Initialize(InventoryMenuExtensionContext context)
        {
            ResolveMenuReferences();

            service ??= new PrototypeTestLabService();
            PrototypeTestLabServiceLocator.Register(service);
            if (context != null && context.ContentRoot != null)
            {
                testLabView = context.ContentRoot.GetComponent<PrototypeTestLabView>();
                if (testLabView == null)
                {
                    testLabView = context.ContentRoot.gameObject.AddComponent<PrototypeTestLabView>();
                }
            }

            ConfigureService();
            testLabView?.Initialize(service);
        }

        public void Refresh()
        {
            ConfigureService();
            testLabView?.Refresh();
        }

        public void Show()
        {
            if (testLabView != null)
            {
                testLabView.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
        }

        public void Dispose()
        {
            PrototypeTestLabServiceLocator.Unregister(service);
            service?.UnregisterAutomationHost();
        }

        private void ResolveMenuReferences()
        {
            if (menuView == null)
            {
                menuView = GetComponent<InventoryScreenView>();
            }

            if (menuController == null && menuView != null)
            {
                menuController = menuView.GetComponent<InventoryScreenController>();
            }
        }

        private void ConfigureService()
        {
            if (service == null || menuController == null)
            {
                return;
            }

            service.Configure(PrototypeTestLabSceneContextResolver.Resolve(menuController));
            PrototypeTestLabServiceLocator.Register(service);
        }
    }
}
#endif
