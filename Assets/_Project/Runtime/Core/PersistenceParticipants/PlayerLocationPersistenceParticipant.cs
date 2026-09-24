using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Player;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerLocationPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const int CurrentParticipantSchemaVersion = 1;
        public const string ParticipantKeyValue = "player.location";
        public const string SameSceneOnlyMode = "same-scene-v1";

        private const float MaximumCoordinateMagnitude = 10000f;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;
        private readonly string sceneKey;
        private readonly string defaultSpawnPointId;
        private readonly Transform playerRoot;
        private readonly PlayerInputReader input;
        private readonly IPlayerMenuController inventoryScreenController;
        private readonly CurrentPlaceTracker placeTracker;

        public PlayerLocationPersistenceParticipant(
            Transform playerRoot,
            Func<DefinitionRegistry> registryProvider,
            string ownerId,
            string sceneKey,
            string defaultSpawnPointId,
            PlayerInputReader input = null,
            IPlayerMenuController inventoryScreenController = null,
            CurrentPlaceTracker placeTracker = null)
        {
            this.playerRoot = playerRoot;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
            this.sceneKey = string.IsNullOrWhiteSpace(sceneKey) ? ResolveLoadedSceneKey() : sceneKey;
            this.defaultSpawnPointId = string.IsNullOrWhiteSpace(defaultSpawnPointId) ? "spawn.prototype.default" : defaultSpawnPointId;
            this.input = input;
            this.inventoryScreenController = inventoryScreenController;
            this.placeTracker = placeTracker;
        }

        public event Action<LocationRestoreEventArgs> LocationRestoreStarted;
        public event Action<LocationRestoreEventArgs> LocationRestoreCompleted;
        public event Action<LocationFallbackEventArgs> LocationFallbackUsed;

        public string ParticipantKey => ParticipantKeyValue;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.PositionAndPlace;
        public int LoadPriority => 0;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerQuestContractPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => true;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (playerRoot == null)
            {
                return PersistenceParticipantSaveResult.Failure("Player location capture failed because player root is missing.");
            }

            Vector3 position = playerRoot.position;
            Quaternion rotation = Normalize(playerRoot.rotation);
            if (!IsFinite(position))
            {
                return PersistenceParticipantSaveResult.Failure("Player location capture failed because player position is not finite.");
            }

            if (!IsValidRotation(rotation))
            {
                return PersistenceParticipantSaveResult.Failure("Player location capture failed because player rotation is invalid.");
            }

            PlayerLocationSaveData saveData = new PlayerLocationSaveData
            {
                schemaVersion = CurrentParticipantSchemaVersion,
                sceneKey = sceneKey,
                sceneBuildIndex = SceneManager.GetActiveScene().buildIndex,
                diagnosticSceneName = SceneManager.GetActiveScene().name,
                placeId = placeTracker == null ? string.Empty : placeTracker.CurrentPlaceId,
                positionX = position.x,
                positionY = position.y,
                positionZ = position.z,
                rotationX = rotation.x,
                rotationY = rotation.y,
                rotationZ = rotation.z,
                rotationW = rotation.w,
                spawnPointId = defaultSpawnPointId,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                locationMode = SameSceneOnlyMode
            };

            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Player location snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported player location participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Player location payload is empty.");
            }

            PlayerLocationSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PlayerLocationSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Player location payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Player location payload did not parse.");
            }

            if (saveData.schemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported player location payload schema version {saveData.schemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(saveData.sceneKey))
            {
                return PersistenceParticipantPrepareResult.Failure("Player location payload is missing a scene key.");
            }

            if (!ValidateLoadedSceneKeys(out string sceneKeyFailure))
            {
                return PersistenceParticipantPrepareResult.Failure(sceneKeyFailure);
            }

            if (!string.Equals(saveData.sceneKey, sceneKey, StringComparison.Ordinal))
            {
                return PersistenceParticipantPrepareResult.Failure($"Cross-scene restore from '{saveData.sceneKey}' to loaded scene '{sceneKey}' is not supported by Feature 4.5.");
            }

            Vector3 savedPosition = new Vector3(saveData.positionX, saveData.positionY, saveData.positionZ);
            Quaternion savedRotation = new Quaternion(saveData.rotationX, saveData.rotationY, saveData.rotationZ, saveData.rotationW);
            if (!IsFinite(savedPosition))
            {
                return PersistenceParticipantPrepareResult.Failure("Player location payload contains a non-finite position.");
            }

            if (Mathf.Abs(savedPosition.x) > MaximumCoordinateMagnitude
                || Mathf.Abs(savedPosition.y) > MaximumCoordinateMagnitude
                || Mathf.Abs(savedPosition.z) > MaximumCoordinateMagnitude)
            {
                return PersistenceParticipantPrepareResult.Failure("Player location payload position is outside the supported prototype bounds.");
            }

            if (!IsValidRotation(savedRotation))
            {
                return PersistenceParticipantPrepareResult.Failure("Player location payload contains an invalid rotation.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            PlaceDefinition place = null;
            if (!string.IsNullOrWhiteSpace(saveData.placeId))
            {
                if (registry == null || !registry.DefinitionsById.TryGetValue(saveData.placeId, out IGameDefinition definition) || definition is not PlaceDefinition resolvedPlace)
                {
                    return PersistenceParticipantPrepareResult.Failure($"Player location payload references unknown place '{saveData.placeId}'.");
                }

                if (!string.IsNullOrWhiteSpace(resolvedPlace.SceneKey) && !string.Equals(resolvedPlace.SceneKey, saveData.sceneKey, StringComparison.Ordinal))
                {
                    return PersistenceParticipantPrepareResult.Failure($"Player location place '{resolvedPlace.Id}' belongs to scene '{resolvedPlace.SceneKey}', not saved scene '{saveData.sceneKey}'.");
                }

                place = resolvedPlace;
            }

            PlayerSpawnPoint spawn = null;
            bool useFallback = !IsPositionSafe(savedPosition, out string fallbackReason);
            Vector3 targetPosition = savedPosition;
            Quaternion targetRotation = Normalize(savedRotation);
            if (useFallback && string.Equals(fallbackReason, "NoGroundBelowSavedPosition", StringComparison.Ordinal)
                && SpawnGroundingUtility.TryGroundWhenUnsupported(savedPosition, playerRoot, out Vector3 groundedSavedPosition, out string groundingReason))
            {
                targetPosition = groundedSavedPosition;
                targetRotation = Normalize(savedRotation);
                fallbackReason = groundingReason;
            }
            else if (useFallback)
            {
                spawn = ResolveSpawn(saveData.spawnPointId, place) ?? ResolveSpawn(defaultSpawnPointId, place) ?? ResolveAnySpawn(place);
                if (spawn == null)
                {
                    return PersistenceParticipantPrepareResult.Failure("Saved player position is unsafe and no fallback spawn point is available.");
                }

                targetPosition = ResolveSpawnTargetPosition(spawn.transform.position, out string spawnGroundingReason);
                targetRotation = Normalize(spawn.transform.rotation);
                if (!string.IsNullOrWhiteSpace(spawnGroundingReason))
                {
                    fallbackReason = string.IsNullOrWhiteSpace(fallbackReason)
                        ? spawnGroundingReason
                        : $"{fallbackReason};{spawnGroundingReason}";
                }
            }

            if (!IsFinite(targetPosition) || !IsValidRotation(targetRotation))
            {
                return PersistenceParticipantPrepareResult.Failure("Resolved player location target is invalid.");
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPlayerLocation(saveData, place, savedPosition, targetPosition, targetRotation, useFallback, spawn, fallbackReason));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (playerRoot == null)
            {
                return PersistenceParticipantCommitResult.Failure("Player location commit failed because player root is missing.");
            }

            if (preparedPayload is not PreparedPlayerLocation prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared player location payload has the wrong type.");
            }

            Vector3 previousPosition = playerRoot.position;
            Quaternion previousRotation = playerRoot.rotation;
            CharacterController characterController = playerRoot.GetComponent<CharacterController>();
            bool wasControllerEnabled = characterController != null && characterController.enabled;
            bool preserveOpenMenu = inventoryScreenController != null && inventoryScreenController.IsOpen;

            LocationRestoreStarted?.Invoke(new LocationRestoreEventArgs(prepared.SaveData.sceneKey, prepared.Place, prepared.TargetPosition, prepared.TargetRotation, prepared.FallbackUsed));

            using (LocationRestoreGuard.Enter())
            {
                try
                {
                    input?.SetGameplayInputBlocked(true);
                    input?.ClearGameplayActionQueues();
                    if (preserveOpenMenu)
                    {
                        inventoryScreenController.BeginPersistenceRestore();
                    }
                    else
                    {
                        inventoryScreenController?.CloseForPrototypeReset();
                    }

                    if (characterController != null)
                    {
                        characterController.enabled = false;
                    }

                    playerRoot.SetPositionAndRotation(prepared.TargetPosition, prepared.TargetRotation);
                    playerRoot.GetComponent<FirstPersonCharacterMotor>()?.ResetTransientMotionForPersistenceRestore();
                    foreach (FirstPersonCameraLook cameraLook in playerRoot.GetComponentsInChildren<FirstPersonCameraLook>(true))
                    {
                        cameraLook.SyncToCurrentRotationForPersistenceRestore();
                    }

                    if (characterController != null)
                    {
                        characterController.enabled = wasControllerEnabled;
                    }

                    placeTracker?.ForceCurrentPlace(prepared.Place, isRestoration: true);
                    input?.ClearGameplayActionQueues();
                    if (preserveOpenMenu)
                    {
                        inventoryScreenController.CompletePersistenceRestore();
                    }
                    else
                    {
                        input?.SetGameplayInputBlocked(false);
                    }

                    if (prepared.FallbackUsed)
                    {
                        LocationFallbackUsed?.Invoke(new LocationFallbackEventArgs(
                            prepared.SaveData.sceneKey,
                            prepared.SaveData.placeId,
                            prepared.SavedPosition,
                            prepared.TargetPosition,
                            prepared.SpawnPoint == null ? string.Empty : prepared.SpawnPoint.SpawnPointId,
                            prepared.FallbackReason,
                            $"Saved position was unsafe; used fallback spawn. Reason={prepared.FallbackReason} Saved={Format(prepared.SavedPosition)} Fallback={Format(prepared.TargetPosition)} Spawn='{(prepared.SpawnPoint == null ? string.Empty : prepared.SpawnPoint.SpawnPointId)}'."));
                    }

                    LocationRestoreCompleted?.Invoke(new LocationRestoreEventArgs(prepared.SaveData.sceneKey, prepared.Place, prepared.TargetPosition, prepared.TargetRotation, prepared.FallbackUsed));
                    return PersistenceParticipantCommitResult.Success(prepared.FallbackUsed
                        ? "Player location restored using a safe grounded fallback."
                        : "Player location restored.");
                }
                catch (Exception exception)
                {
                    if (characterController != null)
                    {
                        characterController.enabled = false;
                    }

                    playerRoot.SetPositionAndRotation(previousPosition, previousRotation);
                    if (characterController != null)
                    {
                        characterController.enabled = wasControllerEnabled;
                    }

                    input?.SetGameplayInputBlocked(false);
                    if (preserveOpenMenu)
                    {
                        inventoryScreenController.CompletePersistenceRestore();
                    }

                    return PersistenceParticipantCommitResult.Failure($"Player location commit failed; previous transform restored: {exception.Message}");
                }
            }
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        public string BuildDiagnosticSummary()
        {
            if (playerRoot == null)
            {
                return $"Scene: {sceneKey}\nPlayer location: missing player root";
            }

            Vector3 p = playerRoot.position;
            Quaternion r = playerRoot.rotation;
            string place = placeTracker == null || placeTracker.CurrentPlace == null
                ? "None"
                : $"{placeTracker.CurrentPlace.DisplayName} ({placeTracker.CurrentPlace.Id})";
            return $"Scene: {sceneKey}\nPlace: {place}\nPosition: {p.x:0.00}, {p.y:0.00}, {p.z:0.00}\nRotation: {r.x:0.###}, {r.y:0.###}, {r.z:0.###}, {r.w:0.###}";
        }

        private PlayerSpawnPoint ResolveSpawn(string spawnPointId, PlaceDefinition place)
        {
            if (string.IsNullOrWhiteSpace(spawnPointId))
            {
                return null;
            }

            return UnityEngine.Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude)
                .Where(spawn => spawn != null && string.Equals(spawn.SpawnPointId, spawnPointId, StringComparison.Ordinal))
                .OrderByDescending(spawn => IsPlaceCompatible(spawn.Place, place))
                .ThenByDescending(spawn => spawn.Priority)
                .FirstOrDefault();
        }

        private PlayerSpawnPoint ResolveAnySpawn(PlaceDefinition place)
        {
            return UnityEngine.Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude)
                .Where(spawn => spawn != null && (place == null || IsPlaceCompatible(spawn.Place, place)))
                .OrderByDescending(spawn => spawn.Priority)
                .ThenBy(spawn => spawn.SpawnPointId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static bool IsPlaceCompatible(PlaceDefinition spawnPlace, PlaceDefinition savedPlace)
        {
            return savedPlace == null
                || spawnPlace == null
                || PlaceHierarchyUtility.ContainsOrIs(savedPlace, spawnPlace)
                || PlaceHierarchyUtility.ContainsOrIs(spawnPlace, savedPlace);
        }

        private bool IsPositionSafe(Vector3 position, out string failureReason)
        {
            failureReason = string.Empty;
            if (!IsFinite(position))
            {
                failureReason = "PositionNotFinite";
                return false;
            }

            if (!HasGroundBelow(position))
            {
                failureReason = "NoGroundBelowSavedPosition";
                return false;
            }

            Collider[] overlaps = Physics.OverlapSphere(position + Vector3.up * 0.9f, 0.35f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap != null && !IsPlayerOwnedCollider(overlap))
                {
                    failureReason = "BodySpaceOverlapsCollision";
                    return false;
                }
            }

            return true;
        }

        private Vector3 ResolveSpawnTargetPosition(Vector3 spawnPosition, out string groundingReason)
        {
            groundingReason = string.Empty;
            if (SpawnGroundingUtility.TryGroundWhenUnsupported(spawnPosition, playerRoot, out Vector3 groundedPosition, out string reason))
            {
                groundingReason = reason;
                return groundedPosition;
            }

            groundingReason = reason;
            return spawnPosition;
        }

        private bool HasGroundBelow(Vector3 position)
        {
            return SpawnGroundingUtility.HasSolidColliderBelow(position, playerRoot);
        }

        private bool IsPlayerOwnedCollider(Collider candidate)
        {
            return playerRoot != null
                && candidate != null
                && (candidate.transform == playerRoot || candidate.transform.IsChildOf(playerRoot));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsValidRotation(Quaternion value)
        {
            float magnitude = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w) && magnitude > 0.0001f;
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            return new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude);
        }

        private static string Format(Vector3 value)
        {
            return $"{value.x:0.###},{value.y:0.###},{value.z:0.###}";
        }

        private static string ResolveLoadedSceneKey()
        {
            SceneKeyIdentity[] identities = UnityEngine.Object.FindObjectsByType<SceneKeyIdentity>(FindObjectsInactive.Exclude);
            SceneKeyIdentity first = identities.FirstOrDefault(identity => identity != null && !string.IsNullOrWhiteSpace(identity.SceneKey));
            return first == null ? "scene.prototype" : first.SceneKey;
        }

        private static bool ValidateLoadedSceneKeys(out string failureReason)
        {
            failureReason = string.Empty;
            SceneKeyIdentity[] identities = UnityEngine.Object.FindObjectsByType<SceneKeyIdentity>(FindObjectsInactive.Exclude);
            int keyedCount = identities.Count(identity => identity != null && !string.IsNullOrWhiteSpace(identity.SceneKey));
            int distinctCount = identities
                .Where(identity => identity != null && !string.IsNullOrWhiteSpace(identity.SceneKey))
                .Select(identity => identity.SceneKey)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (keyedCount > distinctCount)
            {
                failureReason = "Duplicate loaded scene keys were detected.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPlayerLocation
        {
            public PreparedPlayerLocation(PlayerLocationSaveData saveData, PlaceDefinition place, Vector3 savedPosition, Vector3 targetPosition, Quaternion targetRotation, bool fallbackUsed, PlayerSpawnPoint spawnPoint, string fallbackReason)
            {
                SaveData = saveData;
                Place = place;
                SavedPosition = savedPosition;
                TargetPosition = targetPosition;
                TargetRotation = targetRotation;
                FallbackUsed = fallbackUsed;
                SpawnPoint = spawnPoint;
                FallbackReason = string.IsNullOrWhiteSpace(fallbackReason) ? string.Empty : fallbackReason;
            }

            public PlayerLocationSaveData SaveData { get; }
            public PlaceDefinition Place { get; }
            public Vector3 SavedPosition { get; }
            public Vector3 TargetPosition { get; }
            public Quaternion TargetRotation { get; }
            public bool FallbackUsed { get; }
            public PlayerSpawnPoint SpawnPoint { get; }
            public string FallbackReason { get; }
        }
    }

    public sealed class LocationRestoreEventArgs : EventArgs
    {
        public LocationRestoreEventArgs(string sceneKey, PlaceDefinition place, Vector3 position, Quaternion rotation, bool fallbackUsed)
        {
            SceneKey = sceneKey;
            Place = place;
            Position = position;
            Rotation = rotation;
            FallbackUsed = fallbackUsed;
        }

        public string SceneKey { get; }
        public PlaceDefinition Place { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool FallbackUsed { get; }
    }

    public sealed class LocationFallbackEventArgs : EventArgs
    {
        public LocationFallbackEventArgs(string sceneKey, string placeId, Vector3 savedPosition, Vector3 fallbackPosition, string spawnPointId, string reason, string message)
        {
            SceneKey = sceneKey;
            PlaceId = placeId;
            SavedPosition = savedPosition;
            FallbackPosition = fallbackPosition;
            SpawnPointId = spawnPointId;
            Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason;
            Message = message;
        }

        public string SceneKey { get; }
        public string PlaceId { get; }
        public Vector3 SavedPosition { get; }
        public Vector3 FallbackPosition { get; }
        public string SpawnPointId { get; }
        public string Reason { get; }
        public string Message { get; }
    }
}
