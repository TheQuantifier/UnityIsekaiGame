using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Reputation;

namespace UnityIsekaiGame.Social.Influence
{
    public sealed class SocialInfluenceRuntime : ISocialDecisionModifierSource, IDisposable
    {
        private readonly Dictionary<string, SocialInfluenceAttemptRecordData> attemptsById = new Dictionary<string, SocialInfluenceAttemptRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialInfluenceProcessedTransactionData> processedTransactions = new Dictionary<string, SocialInfluenceProcessedTransactionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialInfluenceCooldownData> cooldownsByKey = new Dictionary<string, SocialInfluenceCooldownData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialInfluenceDecisionModifierData> decisionModifiersById = new Dictionary<string, SocialInfluenceDecisionModifierData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PersonKnowledgeRuntime> knowledgeByPerson = new Dictionary<string, PersonKnowledgeRuntime>(StringComparer.Ordinal);
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private InterpersonalAttitudeRuntime attitudes;
        private ReputationRuntime reputation;
        private SocialInteractionRuntime interactions;
        private bool disposed;
        private bool restoring;
        private long sequence;

        public long Revision { get; private set; }
        public bool IsReady => registry != null && !disposed;
        public bool IsDirty { get; private set; }
        public int Count => attemptsById.Count;

        public void Configure(
            DefinitionRegistry definitionRegistry,
            IEnumerable<string> persons,
            InterpersonalAttitudeRuntime attitudeRuntime = null,
            ReputationRuntime reputationRuntime = null,
            SocialInteractionRuntime interactionRuntime = null,
            IEnumerable<PersonKnowledgeRuntime> knowledgeRuntimes = null)
        {
            registry = definitionRegistry ?? registry;
            knownPersonIds = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            attitudes = attitudeRuntime ?? attitudes;
            reputation = reputationRuntime ?? reputation;
            interactions = interactionRuntime ?? interactions;
            foreach (PersonKnowledgeRuntime knowledge in knowledgeRuntimes ?? Array.Empty<PersonKnowledgeRuntime>())
            {
                RegisterKnowledgeRuntime(knowledge);
            }

            disposed = false;
        }

        public void RegisterKnowledgeRuntime(PersonKnowledgeRuntime knowledge)
        {
            if (knowledge != null && !string.IsNullOrWhiteSpace(knowledge.PersonId))
            {
                knowledgeByPerson[knowledge.PersonId.Trim()] = knowledge;
            }
        }

        public IReadOnlyList<SocialInfluenceAttemptSnapshot> CreateSnapshot()
        {
            return attemptsById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.attemptId, StringComparer.Ordinal).Select(item => new SocialInfluenceAttemptSnapshot(item)).ToArray();
        }

