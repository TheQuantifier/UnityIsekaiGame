using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.Social.Reputation;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeQuestRewardEffectExecutor : IQuestRewardEffectExecutor
    {
        private readonly PrototypePersistenceServiceBehaviour services;

        public PrototypeQuestRewardEffectExecutor(PrototypePersistenceServiceBehaviour owner)
        {
            services = owner;
        }

        public QuestRewardEffectResult Execute(QuestRewardEffectRequest request)
        {
            if (services == null || request == null)
            {
                return QuestRewardEffectResult.Failure("Quest reward services are unavailable.");
            }

            if (request.category == QuestRewardCategory.Currency)
            {
                EconomyOperationResult result = services.Economy.Issue(
                    $"quest.reward.{request.grantId}",
                    services.PlayerEconomyAccountId,
                    new MoneyAmount(request.targetDefinitionId, request.quantity),
                    request.recipientPersonId,
                    $"Quest reward for {request.questId}",
                    worldTime: request.worldTime);
                return result.Succeeded
                    ? QuestRewardEffectResult.Success("world.economy", request.grantId, result.Duplicate)
                    : QuestRewardEffectResult.Failure(result.Message);
            }

            if (request.category == QuestRewardCategory.Reputation)
            {
                string audienceId = request.targetDefinitionId switch
                {
                    "reputation.prototype.adventurers-guild" => PrototypeReputationDefinitionFactory.AdventurersGuildAudienceId,
                    "reputation.prototype.city-guard" => PrototypeReputationDefinitionFactory.CityGuardAudienceId,
                    _ => request.targetDefinitionId
                };
                ReputationMutationResult result = services.Reputation.Mutate(new ReputationMutationRequest
                {
                    transactionId = $"quest.reward.{request.grantId}",
                    subjectPersonId = request.recipientPersonId,
                    audienceId = audienceId,
                    dimensionId = PrototypeReputationDefinitionFactory.EsteemId,
                    mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                    delta = request.quantity,
                    sourceId = request.grantId,
                    sourceCategory = ReputationContributionSourceCategory.Quest,
                    authenticity = ReputationAuthenticity.Verified,
                    supportingReferenceId = request.terminalOutcomeId,
                    worldTime = request.worldTime
                });
                return result.Succeeded
                    ? QuestRewardEffectResult.Success("world.reputation", result.RecordId, result.Duplicate)
                    : QuestRewardEffectResult.Failure(result.Message);
            }

            if (request.category == QuestRewardCategory.Item)
            {
                if (!services.RuntimeDefinitionRegistry.TryGet(request.targetDefinitionId, out ItemDefinition item))
                    return QuestRewardEffectResult.Failure($"Quest reward item '{request.targetDefinitionId}' is not registered.");
                if (services.PlayerInventory == null || !services.PlayerInventory.CanAddItemOrInstances(item, request.quantity))
                    return QuestRewardEffectResult.Failure("The player inventory cannot hold the complete quest reward.");
                InventoryAddResult added = services.PlayerInventory.AddItemOrInstances(item, request.quantity);
                return added.AddedAll
                    ? QuestRewardEffectResult.Success("player.inventory", request.grantId)
                    : QuestRewardEffectResult.Failure("The quest reward could not be added to inventory.");
            }

            if (request.category == QuestRewardCategory.Knowledge)
            {
                bool granted = services.NarrativeCoordinator?.GrantInformation(request.targetDefinitionId) == true;
                return granted
                    ? QuestRewardEffectResult.Success("player.knowledge", request.grantId)
                    : QuestRewardEffectResult.Failure("The quest knowledge reward could not be recorded.");
            }

            if (request.category == QuestRewardCategory.LegalPermitStatus)
            {
                LegalOperationResult result = services.Laws.GrantEntitlement(new LegalEntitlementRequest
                {
                    transactionId = $"quest.reward.{request.grantId}",
                    entitlementId = $"legal-entitlement.quest-reward.{request.grantId}",
                    effect = LegalEffectCategory.Permission,
                    personId = request.recipientPersonId,
                    actionId = request.targetDefinitionId,
                    effectiveWorldTime = request.worldTime,
                    visibility = PoliticalVisibility.Restricted,
                    provenanceId = request.terminalOutcomeId,
                    trustedSystemOperation = true
                });
                return result.Succeeded
                    ? QuestRewardEffectResult.Success("world.law", result.SubjectId, result.Duplicate)
                    : QuestRewardEffectResult.Failure(result.Message);
            }

            return QuestRewardEffectResult.Unsupported($"Quest reward category '{request.category}' has no live executor yet.");
        }
    }
}
