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
            NetworkMovementInput requested = new NetworkMovementInput(7, new Vector2(3f, 4f), -90f, true);

            Assert.That(NetworkMovementInputValidator.TryNormalize(requested, 6, out NetworkMovementInput normalized, out string failure), Is.True, failure);
            Assert.That(normalized.Sequence, Is.EqualTo(7));
            Assert.That(normalized.Move.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(normalized.YawDegrees, Is.EqualTo(270f).Within(0.0001f));
            Assert.That(normalized.Sprint, Is.True);
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
            NetworkTransform networkTransform = prefab.GetComponent<NetworkTransform>();
            Assert.That(networkTransform, Is.Not.Null);
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
