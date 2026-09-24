using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Tests
{
    internal static class KnowledgeTestDefinitionFactory
    {
        public static IEnumerable<IGameDefinition> AllSourceDefinitions()
        {
            return Enum.GetValues(typeof(InformationSourceCategory))
                .Cast<InformationSourceCategory>()
                .Where(category => category != InformationSourceCategory.Unknown)
                .Select(SourceDefinition);
        }

        public static InformationSourceDefinition SourceDefinition(InformationSourceCategory category)
        {
            InformationSourceDefinition definition = ScriptableObject.CreateInstance<InformationSourceDefinition>();
            ReliabilityProfileData reliability = ReliabilityProfileData.Default();
            definition.DevelopmentConfigure(
                $"information-source.test.{category.ToString().ToLowerInvariant()}",
                $"Test {category}",
                category,
                reliability);
            return definition;
        }

        public static InformationTransferDefinition TransferDefinition(string id, InformationTransferMode mode)
        {
            InformationTransferDefinition definition = ScriptableObject.CreateInstance<InformationTransferDefinition>();
            definition.DevelopmentConfigure(
                id,
                id,
                mode,
                Enum.GetValues(typeof(KnowledgeDomain)).Cast<KnowledgeDomain>().Where(domain => domain != KnowledgeDomain.Unknown).ToArray(),
                Enum.GetValues(typeof(InformationSourceCategory)).Cast<InformationSourceCategory>().Where(category => category != InformationSourceCategory.Unknown).ToArray(),
                requiresRecall: false,
                allowsSummary: true,
                allowsTranslation: true,
                allowsDemonstration: true,
                fidelity: 800,
                completeness: 800,
                TransferMemoryPolicy.FormCommunicationMemory,
                TransferEvidencePolicy.CreateRecipientEvidence);
            return definition;
        }

        public static DefinitionRegistry AddSourceDefinitions(params IGameDefinition[] definitions)
        {
            return new DefinitionRegistry((definitions ?? Array.Empty<IGameDefinition>()).Concat(AllSourceDefinitions()));
        }
    }
}
