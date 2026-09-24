using NUnit.Framework;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class PlayerStatusEffectsPersistenceTests
    {
        [Test]
        public void ResourcesAndStatusEffectsHaveDistinctAuthoritativeKeys()
        {
            Assert.That(PlayerResourcesPersistenceParticipant.Key, Is.EqualTo("player.resources"));
            Assert.That(PlayerStatusEffectsPersistenceParticipant.Key, Is.EqualTo("player.status-effects"));
            Assert.That(PlayerStatusEffectsPersistenceParticipant.Key, Is.Not.EqualTo(PlayerResourcesPersistenceParticipant.Key));
        }

        [Test]
        public void StatusPayloadContainsNoDuplicateVitalFields()
        {
            string json = PersistenceSerialization.Serialize(new PlayerStatusEffectsSaveData());
            Assert.That(json, Does.Contain("\"statuses\""));
            Assert.That(json, Does.Not.Contain("health"));
            Assert.That(json, Does.Not.Contain("mana"));
            Assert.That(json, Does.Not.Contain("stamina"));
        }

        [Test]
        public void StatusPayloadRoundTripsWithCurrentSchema()
        {
            PlayerStatusEffectsSaveData restored = PersistenceSerialization.Deserialize<PlayerStatusEffectsSaveData>(
                PersistenceSerialization.Serialize(new PlayerStatusEffectsSaveData { actorProfileId = "actor-profile.player" }));
            Assert.That(restored.schemaVersion, Is.EqualTo(PlayerStatusEffectsSaveData.CurrentSchemaVersion));
            Assert.That(restored.actorProfileId, Is.EqualTo("actor-profile.player"));
            Assert.That(restored.statuses, Is.Not.Null);
        }
    }
}
