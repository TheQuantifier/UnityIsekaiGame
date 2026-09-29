using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public enum WorldSceneBindingCategory
    {
        Unknown,
        Location,
        InteractionPoint,
        Connection,
        Entity,
        RouteSegment,
        Journey,
        Checkpoint,
        SpawnAnchor,
        PresentationOnly,
        Custom
    }

    public enum WorldSceneBindingRole
    {
        Primary,
        Auxiliary,
        PresentationOnly
    }

    public enum WorldSceneBindingStatus
    {
        Unregistered,
        WaitingForRuntime,
        WaitingForLogicalRecord,
        Bound,
        Degraded,
        Duplicate,
        Invalid,
        Disposed
    }

    public enum WorldSceneBindingIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum WorldSceneBindingBootstrapMode
    {
        ProductionBindOnly,
        DevelopmentFixtureImport
    }

    public enum SceneBindingTransitionStatus
    {
        Succeeded,
        Preview,
        MissingRuntime,
        MissingBinding,
        MissingConnection,
        MissingPlacement,
        AccessDenied,
        RuntimeRejected,
        InvalidRequest
    }

    public sealed class WorldSceneBindingIssue
    {
        public WorldSceneBindingIssue(WorldSceneBindingIssueSeverity severity, WorldSceneBindingCategory category, string logicalId, string bindingKey, string message)
        {
            Severity = severity;
            Category = category;
            LogicalId = N(logicalId);
            BindingKey = N(bindingKey);
            Message = message ?? string.Empty;
        }

        public WorldSceneBindingIssueSeverity Severity { get; }
        public WorldSceneBindingCategory Category { get; }
        public string LogicalId { get; }
        public string BindingKey { get; }
        public string Message { get; }
        public bool IsError => Severity == WorldSceneBindingIssueSeverity.Error;

        public override string ToString()
        {
            return $"{Severity}: {Category} '{LogicalId}' ({BindingKey}) - {Message}";
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class WorldSceneBindingSnapshot
    {
        public WorldSceneBindingSnapshot(
            string instanceId,
            string worldId,
            string sceneKey,
            string sceneName,
            WorldSceneBindingCategory category,
            WorldSceneBindingRole role,
            string logicalId,
            string bindingKey,
            string displayName,
            WorldSceneBindingStatus status,
            bool required,
            string diagnostics)
        {
            InstanceId = N(instanceId);
            WorldId = N(worldId);
            SceneKey = N(sceneKey);
            SceneName = N(sceneName);
            Category = category;
            Role = role;
            LogicalId = N(logicalId);
            BindingKey = N(bindingKey);
            DisplayName = N(displayName);
            Status = status;
            Required = required;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public string InstanceId { get; }
        public string WorldId { get; }
        public string SceneKey { get; }
        public string SceneName { get; }
        public WorldSceneBindingCategory Category { get; }
        public WorldSceneBindingRole Role { get; }
        public string LogicalId { get; }
        public string BindingKey { get; }
        public string DisplayName { get; }
        public WorldSceneBindingStatus Status { get; }
        public bool Required { get; }
        public string Diagnostics { get; }

        public string StableKey => BuildStableKey(WorldId, SceneKey, Category, LogicalId, BindingKey);

        public static string BuildStableKey(string worldId, string sceneKey, WorldSceneBindingCategory category, string logicalId, string bindingKey)
        {
            return $"{N(worldId)}:{N(sceneKey)}:{category}:{N(logicalId)}:{N(bindingKey)}";
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class WorldSceneBindingValidationReport
    {
        public WorldSceneBindingValidationReport(IEnumerable<WorldSceneBindingSnapshot> bindings, IEnumerable<WorldSceneBindingIssue> issues)
        {
            Bindings = (bindings ?? Array.Empty<WorldSceneBindingSnapshot>())
                .Where(item => item != null)
                .OrderBy(item => item.Category)
                .ThenBy(item => item.LogicalId, StringComparer.Ordinal)
                .ThenBy(item => item.BindingKey, StringComparer.Ordinal)
                .ToArray();
            Issues = (issues ?? Array.Empty<WorldSceneBindingIssue>())
                .Where(item => item != null)
                .OrderByDescending(item => item.Severity)
                .ThenBy(item => item.Category)
                .ThenBy(item => item.LogicalId, StringComparer.Ordinal)
                .ThenBy(item => item.BindingKey, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<WorldSceneBindingSnapshot> Bindings { get; }
        public IReadOnlyList<WorldSceneBindingIssue> Issues { get; }
        public int ErrorCount => Issues.Count(item => item.Severity == WorldSceneBindingIssueSeverity.Error);
        public int WarningCount => Issues.Count(item => item.Severity == WorldSceneBindingIssueSeverity.Warning);
        public int InfoCount => Issues.Count(item => item.Severity == WorldSceneBindingIssueSeverity.Info);
        public bool Succeeded => ErrorCount == 0;
        public int BoundCount => Bindings.Count(item => item.Status == WorldSceneBindingStatus.Bound);
        public int WaitingCount => Bindings.Count(item => item.Status == WorldSceneBindingStatus.WaitingForLogicalRecord || item.Status == WorldSceneBindingStatus.WaitingForRuntime);
        public int DuplicateCount => Bindings.Count(item => item.Status == WorldSceneBindingStatus.Duplicate);
        public string Summary => $"Bindings={Bindings.Count} Bound={BoundCount} Waiting={WaitingCount} Duplicates={DuplicateCount} Errors={ErrorCount} Warnings={WarningCount}";
    }

    public sealed class SceneBindingTransitionRequest
    {
        public string transactionId;
        public EntityLocationReferenceData actor;
        public string connectionId;
        public string fromLocationId;
        public string toLocationId;
        public LocationConnectionAccessContextData accessContext;
        public double worldTime;
        public bool preview;
    }

    public sealed class SceneBindingTransitionResult
    {
        private SceneBindingTransitionResult(SceneBindingTransitionStatus status, string message, string sourceLocationId, string destinationLocationId, string connectionId, bool preview, EntityLocationOperationResult placementResult, LocationConnectionOperationResult connectionResult)
        {
            Status = status;
            Message = message ?? string.Empty;
            SourceLocationId = N(sourceLocationId);
            DestinationLocationId = N(destinationLocationId);
            ConnectionId = N(connectionId);
            Preview = preview;
            PlacementResult = placementResult;
            ConnectionResult = connectionResult;
        }

        public SceneBindingTransitionStatus Status { get; }
        public string Message { get; }
        public string SourceLocationId { get; }
        public string DestinationLocationId { get; }
        public string ConnectionId { get; }
        public bool Preview { get; }
        public EntityLocationOperationResult PlacementResult { get; }
        public LocationConnectionOperationResult ConnectionResult { get; }
        public bool Succeeded => Status == SceneBindingTransitionStatus.Succeeded || Status == SceneBindingTransitionStatus.Preview;

        public static SceneBindingTransitionResult Success(string message, string source, string destination, string connectionId, bool preview, EntityLocationOperationResult placement, LocationConnectionOperationResult connection)
        {
            return new SceneBindingTransitionResult(preview ? SceneBindingTransitionStatus.Preview : SceneBindingTransitionStatus.Succeeded, message, source, destination, connectionId, preview, placement, connection);
        }

        public static SceneBindingTransitionResult Failure(SceneBindingTransitionStatus status, string message, string source = "", string destination = "", string connectionId = "", LocationConnectionOperationResult connection = null)
        {
            return new SceneBindingTransitionResult(status, message, source, destination, connectionId, false, null, connection);
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class SceneBindingMaterializationResult
    {
        private SceneBindingMaterializationResult(bool succeeded, string message, string logicalLocationId, string bindingKey)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            LogicalLocationId = N(logicalLocationId);
            BindingKey = N(bindingKey);
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public string LogicalLocationId { get; }
        public string BindingKey { get; }

        public static SceneBindingMaterializationResult Success(string message, string logicalLocationId, string bindingKey)
        {
            return new SceneBindingMaterializationResult(true, message, logicalLocationId, bindingKey);
        }

        public static SceneBindingMaterializationResult Failure(string message, string logicalLocationId = "", string bindingKey = "")
        {
            return new SceneBindingMaterializationResult(false, message, logicalLocationId, bindingKey);
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
