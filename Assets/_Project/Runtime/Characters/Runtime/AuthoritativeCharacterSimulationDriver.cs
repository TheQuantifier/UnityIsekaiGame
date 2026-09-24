using System;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Integration;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.CharacterSystem
{
    /// <summary>
    /// Single time authority for mutable character simulation. A server can disable
    /// automatic Unity-time advancement and call AdvanceAuthoritative directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthoritativeCharacterSimulationDriver : MonoBehaviour
    {
        [SerializeField] private CharacterSystemCoordinator character;
        [SerializeField] private CharacterResourceCollection resources;
        [SerializeField] private ActorBodyRuntime body;
        [SerializeField] private bool advanceWithUnityTime = true;
        [SerializeField, Min(0.05f)] private float fixedStepSeconds = 0.25f;
        [SerializeField, Min(1)] private int maximumStepsPerFrame = 8;

        private ICharacterSimulationClock clock;
        private ICharacterTransactionIdProvider transactionIds;
        private IDamageHealingService damageHealing = new DamageHealingService();
        private float accumulatedSeconds;

        public event Action<BodyBiologyAdvanceResult> BiologyAdvanced;
        public bool AdvanceWithUnityTime => advanceWithUnityTime;

        private void Awake()
        {
            ResolveReferences();
            clock ??= new UnityCharacterSimulationClock();
            transactionIds ??= new SequentialCharacterTransactionIdProvider();
        }

        private void Update()
        {
            if (!advanceWithUnityTime || clock == null)
            {
                return;
            }

            accumulatedSeconds += Mathf.Max(0f, clock.FrameDeltaSeconds);
            int steps = 0;
            while (accumulatedSeconds + CharacterResourceCollection.Epsilon >= fixedStepSeconds && steps < maximumStepsPerFrame)
            {
                double stepWorldTime = clock.WorldTimeSeconds - accumulatedSeconds + fixedStepSeconds;
                AdvanceAuthoritative(fixedStepSeconds, stepWorldTime, string.Empty);
                accumulatedSeconds -= fixedStepSeconds;
                steps++;
            }
        }

        public void ConfigureServices(ICharacterSimulationClock simulationClock, ICharacterTransactionIdProvider transactionIdProvider, bool automaticUnityTime)
        {
            clock = simulationClock ?? throw new ArgumentNullException(nameof(simulationClock));
            transactionIds = transactionIdProvider ?? throw new ArgumentNullException(nameof(transactionIdProvider));
            advanceWithUnityTime = automaticUnityTime;
        }

        public void ConfigureDamageHealing(IDamageHealingService service)
        {
            damageHealing = service ?? throw new ArgumentNullException(nameof(service));
        }

        public BodyBiologyAdvanceResult AdvanceAuthoritative(float elapsedSeconds, double worldTimeSeconds, string transactionId)
        {
            ResolveReferences();
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            string actorId = character == null ? body == null ? string.Empty : body.ActorBodyId : character.ActorId;
            string resolvedTransactionId = string.IsNullOrWhiteSpace(transactionId)
                ? (transactionIds ??= new SequentialCharacterTransactionIdProvider()).Next(actorId, "simulation-step", worldTimeSeconds)
                : transactionId;

            resources?.TickResources(elapsedSeconds, (float)Math.Max(0d, worldTimeSeconds));
            if (body == null || !body.IsReady)
            {
                return null;
            }

            BodyBiologyAdvanceResult result = new BodyBiologyFacade(body).Advance(new BodyBiologyAdvanceRequest(
                body.ActorBodyId,
                elapsedSeconds,
                resolvedTransactionId,
                "character-simulation-driver",
                damageHealing,
                damageTargetObject: gameObject,
                damageTargetActorId: actorId));
            BiologyAdvanced?.Invoke(result);
            return result;
        }

        private void ResolveReferences()
        {
            character = character == null ? GetComponent<CharacterSystemCoordinator>() : character;
            resources = resources == null ? character == null ? GetComponent<CharacterResourceCollection>() : character.Resources : resources;
            body = body == null ? character == null ? GetComponent<ActorBodyRuntime>() : character.Body : body;
        }
    }
}
