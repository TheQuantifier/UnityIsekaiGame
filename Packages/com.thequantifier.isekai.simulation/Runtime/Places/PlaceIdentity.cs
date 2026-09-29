using UnityEngine;

namespace UnityIsekaiGame.Places
{
    public sealed class PlaceIdentity : MonoBehaviour
    {
        [SerializeField] private PlaceDefinition definition;
        [SerializeField] private Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
        [SerializeField] private bool allowMultipleRuntimeRepresentations;

        public PlaceDefinition Definition => definition;
        public string PlaceId => definition == null ? string.Empty : definition.Id;
        public string DisplayName => definition == null ? name : definition.DisplayName;
        public Bounds LocalBounds => localBounds;
        public bool AllowMultipleRuntimeRepresentations => allowMultipleRuntimeRepresentations;
        public bool HasValidDefinition => definition != null && !string.IsNullOrWhiteSpace(definition.Id);

        private void OnValidate()
        {
            localBounds.size = new Vector3(
                Mathf.Max(0f, localBounds.size.x),
                Mathf.Max(0f, localBounds.size.y),
                Mathf.Max(0f, localBounds.size.z));
        }
    }
}
