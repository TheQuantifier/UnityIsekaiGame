using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class ProfessionCoordinatorTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = "person.test.profession-coordinator";

        [Test]
        public void DeclarationAndCraftingCreateProfessionRankAndMeasuredEvidenceWithoutAutomaticCompetence()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionRegistry registry = catalog.CreateRegistry();
            string[] people = { PersonId };

            PersonProfessionRuntime professions = new PersonProfessionRuntime();
            professions.Configure(registry, people);
            ProfessionEntryRuntime entries = new ProfessionEntryRuntime();
            entries.Configure(registry, professions, people);
            ProfessionalActivityRuntime activities = new ProfessionalActivityRuntime();
            activities.Configure(registry, professions, people);
            TrainingRuntime training = new TrainingRuntime();
            training.Configure(registry, professions, null, people);
            CredentialRuntime credentials = new CredentialRuntime();
            credentials.Configure(registry, professions, training, activities, people, Array.Empty<string>());
            ProfessionalRankRuntime ranks = new ProfessionalRankRuntime();
            ranks.Configure(registry, professions, training, activities, credentials, people, people);
            ProfessionCoordinator coordinator = new ProfessionCoordinator(registry, professions, entries, activities, ranks, null, null);

            Assert.That(professions.QueryByPerson(PersonId).Count, Is.Zero, "Character creation must not grant a profession.");
            ProfessionEntryOperationResult declared = coordinator.DeclareInformalProfession(
                PersonId,
                ProfessionContentIds.CrafterProfessionId,
                ProfessionContentIds.CrafterSelfDeclaredEntryPathId,
                "1",
                "tx.test.declare-crafter");

            Assert.That(declared.Succeeded, Is.True, declared.Message);
            Assert.That(professions.QueryByProfession(ProfessionContentIds.CrafterProfessionId, true).Count, Is.EqualTo(1));
            Assert.That(ranks.QueryByPerson(PersonId, true).Single().rankDefinitionId, Is.EqualTo(ProfessionContentIds.CrafterRankNoviceId));

            CraftingOperationRecordData operation = new CraftingOperationRecordData
            {
                operationId = "crafting-operation.test.profession-coordinator",
                recipeId = "recipe.prototype-sword",
                actorPersonId = PersonId,
                worldTime = "2",
                state = CraftingOperationState.Completed,
                status = CraftingExecutionStatus.Succeeded,
                outputs = { new CraftingOutputItemData { itemInstanceId = "item.instance.test-crafted", itemDefinitionId = "item.prototype-sword", quantity = 1f, createdItemInstance = true } }
            };
            var results = coordinator.RecordCrafting(operation, 835, ProfessionalActivityDifficulty.Skilled);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Succeeded, Is.True, results[0].Message);
            Assert.That(results[0].Evidence.quality, Is.EqualTo(835));
            Assert.That(results[0].Evidence.difficulty, Is.EqualTo(ProfessionalActivityDifficulty.Skilled));
            Assert.That(activities.Evidence.Count, Is.EqualTo(1));

            coordinator.RecordCrafting(operation, 835, ProfessionalActivityDifficulty.Skilled);
            Assert.That(activities.Evidence.Count, Is.EqualTo(1), "A replayed source operation must not duplicate professional experience.");

            for (int index = 2; index <= 5; index++)
            {
                CraftingOperationRecordData additional = operation.Clone();
                additional.operationId = $"crafting-operation.test.profession-coordinator.{index}";
                additional.worldTime = index.ToString();
                coordinator.RecordCrafting(additional, 500, ProfessionalActivityDifficulty.Routine);
            }

            Assert.That(ranks.QueryByPerson(PersonId, true).Single().rankDefinitionId, Is.EqualTo(ProfessionContentIds.CrafterRankSkilledId));
            Assert.That(ranks.QueryByPerson(PersonId).Single(item => item.rankDefinitionId == ProfessionContentIds.CrafterRankNoviceId).state, Is.EqualTo(ProfessionalRankState.Former));
        }

        [Test]
        public void CharacterFoundationMergesOriginAndLaterBirthGiftWithoutGrantingAProfession()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionRegistry registry = catalog.CreateRegistry();
            string[] people = { PersonId };

            PersonProfessionRuntime professions = new PersonProfessionRuntime();
            professions.Configure(registry, people);
            ProfessionEntryRuntime entries = new ProfessionEntryRuntime();
            entries.Configure(registry, professions, people);
            ProfessionalActivityRuntime activities = new ProfessionalActivityRuntime();
            activities.Configure(registry, professions, people);
            TrainingRuntime training = new TrainingRuntime();
            training.Configure(registry, professions, null, people);
            CredentialRuntime credentials = new CredentialRuntime();
            credentials.Configure(registry, professions, training, activities, people, Array.Empty<string>());
            ProfessionalRankRuntime ranks = new ProfessionalRankRuntime();
            ranks.Configure(registry, professions, training, activities, credentials, people, people);
            LifePathRuntime lifePaths = new LifePathRuntime();
            lifePaths.Configure(registry, professions, training, activities, credentials, ranks, null, null, people, Array.Empty<string>());
            ProfessionCoordinator coordinator = new ProfessionCoordinator(registry, professions, entries, activities, ranks, null, lifePaths);

            Assert.That(coordinator.EnsureLifePathFoundation(PersonId, "origin.test", string.Empty, "1", "tx.test.origin"), Is.True);
            Assert.That(coordinator.EnsureLifePathFoundation(PersonId, "origin.test", "birth-gift.test", "2", "tx.test.birth-gift"), Is.True);
            Assert.That(coordinator.EnsureLifePathFoundation(PersonId, "origin.test", "birth-gift.test", "3", "tx.test.foundation-replay"), Is.False);

            LifePathRecordData lifePath = lifePaths.QueryLifePathsByPerson(PersonId).Single();
            Assert.That(lifePath.formativeReferences.Select(item => item.subjectId), Is.EquivalentTo(new[] { "origin.test", "birth-gift.test" }));
            Assert.That(professions.QueryByPerson(PersonId), Is.Empty, "Origins and birth gifts may shape aspirations but must not grant competence or a career.");
        }
    }
}
