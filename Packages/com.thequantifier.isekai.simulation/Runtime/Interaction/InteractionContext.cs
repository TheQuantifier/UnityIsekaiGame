using UnityEngine;

namespace UnityIsekaiGame.Interaction
{
    public readonly struct InteractionContext
    {
        public InteractionContext(GameObject interactor, Transform origin, RaycastHit hit)
        {
            Interactor = interactor;
            Origin = origin;
            Hit = hit;
        }

        public GameObject Interactor { get; }
        public Transform Origin { get; }
        public RaycastHit Hit { get; }
    }
}
