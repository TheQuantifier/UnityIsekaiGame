using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Social.Decisions
{
    public sealed class SocialDecisionModifierSourceCollection : ISocialDecisionModifierSource
    {
        private readonly IReadOnlyList<ISocialDecisionModifierSource> sources;

        public SocialDecisionModifierSourceCollection(IEnumerable<ISocialDecisionModifierSource> sources)
        {
            this.sources = (sources ?? Array.Empty<ISocialDecisionModifierSource>())
                .Where(source => source != null)
                .Distinct()
                .ToArray();
        }

        public static ISocialDecisionModifierSource Compose(params ISocialDecisionModifierSource[] sources)
        {
            ISocialDecisionModifierSource[] active = (sources ?? Array.Empty<ISocialDecisionModifierSource>())
                .Where(source => source != null)
                .Distinct()
                .ToArray();
            return active.Length switch
            {
                0 => null,
                1 => active[0],
                _ => new SocialDecisionModifierSourceCollection(active)
            };
        }

        public int ResolveSocialDecisionScoreModifier(string actorPersonId, string targetPersonId, string intentionDefinitionId, string interactionDefinitionId, double worldTime, out string sourceModifierId)
        {
            List<string> ids = new List<string>();
            int total = 0;
            foreach (ISocialDecisionModifierSource source in sources)
            {
                total += source.ResolveSocialDecisionScoreModifier(actorPersonId, targetPersonId, intentionDefinitionId, interactionDefinitionId, worldTime, out string id);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id.Trim());
                }
            }

            sourceModifierId = string.Join(",", ids.OrderBy(id => id, StringComparer.Ordinal));
            return Math.Max(-250, Math.Min(250, total));
        }
    }
}