        public IReadOnlyList<SocialInfluenceDecisionModifierData> QueryActiveDecisionModifiers(string targetPersonId, double worldTime)
        {
            string target = Clean(targetPersonId);
            return decisionModifiersById.Values
                .Where(item => item.IsActiveAt(worldTime) && (string.IsNullOrWhiteSpace(target) || string.Equals(item.targetPersonId, target, StringComparison.Ordinal)))
                .OrderBy(item => item.modifierId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public int ResolveSocialDecisionScoreModifier(string actorPersonId, string targetPersonId, string intentionDefinitionId, string interactionDefinitionId, double worldTime, out string sourceModifierId)
        {
            string actor = Clean(actorPersonId);
            string target = Clean(targetPersonId);
            string intention = Clean(intentionDefinitionId);
            string interaction = Clean(interactionDefinitionId);
            SocialInfluenceDecisionModifierData[] modifiers = decisionModifiersById.Values
                .Where(item => item.IsActiveAt(worldTime))
                .Where(item => string.IsNullOrWhiteSpace(item.actorPersonId) || string.Equals(item.actorPersonId, actor, StringComparison.Ordinal))
                .Where(item => string.IsNullOrWhiteSpace(item.targetPersonId) || string.Equals(item.targetPersonId, target, StringComparison.Ordinal))
                .Where(item => string.IsNullOrWhiteSpace(item.intentionDefinitionId) || string.Equals(item.intentionDefinitionId, intention, StringComparison.Ordinal))
                .Where(item => string.IsNullOrWhiteSpace(item.interactionDefinitionId) || string.Equals(item.interactionDefinitionId, interaction, StringComparison.Ordinal))
                .OrderBy(item => item.modifierId, StringComparer.Ordinal)
                .ToArray();
            sourceModifierId = string.Join(",", modifiers.Select(item => item.modifierId));
            return Math.Max(-250, Math.Min(250, modifiers.Sum(item => item.scoreDelta)));
        }

        public SocialInfluenceResult Preview(SocialInfluenceRequest request)
        {
            SocialInfluenceRequest clone = CloneRequest(request);
            clone.Preview = true;
            return Resolve(clone);
        }

        public SocialInfluenceResult Execute(SocialInfluenceRequest request)
        {
            SocialInfluenceRequest clone = CloneRequest(request);
            clone.Preview = false;
            return Resolve(clone);
        }

        public SocialInfluenceRuntimeSaveData CreateSaveData()
        {
            return new SocialInfluenceRuntimeSaveData
            {
                revision = Revision,
                attemptSequence = sequence,
                attempts = attemptsById.Values.OrderBy(item => item.attemptId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                processedTransactions = processedTransactions.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                cooldowns = cooldownsByKey.Values.OrderBy(item => item.cooldownKey, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                decisionModifiers = decisionModifiersById.Values.OrderBy(item => item.modifierId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public int PruneHistory(int maximumAttempts)
        {
            string[] remove = attemptsById.Values.OrderByDescending(item => item.worldTime).ThenByDescending(item => item.revision)
                .Skip(Math.Max(0, maximumAttempts)).Select(item => item.attemptId).ToArray();
            foreach (string id in remove) attemptsById.Remove(id);
            HashSet<string> retained = new HashSet<string>(attemptsById.Keys, StringComparer.Ordinal);
            foreach (string tx in processedTransactions.Values.Where(item => !retained.Contains(item.attemptId)).Select(item => item.transactionId).ToArray()) processedTransactions.Remove(tx);
            foreach (string key in cooldownsByKey.Values.Where(item => !retained.Contains(item.sourceAttemptId)).Select(item => item.cooldownKey).ToArray()) cooldownsByKey.Remove(key);
            foreach (string id in decisionModifiersById.Values.Where(item => !retained.Contains(item.sourceAttemptId)).Select(item => item.modifierId).ToArray()) decisionModifiersById.Remove(id);
            if (remove.Length > 0) { Revision++; IsDirty = true; }
            return remove.Length;
        }

        public SocialInfluenceResult RestoreFromSaveData(SocialInfluenceRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failure))
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.RestoreFailed, failure, string.Empty, before);
            }

            restoring = restoringState;
            try
            {
                RestoreInternal(saveData ?? new SocialInfluenceRuntimeSaveData());
            }
            finally
            {
                restoring = false;
            }

            return new SocialInfluenceResult(true, SocialInfluenceStatus.Restored, "Social influence restored.", null, null, null, null, false, false, before, Revision, Array.Empty<string>());
        }

        public static bool ValidateSaveData(SocialInfluenceRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            SocialInfluenceRuntimeSaveData effective = saveData ?? new SocialInfluenceRuntimeSaveData();
            if (effective.schemaVersion != SocialInfluenceRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported Social Influence schema version {effective.schemaVersion}.";
                return false;
            }

            HashSet<string> known = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            HashSet<string> attemptIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialInfluenceAttemptRecordData attempt in effective.attempts ?? new List<SocialInfluenceAttemptRecordData>())
            {
                if (attempt == null || string.IsNullOrWhiteSpace(attempt.attemptId) || !attemptIds.Add(attempt.attemptId))
                {
                    failure = "Social Influence payload contains a missing or duplicate attempt ID.";
                    return false;
                }

                if (!KnownOrEmpty(known, attempt.speakerPersonId) || !KnownOrEmpty(known, attempt.targetPersonId))
                {
                    failure = $"Social Influence attempt '{attempt.attemptId}' references an unknown Person.";
                    return false;
                }

                if (definitionRegistry != null && !string.IsNullOrWhiteSpace(attempt.methodDefinitionId) && !definitionRegistry.TryGet(attempt.methodDefinitionId, out SocialInfluenceMethodDefinition _))
                {
                    failure = $"Social Influence attempt '{attempt.attemptId}' references missing method '{attempt.methodDefinitionId}'.";
                    return false;
                }
            }

            HashSet<string> modifierIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialInfluenceDecisionModifierData modifier in effective.decisionModifiers ?? new List<SocialInfluenceDecisionModifierData>())
            {
                if (modifier == null || string.IsNullOrWhiteSpace(modifier.modifierId) || !modifierIds.Add(modifier.modifierId))
                {
                    failure = "Social Influence payload contains a missing or duplicate decision modifier ID.";
                    return false;
                }

                if (!KnownOrEmpty(known, modifier.actorPersonId) || !KnownOrEmpty(known, modifier.targetPersonId))
                {
                    failure = $"Social Influence decision modifier '{modifier.modifierId}' references an unknown Person.";
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            disposed = true;
            attemptsById.Clear();
            processedTransactions.Clear();
            cooldownsByKey.Clear();
            decisionModifiersById.Clear();
            knowledgeByPerson.Clear();
        }

        private SocialInfluenceResult Resolve(SocialInfluenceRequest request)
        {
            request ??= new SocialInfluenceRequest();
            long before = Revision;
            if (!IsReady || restoring)
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.RuntimeNotReady, "Social Influence runtime is not ready.", request.AttemptId, before);
            }

            string tx = Clean(request.TransactionId);
            if (string.IsNullOrWhiteSpace(tx))
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.InvalidRequest, "Social influence requires a transaction ID.", request.AttemptId, before);
            }

