using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Knowledge
{
    public sealed class KnowledgeQueryService
    {
        private readonly Dictionary<string, PersonKnowledgeRuntime> runtimesByPerson = new Dictionary<string, PersonKnowledgeRuntime>(StringComparer.Ordinal);

        public void Register(PersonKnowledgeRuntime runtime)
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.PersonId))
            {
                return;
            }

            runtimesByPerson[runtime.PersonId] = runtime;
        }

        public void Unregister(PersonKnowledgeRuntime runtime)
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.PersonId))
            {
                return;
            }

            if (runtimesByPerson.TryGetValue(runtime.PersonId, out PersonKnowledgeRuntime found) && found == runtime)
            {
                runtimesByPerson.Remove(runtime.PersonId);
            }
        }

        public bool IsKnowledgeRuntimeReady(string personId)
        {
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime) && runtime.IsReady;
        }

        public KnowledgeSnapshot GetKnowledgeSnapshot(string personId)
        {
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime) ? runtime.CreateSnapshot() : null;
        }

        public bool TryGetBelief(string personId, KnowledgePropositionData proposition, out KnowledgeBeliefRecord belief)
        {
            belief = null;
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime) && runtime.TryGetBelief(proposition, out belief);
        }

        public IReadOnlyList<KnowledgeBeliefRecord> GetBeliefsAboutSubject(string personId, string subjectId)
        {
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime) ? runtime.CreateSnapshot(subjectId: subjectId).Beliefs : Array.Empty<KnowledgeBeliefRecord>();
        }

        public IReadOnlyList<KnowledgeBeliefRecord> GetBeliefsAboutBody(string personId, string bodyId)
        {
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime) ? runtime.CreateSnapshot(bodyId: bodyId).Beliefs : Array.Empty<KnowledgeBeliefRecord>();
        }

        public IReadOnlyList<KnowledgeBeliefRecord> GetBeliefsByDomain(string personId, KnowledgeDomain domain)
        {
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime) ? runtime.CreateSnapshot(domain: domain).Beliefs : Array.Empty<KnowledgeBeliefRecord>();
        }

        public IReadOnlyList<KnowledgeBeliefRecord> GetKnownFacts(string personId)
        {
            return GetKnowledgeSnapshot(personId)?.KnownFacts ?? Array.Empty<KnowledgeBeliefRecord>();
        }

        public IReadOnlyList<KnowledgeBeliefRecord> GetSuspicions(string personId)
        {
            return GetKnowledgeSnapshot(personId)?.Suspicions ?? Array.Empty<KnowledgeBeliefRecord>();
        }

        public IReadOnlyList<KnowledgeBeliefRecord> GetMisconceptions(string personId)
        {
            return GetKnowledgeSnapshot(personId)?.Misconceptions ?? Array.Empty<KnowledgeBeliefRecord>();
        }

        public IReadOnlyList<KnowledgeBeliefRecord> GetStaleBeliefs(string personId)
        {
            return GetKnowledgeSnapshot(personId)?.StaleBeliefs ?? Array.Empty<KnowledgeBeliefRecord>();
        }

        public IReadOnlyList<KnowledgeEvidenceRecord> GetEvidenceForBelief(string personId, string beliefId)
        {
            KnowledgeSnapshot snapshot = GetKnowledgeSnapshot(personId);
            KnowledgeBeliefRecord belief = snapshot?.Beliefs.FirstOrDefault(record => string.Equals(record.BeliefId, beliefId, StringComparison.Ordinal));
            if (snapshot == null || belief == null)
            {
                return Array.Empty<KnowledgeEvidenceRecord>();
            }

            HashSet<string> evidenceIds = new HashSet<string>(belief.SupportingEvidenceIds.Concat(belief.OpposingEvidenceIds), StringComparer.Ordinal);
            return snapshot.Evidence.Where(record => evidenceIds.Contains(record.EvidenceId)).ToArray();
        }

        public bool DoesPersonKnow(string personId, KnowledgePropositionData proposition)
        {
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime) && runtime.DoesPersonKnow(proposition);
        }

        public int GetConfidence(string personId, KnowledgePropositionData proposition)
        {
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime) ? runtime.GetConfidence(proposition) : 0;
        }

        public KnowledgeValidationResult ValidateKnowledge(string personId)
        {
            return TryGetRuntime(personId, out PersonKnowledgeRuntime runtime)
                ? runtime.ValidateKnowledge()
                : new KnowledgeValidationResult(false, new[] { $"Knowledge runtime for Person '{personId}' is missing." }, Array.Empty<string>());
        }

        private bool TryGetRuntime(string personId, out PersonKnowledgeRuntime runtime)
        {
            runtime = null;
            return !string.IsNullOrWhiteSpace(personId) && runtimesByPerson.TryGetValue(personId, out runtime) && runtime != null;
        }
    }
}
