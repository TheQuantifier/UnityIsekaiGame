using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class DefinitionCatalogValidatorTests
    {
        [Test]
        public void Validate_AcceptsCatalogWithUniqueDefinitions()
        {
            DefinitionCatalog catalog = CreateCatalog(
                CreateDefinition("item.health-potion", "Health Potion"),
                CreateDefinition("spell.arcane-bolt", "Arcane Bolt"));

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.HasErrors, Is.False, report.GetSummary());
        }

        [Test]
        public void Validate_ReportsMissingReference()
        {
            DefinitionCatalog catalog = CreateCatalog((ScriptableObject)null);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("missing a reference"));
        }

        [Test]
        public void Validate_ReportsDuplicateDefinitionReferences()
        {
            TestScriptableDefinition definition = CreateDefinition("item.health-potion", "Health Potion");
            DefinitionCatalog catalog = CreateCatalog(definition, definition);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("duplicates asset reference"));
        }

        [Test]
        public void Validate_ReportsIncompatibleDefinitionType()
        {
            DefinitionCatalog catalog = CreateCatalog(ScriptableObject.CreateInstance<UnsupportedScriptableDefinition>());

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("does not implement IGameDefinition"));
        }

        private static DefinitionCatalog CreateCatalog(params ScriptableObject[] definitions)
        {
            return ClassificationTestFactory.CreateCatalog(definitions);
        }

        private static TestScriptableDefinition CreateDefinition(string id, string displayName)
        {
            TestScriptableDefinition definition = ScriptableObject.CreateInstance<TestScriptableDefinition>();
            definition.Initialize(id, displayName);
            return definition;
        }

        private sealed class TestScriptableDefinition : ScriptableObject, IGameDefinition
        {
            private string id;
            private string displayName;

            public string Id => id;
            public string DisplayName => displayName;

            public void Initialize(string definitionId, string definitionDisplayName)
            {
                id = definitionId;
                displayName = definitionDisplayName;
            }
        }

        private sealed class UnsupportedScriptableDefinition : ScriptableObject
        {
        }
    }
}
