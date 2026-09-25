using UnityIsekaiGame.Economy;
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

            return QuestRewardEffectResult.Unsupported($"Quest reward category '{request.category}' has no live executor yet.");
        }
    }
}
