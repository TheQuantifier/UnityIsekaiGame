using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Diplomacy
{
    public static class PrototypeDiplomacyDefinitionFactory
    {
        public const string RecognitionRelationId = "diplomatic-relation.prototype.recognition";
        public const string NeutralRelationId = "diplomatic-relation.prototype.neutral";
        public const string CooperativeRelationId = "diplomatic-relation.prototype.cooperative";
        public const string AllianceRelationId = "diplomatic-relation.prototype.alliance";
        public const string RivalryRelationId = "diplomatic-relation.prototype.rivalry";
        public const string HostileRelationId = "diplomatic-relation.prototype.hostile";
        public const string WarRelationId = "diplomatic-relation.prototype.at-war";
        public const string CeasefireRelationId = "diplomatic-relation.prototype.ceasefire";

        public const string RecognitionAgreementId = "diplomatic-agreement.prototype.recognition";
        public const string CooperationAgreementId = "diplomatic-agreement.prototype.cooperation";
        public const string NonAggressionAgreementId = "diplomatic-agreement.prototype.non-aggression";
        public const string MutualDefenseAgreementId = "diplomatic-agreement.prototype.mutual-defense";
        public const string TradeCooperationAgreementId = "diplomatic-agreement.prototype.trade-cooperation";
        public const string InformationSharingAgreementId = "diplomatic-agreement.prototype.information-sharing";
        public const string CeasefireAgreementId = "diplomatic-agreement.prototype.ceasefire";
        public const string PeaceAgreementId = "diplomatic-agreement.prototype.peace";
        public const string SecretProtocolAgreementId = "diplomatic-agreement.prototype.secret-protocol";

        public const string RecognitionClauseId = "diplomatic-clause.prototype.recognition";
        public const string NonAggressionClauseId = "diplomatic-clause.prototype.non-aggression";
        public const string DefenseAssistanceClauseId = "diplomatic-clause.prototype.defense-assistance";
        public const string TradeResourceClauseId = "diplomatic-clause.prototype.trade-resource";
        public const string InformationSharingClauseId = "diplomatic-clause.prototype.information-sharing";
        public const string CeasefireClauseId = "diplomatic-clause.prototype.ceasefire";
        public const string PeaceClauseId = "diplomatic-clause.prototype.peace";
        public const string WithdrawalClauseId = "diplomatic-clause.prototype.withdrawal";

        public const string FormalWarDefinitionId = "diplomatic-war.prototype.formal-war";
        public const string LimitedWarDefinitionId = "diplomatic-war.prototype.limited-war";
        public const string FactionalConflictWarDefinitionId = "diplomatic-war.prototype.factional-conflict";

        public static DefinitionRegistry AddMissingPrototypeDiplomacyDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            definitions.AddRange(CreateMissingClauseDefinitions(ids));
            definitions.AddRange(CreateMissingRelationDefinitions(ids));
            definitions.AddRange(CreateMissingAgreementDefinitions(ids));
            definitions.AddRange(CreateMissingWarDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<DiplomaticRelationDefinition> CreateMissingRelationDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<DiplomaticRelationDefinition> definitions = new List<DiplomaticRelationDefinition>();
            AddRelation(definitions, ids, RecognitionRelationId, "Prototype Formal Recognition", DiplomaticRelationCategory.Recognized, DiplomaticReciprocityPolicy.MirrorOnCreate);
            AddRelation(definitions, ids, NeutralRelationId, "Prototype Neutral Relation", DiplomaticRelationCategory.Neutral, DiplomaticReciprocityPolicy.MirrorOnCreate);
            AddRelation(definitions, ids, CooperativeRelationId, "Prototype Cooperative Relation", DiplomaticRelationCategory.Cooperative, DiplomaticReciprocityPolicy.MirrorOnCreate, recognitionRequired: true);
            AddRelation(definitions, ids, AllianceRelationId, "Prototype Alliance Relation", DiplomaticRelationCategory.Allied, DiplomaticReciprocityPolicy.MirrorOnCreate, recognitionRequired: true, militaryObligation: false);
            AddRelation(definitions, ids, RivalryRelationId, "Prototype Rivalry Relation", DiplomaticRelationCategory.Rival, DiplomaticReciprocityPolicy.MirrorOnCreate);
            AddRelation(definitions, ids, HostileRelationId, "Prototype Hostile Relation", DiplomaticRelationCategory.Hostile, DiplomaticReciprocityPolicy.MirrorOnCreate);
            AddRelation(definitions, ids, WarRelationId, "Prototype War Relation", DiplomaticRelationCategory.AtWar, DiplomaticReciprocityPolicy.MirrorOnCreate, warState: true);
            AddRelation(definitions, ids, CeasefireRelationId, "Prototype Ceasefire Relation", DiplomaticRelationCategory.Ceasefire, DiplomaticReciprocityPolicy.MirrorOnCreate, warState: true);
            return definitions;
        }

        public static IReadOnlyList<DiplomaticAgreementDefinition> CreateMissingAgreementDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<DiplomaticAgreementDefinition> definitions = new List<DiplomaticAgreementDefinition>();
            AddAgreement(definitions, ids, RecognitionAgreementId, "Prototype Recognition Agreement", DiplomaticAgreementCategory.Recognition, clauseIds: new[] { RecognitionClauseId });
            AddAgreement(definitions, ids, CooperationAgreementId, "Prototype Cooperation Pact", DiplomaticAgreementCategory.Cooperation, clauseIds: new[] { RecognitionClauseId, InformationSharingClauseId, TradeResourceClauseId });
            AddAgreement(definitions, ids, NonAggressionAgreementId, "Prototype Non-Aggression Pact", DiplomaticAgreementCategory.NonAggression, clauseIds: new[] { NonAggressionClauseId, WithdrawalClauseId });
            AddAgreement(definitions, ids, MutualDefenseAgreementId, "Prototype Mutual Defense Pact", DiplomaticAgreementCategory.MutualDefense, automaticAid: false, clauseIds: new[] { DefenseAssistanceClauseId, NonAggressionClauseId, WithdrawalClauseId });
            AddAgreement(definitions, ids, TradeCooperationAgreementId, "Prototype Trade Cooperation Pact", DiplomaticAgreementCategory.TradeOrResource, clauseIds: new[] { TradeResourceClauseId, WithdrawalClauseId });
            AddAgreement(definitions, ids, InformationSharingAgreementId, "Prototype Information Sharing Pact", DiplomaticAgreementCategory.InformationSharing, clauseIds: new[] { InformationSharingClauseId, WithdrawalClauseId });
            AddAgreement(definitions, ids, CeasefireAgreementId, "Prototype Ceasefire Agreement", DiplomaticAgreementCategory.Ceasefire, clauseIds: new[] { CeasefireClauseId, WithdrawalClauseId });
            AddAgreement(definitions, ids, PeaceAgreementId, "Prototype Peace Agreement", DiplomaticAgreementCategory.Peace, clauseIds: new[] { PeaceClauseId, RecognitionClauseId, WithdrawalClauseId });
            AddAgreement(definitions, ids, SecretProtocolAgreementId, "Prototype Secret Protocol", DiplomaticAgreementCategory.SecretProtocol, DiplomaticVisibility.Secret, secretClauses: true, clauseIds: new[] { InformationSharingClauseId, DefenseAssistanceClauseId, WithdrawalClauseId });
            return definitions;
        }

        public static IReadOnlyList<DiplomaticClauseDefinition> CreateMissingClauseDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<DiplomaticClauseDefinition> definitions = new List<DiplomaticClauseDefinition>();
            AddClause(definitions, ids, RecognitionClauseId, "Recognition Clause", DiplomaticClauseCategory.Recognition, DiplomaticClauseParameterType.ActorReference);
            AddClause(definitions, ids, NonAggressionClauseId, "Non-Aggression Clause", DiplomaticClauseCategory.NonAggression, DiplomaticClauseParameterType.Boolean);
            AddClause(definitions, ids, DefenseAssistanceClauseId, "Defense Assistance Clause", DiplomaticClauseCategory.DefenseAssistance, DiplomaticClauseParameterType.Text);
            AddClause(definitions, ids, TradeResourceClauseId, "Trade Resource Clause", DiplomaticClauseCategory.TradeOrResource, DiplomaticClauseParameterType.StableId, externalContract: true);
            AddClause(definitions, ids, InformationSharingClauseId, "Information Sharing Clause", DiplomaticClauseCategory.InformationSharing, DiplomaticClauseParameterType.StableId);
            AddClause(definitions, ids, CeasefireClauseId, "Ceasefire Clause", DiplomaticClauseCategory.Ceasefire, DiplomaticClauseParameterType.Decimal);
            AddClause(definitions, ids, PeaceClauseId, "Peace Clause", DiplomaticClauseCategory.Peace, DiplomaticClauseParameterType.Text);
            AddClause(definitions, ids, WithdrawalClauseId, "Withdrawal Clause", DiplomaticClauseCategory.Withdrawal, DiplomaticClauseParameterType.Integer);
            return definitions;
        }

        public static IReadOnlyList<DiplomaticWarDefinition> CreateMissingWarDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<DiplomaticWarDefinition> definitions = new List<DiplomaticWarDefinition>();
            AddWar(definitions, ids, FormalWarDefinitionId, "Prototype Formal War", DiplomaticWarCategory.FormalWar, declarationRequired: true, factionsAllowed: false);
            AddWar(definitions, ids, LimitedWarDefinitionId, "Prototype Limited War", DiplomaticWarCategory.LimitedWar);
            AddWar(definitions, ids, FactionalConflictWarDefinitionId, "Prototype Factional Conflict", DiplomaticWarCategory.FactionalConflict, factionsAllowed: true);
            return definitions;
        }

        private static void AddRelation(ICollection<DiplomaticRelationDefinition> definitions, ISet<string> ids, string id, string name, DiplomaticRelationCategory category, DiplomaticReciprocityPolicy reciprocity, bool recognitionRequired = false, bool militaryObligation = false, bool warState = false)
        {
            if (ids.Contains(id)) return;
            DiplomaticRelationDefinition definition = ScriptableObject.CreateInstance<DiplomaticRelationDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, reciprocity, recognitionRequired: recognitionRequired, militaryObligation: militaryObligation, warState: warState, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddAgreement(ICollection<DiplomaticAgreementDefinition> definitions, ISet<string> ids, string id, string name, DiplomaticAgreementCategory category, DiplomaticVisibility visibility = DiplomaticVisibility.Public, bool secretClauses = false, bool automaticAid = false, IEnumerable<string> clauseIds = null)
        {
            if (ids.Contains(id)) return;
            DiplomaticAgreementDefinition definition = ScriptableObject.CreateInstance<DiplomaticAgreementDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, visibility, secretClauses: secretClauses, automaticMilitaryAid: automaticAid, clauseIds: clauseIds, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddClause(ICollection<DiplomaticClauseDefinition> definitions, ISet<string> ids, string id, string name, DiplomaticClauseCategory category, DiplomaticClauseParameterType parameterType, bool externalContract = false)
        {
            if (ids.Contains(id)) return;
            DiplomaticClauseDefinition definition = ScriptableObject.CreateInstance<DiplomaticClauseDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, parameterTypes: new[] { parameterType }, externalContract: externalContract, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddWar(ICollection<DiplomaticWarDefinition> definitions, ISet<string> ids, string id, string name, DiplomaticWarCategory category, bool declarationRequired = true, bool factionsAllowed = true)
        {
            if (ids.Contains(id)) return;
            DiplomaticWarDefinition definition = ScriptableObject.CreateInstance<DiplomaticWarDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, declarationRequired: declarationRequired, factionsAllowed: factionsAllowed, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static HashSet<string> Set(IEnumerable<string> ids) => new HashSet<string>((ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        private static string[] Tags() => new[] { "prototype", "diplomacy", "organizations" };
    }
}