            if (!request.Preview && processedTransactions.TryGetValue(tx, out SocialInfluenceProcessedTransactionData processed))
            {
                attemptsById.TryGetValue(processed.attemptId, out SocialInfluenceAttemptRecordData existing);
                return new SocialInfluenceResult(true, SocialInfluenceStatus.Duplicate, "Social influence transaction already processed.", existing, ResolveModifier(existing?.decisionModifierId), null, null, false, true, before, before, existing?.diagnostics);
            }

            string methodId = Clean(request.MethodDefinitionId);
            if (!registry.TryGet(methodId, out SocialInfluenceMethodDefinition method))
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.MissingMethod, $"Social Influence Method '{methodId}' is missing.", request.AttemptId, before);
            }

            string speaker = Clean(request.SpeakerPersonId);
            string target = Clean(request.TargetPersonId);
            if (!ValidateKnownPerson(speaker) || !ValidateKnownPerson(target))
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.MissingPerson, "Social influence speaker and target must be known Persons.", request.AttemptId, before);
            }

            SocialInfluenceSubjectData subject = request.Subject?.Clone() ?? new SocialInfluenceSubjectData();
            if (!method.SupportedIntents.Contains(request.Intent))
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.UnsupportedIntent, $"Method '{method.Id}' does not support intent '{request.Intent}'.", request.AttemptId, before);
            }

            if (!method.SupportedSubjectKinds.Contains(subject.kind))
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.UnsupportedSubject, $"Method '{method.Id}' does not support subject '{subject.kind}'.", request.AttemptId, before);
            }

            bool deception = request.DeceptionMode != SocialInfluenceDeceptionMode.NoDeception;
            if (deception && !method.DeceptionAllowed)
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.DisclosureBlocked, $"Method '{method.Id}' cannot carry deception mode '{request.DeceptionMode}'.", request.AttemptId, before);
            }

            if (RequiresClaim(request.Intent) && request.Claim == null)
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.MissingClaim, "Influence intent requires a Knowledge proposition claim.", request.AttemptId, before);
            }

            string cooldownKey = CooldownKey(speaker, target, method.Id, subject.subjectId);
            if (!request.Preview && IsOnCooldown(cooldownKey, request.WorldTime, method.CooldownSeconds))
            {
                return SocialInfluenceResult.Failure(SocialInfluenceStatus.CooldownActive, "Social influence method is on cooldown for this speaker, target, and subject.", request.AttemptId, before);
            }

            string attemptId = string.IsNullOrWhiteSpace(request.AttemptId) ? BuildStableId("social-influence-attempt", $"{tx}.{method.Id}.{speaker}.{target}") : Clean(request.AttemptId);
            int deterministicRoll = DeterministicRoll($"{attemptId}.{request.DeterministicSeed}.{Revision}");
            int evidenceScore = Math.Max(request.EvidenceStrength, request.EvidencePackage.Sum(item => Math.Max(0, item?.strength ?? 0)));
            int clarity = request.Arguments.Sum(item => Math.Max(0, item?.clarity ?? 0));
            int influence = method.BaseInfluence + request.SpeakerResolve + ((evidenceScore * method.EvidenceWeight) / 100) + ((request.RelationshipModifier * method.RelationshipWeight) / 100) + ((request.ReputationModifier * method.ReputationWeight) / 100) + clarity + deterministicRoll;
            int resistance = method.BaseResistance + request.TargetResistance + Math.Max(0, request.Difficulty);
            if (deception) resistance += method.DeceptionDetectionBase / 2;
            int margin = influence - resistance;
            SocialInfluenceMarginClass marginClass = margin >= 250 ? SocialInfluenceMarginClass.Critical : margin >= 0 ? SocialInfluenceMarginClass.Success : margin >= -150 ? SocialInfluenceMarginClass.Partial : SocialInfluenceMarginClass.Failure;
            SocialInfluenceBeliefOutcome belief = ResolveBeliefOutcome(request.Intent, marginClass);
            SocialInfluenceComplianceOutcome compliance = ResolveComplianceOutcome(request, method, marginClass);
            SocialInfluenceDetectionOutcome detection = ResolveDetectionOutcome(request, method, margin, deterministicRoll);
            SocialInfluenceHonestyClassification honesty = ResolveHonesty(request);

            SocialInfluenceAttemptRecordData attempt = new SocialInfluenceAttemptRecordData
            {
                attemptId = attemptId,
                transactionId = tx,
                methodDefinitionId = method.Id,
                speakerPersonId = speaker,
                targetPersonId = target,
                witnessPersonIdsCsv = string.Join(",", Clean(request.WitnessPersonIds)),
                intent = request.Intent,
                subject = subject,
                claim = request.Claim?.Clone(),
                truthStatus = request.TruthStatus,
                speakerBeliefState = request.SpeakerBeliefState,
                honesty = honesty,
                deceptionMode = request.DeceptionMode,
                beliefOutcome = belief,
                complianceOutcome = compliance,
                detectionOutcome = detection,
                marginClass = marginClass,
                visibility = request.Visibility,
                influenceScore = influence,
                resistanceScore = resistance,
                margin = margin,
                deterministicRoll = deterministicRoll,
                worldTime = request.WorldTime,
                deterministicSeed = request.DeterministicSeed ?? string.Empty,
                diagnostics = Diagnostics(request, marginClass, belief, compliance, detection, honesty),
                revision = 1L
            };

            KnowledgeOperationResult knowledge = null;
            if (method.CreatesBeliefEvidence && request.CommitBeliefEvidence && request.Claim != null && belief is SocialInfluenceBeliefOutcome.Accepted or SocialInfluenceBeliefOutcome.ConfidenceIncreased or SocialInfluenceBeliefOutcome.DoubtCreated or SocialInfluenceBeliefOutcome.ConfidenceDecreased)
            {
                knowledge = ApplyKnowledge(request, attempt, margin, preview: request.Preview);
                if (knowledge != null)
                {
                    attempt.knowledgeEvidenceId = knowledge.Evidence?.EvidenceId ?? string.Empty;
                    attempt.knowledgeBeliefId = knowledge.ResultingBelief?.BeliefId ?? string.Empty;
                }
            }

            SocialInteractionResult interaction = null;
            if (!request.Preview && interactions != null && compliance is SocialInfluenceComplianceOutcome.PromiseAccepted or SocialInfluenceComplianceOutcome.AcceptedRequest or SocialInfluenceComplianceOutcome.FearBasedCompliance)
            {
                interaction = interactions.Execute(BuildInteractionRequest(request, attempt));
                attempt.interactionRecordId = interaction?.Record?.InteractionRecordId ?? string.Empty;
            }

            SocialInfluenceDecisionModifierData modifier = null;
            if (method.AllowsDecisionModifier && request.CommitDecisionModifier && marginClass != SocialInfluenceMarginClass.Failure)
            {
                modifier = BuildDecisionModifier(request, method, attempt, margin);
                attempt.decisionModifierId = modifier.modifierId;
            }

            bool succeeded = knowledge == null || knowledge.Succeeded || knowledge.Code == KnowledgeResultCode.Preview || knowledge.Code == KnowledgeResultCode.Duplicate;
            if (!succeeded)
            {
                return new SocialInfluenceResult(false, SocialInfluenceStatus.BeliefRejected, knowledge.Message, attempt, modifier, knowledge, interaction, request.Preview, false, before, before, attempt.diagnostics);
            }

            if (!request.Preview)
            {
                attemptsById[attempt.attemptId] = attempt.Clone();
                processedTransactions[tx] = new SocialInfluenceProcessedTransactionData { transactionId = tx, attemptId = attempt.attemptId, status = SocialInfluenceStatus.Succeeded };
                cooldownsByKey[cooldownKey] = new SocialInfluenceCooldownData { cooldownKey = cooldownKey, lastWorldTime = request.WorldTime, sourceAttemptId = attempt.attemptId };
                if (modifier != null)
                {
                    decisionModifiersById[modifier.modifierId] = modifier.Clone();
                }

                if (detection is SocialInfluenceDetectionOutcome.Detected or SocialInfluenceDetectionOutcome.Proven)
                {
                    ApplyDetectionAttitude(request, attempt);
                }

                Revision++;
                sequence++;
                IsDirty = true;
            }

            return new SocialInfluenceResult(true, request.Preview ? SocialInfluenceStatus.Preview : SocialInfluenceStatus.Succeeded, request.Preview ? "Social influence previewed." : "Social influence executed.", attempt, modifier, knowledge, interaction, request.Preview, false, before, request.Preview ? before : Revision, attempt.diagnostics);
        }

        private KnowledgeOperationResult ApplyKnowledge(SocialInfluenceRequest request, SocialInfluenceAttemptRecordData attempt, int margin, bool preview)
        {
            if (!knowledgeByPerson.TryGetValue(attempt.targetPersonId, out PersonKnowledgeRuntime targetKnowledge) || targetKnowledge == null)
            {
                return null;
            }

            KnowledgeObservationRequest observation = new KnowledgeObservationRequest
            {
                PersonId = attempt.targetPersonId,
                TransactionId = $"social-influence.knowledge.{attempt.transactionId}",
                Proposition = request.Claim?.Clone(),
                AcquisitionSource = KnowledgeAcquisitionSource.Testimony,
                Provenance = KnowledgeProvenance.Testimony,
                Direction = request.Intent is SocialInfluenceIntent.CreateDoubt or SocialInfluenceIntent.DecreaseBeliefConfidence ? KnowledgeEvidenceDirection.Opposes : KnowledgeEvidenceDirection.Supports,
                Strength = Math.Max(50, Math.Min(1000, Math.Abs(margin) + 300)),
                Credibility = Math.Max(50, Math.Min(1000, request.SpeakerResolve + request.ReputationModifier)),
                GameTimeSeconds = request.WorldTime,
                SourceId = attempt.speakerPersonId,
                EvidenceId = $"evidence.{attempt.attemptId}",
                Visibility = request.Visibility switch
                {
                    SocialInfluenceVisibility.Public => KnowledgeVisibility.Public,
                    SocialInfluenceVisibility.Witnessed => KnowledgeVisibility.PersonallyObservable,
                    SocialInfluenceVisibility.DevelopmentOnly => KnowledgeVisibility.DevelopmentOnly,
                    _ => KnowledgeVisibility.Private
                },
                PrivateAccessAuthorized = true,
                MarkAsMisconception = attempt.honesty == SocialInfluenceHonestyClassification.DirectLie && request.TruthStatus == SocialInfluenceTruthStatus.False,
                Tags = new[] { "social-influence", request.Intent.ToString(), attempt.methodDefinitionId }
            };
            return preview ? targetKnowledge.PreviewObservation(observation) : targetKnowledge.RecordObservation(observation);
        }

        private void ApplyDetectionAttitude(SocialInfluenceRequest request, SocialInfluenceAttemptRecordData attempt)
        {
            if (attitudes == null) return;
            attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = $"social-influence.detected.{attempt.transactionId}",
                observerPersonId = attempt.targetPersonId,
                subjectPersonId = attempt.speakerPersonId,
                dimensionId = PrototypeAttitudeDefinitionFactory.TrustId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                value = -25,
                sourceId = attempt.attemptId,
                sourceCategory = AttitudeContributionSourceCategory.Scripted,
                worldTime = request.WorldTime
            });
            attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = $"social-influence.detected-hostility.{attempt.transactionId}",
                observerPersonId = attempt.targetPersonId,
                subjectPersonId = attempt.speakerPersonId,
                dimensionId = PrototypeAttitudeDefinitionFactory.HostilityId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                value = 15,
                sourceId = attempt.attemptId,
                sourceCategory = AttitudeContributionSourceCategory.Scripted,
                worldTime = request.WorldTime
            });
        }

        private SocialInteractionRequest BuildInteractionRequest(SocialInfluenceRequest request, SocialInfluenceAttemptRecordData attempt)
        {
            string interactionId = string.IsNullOrWhiteSpace(request.InteractionDefinitionId) ? PrototypeSocialInteractionDefinitionFactory.PromiseId : request.InteractionDefinitionId;
            return new SocialInteractionRequest
            {
                TransactionId = $"social-influence.interaction.{attempt.transactionId}",
                InteractionRecordId = $"social-interaction-record.{attempt.attemptId}",
                InteractionDefinitionId = interactionId,
                InitiatorPersonId = attempt.speakerPersonId,
                TargetPersonId = attempt.targetPersonId,
                WitnessPersonIds = Clean(request.WitnessPersonIds).ToArray(),
                Subject = attempt.subject.ToInteractionSubject(),
                Channel = SocialInteractionCommunicationChannel.Conversation,
                Response = SocialInteractionResponse.Accept,
                WorldTime = request.WorldTime,
                DeterministicSeed = request.DeterministicSeed ?? string.Empty,
                OriginatingReferenceId = attempt.attemptId
            };
        }

        private SocialInfluenceDecisionModifierData BuildDecisionModifier(SocialInfluenceRequest request, SocialInfluenceMethodDefinition method, SocialInfluenceAttemptRecordData attempt, int margin)
        {
            int capped = Math.Max(-method.MaximumDecisionModifier, Math.Min(method.MaximumDecisionModifier, margin / 5));
            if (request.Intent is SocialInfluenceIntent.DiscourageAction or SocialInfluenceIntent.CreateDoubt or SocialInfluenceIntent.ConcealTruth)
            {
                capped = -Math.Abs(capped);
            }
            else
            {
                capped = Math.Abs(capped);
            }

            return new SocialInfluenceDecisionModifierData
            {
                modifierId = $"social-influence-modifier.{attempt.attemptId}",
                sourceAttemptId = attempt.attemptId,
                actorPersonId = attempt.targetPersonId,
                targetPersonId = string.IsNullOrWhiteSpace(request.Subject?.ownerPersonId) ? attempt.speakerPersonId : request.Subject.ownerPersonId,
                intentionDefinitionId = Clean(request.IntentionDefinitionId),
                interactionDefinitionId = Clean(request.InteractionDefinitionId),
                subjectId = request.Subject?.subjectId ?? string.Empty,
                scoreDelta = capped,
                createdWorldTime = request.WorldTime,
                expirationWorldTime = method.ModifierDurationSeconds <= 0d ? -1d : request.WorldTime + method.ModifierDurationSeconds,
                active = true,
                revision = 1L
            };
        }

        private void RestoreInternal(SocialInfluenceRuntimeSaveData saveData)
        {
            attemptsById.Clear();
            processedTransactions.Clear();
            cooldownsByKey.Clear();
            decisionModifiersById.Clear();
            foreach (SocialInfluenceAttemptRecordData attempt in saveData?.attempts ?? new List<SocialInfluenceAttemptRecordData>()) attemptsById[attempt.attemptId] = attempt.Clone();
            foreach (SocialInfluenceProcessedTransactionData tx in saveData?.processedTransactions ?? new List<SocialInfluenceProcessedTransactionData>()) processedTransactions[tx.transactionId] = tx.Clone();
            foreach (SocialInfluenceCooldownData cooldown in saveData?.cooldowns ?? new List<SocialInfluenceCooldownData>()) cooldownsByKey[cooldown.cooldownKey] = cooldown.Clone();
            foreach (SocialInfluenceDecisionModifierData modifier in saveData?.decisionModifiers ?? new List<SocialInfluenceDecisionModifierData>()) decisionModifiersById[modifier.modifierId] = modifier.Clone();
            Revision = saveData?.revision ?? 0L;
            sequence = saveData?.attemptSequence ?? 0L;
            IsDirty = false;
        }

        private SocialInfluenceDecisionModifierData ResolveModifier(string modifierId) => !string.IsNullOrWhiteSpace(modifierId) && decisionModifiersById.TryGetValue(modifierId, out SocialInfluenceDecisionModifierData modifier) ? modifier.Clone() : null;
        private bool ValidateKnownPerson(string personId) => !string.IsNullOrWhiteSpace(personId) && knownPersonIds.Contains(Clean(personId));
        private bool IsOnCooldown(string key, double worldTime, double seconds) => cooldownsByKey.TryGetValue(key, out SocialInfluenceCooldownData cooldown) && seconds > 0d && worldTime < cooldown.lastWorldTime + seconds;
        private static bool KnownOrEmpty(HashSet<string> known, string personId) => string.IsNullOrWhiteSpace(personId) || known.Contains(Clean(personId));
        private static bool RequiresClaim(SocialInfluenceIntent intent) => intent is SocialInfluenceIntent.ChangeBelief or SocialInfluenceIntent.IncreaseBeliefConfidence or SocialInfluenceIntent.DecreaseBeliefConfidence or SocialInfluenceIntent.CreateDoubt or SocialInfluenceIntent.CorrectBelief or SocialInfluenceIntent.ConcealTruth or SocialInfluenceIntent.AvoidBlame;
        private static SocialInfluenceBeliefOutcome ResolveBeliefOutcome(SocialInfluenceIntent intent, SocialInfluenceMarginClass margin) => !RequiresClaim(intent) ? SocialInfluenceBeliefOutcome.None : margin == SocialInfluenceMarginClass.Failure ? SocialInfluenceBeliefOutcome.Rejected : intent == SocialInfluenceIntent.CreateDoubt ? SocialInfluenceBeliefOutcome.DoubtCreated : intent == SocialInfluenceIntent.DecreaseBeliefConfidence ? SocialInfluenceBeliefOutcome.ConfidenceDecreased : SocialInfluenceBeliefOutcome.Accepted;
        private static SocialInfluenceComplianceOutcome ResolveComplianceOutcome(SocialInfluenceRequest request, SocialInfluenceMethodDefinition method, SocialInfluenceMarginClass margin)
        {
            if (!method.AllowsCompliance || request.PlayerTargetRequiresExternalConsent && !request.PlayerConsentGranted) return SocialInfluenceComplianceOutcome.Refused;
            if (request.Intent == SocialInfluenceIntent.GainPromise && margin >= SocialInfluenceMarginClass.Success) return SocialInfluenceComplianceOutcome.PromiseAccepted;
            if (request.Intent == SocialInfluenceIntent.GainCompliance && margin >= SocialInfluenceMarginClass.Success) return SocialInfluenceComplianceOutcome.AcceptedRequest;
            if (request.Intent == SocialInfluenceIntent.Intimidate && margin >= SocialInfluenceMarginClass.Partial) return SocialInfluenceComplianceOutcome.FearBasedCompliance;
            return SocialInfluenceComplianceOutcome.None;
        }
        private static SocialInfluenceDetectionOutcome ResolveDetectionOutcome(SocialInfluenceRequest request, SocialInfluenceMethodDefinition method, int margin, int roll)
        {
            if (request.DeceptionMode == SocialInfluenceDeceptionMode.NoDeception) return SocialInfluenceDetectionOutcome.NotApplicable;
            int detection = method.DeceptionDetectionBase + request.TargetResistance - request.SpeakerResolve - roll - Math.Max(0, margin / 2);
            return detection >= 350 ? SocialInfluenceDetectionOutcome.Proven : detection >= 200 ? SocialInfluenceDetectionOutcome.Detected : detection >= 50 ? SocialInfluenceDetectionOutcome.SuspicionRaised : SocialInfluenceDetectionOutcome.NotDetected;
        }
        private static SocialInfluenceHonestyClassification ResolveHonesty(SocialInfluenceRequest request)
        {
            if (request.DeceptionMode == SocialInfluenceDeceptionMode.MisleadingOmission) return SocialInfluenceHonestyClassification.MisleadingOmission;
            if (request.DeceptionMode == SocialInfluenceDeceptionMode.TechnicallyTrueMisdirection) return SocialInfluenceHonestyClassification.Misdirection;
            if (request.DeceptionMode != SocialInfluenceDeceptionMode.NoDeception || request.TruthStatus == SocialInfluenceTruthStatus.False && request.SpeakerBeliefState == SocialInfluenceSpeakerBeliefState.BelievesFalse) return SocialInfluenceHonestyClassification.DirectLie;
            if (request.TruthStatus == SocialInfluenceTruthStatus.True) return SocialInfluenceHonestyClassification.HonestTrue;
            if (request.TruthStatus == SocialInfluenceTruthStatus.False && request.SpeakerBeliefState == SocialInfluenceSpeakerBeliefState.BelievesTrue) return SocialInfluenceHonestyClassification.HonestError;
            return SocialInfluenceHonestyClassification.Indeterminate;
        }
        private static string[] Diagnostics(SocialInfluenceRequest request, SocialInfluenceMarginClass margin, SocialInfluenceBeliefOutcome belief, SocialInfluenceComplianceOutcome compliance, SocialInfluenceDetectionOutcome detection, SocialInfluenceHonestyClassification honesty) => new[] { $"Margin={margin}", $"Belief={belief}", $"Compliance={compliance}", $"Detection={detection}", $"Honesty={honesty}", $"Intent={request.Intent}", $"Subject={request.Subject?.kind}:{request.Subject?.subjectId}" };
        private static SocialInfluenceRequest CloneRequest(SocialInfluenceRequest request) => request == null ? new SocialInfluenceRequest() : new SocialInfluenceRequest { TransactionId = request.TransactionId, AttemptId = request.AttemptId, MethodDefinitionId = request.MethodDefinitionId, SpeakerPersonId = request.SpeakerPersonId, TargetPersonId = request.TargetPersonId, WitnessPersonIds = Clean(request.WitnessPersonIds).ToArray(), Intent = request.Intent, Subject = request.Subject?.Clone(), Claim = request.Claim?.Clone(), EvidencePackage = (request.EvidencePackage ?? Array.Empty<SocialInfluenceEvidenceReferenceData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(), Arguments = (request.Arguments ?? Array.Empty<SocialInfluenceArgumentData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(), TruthStatus = request.TruthStatus, SpeakerBeliefState = request.SpeakerBeliefState, DeceptionMode = request.DeceptionMode, Visibility = request.Visibility, SpeakerResolve = request.SpeakerResolve, TargetResistance = request.TargetResistance, EvidenceStrength = request.EvidenceStrength, RelationshipModifier = request.RelationshipModifier, ReputationModifier = request.ReputationModifier, Difficulty = request.Difficulty, WorldTime = request.WorldTime, DeterministicSeed = request.DeterministicSeed, IntentionDefinitionId = request.IntentionDefinitionId, InteractionDefinitionId = request.InteractionDefinitionId, CommitBeliefEvidence = request.CommitBeliefEvidence, CommitDecisionModifier = request.CommitDecisionModifier, PlayerTargetRequiresExternalConsent = request.PlayerTargetRequiresExternalConsent, PlayerConsentGranted = request.PlayerConsentGranted, Preview = request.Preview };
        private static string CooldownKey(string speaker, string target, string methodId, string subjectId) => $"{Clean(speaker)}|{Clean(target)}|{Clean(methodId)}|{Clean(subjectId)}";
        private static IEnumerable<string> Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);
        private static string Clean(string value) => value?.Trim() ?? string.Empty;
        private static int DeterministicRoll(string source)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(source ?? string.Empty));
            int value = BitConverter.ToInt32(hash, 0) & int.MaxValue;
            return value % 101;
        }

        private static string BuildStableId(string prefix, string source)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(source ?? string.Empty));
            return $"{prefix}.{BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant()}";
        }
    }
}
