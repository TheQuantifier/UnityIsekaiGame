using System;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.Networking;

namespace UnityIsekaiGame.Tests
{
    public sealed class LocalConnectionProtocolTests
    {
        [TestCase("127.0.0.1", 7777)]
        [TestCase("localhost", 1)]
        [TestCase("game-server.local", 65535)]
        public void Endpoint_accepts_supported_addresses_and_ports(string address, int port)
        {
            Assert.That(LocalServerEndpoint.TryCreate(address, port, out LocalServerEndpoint endpoint, out string failure), Is.True, failure);
            Assert.That(endpoint.Port, Is.EqualTo(port));
        }

        [TestCase("", 7777)]
        [TestCase("127.0.0.1", 0)]
        [TestCase("127.0.0.1", 65536)]
        [TestCase("::1", 7777)]
        [TestCase("bad host name", 7777)]
        public void Endpoint_rejects_invalid_values(string address, int port)
        {
            Assert.That(LocalServerEndpoint.TryCreate(address, port, out _, out string failure), Is.False);
            Assert.That(failure, Is.Not.Empty);
        }

        [Test]
        public void Connection_request_round_trips()
        {
            ConnectionRequestPayload source = new ConnectionRequestPayload(Guid.NewGuid().ToString("N"), "player.one", "0.1.0");

            Assert.That(LocalConnectionProtocol.TryEncode(source, out byte[] payload, out string encodeFailure), Is.True, encodeFailure);
            Assert.That(LocalConnectionProtocol.TryDecode(payload, out ConnectionRequestPayload decoded, out string decodeFailure), Is.True, decodeFailure);
            Assert.That(decoded.ProtocolVersion, Is.EqualTo(LocalConnectionProtocol.CurrentVersion));
            Assert.That(decoded.ClientInstanceId, Is.EqualTo(source.ClientInstanceId));
            Assert.That(decoded.PlayerId, Is.EqualTo(source.PlayerId));
            Assert.That(decoded.BuildVersion, Is.EqualTo(source.BuildVersion));
        }

        [Test]
        public void Connection_request_rejects_malformed_and_oversized_payloads()
        {
            Assert.That(LocalConnectionProtocol.TryDecode(new byte[] { 1, 2, 3 }, out _, out string malformedFailure), Is.False);
            Assert.That(malformedFailure, Is.Not.Empty);

            byte[] oversized = Enumerable.Repeat((byte)'x', LocalConnectionProtocol.MaximumPayloadBytes + 1).ToArray();
            Assert.That(LocalConnectionProtocol.TryDecode(oversized, out _, out string oversizedFailure), Is.False);
            Assert.That(oversizedFailure, Does.Contain("exceeds"));
        }

        [Test]
        public void Admission_rejects_full_server_and_duplicate_player()
        {
            ConnectionRequestPayload request = new ConnectionRequestPayload(Guid.NewGuid().ToString("N"), "player.one", "0.1.0");
            Assert.That(LocalConnectionProtocol.TryEncode(request, out byte[] payload, out string failure), Is.True, failure);

            ConnectionAdmissionResult full = LocalConnectionAdmission.Evaluate(payload, 2, 2, Array.Empty<string>());
            Assert.That(full.Approved, Is.False);
            Assert.That(full.Reason, Does.Contain("full"));

            ConnectionAdmissionResult duplicate = LocalConnectionAdmission.Evaluate(payload, 1, 2, new[] { "PLAYER.ONE" });
            Assert.That(duplicate.Approved, Is.False);
            Assert.That(duplicate.Reason, Does.Contain("already connected"));
        }

        [Test]
        public void Admission_accepts_valid_unique_player()
        {
            ConnectionRequestPayload request = new ConnectionRequestPayload(Guid.NewGuid().ToString("N"), "player.two", "0.1.0");
            Assert.That(LocalConnectionProtocol.TryEncode(request, out byte[] payload, out string failure), Is.True, failure);

            ConnectionAdmissionResult result = LocalConnectionAdmission.Evaluate(payload, 1, 4, new[] { "player.one" });

            Assert.That(result.Approved, Is.True, result.Reason);
            Assert.That(result.Request.PlayerId, Is.EqualTo("player.two"));
        }
    }
}
