using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Editor;
using UnityIsekaiGame.Networking;

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
            Assert.That(prefab.GetComponent<NetworkPlayerMovement>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
        }
    }
}
