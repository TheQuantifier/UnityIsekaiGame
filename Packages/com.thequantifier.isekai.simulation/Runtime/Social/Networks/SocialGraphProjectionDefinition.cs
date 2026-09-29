using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Networks
{
    [CreateAssetMenu(fileName = "SocialGraphProjectionDefinition", menuName = "Unity Isekai Game/Social/Social Graph Projection Definition")]
    public sealed class SocialGraphProjectionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string projectionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SocialGraphEdgeKind[] includedEdgeKinds = Array.Empty<SocialGraphEdgeKind>();
        [SerializeField] private SocialGraphDirectionPolicy directionPolicy = SocialGraphDirectionPolicy.PreserveDirection;
        [SerializeField] private SocialGraphWeightPolicy weightPolicy = SocialGraphWeightPolicy.Composite;
        [SerializeField] private int minimumEdgeWeight = 1;
        [SerializeField] private int maximumNodes = 128;
        [SerializeField] private int maximumEdges = 512;
        [SerializeField] private int maximumTraversalDepth = 4;
        [SerializeField] private int maximumAnalysisNodes = 64;
        [SerializeField] private int maximumCliqueSize = 5;
        [SerializeField] private int maximumAnalysisResults = 16;
        [SerializeField] private double timeWindow = -1d;
        [SerializeField] private SocialGraphVisibility visibility = SocialGraphVisibility.Authoritative;
        [SerializeField] private string[] relationshipDefinitionFilters = Array.Empty<string>();
        [SerializeField] private string[] attitudeDimensionFilters = Array.Empty<string>();
        [SerializeField] private string[] interactionDefinitionFilters = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private SocialGraphEdgeSourceWeightData[] edgeWeights = Array.Empty<SocialGraphEdgeSourceWeightData>();
        [SerializeField] private int version = 1;

        public string Id => projectionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public IReadOnlyList<SocialGraphEdgeKind> IncludedEdgeKinds => includedEdgeKinds ?? Array.Empty<SocialGraphEdgeKind>();
        public SocialGraphDirectionPolicy DirectionPolicy => directionPolicy;
        public SocialGraphWeightPolicy WeightPolicy => weightPolicy;
        public int MinimumEdgeWeight => minimumEdgeWeight;
        public int MaximumNodes => maximumNodes;
        public int MaximumEdges => maximumEdges;
        public int MaximumTraversalDepth => maximumTraversalDepth;
        public int MaximumAnalysisNodes => maximumAnalysisNodes;
        public int MaximumCliqueSize => maximumCliqueSize;
        public int MaximumAnalysisResults => maximumAnalysisResults;
        public double TimeWindow => timeWindow;
        public SocialGraphVisibility Visibility => visibility;
        public IReadOnlyList<string> RelationshipDefinitionFilters => relationshipDefinitionFilters ?? Array.Empty<string>();
        public IReadOnlyList<string> AttitudeDimensionFilters => attitudeDimensionFilters ?? Array.Empty<string>();
        public IReadOnlyList<string> InteractionDefinitionFilters => interactionDefinitionFilters ?? Array.Empty<string>();
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public IReadOnlyList<SocialGraphEdgeSourceWeightData> EdgeWeights => edgeWeights ?? Array.Empty<SocialGraphEdgeSourceWeightData>();

        public int Version => version;

        private void OnValidate()
        {
            projectionId = projectionId?.Trim();
            minimumEdgeWeight = Math.Max(0, minimumEdgeWeight);
            maximumNodes = Math.Max(1, maximumNodes);
            maximumEdges = Math.Max(1, maximumEdges);
            maximumTraversalDepth = Math.Max(1, maximumTraversalDepth);
            maximumAnalysisNodes = Math.Max(1, maximumAnalysisNodes);
            maximumCliqueSize = Math.Max(2, maximumCliqueSize);
            maximumAnalysisResults = Math.Max(1, maximumAnalysisResults);
            version = Math.Max(1, version);
            relationshipDefinitionFilters = Clean(relationshipDefinitionFilters);
            attitudeDimensionFilters = Clean(attitudeDimensionFilters);
            interactionDefinitionFilters = Clean(interactionDefinitionFilters);
            tags = Clean(tags);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            IEnumerable<SocialGraphEdgeKind> edgeKinds,
            SocialGraphDirectionPolicy direction,
            SocialGraphWeightPolicy weight,
            int minimumWeight = 1,
            int maxNodes = 128,
            int maxEdges = 512,
            int maxDepth = 4,
            int maxAnalysisNodes = 64,
            int maxClique = 5,
            int maxResults = 16,
            double window = -1d,
            SocialGraphVisibility graphVisibility = SocialGraphVisibility.Authoritative,
            IEnumerable<string> relationshipFilters = null,
            IEnumerable<string> attitudeFilters = null,
            IEnumerable<string> interactionFilters = null,
            IEnumerable<SocialGraphEdgeSourceWeightData> weights = null,
            string text = "",
            IEnumerable<string> tagIds = null)
        {
            projectionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            description = text ?? string.Empty;
            includedEdgeKinds = (edgeKinds ?? Array.Empty<SocialGraphEdgeKind>()).Distinct().OrderBy(item => item).ToArray();
            directionPolicy = direction;
            weightPolicy = weight;
            minimumEdgeWeight = Math.Max(0, minimumWeight);
            maximumNodes = Math.Max(1, maxNodes);
            maximumEdges = Math.Max(1, maxEdges);
            maximumTraversalDepth = Math.Max(1, maxDepth);
            maximumAnalysisNodes = Math.Max(1, maxAnalysisNodes);
            maximumCliqueSize = Math.Max(2, maxClique);
            maximumAnalysisResults = Math.Max(1, maxResults);
            timeWindow = window;
            visibility = graphVisibility;
            relationshipDefinitionFilters = Clean(relationshipFilters);
            attitudeDimensionFilters = Clean(attitudeFilters);
            interactionDefinitionFilters = Clean(interactionFilters);
            edgeWeights = (weights ?? Array.Empty<SocialGraphEdgeSourceWeightData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            tags = Clean(tagIds);
            version = 1;
        }

        public int WeightFor(SocialGraphEdgeKind edgeKind)
        {
            SocialGraphEdgeSourceWeightData match = EdgeWeights.FirstOrDefault(item => item != null && item.edgeKind == edgeKind);
            return Math.Max(1, match?.authoredWeight ?? 50);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Social Graph Projection Definition '{name}' is missing a stable ID.");
            else if (!Id.StartsWith("social-graph.projection.", StringComparison.Ordinal)) report.AddWarning($"Social Graph Projection Definition '{Id}' should use the 'social-graph.projection.' namespace prefix.");
            if (IncludedEdgeKinds.Count == 0) report.AddError($"Social Graph Projection Definition '{DisplayName}' must include at least one edge source kind.");
            if (!Enum.IsDefined(typeof(SocialGraphDirectionPolicy), directionPolicy)) report.AddError($"Social Graph Projection Definition '{DisplayName}' has invalid direction policy '{directionPolicy}'.");
            if (!Enum.IsDefined(typeof(SocialGraphWeightPolicy), weightPolicy)) report.AddError($"Social Graph Projection Definition '{DisplayName}' has invalid weight policy '{weightPolicy}'.");
            if (!Enum.IsDefined(typeof(SocialGraphVisibility), visibility)) report.AddError($"Social Graph Projection Definition '{DisplayName}' has invalid visibility '{visibility}'.");
            if (maximumNodes < 1 || maximumEdges < 1 || maximumTraversalDepth < 1 || maximumAnalysisNodes < 1 || maximumCliqueSize < 2 || maximumAnalysisResults < 1) report.AddError($"Social Graph Projection Definition '{DisplayName}' has invalid graph limits.");
            if (timeWindow < -1d || double.IsNaN(timeWindow) || double.IsInfinity(timeWindow)) report.AddError($"Social Graph Projection Definition '{DisplayName}' has invalid time window '{timeWindow}'.");
            foreach (SocialGraphEdgeKind kind in IncludedEdgeKinds)
            {
                if (!Enum.IsDefined(typeof(SocialGraphEdgeKind), kind)) report.AddError($"Social Graph Projection Definition '{DisplayName}' includes invalid edge kind '{kind}'.");
            }
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
