using UnityEngine;

namespace UnityIsekaiGame.ActorLifecycle
{
    public static class ActorLifecycleUtility
    {
        public static bool CanAct(GameObject actorObject)
        {
            if (actorObject == null)
            {
                return false;
            }

            ActorLifecycleController lifecycle = actorObject.GetComponentInParent<ActorLifecycleController>();
            return lifecycle == null || lifecycle.CanAct;
        }

        public static ActorLifecycleState GetState(GameObject actorObject)
        {
            ActorLifecycleController lifecycle = actorObject == null ? null : actorObject.GetComponentInParent<ActorLifecycleController>();
            return lifecycle == null ? ActorLifecycleState.Active : lifecycle.State;
        }
    }
}
