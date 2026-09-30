using System;

namespace UnityIsekaiGame.Beings
{
    [Serializable]
    public struct ActorSaveData
    {
        public string actorProfileId;
        public string beingDefinitionId;
        public string personDefinitionId;
        public float currentHealth;
        public float currentStamina;
        public float currentMana;
    }
}
