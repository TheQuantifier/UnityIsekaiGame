using NUnit.Framework;
using System.IO;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Client;

namespace UnityIsekaiGame.Tests
{
    public sealed class AuthoritativeCombatProtocolTests
    {
        [Test]
        public void Commands_reject_replay_non_normalized_aim_and_invalid_action_contracts()
        {
            CombatCommandValidationResult replay = NetworkCombatCommandValidator.Validate(
                new NetworkCombatCommand(4u, CombatAuthorityCommandType.PrimaryAttack, Vector3.forward), 4u);
            Assert.That(replay.Succeeded, Is.False);
            Assert.That(replay.Failure, Is.EqualTo(CombatAuthorityFailure.ReplayedCommand));

            CombatCommandValidationResult aim = NetworkCombatCommandValidator.Validate(
                new NetworkCombatCommand(5u, CombatAuthorityCommandType.PrimaryAttack, Vector3.forward * 2f), 4u);
            Assert.That(aim.Succeeded, Is.False);
            Assert.That(aim.Failure, Is.EqualTo(CombatAuthorityFailure.InvalidAim));

            CombatCommandValidationResult missingAbility = NetworkCombatCommandValidator.Validate(
                new NetworkCombatCommand(5u, CombatAuthorityCommandType.CastAbility, Vector3.forward), 4u);
            Assert.That(missingAbility.Succeeded, Is.False);
            Assert.That(missingAbility.Failure, Is.EqualTo(CombatAuthorityFailure.ActionUnavailable));

            CombatCommandValidationResult attackWithAbility = NetworkCombatCommandValidator.Validate(
                new NetworkCombatCommand(5u, CombatAuthorityCommandType.PrimaryAttack, Vector3.forward, "ability.arcane-bolt"), 4u);
            Assert.That(attackWithAbility.Succeeded, Is.False);
            Assert.That(attackWithAbility.Failure, Is.EqualTo(CombatAuthorityFailure.InvalidCommand));
        }

