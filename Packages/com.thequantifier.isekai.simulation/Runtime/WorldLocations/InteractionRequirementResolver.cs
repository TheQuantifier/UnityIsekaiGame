using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class InteractionRequirementContext
    {
        public InteractionPointSnapshot Point { get; set; }
        public InteractionServiceDefinition Service { get; set; }
        public EntityLocationReferenceData Consumer { get; set; }
        public EntityLocationReferenceData Provider { get; set; }
        public double WorldTime { get; set; }
    }

    public sealed class InteractionRequirementResolution
    {
        public InteractionRequirementResolution(IEnumerable<string> unmetRequirementIds, string message = null)
        {
            UnmetRequirementIds = (unmetRequirementIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Message = message ?? string.Empty;
        }

        public IReadOnlyList<string> UnmetRequirementIds { get; }
        public string Message { get; }
        public bool Satisfied => UnmetRequirementIds.Count == 0;

        public static InteractionRequirementResolution Success(string message = "Declarative interaction requirements satisfied.") => new InteractionRequirementResolution(Array.Empty<string>(), message);
    }

    public interface IInteractionRequirementResolver
    {
        InteractionRequirementResolution Evaluate(InteractionRequirementContext context);
    }
}
