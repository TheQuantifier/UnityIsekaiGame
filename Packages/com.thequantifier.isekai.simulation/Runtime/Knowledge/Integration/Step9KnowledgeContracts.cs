using System;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Knowledge.Integration
{
    public interface IItemKnowledgeService
    {
        Step9KnowledgeContractResult QueryItemKnowledge(ItemKnowledgeRequest request, InformationAccessContext accessContext);
    }

    public interface IRecipeKnowledgeService
    {
        Step9KnowledgeContractResult QueryRecipeKnowledge(RecipeKnowledgeRequest request, InformationAccessContext accessContext);
    }

    public interface IProductionDiscoveryService
    {
        Step9KnowledgeContractResult DiscoverProductionKnowledge(ProductionDiscoveryRequest request, InformationAccessContext accessContext);
    }

    public interface IItemIdentificationKnowledgeSink
    {
        Step9KnowledgeContractResult RecordItemIdentification(ItemKnowledgeRequest request, string transactionId);
    }

    public interface ICraftingHistoryRecorder
    {
        Step9KnowledgeContractResult RecordCraftingHistory(CraftedItemProvenanceRequest request, string transactionId);
    }

    public interface IRecipeTeachingService
    {
        Step9KnowledgeContractResult TeachRecipe(RecipeKnowledgeRequest request, string transactionId);
    }

    public interface IProvenanceRecordService
    {
        Step9KnowledgeContractResult RecordProvenance(CraftedItemProvenanceRequest request, string transactionId);
    }

    [Serializable]
    public sealed class ItemKnowledgeRequest
    {
        public string RequestingPersonId { get; set; }
        public string ItemDefinitionId { get; set; }
        public string ItemInstanceId { get; set; }
        public string SubjectId => string.IsNullOrWhiteSpace(ItemInstanceId) ? ItemDefinitionId ?? string.Empty : ItemInstanceId;
    }

    [Serializable]
    public sealed class RecipeKnowledgeRequest
    {
        public string RequestingPersonId { get; set; }
        public string RecipeDefinitionId { get; set; }
        public string RequiredSkillId { get; set; }
    }

    [Serializable]
    public sealed class ProductionDiscoveryRequest
    {
        public string RequestingPersonId { get; set; }
        public string ProductionDefinitionId { get; set; }
        public string ObservedActorId { get; set; }
        public string ObservedItemId { get; set; }
    }

    [Serializable]
    public sealed class CraftedItemProvenanceRequest
    {
        public string CraftedItemInstanceId { get; set; }
        public string CrafterPersonId { get; set; }
        public string RecipeDefinitionId { get; set; }
        public string WorkstationId { get; set; }
        public double WorldTimeSeconds { get; set; }
    }

    public sealed class Step9KnowledgeContractResult
    {
        public Step9KnowledgeContractResult(bool succeeded, string code, string message)
        {
            Succeeded = succeeded;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }

        public static Step9KnowledgeContractResult PreviewReady(string message)
        {
            return new Step9KnowledgeContractResult(true, "ContractReady", message);
        }

        public static Step9KnowledgeContractResult Deferred(string message)
        {
            return new Step9KnowledgeContractResult(false, "DeferredToStep9", message);
        }
    }
}
