using UnityEngine;
using UnityIsekaiGame.Input;

namespace UnityIsekaiGame.Interaction
{
    public sealed class CameraInteractionDetector : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private GameObject interactor;
        [SerializeField] private Transform rayOrigin;
        [SerializeField, Min(0.1f)] private float maxDistance = 3f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        private RaycastHit[] hitBuffer = new RaycastHit[32];

        private IInteractable currentInteractable;
        private RaycastHit currentHit;

        public IInteractable CurrentInteractable => currentInteractable;
        public RaycastHit CurrentHit => currentHit;
        public bool HasTarget => currentInteractable != null;
        public float MaxDistance => maxDistance;
        public LayerMask InteractionMask => interactionMask;
        public QueryTriggerInteraction TriggerInteraction => triggerInteraction;
        public GameObject Interactor => interactor;
        public Transform RayOrigin => rayOrigin;

        private void Reset()
        {
            rayOrigin = transform;
        }

        private void Awake()
        {
            if (interactor == null)
            {
                interactor = gameObject;
            }

            if (rayOrigin == null)
            {
                rayOrigin = transform;
            }
        }

        private void Update()
        {
            RefreshTarget();

            if (input == null || !input.ConsumeInteract() || currentInteractable == null)
            {
                return;
            }

            InteractionContext context = CreateContext();
            if (currentInteractable.CanInteract(context))
            {
                currentInteractable.Interact(context);
            }
        }

        public void RefreshTarget()
        {
            currentInteractable = null;
            currentHit = default;

            if (rayOrigin == null)
            {
                return;
            }

            int hitCount = Physics.RaycastNonAlloc(rayOrigin.position, rayOrigin.forward, hitBuffer, maxDistance, interactionMask, triggerInteraction);
            while (hitCount == hitBuffer.Length)
            {
                hitBuffer = new RaycastHit[hitBuffer.Length * 2];
                hitCount = Physics.RaycastNonAlloc(rayOrigin.position, rayOrigin.forward, hitBuffer, maxDistance, interactionMask, triggerInteraction);
            }

            SortHitsByDistance(hitBuffer, hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || IsInteractorCollider(hitCollider))
                {
                    continue;
                }

                IInteractable interactable = hitCollider.GetComponentInParent<IInteractable>();
                if (interactable == null)
                {
                    // Volumes such as area triggers must not steal focus. Solid scene geometry is
                    // an occluder, so it stops interactions from passing through walls and props.
                    if (!hitCollider.isTrigger)
                    {
                        return;
                    }

                    continue;
                }

                InteractionContext context = new InteractionContext(interactor, rayOrigin, hit);
                if (!interactable.CanInteract(context))
                {
                    // An unavailable target still occludes objects behind it.
                    return;
                }

                currentHit = hit;
                currentInteractable = interactable;
                return;
            }
        }

        private InteractionContext CreateContext()
        {
            return new InteractionContext(interactor, rayOrigin, currentHit);
        }

        private bool IsInteractorCollider(Collider hitCollider)
        {
            return interactor != null && hitCollider.transform.IsChildOf(interactor.transform);
        }

        private static void SortHitsByDistance(RaycastHit[] hits, int count)
        {
            // Physics.RaycastNonAlloc does not guarantee result order. Insertion sort is cheap for
            // the small number of colliders normally crossed by an interaction ray and allocates
            // no per-frame comparer/delegate objects.
            for (int i = 1; i < count; i++)
            {
                RaycastHit value = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > value.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }

                hits[j + 1] = value;
            }
        }
    }
}
