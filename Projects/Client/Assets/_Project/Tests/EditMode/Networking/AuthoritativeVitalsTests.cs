using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Tests
{
    public sealed class AuthoritativeVitalsTests
    {
        [Test]
        public void Sprint_spend_recovery_and_restart_threshold_are_server_simulated()
        {
            AuthoritativeVitalsModel model = CreateModel();

            Assert.That(model.EvaluateSprint(true, true, 5f, 0d), Is.True);
            Assert.That(model.State.Stamina, Is.Zero.Within(0.001f));

            model.Advance(0.5f, 0.5d);
            Assert.That(model.State.Stamina, Is.Zero.Within(0.001f), "Regeneration must respect the post-spend delay.");

            model.Advance(1f, 1.1d);
            Assert.That(model.State.Stamina, Is.EqualTo(15f).Within(0.001f));
            Assert.That(model.EvaluateSprint(true, true, 0.1f, 1.1d), Is.False, "Exhausted sprint cannot restart below its threshold.");

            model.Advance(1f, 2.1d);
            Assert.That(model.State.Stamina, Is.EqualTo(30f).Within(0.001f));
            Assert.That(model.EvaluateSprint(true, true, 0.1f, 2.1d), Is.True);
            Assert.That(model.State.Stamina, Is.EqualTo(28f).Within(0.001f));
        }

        [Test]
        public void Defeat_blocks_activity_until_an_authoritative_revive()
        {
            AuthoritativeVitalsModel model = CreateModel();

            Assert.That(model.TryDamage(100f), Is.True);
            Assert.That(model.State.IsDefeated, Is.True);
            Assert.That(model.EvaluateSprint(true, true, 1f, 0d), Is.False);
            Assert.That(model.TrySpendMana(10f, 0d), Is.False);
            Assert.That(model.TryHeal(10f), Is.False);

            Assert.That(model.ReviveToMaximum(), Is.True);
            Assert.That(model.State.LifeState, Is.EqualTo(NetworkActorLifeState.Active));
            Assert.That(model.State.Health, Is.EqualTo(model.State.MaximumHealth));
            Assert.That(model.State.Stamina, Is.EqualTo(model.State.MaximumStamina));
            Assert.That(model.State.Mana, Is.EqualTo(model.State.MaximumMana));
        }

        [Test]
        public void Vitals_state_rejects_non_finite_deserialized_values()
        {
            NetworkVitalsState state = new NetworkVitalsState(50f, 100f, 50f, 100f, 50f, 100f, NetworkActorLifeState.Active, 1u);
            state.Health = float.NaN;

            Assert.That(NetworkVitalsStateValidator.TryValidate(state, out string failure), Is.False);
            Assert.That(failure, Does.Contain("non-finite"));
        }

        [Test]
        public void Vitals_constructors_sanitize_non_finite_inputs()
        {
            NetworkVitalsState state = new NetworkVitalsState(float.NaN, float.PositiveInfinity, 5f, 10f, 5f, 10f, NetworkActorLifeState.Active, 1u);
            AuthoritativeVitalsTuning tuning = new AuthoritativeVitalsTuning(float.NaN, 1f, 1f, float.PositiveInfinity, 1f, 1f, 1f);

            Assert.That(state.Health, Is.Zero);
            Assert.That(state.MaximumHealth, Is.Zero);
            Assert.That(tuning.HealthRegenerationPerSecond, Is.Zero);
            Assert.That(tuning.SprintDrainPerSecond, Is.Zero);
        }

        [Test]
        public void External_resource_replica_rejects_local_mutation_and_accepts_server_snapshots()
        {
            GameObject root = new GameObject("Resource replica test");
            ResourceDefinition health = CreateResource(ResourceIds.Health, 100f);
            try
            {
                CharacterResourceCollection resources = root.AddComponent<CharacterResourceCollection>();
                resources.Configure(new[] { health });
                resources.SetExternalReplicaAuthority(true);

                ResourceChangeResult localDamage = resources.ApplyDamage(ResourceIds.Health, 10f, "test", "Must be rejected");
                Assert.That(localDamage.Succeeded, Is.False);
                Assert.That(localDamage.Code, Is.EqualTo("ExternalAuthority"));
                Assert.That(resources.ApplyExternalReplicaSnapshot(ResourceIds.Health, 175f, 250f), Is.True);
                Assert.That(resources.GetCurrent(ResourceIds.Health), Is.EqualTo(175f).Within(0.001f));
                Assert.That(resources.GetMaximum(ResourceIds.Health), Is.EqualTo(250f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(health);
            }
        }

        private static AuthoritativeVitalsModel CreateModel()
        {
            return new AuthoritativeVitalsModel(
                new NetworkVitalsState(100f, 100f, 100f, 100f, 100f, 100f, NetworkActorLifeState.Active, 1u),
                new AuthoritativeVitalsTuning(0f, 15f, 8f, 20f, 20f, 1f, 1.5f));
        }

        private static ResourceDefinition CreateResource(string id, float maximum)
        {
            ResourceDefinition definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            SetField(definition, "resourceId", id);
            SetField(definition, "displayName", id);
            SetField(definition, "defaultMaximum", maximum);
            return definition;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
