using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
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
                true), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Reconciliation_corrects_only_error_beyond_tolerance()
        {
            Vector3 correctionError = LocalPlayerMovementBridge.CalculateCorrectionError(
                new Vector3(1f, -0.5f, 0f),
                0.25f,
                0.1f,
                false);

            Assert.That(correctionError.x, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(correctionError.y, Is.EqualTo(-0.4f).Within(0.0001f));
            Assert.That(correctionError.z, Is.Zero.Within(0.0001f));
        }
    }
}
