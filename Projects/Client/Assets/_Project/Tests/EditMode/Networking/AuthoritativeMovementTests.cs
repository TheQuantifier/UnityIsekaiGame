using System.IO;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Configuration;
using UnityIsekaiGame.Editor;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Client;

namespace UnityIsekaiGame.Tests
{
    public sealed class AuthoritativeMovementTests
    {
        [Test]
        public void Movement_input_is_clamped_and_yaw_is_normalized()
        {
            NetworkMovementInput requested = new NetworkMovementInput(7, new Vector2(3f, 4f), -90f, true, true);

            Assert.That(NetworkMovementInputValidator.TryNormalize(requested, 6, out NetworkMovementInput normalized, out string failure), Is.True, failure);
            Assert.That(normalized.Sequence, Is.EqualTo(7));
            Assert.That(normalized.Move.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(normalized.YawDegrees, Is.EqualTo(270f).Within(0.0001f));
            Assert.That(normalized.Sprint, Is.True);
            Assert.That(normalized.Jump, Is.True,
                "Jump intent must remain atomic with the input sequence used for reconciliation.");
        }

        [Test]
        public void Movement_input_rejects_replayed_and_non_finite_commands()
        {
            Assert.That(NetworkMovementInputValidator.TryNormalize(
                new NetworkMovementInput(4, Vector2.up, 0f, false),
                4,
                out _,
                out string replayFailure), Is.False);
            Assert.That(replayFailure, Does.Contain("not newer"));

            Assert.That(NetworkMovementInputValidator.TryNormalize(
                new NetworkMovementInput(5, new Vector2(float.NaN, 0f), 0f, false),
                4,
                out _,
                out string finiteFailure), Is.False);
            Assert.That(finiteFailure, Does.Contain("non-finite"));
        }

        [Test]
        public void Movement_sequence_comparison_handles_uint_wraparound()
        {
            Assert.That(NetworkMovementInputValidator.IsNewer(uint.MaxValue, uint.MaxValue - 1), Is.True);
            Assert.That(NetworkMovementInputValidator.IsNewer(1, uint.MaxValue), Is.True);
            Assert.That(NetworkMovementInputValidator.IsNewer(uint.MaxValue - 1, 1), Is.False);
        }

        [Test]
        public void Network_player_prefab_has_server_authoritative_movement_components()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalNetworkFoundationAuthoring.PlayerActorPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkPlayerActor>(), Is.Not.Null);
            NetworkPlayerMovement movement = prefab.GetComponent<NetworkPlayerMovement>();
            Assert.That(movement, Is.Not.Null);
            Assert.That(movement.FallRecoveryDistance, Is.GreaterThanOrEqualTo(1f));
            Assert.That(movement.FallRecoveryNearbyRadius, Is.EqualTo(10f).Within(0.001f));
            NetworkTransform networkTransform = prefab.GetComponent<NetworkTransform>();
            Assert.That(networkTransform, Is.Not.Null);
            Assert.That(networkTransform.Interpolate, Is.True,
                "Remote player replicas must interpolate server snapshots; only the owning client disables interpolation for local prediction.");
            Assert.That(networkTransform.UseUnreliableDeltas, Is.True,
                "Frequent movement snapshots must not queue behind a dropped reliable transform packet.");
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
        }

        [Test]
        public void Local_network_timing_uses_sixty_hertz_authority_updates()
        {
            Assert.That(LocalServerEndpoint.DefaultTickRate, Is.EqualTo(60u));
            Assert.That(1f / LocalServerEndpoint.DefaultTickRate, Is.EqualTo(1f / 60f).Within(0.000001f));
        }

        [Test]
        public void Fall_recovery_uses_the_nearest_surface_below_at_the_correct_controller_height()
        {
            GameObject player = new GameObject("Recovery Player");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                CharacterController controller = player.AddComponent<CharacterController>();
                controller.height = 2f;
                controller.center = new Vector3(0f, 1f, 0f);
                player.transform.position = new Vector3(0f, 8f, 0f);
                ground.name = "Ground Below";
                ground.transform.SetPositionAndRotation(new Vector3(0f, 2f, 0f), Quaternion.identity);
                ground.transform.localScale = new Vector3(5f, 1f, 5f);
                Physics.SyncTransforms();

                bool recovered = AuthoritativeFallRecovery.TryResolve(
                    player.transform.position,
                    player.transform,
                    10f,
                    out Vector3 position,
                    out string reason);

                Assert.That(recovered, Is.True, reason);
                Assert.That(position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(position.z, Is.EqualTo(0f).Within(0.001f));
                Assert.That(position.y, Is.EqualTo(2.52f).Within(0.01f));
                Assert.That(reason, Does.StartWith("SurfaceBelow:"));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(ground);
            }
        }

