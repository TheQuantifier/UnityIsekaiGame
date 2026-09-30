using UnityEngine;

namespace UnityIsekaiGame.Persistence
{
    public sealed class SceneKeyIdentity : MonoBehaviour
    {
        [SerializeField] private string sceneKey = "scene.prototype";

        public string SceneKey => sceneKey;

        public void DevelopmentSetSceneKey(string key)
        {
            sceneKey = key;
        }
    }
}
