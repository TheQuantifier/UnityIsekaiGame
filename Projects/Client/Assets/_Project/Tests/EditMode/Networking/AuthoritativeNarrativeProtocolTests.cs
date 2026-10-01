using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Client;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Tests
{
    public sealed class AuthoritativeNarrativeProtocolTests
    {
        [Test]
        public void Commands_reject_replays_invalid_types_and_missing_targets()
        {
            NarrativeCommandValidationResult replay = NetworkNarrativeCommandValidator.Validate(
                new NetworkNarrativeCommand(7u, NarrativeAuthorityCommandType.Interact, "interaction.point"), 7u);
            Assert.That(replay.Succeeded, Is.False);
            Assert.That(replay.Failure, Is.EqualTo(NarrativeAuthorityFailure.ReplayedCommand));

            NarrativeCommandValidationResult invalid = NetworkNarrativeCommandValidator.Validate(
                new NetworkNarrativeCommand(8u, NarrativeAuthorityCommandType.None), 7u);
            Assert.That(invalid.Succeeded, Is.False);
            Assert.That(invalid.Failure, Is.EqualTo(NarrativeAuthorityFailure.InvalidCommand));

            NarrativeCommandValidationResult missing = NetworkNarrativeCommandValidator.Validate(
                new NetworkNarrativeCommand(8u, NarrativeAuthorityCommandType.AcceptQuestListing), 7u);
            Assert.That(missing.Succeeded, Is.False);
            Assert.That(missing.Failure, Is.EqualTo(NarrativeAuthorityFailure.InvalidIdentifier));
        }

        [Test]
        public void Commands_reject_control_characters_and_negative_values()
        {
            NarrativeCommandValidationResult identifier = NetworkNarrativeCommandValidator.Validate(
                new NetworkNarrativeCommand(1u, NarrativeAuthorityCommandType.Interact, "interaction\nspoof"), 0u);
            Assert.That(identifier.Succeeded, Is.False);
            Assert.That(identifier.Failure, Is.EqualTo(NarrativeAuthorityFailure.InvalidIdentifier));

            NarrativeCommandValidationResult value = NetworkNarrativeCommandValidator.Validate(
                new NetworkNarrativeCommand(1u, NarrativeAuthorityCommandType.SetPartyReady, "self", value: -1), 0u);
            Assert.That(value.Succeeded, Is.False);
            Assert.That(value.Failure, Is.EqualTo(NarrativeAuthorityFailure.InvalidCommand));
        }

        [Test]
        public void Valid_party_creation_and_sequence_wraparound_are_supported()
        {
            NarrativeCommandValidationResult result = NetworkNarrativeCommandValidator.Validate(
                new NetworkNarrativeCommand(1u, NarrativeAuthorityCommandType.CreateParty, "Adventuring Party"), uint.MaxValue);
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        [Test]
        public void Result_messages_are_protocol_bounded()
        {
            NetworkNarrativeCommand command = new NetworkNarrativeCommand(1u, NarrativeAuthorityCommandType.Interact, "interaction.point");
            NetworkNarrativeCommandResult result = NetworkNarrativeCommandResult.Reject(
                command,
                NarrativeAuthorityFailure.ServerRejected,
                new string('界', 300));
            Assert.That(Encoding.UTF8.GetByteCount(result.MessageText),
                Is.LessThanOrEqualTo(NarrativeAuthorityLimits.MaximumResultMessageBytes));
        }

        [Test]
        public void Interaction_range_is_rechecked_against_authoritative_position()
        {
            GameObject root = new GameObject("Authoritative Interaction Range Test");
            try
            {
                InteractionPointSceneBinding binding = root.AddComponent<InteractionPointSceneBinding>();
                binding.ConfigureInteraction(3f, true);
                root.transform.position = new Vector3(10f, 0f, 10f);

                Assert.That(binding.IsWithinAuthoritativeRange(new Vector3(12.9f, 0f, 10f)), Is.True);
                Assert.That(binding.IsWithinAuthoritativeRange(new Vector3(14f, 0f, 10f)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Identical_in_flight_interactions_are_deduplicated()
        {
            NetworkNarrativeCommand pending = new NetworkNarrativeCommand(
                17u,
                NarrativeAuthorityCommandType.Interact,
                "interaction-point.guild-desk");

            Assert.That(LocalNarrativeAuthorityBridge.RepresentsSamePendingAction(
                pending,
                NarrativeAuthorityCommandType.Interact,
                "interaction-point.guild-desk",
                string.Empty,
                0,
                0), Is.True);
            Assert.That(LocalNarrativeAuthorityBridge.RepresentsSamePendingAction(
                pending,
                NarrativeAuthorityCommandType.Interact,
                "interaction-point.mayor-desk",
                string.Empty,
                0,
                0), Is.False);
        }

        [Test]
        public void Narrative_snapshots_apply_only_when_a_complete_generation_is_present()
        {
            System.Random random = new System.Random(8173);
            StringBuilder body = new StringBuilder(16000);
            for (int i = 0; i < 16000; i++) body.Append((char)('a' + random.Next(26)));
            string json = $"{{\"schemaVersion\":1,\"personId\":\"person.test\",\"payload\":\"{body}\"}}";
            string[] generationSeven = NetworkNarrativeSnapshotCodec.CreateChunks(json, 7u);
            string[] generationEight = NetworkNarrativeSnapshotCodec.CreateChunks(json.Replace("person.test", "person.next"), 8u);

            Assert.That(generationSeven.Length, Is.GreaterThan(1));
            Assert.That(NetworkNarrativeSnapshotCodec.TryAssemble(generationSeven, 7u, out string assembled), Is.True);
            Assert.That(assembled, Is.EqualTo(json));

            List<string> incomplete = new List<string>(generationSeven);
            incomplete.RemoveAt(incomplete.Count - 1);
            Assert.That(NetworkNarrativeSnapshotCodec.TryAssemble(incomplete, 7u, out _), Is.False);

            List<string> mixed = new List<string>(generationSeven) { [0] = generationEight[0] };
            Assert.That(NetworkNarrativeSnapshotCodec.TryAssemble(mixed, 7u, out _), Is.False,
                "Chunks from an uncommitted generation must never be parsed as a client snapshot.");
        }
    }
}
