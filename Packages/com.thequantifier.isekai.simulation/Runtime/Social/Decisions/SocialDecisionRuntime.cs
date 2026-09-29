using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Decisions
{
    public sealed class SocialDecisionRuntime : IDisposable
    {
        private readonly Dictionary<string, SocialDecisionPersonStateData> statesByPerson = new Dictionary<string, SocialDecisionPersonStateData>(StringComparer.Ordinal);
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private RelationshipRuntime relationships;
        private InterpersonalAttitudeRuntime attitudes;
        private ReputationRuntime reputation;
        private RumorRuntime rumors;
        private SocialInteractionRuntime interactions;
        private SocialNormRuntime norms;
        private SocialNetworkRuntime networks;
        private ISocialDecisionModifierSource modifierSource;
        private bool disposed;
        private bool restoring;
        private long sequence;

        public long Revision { get; private set; }
        public bool IsReady => registry != null && interactions != null && !disposed;
        public bool IsDirty { get; private set; }
        public int Count => statesByPerson.Count;

        public void Configure(
            DefinitionRegistry definitionRegistry,
            IEnumerable<string> persons,
            SocialInteractionRuntime interactionRuntime,
            RelationshipRuntime relationshipRuntime = null,
            InterpersonalAttitudeRuntime attitudeRuntime = null,
            ReputationRuntime reputationRuntime = null,
            RumorRuntime rumorRuntime = null,
            SocialNormRuntime normRuntime = null,
            SocialNetworkRuntime networkRuntime = null,
            ISocialDecisionModifierSource socialDecisionModifierSource = null)
        {
            registry = definitionRegistry ?? registry;
            knownPersonIds = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            interactions = interactionRuntime ?? interactions;
            relationships = relationshipRuntime ?? relationships;
            attitudes = attitudeRuntime ?? attitudes;
            reputation = reputationRuntime ?? reputation;
            rumors = rumorRuntime ?? rumors;
            norms = normRuntime ?? norms;
            networks = networkRuntime ?? networks;
            modifierSource = socialDecisionModifierSource ?? modifierSource;
            disposed = false;
        }

        public bool TryGetState(string personId, out SocialDecisionPersonStateSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(personId) && statesByPerson.TryGetValue(Clean(personId), out SocialDecisionPersonStateData state))
            {
                snapshot = new SocialDecisionPersonStateSnapshot(state);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<SocialDecisionPersonStateSnapshot> CreateSnapshot()
        {
            return statesByPerson.Values.OrderBy(item => item.personId, StringComparer.Ordinal).Select(item => new SocialDecisionPersonStateSnapshot(item)).ToArray();
        }

        public SocialDecisionResult AssignProfile(string personId, string profileId, double worldTime = 0d)
        {
            long before = Revision;
            string actor = Clean(personId);
            string profile = Clean(profileId);
            if (!IsReady)
            {
                return Failure(SocialDecisionStatus.RuntimeNotReady, "Social Decision runtime is not ready.", string.Empty, actor, before);
            }

            if (!ValidateKnownPerson(actor))
            {
                return Failure(SocialDecisionStatus.InvalidRequest, $"Person '{actor}' is unknown.", string.Empty, actor, before);
            }

            if (!registry.TryGet(profile, out SocialDecisionProfileDefinition _))
            {
                return Failure(SocialDecisionStatus.MissingProfile, $"Social Decision Profile '{profile}' is missing.", string.Empty, actor, before);
            }

            SocialDecisionPersonStateData state = ResolveState(actor);
            state.decisionProfileId = profile;
            state.lastEvaluationWorldTime = Math.Max(state.lastEvaluationWorldTime, worldTime);
            state.revision++;
            Revision++;
            IsDirty = true;
            return new SocialDecisionResult(true, SocialDecisionStatus.CandidateSelected, "Social decision profile assigned.", string.Empty, actor, null, Array.Empty<SocialDecisionActionCandidateData>(), Array.Empty<SocialDecisionTargetCandidateData>(), null, false, before, Revision, Array.Empty<string>());
        }

        public SocialDecisionResult Evaluate(SocialDecisionRequest request)
        {
            request ??= new SocialDecisionRequest();
            long before = Revision;
            string actor = Clean(request.ActorPersonId);
            if (!IsReady || restoring)
            {
                return Failure(SocialDecisionStatus.RuntimeNotReady, "Social Decision runtime is not ready.", string.Empty, actor, before);
            }

            if (!ValidateKnownPerson(actor))
            {
                return Failure(SocialDecisionStatus.InvalidRequest, $"Actor Person '{actor}' is unknown.", string.Empty, actor, before);
            }

            SocialDecisionPersonStateData state = ResolveEvaluationState(actor, request.CommitDecisionState);
            string profileId = Clean(request.DecisionProfileId);
            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = state.decisionProfileId;
            }

            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = PrototypeSocialDecisionDefinitionFactory.SociallyNeutralProfileId;
            }

            if (!registry.TryGet(profileId, out SocialDecisionProfileDefinition profile))
            {
                return Failure(SocialDecisionStatus.MissingProfile, $"Social Decision Profile '{profileId}' is missing.", string.Empty, actor, before);
            }

            if (request.ActorControlPolicy == SocialDecisionActorControlPolicy.Disabled || (request.ActorControlPolicy == SocialDecisionActorControlPolicy.PlayerControlled && !profile.AllowPlayerControlled))
            {
                return CommitNoAction(request, state, profile, SocialDecisionStatus.Disabled, "Social decision-making is disabled for this actor.", before);
            }

            double worldTime = request.WorldTime;
            if (!request.ForceEvaluate && state.nextEligibleEvaluationWorldTime >= 0d && worldTime < state.nextEligibleEvaluationWorldTime)
            {
                return CommitNoAction(request, state, profile, SocialDecisionStatus.EvaluationCooldown, "Social evaluation is on cooldown.", before);
            }

            int targetLimit = Math.Max(0, request.MaximumTargetsOverride ?? profile.MaximumTargets);
            int candidateLimit = Math.Max(0, request.MaximumCandidatesOverride ?? profile.MaximumCandidates);
            bool truncated = false;
            List<string> diagnostics = new List<string>();
            SocialDecisionTargetCandidateData[] targets = DiscoverTargets(actor, request, targetLimit, ref truncated).ToArray();
            IReadOnlyList<SocialIntentionDefinition> intentions = ResolveIntentions(profile, request.ExplicitIntentionDefinitionId, ref truncated);
            List<SocialDecisionActionCandidateData> candidates = new List<SocialDecisionActionCandidateData>();
            foreach (SocialIntentionDefinition intention in intentions)
            {
                IEnumerable<SocialDecisionTargetCandidateData> targetSet = intention.RequiresTarget ? targets.Where(item => item.accepted) : new[] { new SocialDecisionTargetCandidateData { personId = string.Empty, source = SocialDecisionTargetSource.Explicit, accepted = true } };
                foreach (SocialDecisionTargetCandidateData target in targetSet)
                {
                    foreach (SocialDecisionActionCandidateData candidate in GenerateCandidates(actor, target.personId, intention, profile, request, state))
                    {
                        candidates.Add(candidate);
                        if (candidateLimit > 0 && candidates.Count >= candidateLimit)
                        {
                            truncated = true;
                            diagnostics.Add("Candidate generation truncated by profile limit.");
                            goto CandidateGenerationFinished;
                        }
                    }
                }
            }

CandidateGenerationFinished:
            SocialDecisionActionCandidateData selected = candidates
                .Where(item => item.hardRequirementsPassed && item.finalScore >= profile.ScoreThreshold)
                .OrderByDescending(item => item.finalScore)
                .ThenByDescending(item => item.urgency)
                .ThenByDescending(item => item.basePriority)
                .ThenBy(item => item.interactionDefinitionId, StringComparer.Ordinal)
                .ThenBy(item => item.targetPersonId, StringComparer.Ordinal)
                .ThenBy(item => item.candidateKey, StringComparer.Ordinal)
                .FirstOrDefault();

            if (selected == null)
            {
                diagnostics.Add(candidates.Count == 0 ? "No social action candidates were generated." : "All candidates were rejected or below threshold.");
                return CommitNoAction(request, state, profile, SocialDecisionStatus.NoAction, "No social action selected.", before, candidates, targets, truncated, diagnostics);
            }

            selected.selected = true;
            string decisionId = BuildDecisionId(actor, selected.candidateKey, worldTime, request.DeterministicSeed);
            SocialDecisionExecutionMode mode = request.ExecutionMode ?? profile.DefaultExecutionMode;
            SocialInteractionResult execution = null;
            SocialDecisionStatus status = SocialDecisionStatus.CandidateSelected;
            string message = "Social action candidate selected.";
            if (mode == SocialDecisionExecutionMode.SubmitForExecution && !selected.noInteraction)
            {
                SocialInteractionRequest interactionRequest = BuildInteractionRequest(request, selected, decisionId, preview: false);
                SocialInteractionResult preview = interactions.Preview(BuildInteractionRequest(request, selected, decisionId, preview: true));
                if (!preview.Succeeded)
                {
                    status = SocialDecisionStatus.StaleDecision;
                    message = $"Selected social action became stale before execution: {preview.Message}";
                }
                else
                {
                    execution = interactions.Execute(interactionRequest);
                    status = execution.Succeeded ? SocialDecisionStatus.Submitted : SocialDecisionStatus.ExecutionRejected;
                    message = execution.Message;
                }
            }
            else if (selected.noInteraction)
            {
                status = SocialDecisionStatus.NoAction;
                message = "No social interaction was forced; selected intention is satisfied by avoidance or deferral.";
            }

            if (request.CommitDecisionState)
            {
                CommitSelection(state, profile, selected, decisionId, status, message, worldTime, execution);
            }

            return new SocialDecisionResult(status != SocialDecisionStatus.ExecutionRejected && status != SocialDecisionStatus.StaleDecision, status, message, decisionId, actor, selected, LimitDiagnostics(candidates, profile), targets, execution, truncated, before, Revision, diagnostics);
        }

        public SocialDecisionRuntimeSaveData CreateSaveData()
        {
            return new SocialDecisionRuntimeSaveData
            {
                revision = Revision,
                decisionSequence = sequence,
                personStates = statesByPerson.Values.OrderBy(item => item.personId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public int PruneHistory(int maximumEntriesPerPerson)
        {
            int maximum = Math.Max(0, maximumEntriesPerPerson);
            int removed = 0;
            foreach (SocialDecisionPersonStateData state in statesByPerson.Values)
            {
                if (state.recentHistory == null || state.recentHistory.Count <= maximum)
                {
                    continue;
                }

                int removeCount = state.recentHistory.Count - maximum;
                state.recentHistory = state.recentHistory
                    .OrderByDescending(item => item.evaluationWorldTime)
                    .ThenByDescending(item => item.revision)
                    .Take(maximum)
                    .OrderBy(item => item.evaluationWorldTime)
                    .ThenBy(item => item.revision)
                    .ToList();
                removed += removeCount;
                state.revision++;
            }

            if (removed > 0)
            {
                Revision++;
                IsDirty = true;
            }

            return removed;
        }

        public SocialDecisionResult RestoreFromSaveData(SocialDecisionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failure))
            {
                return Failure(SocialDecisionStatus.ValidationFailed, failure, string.Empty, string.Empty, before);
            }

            restoring = restoringState;
            try
            {
                RestoreInternal(saveData ?? new SocialDecisionRuntimeSaveData());
            }
            finally
            {
                restoring = false;
            }

            return new SocialDecisionResult(true, SocialDecisionStatus.Restored, "Social decisions restored.", string.Empty, string.Empty, null, Array.Empty<SocialDecisionActionCandidateData>(), Array.Empty<SocialDecisionTargetCandidateData>(), null, false, before, Revision, Array.Empty<string>());
        }

        public static bool ValidateSaveData(SocialDecisionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            SocialDecisionRuntimeSaveData effective = saveData ?? new SocialDecisionRuntimeSaveData();
            if (effective.schemaVersion != SocialDecisionRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported Social Decision schema version {effective.schemaVersion}.";
                return false;
            }

            HashSet<string> known = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            HashSet<string> stateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialDecisionPersonStateData state in effective.personStates ?? new List<SocialDecisionPersonStateData>())
            {
                string personId = Clean(state?.personId);
                if (string.IsNullOrWhiteSpace(personId) || !known.Contains(personId))
                {
                    failure = $"Social Decision state references unknown Person '{personId}'.";
                    return false;
                }

                if (!stateIds.Add(personId))
                {
                    failure = $"Duplicate Social Decision state for Person '{personId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(state.decisionProfileId) && definitionRegistry != null && !definitionRegistry.TryGet(state.decisionProfileId, out SocialDecisionProfileDefinition _))
                {
                    failure = $"Social Decision state for '{personId}' references missing profile '{state.decisionProfileId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(state.activeIntentionDefinitionId) && definitionRegistry != null && !definitionRegistry.TryGet(state.activeIntentionDefinitionId, out SocialIntentionDefinition _))
                {
                    failure = $"Social Decision state for '{personId}' references missing intention '{state.activeIntentionDefinitionId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(state.activeTargetPersonId) && !known.Contains(state.activeTargetPersonId))
                {
                    failure = $"Social Decision state for '{personId}' references unknown target '{state.activeTargetPersonId}'.";
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            disposed = true;
            statesByPerson.Clear();
        }

        private IEnumerable<SocialDecisionActionCandidateData> GenerateCandidates(string actor, string target, SocialIntentionDefinition intention, SocialDecisionProfileDefinition profile, SocialDecisionRequest request, SocialDecisionPersonStateData state)
        {
            if (intention.AllowNoInteractionSelection)
            {
                yield return ScoreCandidate(actor, target, intention, profile, request, state, string.Empty, noInteraction: true);
            }

            foreach (string interactionId in intention.EligibleInteractionDefinitionIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(request.ExplicitInteractionDefinitionId) && !string.Equals(interactionId, request.ExplicitInteractionDefinitionId, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return ScoreCandidate(actor, target, intention, profile, request, state, interactionId, noInteraction: false);
            }
        }

        private SocialDecisionActionCandidateData ScoreCandidate(string actor, string target, SocialIntentionDefinition intention, SocialDecisionProfileDefinition profile, SocialDecisionRequest request, SocialDecisionPersonStateData state, string interactionId, bool noInteraction)
        {
            SocialDecisionActionCandidateData candidate = new SocialDecisionActionCandidateData
            {
                candidateKey = $"{intention.Id}|{target}|{interactionId}|{request.WorldTime:0.###}",
                intentionDefinitionId = intention.Id,
                intentionCategory = intention.Category,
                targetPersonId = target,
                interactionDefinitionId = interactionId,
                basePriority = intention.BasePriority,
                urgency = Urgency(intention, request.WorldTime, state),
                noInteraction = noInteraction,
                hardRequirementsPassed = true
            };

            if (intention.RequiresTarget && !ValidateKnownPerson(target))
            {
                candidate.hardRequirementsPassed = false;
                candidate.rejectionReason = "Target is missing or unavailable.";
            }

            if (!noInteraction && !registry.TryGet(interactionId, out SocialInteractionDefinition _))
            {
                candidate.hardRequirementsPassed = false;
                candidate.rejectionReason = $"Social Interaction '{interactionId}' is missing.";
            }

            if (candidate.hardRequirementsPassed && !noInteraction)
            {
                SocialInteractionResult preview = interactions.Preview(BuildInteractionRequest(request, candidate, "preview", preview: true));
                candidate.previewStatus = preview.Status.ToString();
                candidate.previewMessage = preview.Message;
                if (!preview.Succeeded)
                {
                    candidate.hardRequirementsPassed = false;
                    candidate.rejectionReason = $"Feature 12.5 preview rejected candidate: {preview.Message}";
                }
            }

            List<SocialDecisionConsiderationResultData> considerations = new List<SocialDecisionConsiderationResultData>();
            foreach (SocialConsiderationDefinition consideration in ResolveConsiderations(profile, intention))
            {
                SocialDecisionConsiderationResultData result = EvaluateConsideration(actor, target, consideration, request, state);
                considerations.Add(result);
                if (result.rejected || (consideration.Required && result.missingData))
                {
                    candidate.hardRequirementsPassed = false;
                    candidate.rejectionReason = string.IsNullOrWhiteSpace(candidate.rejectionReason) ? $"Required consideration '{consideration.Id}' rejected candidate." : candidate.rejectionReason;
                }
            }

            candidate.considerations = considerations.ToArray();
            candidate.considerationScore = considerations.Sum(item => item.weightedScore);
            candidate.cooldownPenalty = IsOnCooldown(state, CooldownKey(actor, target, intention.Id, interactionId), request.WorldTime, intention.CooldownSeconds) ? 200 : 0;
            candidate.repetitionPenalty = RecentRepetitionPenalty(state, target, intention.Id, interactionId);
            candidate.externalModifier = modifierSource?.ResolveSocialDecisionScoreModifier(actor, target, intention.Id, interactionId, request.WorldTime, out string _) ?? 0;
            candidate.finalScore = Math.Max(0, Math.Min(1000, candidate.basePriority + candidate.urgency + candidate.considerationScore + candidate.externalModifier - candidate.cooldownPenalty - candidate.repetitionPenalty));
            if (candidate.cooldownPenalty > 0)
            {
                candidate.hardRequirementsPassed = false;
                candidate.rejectionReason = "Candidate is on cooldown.";
            }

            return candidate;
        }

        private SocialDecisionConsiderationResultData EvaluateConsideration(string actor, string target, SocialConsiderationDefinition definition, SocialDecisionRequest request, SocialDecisionPersonStateData state)
        {
            bool missing = false;
            int raw = definition.Input switch
            {
                SocialDecisionConsiderationInput.Constant => 50,
                SocialDecisionConsiderationInput.TrustTowardTarget => Attitude(actor, target, PrototypeAttitudeDefinitionFactory.TrustId, ref missing),
                SocialDecisionConsiderationInput.AffectionTowardTarget => Attitude(actor, target, PrototypeAttitudeDefinitionFactory.AffectionId, ref missing),
                SocialDecisionConsiderationInput.RespectTowardTarget => Attitude(actor, target, PrototypeAttitudeDefinitionFactory.RespectId, ref missing),
                SocialDecisionConsiderationInput.FearTowardTarget => Attitude(actor, target, PrototypeAttitudeDefinitionFactory.FearId, ref missing),
                SocialDecisionConsiderationInput.LoyaltyTowardTarget => Attitude(actor, target, PrototypeAttitudeDefinitionFactory.LoyaltyId, ref missing),
                SocialDecisionConsiderationInput.HostilityTowardTarget => Attitude(actor, target, PrototypeAttitudeDefinitionFactory.HostilityId, ref missing),
                SocialDecisionConsiderationInput.TargetTrustTowardActor => Attitude(target, actor, PrototypeAttitudeDefinitionFactory.TrustId, ref missing),
                SocialDecisionConsiderationInput.RelationshipExists => relationships != null && relationships.QueryBetween(actor, target, true).Count > 0 ? 100 : 0,
                SocialDecisionConsiderationInput.SharedGroupMembership => SharedGroup(actor, target) ? 100 : 0,
                SocialDecisionConsiderationInput.TargetIsolation => TargetIsolated(target, request.WorldTime) ? 100 : 0,
                SocialDecisionConsiderationInput.GraphDistance => GraphNeighbor(actor, target, request.WorldTime) ? 100 : 0,
                SocialDecisionConsiderationInput.ReputationEsteem => Reputation(target, PrototypeReputationDefinitionFactory.EsteemId, ref missing),
                SocialDecisionConsiderationInput.ReputationDanger => Reputation(target, PrototypeReputationDefinitionFactory.PerceivedDangerId, ref missing),
                SocialDecisionConsiderationInput.PendingRequest => string.IsNullOrWhiteSpace(request.PendingInteractionId) ? 0 : 100,
                SocialDecisionConsiderationInput.RepetitionPenalty => Math.Max(0, 100 - state.repetitionCount * 20),
                SocialDecisionConsiderationInput.Cooldown => 100,
                SocialDecisionConsiderationInput.ScriptedPriority => string.IsNullOrWhiteSpace(request.ExplicitIntentionDefinitionId) ? 0 : 100,
                _ => 0
            };

            if (missing && definition.MissingDataPolicy == SocialDecisionMissingDataPolicy.RejectCandidate)
            {
                return Result(definition, raw, 0, 0, true, true, "Required data missing.");
            }

            if (missing && definition.MissingDataPolicy == SocialDecisionMissingDataPolicy.IgnoreConsideration)
            {
                return Result(definition, raw, 0, 0, true, false, "Missing data ignored.");
            }

            int normalized = Normalize(raw, definition.InputMinimum, definition.InputMaximum, definition.ResponseCurve);
            int weighted = (normalized * definition.Weight) / 100;
            return Result(definition, raw, normalized, weighted, missing, false, string.Empty);
        }

        private SocialInteractionRequest BuildInteractionRequest(SocialDecisionRequest request, SocialDecisionActionCandidateData candidate, string decisionId, bool preview)
        {
            string tx = preview ? $"preview.{decisionId}.{candidate.candidateKey}" : $"social-decision.{decisionId}";
            return new SocialInteractionRequest
            {
                TransactionId = BuildStableId("tx.social-decision", tx),
                InteractionRecordId = BuildStableId("social-interaction-record", $"{decisionId}.{candidate.interactionDefinitionId}.{candidate.targetPersonId}"),
                InteractionDefinitionId = candidate.interactionDefinitionId,
                InitiatorPersonId = Clean(request.ActorPersonId),
                TargetPersonId = candidate.targetPersonId,
                WitnessPersonIds = Clean(request.WitnessPersonIds).ToArray(),
                AudienceId = Clean(request.AudienceId),
                PlaceId = Clean(request.PlaceId),
                Subject = new SocialInteractionSubjectData { kind = SocialInteractionSubjectKind.Person, subjectId = candidate.targetPersonId, ownerPersonId = candidate.targetPersonId, tags = new[] { "social-decision", candidate.intentionDefinitionId } },
                Channel = SocialInteractionCommunicationChannel.Conversation,
                WorldTime = request.WorldTime,
                DeterministicSeed = request.DeterministicSeed ?? string.Empty,
                OriginatingReferenceId = decisionId,
                Preview = preview
            };
        }

        private IEnumerable<SocialDecisionTargetCandidateData> DiscoverTargets(string actor, SocialDecisionRequest request, int limit, ref bool truncated)
        {
            Dictionary<string, SocialDecisionTargetCandidateData> targets = new Dictionary<string, SocialDecisionTargetCandidateData>(StringComparer.Ordinal);
            AddTarget(targets, actor, request.ExplicitTargetPersonId, SocialDecisionTargetSource.Explicit, 100);
            foreach (string target in Clean(request.AvailableTargetPersonIds))
            {
                AddTarget(targets, actor, target, SocialDecisionTargetSource.AvailableContext, 80);
            }

            if (relationships != null)
            {
                foreach (RelationshipSnapshot relationship in relationships.QueryByPerson(actor, true))
                {
                    foreach (string target in relationship.Participants.Select(item => item.personId))
                    {
                        AddTarget(targets, actor, target, SocialDecisionTargetSource.Relationship, 60);
                    }
                }
            }

            if (networks != null)
            {
                foreach (SocialGroupMembershipSnapshot membership in networks.QueryMembershipsByPerson(actor, true))
                {
                    foreach (SocialGroupMembershipSnapshot member in networks.QueryMembers(membership.GroupId, true))
                    {
                        AddTarget(targets, actor, member.PersonId, SocialDecisionTargetSource.GroupMembership, 55);
                    }
                }

                foreach (SocialGraphNeighborResult neighbor in networks.QueryNeighbors(actor, new SocialGraphQueryRequest { ProjectionDefinitionId = PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, WorldTime = request.WorldTime, MaxDepth = 2, MaxVisitedNodes = Math.Max(8, limit * 2) }).Take(Math.Max(0, limit)))
                {
                    AddTarget(targets, actor, neighbor.PersonId, SocialDecisionTargetSource.SocialGraph, 50);
                }
            }

            IEnumerable<SocialDecisionTargetCandidateData> ordered = targets.Values.OrderByDescending(item => item.priority).ThenBy(item => item.personId, StringComparer.Ordinal);
            if (limit > 0 && targets.Count > limit)
            {
                truncated = true;
                ordered = ordered.Take(limit);
            }

            return ordered.Select(item => item.Clone()).ToArray();
        }

        private IReadOnlyList<SocialIntentionDefinition> ResolveIntentions(SocialDecisionProfileDefinition profile, string explicitIntentionId, ref bool truncated)
        {
            IEnumerable<string> ids = string.IsNullOrWhiteSpace(explicitIntentionId) ? profile.EnabledIntentionIds : new[] { explicitIntentionId };
            List<SocialIntentionDefinition> intentions = ids.Select(id => registry.TryGet(id, out SocialIntentionDefinition definition) ? definition : null).Where(item => item != null).OrderByDescending(item => item.BasePriority).ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
            if (profile.MaximumIntentions > 0 && intentions.Count > profile.MaximumIntentions)
            {
                truncated = true;
                intentions = intentions.Take(profile.MaximumIntentions).ToList();
            }

            return intentions;
        }

        private IEnumerable<SocialConsiderationDefinition> ResolveConsiderations(SocialDecisionProfileDefinition profile, SocialIntentionDefinition intention)
        {
            return profile.ConsiderationIds.Concat(intention.ConsiderationIds)
                .Distinct(StringComparer.Ordinal)
                .Select(id => registry.TryGet(id, out SocialConsiderationDefinition definition) ? definition : null)
                .Where(item => item != null)
                .OrderBy(item => item.Id, StringComparer.Ordinal);
        }

        private void CommitSelection(SocialDecisionPersonStateData state, SocialDecisionProfileDefinition profile, SocialDecisionActionCandidateData selected, string decisionId, SocialDecisionStatus status, string message, double worldTime, SocialInteractionResult execution)
        {
            state.decisionProfileId = profile.Id;
            state.activeDecisionId = decisionId;
            state.activeIntentionDefinitionId = selected.intentionDefinitionId;
            state.activeTargetPersonId = selected.targetPersonId;
            state.selectedInteractionDefinitionId = selected.interactionDefinitionId;
            state.pendingInteractionReferenceId = execution?.Pending?.PendingInteractionId ?? string.Empty;
            state.lifecycleState = status == SocialDecisionStatus.Submitted ? SocialDecisionLifecycleState.Completed : status == SocialDecisionStatus.NoAction ? SocialDecisionLifecycleState.Deferred : SocialDecisionLifecycleState.CandidateSelected;
            state.intentionStartWorldTime = state.intentionStartWorldTime < 0d ? worldTime : state.intentionStartWorldTime;
            state.lastEvaluationWorldTime = worldTime;
            state.nextEligibleEvaluationWorldTime = worldTime + profile.EvaluationIntervalSeconds;
            state.repetitionCount++;
            state.revision++;
            StoreCooldown(state, CooldownKey(state.personId, selected.targetPersonId, selected.intentionDefinitionId, selected.interactionDefinitionId), worldTime, decisionId);
            state.recentHistory.Add(new SocialDecisionHistoryEntryData { decisionId = decisionId, actorPersonId = state.personId, targetPersonId = selected.targetPersonId, intentionDefinitionId = selected.intentionDefinitionId, interactionDefinitionId = selected.interactionDefinitionId, status = status, lifecycleState = state.lifecycleState, score = selected.finalScore, evaluationWorldTime = worldTime, executionInteractionRecordId = execution?.Record?.InteractionRecordId ?? string.Empty, failureReason = status == SocialDecisionStatus.ExecutionRejected || status == SocialDecisionStatus.StaleDecision ? message : string.Empty, revision = state.revision });
            state.recentHistory = state.recentHistory.OrderByDescending(item => item.evaluationWorldTime).ThenBy(item => item.decisionId, StringComparer.Ordinal).Take(Math.Max(1, profile.MaximumDiagnostics)).Select(item => item.Clone()).ToList();
            Revision++;
            IsDirty = true;
        }

        private SocialDecisionResult CommitNoAction(SocialDecisionRequest request, SocialDecisionPersonStateData state, SocialDecisionProfileDefinition profile, SocialDecisionStatus status, string message, long before, IEnumerable<SocialDecisionActionCandidateData> candidates = null, IEnumerable<SocialDecisionTargetCandidateData> targets = null, bool truncated = false, IEnumerable<string> diagnostics = null)
        {
            if (request.CommitDecisionState)
            {
                state.decisionProfileId = profile.Id;
                state.lifecycleState = SocialDecisionLifecycleState.Idle;
                state.lastEvaluationWorldTime = request.WorldTime;
                state.nextEligibleEvaluationWorldTime = request.WorldTime + profile.EvaluationIntervalSeconds;
                state.revision++;
                Revision++;
                IsDirty = true;
            }

            return new SocialDecisionResult(status == SocialDecisionStatus.NoAction || status == SocialDecisionStatus.EvaluationCooldown || status == SocialDecisionStatus.Disabled, status, message, string.Empty, state.personId, null, LimitDiagnostics(candidates ?? Array.Empty<SocialDecisionActionCandidateData>(), profile), targets ?? Array.Empty<SocialDecisionTargetCandidateData>(), null, truncated, before, Revision, diagnostics ?? Array.Empty<string>());
        }

        private SocialDecisionPersonStateData ResolveState(string personId)
        {
            personId = Clean(personId);
            if (!statesByPerson.TryGetValue(personId, out SocialDecisionPersonStateData state))
            {
                state = new SocialDecisionPersonStateData { personId = personId };
                statesByPerson[personId] = state;
            }

            return state;
        }

        private SocialDecisionPersonStateData ResolveEvaluationState(string personId, bool commitDecisionState)
        {
            personId = Clean(personId);
            if (commitDecisionState)
            {
                return ResolveState(personId);
            }

            return statesByPerson.TryGetValue(personId, out SocialDecisionPersonStateData state)
                ? state.Clone()
                : new SocialDecisionPersonStateData { personId = personId };
        }

        private void RestoreInternal(SocialDecisionRuntimeSaveData saveData)
        {
            statesByPerson.Clear();
            foreach (SocialDecisionPersonStateData state in saveData?.personStates ?? new List<SocialDecisionPersonStateData>())
            {
                SocialDecisionPersonStateData clone = state.Clone();
                statesByPerson[clone.personId] = clone;
            }

            Revision = saveData?.revision ?? 0L;
            sequence = saveData?.decisionSequence ?? 0L;
            IsDirty = false;
        }

        private int Attitude(string observer, string subject, string dimensionId, ref bool missing)
        {
            if (attitudes == null || string.IsNullOrWhiteSpace(observer) || string.IsNullOrWhiteSpace(subject))
            {
                missing = true;
                return 0;
            }

            AttitudeEffectiveValueSnapshot value = attitudes.ResolveValue(observer, subject, dimensionId);
            missing = missing || !value.RecordExists;
            return value.EffectiveValue;
        }

        private int Reputation(string subject, string dimensionId, ref bool missing)
        {
            if (reputation == null || string.IsNullOrWhiteSpace(subject))
            {
                missing = true;
                return 0;
            }

            ReputationEffectiveValueSnapshot value = reputation.ResolveValue(subject, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, dimensionId, true);
            missing = missing || !value.RecordExists;
            return value.EffectiveValue;
        }

        private bool SharedGroup(string actor, string target)
        {
            if (networks == null || string.IsNullOrWhiteSpace(target)) return false;
            HashSet<string> actorGroups = new HashSet<string>(networks.QueryMembershipsByPerson(actor, true).Select(item => item.GroupId), StringComparer.Ordinal);
            return networks.QueryMembershipsByPerson(target, true).Any(item => actorGroups.Contains(item.GroupId));
        }

        private bool TargetIsolated(string target, double worldTime)
        {
            if (networks == null || string.IsNullOrWhiteSpace(target)) return false;
            return networks.BuildGraph(new SocialGraphQueryRequest { ProjectionDefinitionId = PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, WorldTime = worldTime, MaxDepth = 1, MaxVisitedNodes = 32 }).Nodes.Any(item => string.Equals(item.NodeId, target, StringComparison.Ordinal) && item.Isolated);
        }

        private bool GraphNeighbor(string actor, string target, double worldTime)
        {
            return networks != null && networks.QueryNeighbors(actor, new SocialGraphQueryRequest { ProjectionDefinitionId = PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, WorldTime = worldTime, MaxDepth = 2, MaxVisitedNodes = 32 }).Any(item => string.Equals(item.PersonId, target, StringComparison.Ordinal));
        }

        private static SocialDecisionConsiderationResultData Result(SocialConsiderationDefinition definition, int raw, int normalized, int weighted, bool missing, bool rejected, string diagnostics)
        {
            return new SocialDecisionConsiderationResultData { considerationId = definition.Id, input = definition.Input, rawValue = raw, normalizedValue = normalized, weightedScore = weighted, missingData = missing, rejected = rejected, diagnostics = diagnostics ?? string.Empty };
        }

        private static int Normalize(int raw, int minimum, int maximum, SocialDecisionResponseCurve curve)
        {
            if (maximum <= minimum)
            {
                return raw >= maximum ? 100 : 0;
            }

            double t = Math.Max(0d, Math.Min(1d, (raw - minimum) / (double)(maximum - minimum)));
            t = curve switch
            {
                SocialDecisionResponseCurve.InverseLinear => 1d - t,
                SocialDecisionResponseCurve.Step => t >= 0.5d ? 1d : 0d,
                SocialDecisionResponseCurve.Threshold => t >= 0.75d ? 1d : 0d,
                SocialDecisionResponseCurve.Quadratic => t * t,
                _ => t
            };
            return (int)Math.Round(t * 100d);
        }

        private static int Urgency(SocialIntentionDefinition intention, double worldTime, SocialDecisionPersonStateData state)
        {
            if (!string.Equals(state.activeIntentionDefinitionId, intention.Id, StringComparison.Ordinal) || state.intentionStartWorldTime < 0d) return 0;
            return Math.Min(100, (int)Math.Max(0d, worldTime - state.intentionStartWorldTime));
        }

        private static bool IsOnCooldown(SocialDecisionPersonStateData state, string key, double worldTime, double seconds)
        {
            if (seconds <= 0d) return false;
            SocialDecisionCooldownData cooldown = state.cooldowns.FirstOrDefault(item => string.Equals(item.cooldownKey, key, StringComparison.Ordinal));
            return cooldown != null && worldTime < cooldown.lastWorldTime + seconds;
        }

        private static int RecentRepetitionPenalty(SocialDecisionPersonStateData state, string target, string intentionId, string interactionId)
        {
            int repetitions = state.recentHistory.Count(item => string.Equals(item.targetPersonId, target, StringComparison.Ordinal) && string.Equals(item.intentionDefinitionId, intentionId, StringComparison.Ordinal) && string.Equals(item.interactionDefinitionId, interactionId, StringComparison.Ordinal));
            return Math.Min(200, repetitions * 25);
        }

        private static void StoreCooldown(SocialDecisionPersonStateData state, string key, double worldTime, string decisionId)
        {
            SocialDecisionCooldownData existing = state.cooldowns.FirstOrDefault(item => string.Equals(item.cooldownKey, key, StringComparison.Ordinal));
            if (existing == null)
            {
                state.cooldowns.Add(new SocialDecisionCooldownData { cooldownKey = key, lastWorldTime = worldTime, sourceDecisionId = decisionId });
            }
            else
            {
                existing.lastWorldTime = worldTime;
                existing.sourceDecisionId = decisionId;
            }
        }

        private static IEnumerable<SocialDecisionActionCandidateData> LimitDiagnostics(IEnumerable<SocialDecisionActionCandidateData> candidates, SocialDecisionProfileDefinition profile)
        {
            return (candidates ?? Array.Empty<SocialDecisionActionCandidateData>()).OrderByDescending(item => item.finalScore).ThenBy(item => item.candidateKey, StringComparer.Ordinal).Take(Math.Max(1, profile.MaximumDiagnostics)).Select(item => item.Clone()).ToArray();
        }

        private static void AddTarget(Dictionary<string, SocialDecisionTargetCandidateData> targets, string actor, string target, SocialDecisionTargetSource source, int priority)
        {
            target = Clean(target);
            if (string.IsNullOrWhiteSpace(target) || string.Equals(actor, target, StringComparison.Ordinal)) return;
            if (targets.TryGetValue(target, out SocialDecisionTargetCandidateData existing))
            {
                if (priority > existing.priority)
                {
                    existing.priority = priority;
                    existing.source = source;
                }
                return;
            }

            targets[target] = new SocialDecisionTargetCandidateData { personId = target, source = source, priority = priority, accepted = true };
        }

        private bool ValidateKnownPerson(string personId) => !string.IsNullOrWhiteSpace(personId) && knownPersonIds.Contains(Clean(personId));
        private static string CooldownKey(string actor, string target, string intentionId, string interactionId) => $"{Clean(actor)}|{Clean(target)}|{Clean(intentionId)}|{Clean(interactionId)}";
        private string BuildDecisionId(string actor, string candidateKey, double worldTime, string seed) => BuildStableId("social-decision", $"{actor}.{candidateKey}.{worldTime:0.###}.{seed}.{DependencyRevisionSeed()}");
        private long DependencyRevisionSeed() => (relationships?.Revision ?? 0L) ^ (attitudes?.Revision ?? 0L) ^ (reputation?.Revision ?? 0L) ^ (rumors?.Revision ?? 0L) ^ (interactions?.Revision ?? 0L) ^ (norms?.Revision ?? 0L) ^ (networks?.Revision ?? 0L);
        private static SocialDecisionResult Failure(SocialDecisionStatus status, string message, string decisionId, string actor, long revision) => new SocialDecisionResult(false, status, message, decisionId, actor, null, Array.Empty<SocialDecisionActionCandidateData>(), Array.Empty<SocialDecisionTargetCandidateData>(), null, false, revision, revision, Array.Empty<string>());
        private static IEnumerable<string> Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);
        private static string Clean(string value) => value?.Trim() ?? string.Empty;

        private static string BuildStableId(string prefix, string source)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(source ?? string.Empty));
            return $"{prefix}.{BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant()}";
        }
    }
}