        [Test]
        public void Valid_cast_command_and_sequence_wraparound_are_supported()
        {
            CombatCommandValidationResult result = NetworkCombatCommandValidator.Validate(
                new NetworkCombatCommand(1u, CombatAuthorityCommandType.CastAbility, Vector3.forward, "ability.arcane-bolt"), uint.MaxValue);
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        [Test]
        public void Combatant_snapshot_rejects_duplicate_ids_and_invalid_health()
        {
            NetworkCombatantState[] duplicate =
            {
                new NetworkCombatantState("scene.prototype.enemy.a", Vector3.zero, Quaternion.identity, 10f, 10f, false),
                new NetworkCombatantState("scene.prototype.enemy.a", Vector3.one, Quaternion.identity, 5f, 10f, false)
            };
            Assert.That(NetworkCombatantSnapshotValidator.TryValidate(duplicate, out string duplicateFailure), Is.False);
            Assert.That(duplicateFailure, Does.Contain("duplicate"));

            NetworkCombatantState[] invalidHealth =
            {
                new NetworkCombatantState("scene.prototype.enemy.a", Vector3.zero, Quaternion.identity, 11f, 10f, false)
            };
            Assert.That(NetworkCombatantSnapshotValidator.TryValidate(invalidHealth, out string healthFailure), Is.False);
            Assert.That(healthFailure, Does.Contain("invalid"));
        }

        [Test]
        public void Combatant_snapshot_generation_rejects_mixed_replication_payloads()
        {
            NetworkCombatantState[] snapshot =
            {
                new NetworkCombatantState("enemy.a", Vector3.zero, Quaternion.identity, 10f, 10f, false, 7u),
                new NetworkCombatantState("enemy.b", Vector3.one, Quaternion.identity, 10f, 10f, false, 6u)
            };

            Assert.That(NetworkCombatantSnapshotValidator.HasCommittedGeneration(snapshot, 7u, out string failure), Is.False);
            Assert.That(failure, Does.Contain("generation 6"));

            snapshot[1].SnapshotGeneration = 7u;
            Assert.That(NetworkCombatantSnapshotValidator.HasCommittedGeneration(snapshot, 7u, out failure), Is.True, failure);
        }

        [Test]
        public void Combat_result_messages_are_protocol_bounded()
        {
            NetworkCombatCommandResult result = NetworkCombatCommandResult.Reject(
                1u, CombatAuthorityFailure.ServerRejected, new string('界', 200), "ability.arcane-bolt");
            Assert.That(Encoding.UTF8.GetByteCount(result.MessageText),
                Is.LessThanOrEqualTo(CombatAuthorityLimits.MaximumResultMessageBytes));
        }

        [Test]
        public void Authoritative_vitals_spend_stamina_atomically_and_delay_recovery()
        {
            NetworkVitalsState initial = new NetworkVitalsState(100f, 100f, 20f, 100f, 100f, 100f, NetworkActorLifeState.Active, 1u);
            AuthoritativeVitalsModel model = new AuthoritativeVitalsModel(initial,
                new AuthoritativeVitalsTuning(0f, 10f, 0f, 0f, 0f, 1f, 0f));

            Assert.That(model.TrySpendStamina(12f, 5d), Is.True);
            Assert.That(model.State.Stamina, Is.EqualTo(8f).Within(0.001f));
            Assert.That(model.TrySpendStamina(9f, 5d), Is.False);
            Assert.That(model.State.Stamina, Is.EqualTo(8f).Within(0.001f));
            model.Advance(0.5f, 5.5d);
            Assert.That(model.State.Stamina, Is.EqualTo(8f).Within(0.001f));
            model.Advance(0.5f, 6.1d);
            Assert.That(model.State.Stamina, Is.GreaterThan(8f));
        }

        [Test]
        public void Connected_combat_components_reject_local_execution()
        {
            GameObject owner = new GameObject("Authoritative Combat Replica Test");
            try
            {
                PlayerMeleeCombat melee = owner.AddComponent<PlayerMeleeCombat>();
                PlayerSpellcaster spells = owner.AddComponent<PlayerSpellcaster>();
                melee.SetExternalAuthority(true);
                spells.SetExternalAuthority(true);

                Assert.That(melee.ExternalAuthorityActive, Is.True);
                Assert.That(melee.TryAttack().Started, Is.False);
                Assert.That(spells.ExternalAuthorityActive, Is.True);
                Assert.That(spells.TryCastPrimarySpell().Succeeded, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Replicated_entity_movement_uses_frame_rate_independent_smoothing()
        {
            float oneSixtieth = LocalCombatAuthorityBridge.CalculateReplicaInterpolationFactor(18f, 1f / 60f);
            float twoSixtieths = LocalCombatAuthorityBridge.CalculateReplicaInterpolationFactor(18f, 2f / 60f);
            float composed = 1f - (1f - oneSixtieth) * (1f - oneSixtieth);

            Assert.That(oneSixtieth, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(twoSixtieths, Is.EqualTo(composed).Within(0.0001f));
            Assert.That(LocalCombatAuthorityBridge.CalculateReplicaInterpolationFactor(18f, 0f), Is.Zero);
        }

        [Test]
        public void Online_ranged_attacks_use_authoritative_ammunition_and_projectile_simulation()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string serverCombat = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.server/Runtime/Networking/ServerPlayerCombatAuthority.cs"));
            string serverInventory = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.server/Runtime/Networking/ServerPlayerInventoryAuthority.cs"));
            string clientCombat = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.client/Runtime/Networking/LocalCombatAuthorityBridge.cs"));

            StringAssert.DoesNotContain("Online ranged weapon attacks remain disabled", serverCombat);
            StringAssert.Contains("CountAuthoritativeItem(ammunition)", serverCombat);
            StringAssert.Contains("TryConsumeAuthoritativeItem(ammunition, 1", serverCombat);
            StringAssert.Contains("PendingProjectile.ForRangedWeapon", serverCombat);
            StringAssert.Contains("projectile.RangedWeapon.DamageType", serverCombat);
            StringAssert.Contains("PublishAndPersist();", serverInventory);
            StringAssert.Contains("SpawnPredictedRangedProjectile", clientCombat);
            StringAssert.Contains("ProjectileVisualPrefab", clientCombat);
        }
    }
}
