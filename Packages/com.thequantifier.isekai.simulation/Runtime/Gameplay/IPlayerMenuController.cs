namespace UnityIsekaiGame.Gameplay
{
    public interface IPlayerMenuController
    {
        bool IsOpen { get; }
        void CloseForPrototypeReset();
        void BeginPersistenceRestore();
        void CompletePersistenceRestore();
    }
}
