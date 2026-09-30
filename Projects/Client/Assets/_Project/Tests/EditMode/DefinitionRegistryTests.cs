using NUnit.Framework;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class DefinitionRegistryTests
    {
        [Test]
        public void Registry_ResolvesKnownDefinitionById()
        {
            TestDefinition definition = new TestDefinition("item.health-potion", "Health Potion");
            DefinitionValidationReport report = new DefinitionValidationReport();
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { definition }, report);

            bool found = registry.TryGet("item.health-potion", out IGameDefinition resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(definition));
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
        }

        [Test]
        public void Registry_ReturnsFalseForMissingDefinition()
        {
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { new TestDefinition("item.health-potion", "Health Potion") });

            bool found = registry.TryGet("item.missing", out IGameDefinition resolved);

            Assert.That(found, Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void Registry_ReturnsFalseForWrongDefinitionType()
        {
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { new TestDefinition("item.health-potion", "Health Potion") });

            bool found = registry.TryGet<OtherTestDefinition>("item.health-potion", out OtherTestDefinition resolved);

            Assert.That(found, Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void Registry_ReportsDuplicateIds()
        {
            DefinitionValidationReport report = new DefinitionValidationReport();

            _ = new DefinitionRegistry(
                new IGameDefinition[]
                {
                    new TestDefinition("item.health-potion", "Health Potion"),
                    new TestDefinition("item.health-potion", "Other Potion")
                },
                report);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("Duplicate definition ID"));
        }

        [Test]
        public void Registry_ReportsInvalidIds()
        {
            DefinitionValidationReport report = new DefinitionValidationReport();

            _ = new DefinitionRegistry(new IGameDefinition[] { new TestDefinition("Item Health Potion", "Health Potion") }, report);

            Assert.That(report.HasErrors, Is.True);
        }

        private sealed class TestDefinition : IGameDefinition
        {
            public TestDefinition(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; }
            public string DisplayName { get; }
        }

        private sealed class OtherTestDefinition : IGameDefinition
        {
            public string Id => "other.definition";
            public string DisplayName => "Other Definition";
        }
    }
}