        [Test]
        public void Fall_recovery_moves_up_to_a_surface_above_when_the_column_has_nothing_below()
        {
            GameObject player = new GameObject("Recovery Player");
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                CharacterController controller = player.AddComponent<CharacterController>();
                controller.height = 2f;
                controller.center = new Vector3(0f, 1f, 0f);
                player.transform.position = new Vector3(0f, -5f, 0f);
                platform.name = "Platform Above";
                platform.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                platform.transform.localScale = new Vector3(5f, 1f, 5f);
                Physics.SyncTransforms();

                bool recovered = AuthoritativeFallRecovery.TryResolve(
                    player.transform.position,
                    player.transform,
                    10f,
                    out Vector3 position,
                    out string reason);

                Assert.That(recovered, Is.True, reason);
                Assert.That(position.y, Is.EqualTo(0.52f).Within(0.01f));
                Assert.That(reason, Does.StartWith("SurfaceAbove:"));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(platform);
            }
        }

        [Test]
        public void Fall_recovery_searches_nearby_columns_when_the_current_column_is_empty()
        {
            GameObject player = new GameObject("Recovery Player");
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                CharacterController controller = player.AddComponent<CharacterController>();
                controller.height = 2f;
                controller.center = new Vector3(0f, 1f, 0f);
                player.transform.position = new Vector3(0f, -5f, 0f);
                platform.name = "Nearby Platform";
                platform.transform.SetPositionAndRotation(new Vector3(5f, 0f, 0f), Quaternion.identity);
                platform.transform.localScale = new Vector3(2f, 1f, 2f);
                Physics.SyncTransforms();

                bool recovered = AuthoritativeFallRecovery.TryResolve(
                    player.transform.position,
                    player.transform,
                    10f,
                    out Vector3 position,
                    out string reason);

                Assert.That(recovered, Is.True, reason);
                Assert.That(Vector2.Distance(Vector2.zero, new Vector2(position.x, position.z)), Is.LessThanOrEqualTo(10.001f));
                Assert.That(position.x, Is.GreaterThan(3.9f));
                Assert.That(position.y, Is.EqualTo(0.52f).Within(0.01f));
                Assert.That(reason, Does.StartWith("NearbySurface:"));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(platform);
            }
        }

        [Test]
        public void Fall_recovery_returns_false_when_no_surface_exists_for_safe_preset_fallback()
        {
            GameObject player = new GameObject("Recovery Player");
            try
            {
                player.AddComponent<CharacterController>();
                player.transform.position = new Vector3(2500f, -500f, 2500f);
                Physics.SyncTransforms();

                bool recovered = AuthoritativeFallRecovery.TryResolve(
                    player.transform.position,
                    player.transform,
                    10f,
                    out Vector3 position,
                    out string reason);

                Assert.That(recovered, Is.False);
                Assert.That(position, Is.EqualTo(player.transform.position));
                Assert.That(reason, Does.StartWith("NoSolidSurfaceWithin"));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Delivery_timing_trace_samples_ten_times_per_second_at_sixty_hertz()
        {
            Assert.That(NetworkMovementTrace.DeliveryTimingSampleStride, Is.EqualTo(6u));
            Assert.That(NetworkMovementTrace.ShouldSampleDeliveryTiming(0u), Is.False);
            Assert.That(NetworkMovementTrace.ShouldSampleDeliveryTiming(5u), Is.False);
            Assert.That(NetworkMovementTrace.ShouldSampleDeliveryTiming(6u), Is.True);
            Assert.That(NetworkMovementTrace.ShouldSampleDeliveryTiming(12u), Is.True);
            Assert.That(NetworkMovementTrace.CalculateSequenceSpan(60u, 72u), Is.EqualTo(13u));
            Assert.That(NetworkMovementTrace.CalculateSequenceSpan(uint.MaxValue, 2u), Is.EqualTo(4u));
        }

        [Test]
        public void Movement_trace_can_be_toggled_by_a_runtime_control_file()
        {
            string controlFile = Path.GetTempFileName();
            string[] arguments = { NetworkMovementTrace.ControlFileFlag, controlFile };
            try
            {
                Assert.That(NetworkMovementTrace.IsRequested(arguments), Is.True);
                File.Delete(controlFile);
                Assert.That(NetworkMovementTrace.IsRequested(arguments), Is.False);
            }
            finally
            {
                if (File.Exists(controlFile)) File.Delete(controlFile);
            }
        }

        [Test]
        public void Token_bucket_allows_a_bounded_burst_and_refills_over_time()
        {
            var limiter = new TokenBucketRateLimiter(2d, 1d);
            Assert.That(limiter.TryConsume(10d), Is.True);
            Assert.That(limiter.TryConsume(10d), Is.True);
            Assert.That(limiter.TryConsume(10d), Is.False);
            Assert.That(limiter.TryConsume(10.5d), Is.False);
            Assert.That(limiter.TryConsume(11d), Is.True);
        }

        [Test]
        public void Authoritative_movement_state_keeps_sequence_and_simulated_position_atomic()
        {
            var state = new NetworkMovementState(
                true,
                91ul,
                407u,
                new Vector3(12f, 3f, -8f),
                135f,
                7.5f,
                -2.5f,
                true,
                405u,
                405u);

            Assert.That(state.IsInitialized, Is.True);
            Assert.That(state.SimulationTick, Is.EqualTo(91ul));
            Assert.That(state.InputSequence, Is.EqualTo(407u));
            Assert.That(state.Position, Is.EqualTo(new Vector3(12f, 3f, -8f)));
            Assert.That(state.Grounded, Is.True);
            Assert.That(state.LastProcessedJumpSequence, Is.EqualTo(405u));
            Assert.That(state.LastExecutedJumpSequence, Is.EqualTo(405u));
        }

        [Test]
        public void Sequence_replay_preserves_unacknowledged_local_displacement()
        {
            Vector3 replayed = LocalPlayerMovementBridge.CalculateReplayedPosition(
                new Vector3(12f, 3f, 8f),
                new Vector3(10f, 2f, 5f),
                new Vector3(9f, 2.5f, 4f));

            Assert.That(replayed, Is.EqualTo(new Vector3(13f, 2.5f, 9f)),
                "Reconciliation must rebase the current prediction by the error measured at the acknowledged input, not by a delayed-current comparison.");
        }

        [Test]
        public void Authoritative_jump_outcome_explicitly_rejects_only_processed_unexecuted_prediction()
        {
            Assert.That(LocalPlayerMovementBridge.IsPredictedJumpRejected(40u, 39u, 41u), Is.False);
            Assert.That(LocalPlayerMovementBridge.IsPredictedJumpRejected(41u, 39u, 41u), Is.True);
            Assert.That(LocalPlayerMovementBridge.IsPredictedJumpRejected(41u, 41u, 41u), Is.False);
            Assert.That(LocalPlayerMovementBridge.IsPredictedJumpRejected(1u, uint.MaxValue, uint.MaxValue), Is.False,
                "A jump executed immediately before sequence wrap must remain accepted.");
        }

        [Test]
        public void Local_prediction_tuning_matches_authoritative_network_actor()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalNetworkFoundationAuthoring.PlayerActorPrefabPath);
            PlayerMovementSettings settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Packages/com.thequantifier.isekai.content/Content/Prototype/Configuration/PrototypePlayerMovementSettings.asset");
            Assert.That(prefab, Is.Not.Null);
            NetworkPlayerMovement movement = prefab.GetComponent<NetworkPlayerMovement>();

            Assert.That(settings, Is.Not.Null);
            Assert.That(movement, Is.Not.Null);
            Assert.That(movement.WalkSpeed, Is.EqualTo(settings.WalkSpeed).Within(0.0001f));
            Assert.That(movement.SprintMultiplier, Is.EqualTo(settings.SprintSpeedMultiplier).Within(0.0001f));
            Assert.That(movement.Acceleration, Is.EqualTo(settings.Acceleration).Within(0.0001f));
            Assert.That(movement.Deceleration, Is.EqualTo(settings.Deceleration).Within(0.0001f));
            Assert.That(movement.JumpHeight, Is.EqualTo(settings.JumpHeight).Within(0.0001f));
            Assert.That(movement.Gravity, Is.EqualTo(settings.Gravity).Within(0.0001f));
            Assert.That(movement.GroundedStickForce, Is.EqualTo(settings.GroundedStickForce).Within(0.0001f));
        }

        [Test]
        public void Input_send_accumulator_preserves_fractional_frame_time()
        {
            double accumulator = 0d;
            int sends = 0;
            for (int frame = 0; frame < 1000; frame++)
            {
                if (LocalPlayerMovementBridge.AdvanceInputSendAccumulator(
                        ref accumulator,
                        0.016d,
                        1d / 60d))
                {
                    sends++;
                }
            }

            Assert.That(sends, Is.EqualTo(960).Within(1));
        }

        [Test]
        public void Reconciliation_ignores_expected_network_prediction_lead()
        {
            float tolerance = LocalPlayerMovementBridge.CalculateHorizontalPredictionTolerance(
                0.12f,
                1.25f,
                7.5f,
                50ul,
                60f);

            Assert.That(tolerance, Is.EqualTo(0.745f).Within(0.001f));
            Assert.That(LocalPlayerMovementBridge.CalculateCorrectionError(
                new Vector3(0.5f, -0.3f, 0f),
                tolerance,
                0.08f,
                false,
                true), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Reconciliation_corrects_only_error_beyond_tolerance()
        {
            Vector3 correctionError = LocalPlayerMovementBridge.CalculateCorrectionError(
                new Vector3(1f, -0.5f, 0f),
                0.25f,
                0.1f,
                false,
                false);

            Assert.That(correctionError.x, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(correctionError.y, Is.EqualTo(-0.4f).Within(0.0001f));
            Assert.That(correctionError.z, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void Airborne_reconciliation_does_not_fight_predicted_moving_jump()
        {
            Vector3 correctionError = LocalPlayerMovementBridge.CalculateCorrectionError(
                new Vector3(-1f, -0.75f, 0.5f),
                0.1f,
                0.1f,
                true,
                true);

            Assert.That(correctionError, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Reconciliation_skips_zero_controller_moves_and_allows_server_landing_to_catch_up()
        {
            Assert.That(LocalPlayerMovementBridge.ShouldApplyControllerCorrection(Vector3.zero), Is.False);
            Assert.That(LocalPlayerMovementBridge.ShouldApplyControllerCorrection(new Vector3(0.001f, 0f, 0f)), Is.True);
            Assert.That(LocalPlayerMovementBridge.CalculateLandingPredictionGrace(0.08f, 0.25f, 17ul),
                Is.EqualTo(0.097f).Within(0.0001f));
            Assert.That(LocalPlayerMovementBridge.CalculateLandingPredictionGrace(0.08f, 0.25f, 1000ul),
                Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Reconciliation_allows_authority_to_catch_up_after_local_horizontal_movement()
        {
            Assert.That(LocalPlayerMovementBridge.CalculateMovementPredictionGrace(0.05f, 0.2f, 17ul),
                Is.EqualTo(0.067f).Within(0.0001f));
            Assert.That(LocalPlayerMovementBridge.CalculateMovementPredictionGrace(0.05f, 0.2f, 1000ul),
                Is.EqualTo(0.2f).Within(0.0001f));

            Vector3 delayedAuthorityError = new Vector3(-0.75f, 0f, 0.2f);
            Assert.That(LocalPlayerMovementBridge.CalculateCorrectionError(
                delayedAuthorityError,
                0.12f,
                0.08f,
                true,
                false), Is.EqualTo(Vector3.zero),
                "An in-flight server snapshot must not fight active local walking or sprinting.");
        }

        [Test]
        public void Stop_reconciliation_waits_for_the_exact_input_acknowledgement()
        {
            Assert.That(LocalPlayerMovementBridge.HasAcknowledgedSequence(41u, 42u), Is.False);
            Assert.That(LocalPlayerMovementBridge.HasAcknowledgedSequence(42u, 42u), Is.True);
            Assert.That(LocalPlayerMovementBridge.HasAcknowledgedSequence(43u, 42u), Is.True);
            Assert.That(LocalPlayerMovementBridge.HasAcknowledgedSequence(1u, uint.MaxValue), Is.True,
                "Acknowledgement comparison must remain valid across sequence wraparound.");
        }

        [Test]
        public void Stop_reconciliation_allows_server_braking_and_snapshot_delivery_to_settle()
        {
            float grace = LocalPlayerMovementBridge.CalculateStopSettlementGrace(
                7.5f,
                72f,
                50ul,
                60f);

            Assert.That(grace, Is.EqualTo(0.1875f).Within(0.0001f));
            Assert.That(grace, Is.GreaterThan(7.5f / 72f),
                "The hold must include both authoritative braking and return snapshot delivery.");
        }

        [Test]
        public void Sprint_prediction_obeys_authoritative_exhaustion_and_restart_threshold()
        {
            bool exhausted = false;
            NetworkVitalsState empty = new NetworkVitalsState(
                100f, 100f, 0f, 100f, 100f, 100f, NetworkActorLifeState.Active, 1u);
            Assert.That(LocalPlayerMovementBridge.EvaluatePredictedSprintAvailability(
                empty, 20f, ref exhausted), Is.False);
            Assert.That(exhausted, Is.True);

            NetworkVitalsState belowThreshold = new NetworkVitalsState(
                100f, 100f, 20f, 100f, 100f, 100f, NetworkActorLifeState.Active, 2u);
            Assert.That(LocalPlayerMovementBridge.EvaluatePredictedSprintAvailability(
                belowThreshold, 20f, ref exhausted), Is.False);

            NetworkVitalsState recovered = new NetworkVitalsState(
                100f, 100f, 20.1f, 100f, 100f, 100f, NetworkActorLifeState.Active, 3u);
            Assert.That(LocalPlayerMovementBridge.EvaluatePredictedSprintAvailability(
                recovered, 20f, ref exhausted), Is.True);
            Assert.That(exhausted, Is.False);
        }
    }
}
