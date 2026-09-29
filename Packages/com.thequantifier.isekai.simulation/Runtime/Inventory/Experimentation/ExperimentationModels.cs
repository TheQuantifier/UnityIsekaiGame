using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Experimentation
{
    [Serializable]
    public sealed class ExperimentVariableDefinitionData
    {
        public string variableId;
        public ExperimentVariableCategory category = ExperimentVariableCategory.Unknown;
        public string targetSubjectId;
        public ExperimentValueType valueType = ExperimentValueType.None;
        public string unit;
        public float minimumValue;
        public float maximumValue;
        public string[] allowedValueIds = Array.Empty<string>();
        public string controlValue;
        public ExperimentVariableRole role = ExperimentVariableRole.Independent;
        public string requiredMeasurementMethodId;
        public string accessPolicyId;
        public bool visible = true;

        public ExperimentVariableDefinitionData Clone()
        {
            return new ExperimentVariableDefinitionData
            {
                variableId = variableId ?? string.Empty,
                category = category,
                targetSubjectId = targetSubjectId ?? string.Empty,
                valueType = valueType,
                unit = unit ?? string.Empty,
                minimumValue = minimumValue,
                maximumValue = maximumValue,
                allowedValueIds = NormalizeIds(allowedValueIds),
                controlValue = controlValue ?? string.Empty,
                role = role,
                requiredMeasurementMethodId = requiredMeasurementMethodId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                visible = visible
            };
        }

        public static string[] NormalizeIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static string[] NormalizeOrderedIds(IEnumerable<string> values)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Where(seen.Add)
                .ToArray();
        }
    }

    [Serializable]
    public sealed class ExperimentControlDefinitionData
    {
        public string controlId;
        public string baselineType;
        public string baselineReferenceId;
        public string[] heldVariableIds = Array.Empty<string>();
        public string comparisonPolicyId;

        public ExperimentControlDefinitionData Clone()
        {
            return new ExperimentControlDefinitionData
            {
                controlId = controlId ?? string.Empty,
                baselineType = baselineType ?? string.Empty,
                baselineReferenceId = baselineReferenceId ?? string.Empty,
                heldVariableIds = ExperimentVariableDefinitionData.NormalizeIds(heldVariableIds),
                comparisonPolicyId = comparisonPolicyId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ExperimentPolicyData
    {
        public int minimumTrials = 1;
        public int independentReproductionThreshold = 1;
        public int confirmationEvidenceThreshold = 2;
        public int minimumEvidenceStrength = 500;
        public bool allowDestructiveTesting;
        public bool allowAccidentalOutputs;
        public bool allowAuthoritativeRegistrationProposal;

        public ExperimentPolicyData Clone()
        {
            return new ExperimentPolicyData
            {
                minimumTrials = Math.Max(1, minimumTrials),
                independentReproductionThreshold = Math.Max(1, independentReproductionThreshold),
                confirmationEvidenceThreshold = Math.Max(1, confirmationEvidenceThreshold),
                minimumEvidenceStrength = Math.Max(0, minimumEvidenceStrength),
                allowDestructiveTesting = allowDestructiveTesting,
                allowAccidentalOutputs = allowAccidentalOutputs,
                allowAuthoritativeRegistrationProposal = allowAuthoritativeRegistrationProposal
            };
        }
    }

    [Serializable]
    public sealed class HypothesisClaimData
    {
        public HypothesisClaimType claimType = HypothesisClaimType.Unknown;
        public string subjectId;
        public string predicateId;
        public string objectId;
        public string proposedStableValueId;
        public string proposedQualitativeValue;
        public float minimumValue;
        public float maximumValue;
        public string unit;
        public string displayText;

        public HypothesisClaimData Clone()
        {
            return new HypothesisClaimData
            {
                claimType = claimType,
                subjectId = subjectId ?? string.Empty,
                predicateId = predicateId ?? string.Empty,
                objectId = objectId ?? string.Empty,
                proposedStableValueId = proposedStableValueId ?? string.Empty,
                proposedQualitativeValue = proposedQualitativeValue ?? string.Empty,
                minimumValue = minimumValue,
                maximumValue = maximumValue,
                unit = unit ?? string.Empty,
                displayText = displayText ?? string.Empty
            };
        }

        public string Signature => string.Join("|",
            claimType,
            subjectId ?? string.Empty,
            predicateId ?? string.Empty,
            objectId ?? string.Empty,
            proposedStableValueId ?? string.Empty,
            proposedQualitativeValue ?? string.Empty,
            minimumValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            maximumValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            unit ?? string.Empty);
    }

    [Serializable]
    public sealed class ExperimentHypothesisData
    {
        public string hypothesisId;
        public HypothesisClaimData claim = new HypothesisClaimData();
        public string targetRecipeId;
        public string targetMaterialId;
        public string targetToolId;
        public string targetStationId;
        public string targetItemInstanceId;
        public string authorPersonId;
        public string creationWorldTime;
        public string motivation;
        public string[] priorBeliefIds = Array.Empty<string>();
        public string[] supportingEvidenceIds = Array.Empty<string>();
        public string[] contradictingEvidenceIds = Array.Empty<string>();
        public int confidence;
        public HypothesisStatus status = HypothesisStatus.Proposed;
        public HypothesisTestabilityState testability = HypothesisTestabilityState.Unknown;
        public string falsificationCriteria;
        public string confirmationPolicyId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public ExperimentHypothesisData Clone()
        {
            return new ExperimentHypothesisData
            {
                hypothesisId = hypothesisId ?? string.Empty,
                claim = claim?.Clone() ?? new HypothesisClaimData(),
                targetRecipeId = targetRecipeId ?? string.Empty,
                targetMaterialId = targetMaterialId ?? string.Empty,
                targetToolId = targetToolId ?? string.Empty,
                targetStationId = targetStationId ?? string.Empty,
                targetItemInstanceId = targetItemInstanceId ?? string.Empty,
                authorPersonId = authorPersonId ?? string.Empty,
                creationWorldTime = creationWorldTime ?? string.Empty,
                motivation = motivation ?? string.Empty,
                priorBeliefIds = ExperimentVariableDefinitionData.NormalizeIds(priorBeliefIds),
                supportingEvidenceIds = ExperimentVariableDefinitionData.NormalizeIds(supportingEvidenceIds),
                contradictingEvidenceIds = ExperimentVariableDefinitionData.NormalizeIds(contradictingEvidenceIds),
                confidence = Math.Max(0, Math.Min(1000, confidence)),
                status = status,
                testability = testability,
                falsificationCriteria = falsificationCriteria ?? string.Empty,
                confirmationPolicyId = confirmationPolicyId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ExperimentVariableAssignmentData
    {
        public string assignmentId;
        public string variableId;
        public ExperimentValueType valueType = ExperimentValueType.None;
        public string stableValueId;
        public string qualitativeValue;
        public float numericValue;
        public float minimumValue;
        public float maximumValue;
        public bool booleanValue;
        public string unit;
        public bool isControlValue;
        public string accessPolicyId;

        public ExperimentVariableAssignmentData Clone()
        {
            return new ExperimentVariableAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                variableId = variableId ?? string.Empty,
                valueType = valueType,
                stableValueId = stableValueId ?? string.Empty,
                qualitativeValue = qualitativeValue ?? string.Empty,
                numericValue = numericValue,
                minimumValue = minimumValue,
                maximumValue = maximumValue,
                booleanValue = booleanValue,
                unit = unit ?? string.Empty,
                isControlValue = isControlValue,
                accessPolicyId = accessPolicyId ?? string.Empty
            };
        }

        public string Signature => string.Join("|",
            variableId ?? string.Empty,
            valueType,
            stableValueId ?? string.Empty,
            qualitativeValue ?? string.Empty,
            numericValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            minimumValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            maximumValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            booleanValue ? "true" : "false",
            unit ?? string.Empty,
            isControlValue ? "control" : "experimental");
    }

    [Serializable]
    public sealed class ExperimentControlAssignmentData
    {
        public string assignmentId;
        public string controlId;
        public string baselineReferenceId;
        public string[] heldVariableIds = Array.Empty<string>();
        public string controlTrialId;

        public ExperimentControlAssignmentData Clone()
        {
            return new ExperimentControlAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                controlId = controlId ?? string.Empty,
                baselineReferenceId = baselineReferenceId ?? string.Empty,
                heldVariableIds = ExperimentVariableDefinitionData.NormalizeIds(heldVariableIds),
                controlTrialId = controlTrialId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ExperimentPlanData
    {
        public string planId;
        public string experimentDefinitionId;
        public ExperimentPlanMode mode = ExperimentPlanMode.Controlled;
        public string[] hypothesisIds = Array.Empty<string>();
        public string[] targetSubjectIds = Array.Empty<string>();
        public int trialCount = 1;
        public List<ExperimentVariableAssignmentData> variables = new List<ExperimentVariableAssignmentData>();
        public List<ExperimentControlAssignmentData> controls = new List<ExperimentControlAssignmentData>();
        public string recipeDefinitionId;
        public string recipeVersionId;
        public string recipeVariantId;
        public string[] inputItemInstanceIds = Array.Empty<string>();
        public string[] toolItemInstanceIds = Array.Empty<string>();
        public string[] stationInstanceIds = Array.Empty<string>();
        public string[] requirementIds = Array.Empty<string>();
        public string[] expectedObservationIds = Array.Empty<string>();
        public string[] measurementMethodIds = Array.Empty<string>();
        public CraftingFailurePolicy failurePolicy = CraftingFailurePolicy.FullRollback;
        public string evidencePolicyId;
        public string reproducibilityPolicyId;
        public string validFromWorldTime;
        public string validUntilWorldTime;
        public long dependencyRevisionToken;
        public string correlationId;
        public string deterministicSeed;
        public bool previewOnly;
        public long revision = 1L;

        public ExperimentPlanData Clone()
        {
            return new ExperimentPlanData
            {
                planId = planId ?? string.Empty,
                experimentDefinitionId = experimentDefinitionId ?? string.Empty,
                mode = mode,
                hypothesisIds = ExperimentVariableDefinitionData.NormalizeIds(hypothesisIds),
                targetSubjectIds = ExperimentVariableDefinitionData.NormalizeIds(targetSubjectIds),
                trialCount = Math.Max(1, trialCount),
                variables = (variables ?? new List<ExperimentVariableAssignmentData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.assignmentId, StringComparer.Ordinal).ToList(),
                controls = (controls ?? new List<ExperimentControlAssignmentData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.assignmentId, StringComparer.Ordinal).ToList(),
                recipeDefinitionId = recipeDefinitionId ?? string.Empty,
                recipeVersionId = recipeVersionId ?? string.Empty,
                recipeVariantId = recipeVariantId ?? string.Empty,
                inputItemInstanceIds = ExperimentVariableDefinitionData.NormalizeIds(inputItemInstanceIds),
                toolItemInstanceIds = ExperimentVariableDefinitionData.NormalizeIds(toolItemInstanceIds),
                stationInstanceIds = ExperimentVariableDefinitionData.NormalizeIds(stationInstanceIds),
                requirementIds = ExperimentVariableDefinitionData.NormalizeIds(requirementIds),
                expectedObservationIds = ExperimentVariableDefinitionData.NormalizeIds(expectedObservationIds),
                measurementMethodIds = ExperimentVariableDefinitionData.NormalizeIds(measurementMethodIds),
                failurePolicy = failurePolicy,
                evidencePolicyId = evidencePolicyId ?? string.Empty,
                reproducibilityPolicyId = reproducibilityPolicyId ?? string.Empty,
                validFromWorldTime = validFromWorldTime ?? string.Empty,
                validUntilWorldTime = validUntilWorldTime ?? string.Empty,
                dependencyRevisionToken = Math.Max(0L, dependencyRevisionToken),
                correlationId = correlationId ?? string.Empty,
                deterministicSeed = deterministicSeed ?? string.Empty,
                previewOnly = previewOnly,
                revision = Math.Max(1L, revision)
            };
        }

        public string TrialSignature => string.Join("|",
            experimentDefinitionId ?? string.Empty,
            mode,
            recipeDefinitionId ?? string.Empty,
            recipeVersionId ?? string.Empty,
            recipeVariantId ?? string.Empty,
            string.Join(",", variables.Select(item => item?.Signature ?? string.Empty).OrderBy(value => value, StringComparer.Ordinal)),
            string.Join(",", inputItemInstanceIds ?? Array.Empty<string>()),
            string.Join(",", toolItemInstanceIds ?? Array.Empty<string>()),
            string.Join(",", stationInstanceIds ?? Array.Empty<string>()));
    }

    [Serializable]
    public sealed class ExperimentRunData
    {
        public string experimentRunId;
        public string experimentDefinitionId;
        public string planId;
        public string parentResearchProjectId;
        public string actingPersonId;
        public string actingOrganizationId;
        public string supervisorPersonId;
        public string reviewerPersonId;
        public string targetRecipeId;
        public string targetItemInstanceId;
        public string targetMaterialId;
        public string targetToolItemId;
        public string targetStationId;
        public string targetProductionJobId;
        public string targetBatchId;
        public string targetLotId;
        public string[] hypothesisIds = Array.Empty<string>();
        public string[] trialIds = Array.Empty<string>();
        public string[] controlReferences = Array.Empty<string>();
        public string[] inputItemIds = Array.Empty<string>();
        public string[] toolIds = Array.Empty<string>();
        public string[] stationIds = Array.Empty<string>();
        public string procedureReferenceId;
        public string startWorldTime;
        public string completionWorldTime;
        public ExperimentRunState state = ExperimentRunState.Draft;
        public ExperimentSafetyState safetyState = ExperimentSafetyState.Unknown;
        public string[] executionOperationIds = Array.Empty<string>();
        public string[] productionJobIds = Array.Empty<string>();
        public string[] outcomeIds = Array.Empty<string>();
        public string[] evidenceIds = Array.Empty<string>();
        public string[] observationIds = Array.Empty<string>();
        public string[] discoveryClaimIds = Array.Empty<string>();
        public string[] recordIds = Array.Empty<string>();
        public string provenance;
        public long revision = 1L;

        public ExperimentRunData Clone()
        {
            return new ExperimentRunData
            {
                experimentRunId = experimentRunId ?? string.Empty,
                experimentDefinitionId = experimentDefinitionId ?? string.Empty,
                planId = planId ?? string.Empty,
                parentResearchProjectId = parentResearchProjectId ?? string.Empty,
                actingPersonId = actingPersonId ?? string.Empty,
                actingOrganizationId = actingOrganizationId ?? string.Empty,
                supervisorPersonId = supervisorPersonId ?? string.Empty,
                reviewerPersonId = reviewerPersonId ?? string.Empty,
                targetRecipeId = targetRecipeId ?? string.Empty,
                targetItemInstanceId = targetItemInstanceId ?? string.Empty,
                targetMaterialId = targetMaterialId ?? string.Empty,
                targetToolItemId = targetToolItemId ?? string.Empty,
                targetStationId = targetStationId ?? string.Empty,
                targetProductionJobId = targetProductionJobId ?? string.Empty,
                targetBatchId = targetBatchId ?? string.Empty,
                targetLotId = targetLotId ?? string.Empty,
                hypothesisIds = ExperimentVariableDefinitionData.NormalizeIds(hypothesisIds),
                trialIds = ExperimentVariableDefinitionData.NormalizeOrderedIds(trialIds),
                controlReferences = ExperimentVariableDefinitionData.NormalizeIds(controlReferences),
                inputItemIds = ExperimentVariableDefinitionData.NormalizeIds(inputItemIds),
                toolIds = ExperimentVariableDefinitionData.NormalizeIds(toolIds),
                stationIds = ExperimentVariableDefinitionData.NormalizeIds(stationIds),
                procedureReferenceId = procedureReferenceId ?? string.Empty,
                startWorldTime = startWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                state = state,
                safetyState = safetyState,
                executionOperationIds = ExperimentVariableDefinitionData.NormalizeIds(executionOperationIds),
                productionJobIds = ExperimentVariableDefinitionData.NormalizeIds(productionJobIds),
                outcomeIds = ExperimentVariableDefinitionData.NormalizeIds(outcomeIds),
                evidenceIds = ExperimentVariableDefinitionData.NormalizeIds(evidenceIds),
                observationIds = ExperimentVariableDefinitionData.NormalizeIds(observationIds),
                discoveryClaimIds = ExperimentVariableDefinitionData.NormalizeIds(discoveryClaimIds),
                recordIds = ExperimentVariableDefinitionData.NormalizeIds(recordIds),
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ExperimentTrialData
    {
        public string trialId;
        public string experimentRunId;
        public int trialIndex;
        public ExperimentTrialKind trialKind = ExperimentTrialKind.Experimental;
        public List<ExperimentVariableAssignmentData> variables = new List<ExperimentVariableAssignmentData>();
        public string[] inputItemIds = Array.Empty<string>();
        public string[] toolIds = Array.Empty<string>();
        public string[] stationIds = Array.Empty<string>();
        public string[] workerIds = Array.Empty<string>();
        public string recipeDefinitionId;
        public string recipeVersionId;
        public string recipeVariantId;
        public string craftingOperationId;
        public string productionJobId;
        public string deterministicSeed;
        public string startWorldTime;
        public string completionWorldTime;
        public ExperimentTrialOutcome outcome = ExperimentTrialOutcome.Unknown;
        public string[] outputItemIds = Array.Empty<string>();
        public string failureCode;
        public string[] observationIds = Array.Empty<string>();
        public string[] evidenceIds = Array.Empty<string>();
        public string[] measurementIds = Array.Empty<string>();
        public string provenance;
        public long revision = 1L;

        public ExperimentTrialData Clone()
        {
            return new ExperimentTrialData
            {
                trialId = trialId ?? string.Empty,
                experimentRunId = experimentRunId ?? string.Empty,
                trialIndex = Math.Max(0, trialIndex),
                trialKind = trialKind,
                variables = (variables ?? new List<ExperimentVariableAssignmentData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.assignmentId, StringComparer.Ordinal).ToList(),
                inputItemIds = ExperimentVariableDefinitionData.NormalizeIds(inputItemIds),
                toolIds = ExperimentVariableDefinitionData.NormalizeIds(toolIds),
                stationIds = ExperimentVariableDefinitionData.NormalizeIds(stationIds),
                workerIds = ExperimentVariableDefinitionData.NormalizeIds(workerIds),
                recipeDefinitionId = recipeDefinitionId ?? string.Empty,
                recipeVersionId = recipeVersionId ?? string.Empty,
                recipeVariantId = recipeVariantId ?? string.Empty,
                craftingOperationId = craftingOperationId ?? string.Empty,
                productionJobId = productionJobId ?? string.Empty,
                deterministicSeed = deterministicSeed ?? string.Empty,
                startWorldTime = startWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                outcome = outcome,
                outputItemIds = ExperimentVariableDefinitionData.NormalizeIds(outputItemIds),
                failureCode = failureCode ?? string.Empty,
                observationIds = ExperimentVariableDefinitionData.NormalizeIds(observationIds),
                evidenceIds = ExperimentVariableDefinitionData.NormalizeIds(evidenceIds),
                measurementIds = ExperimentVariableDefinitionData.NormalizeIds(measurementIds),
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }

        public string ReproducibilitySignature => string.Join("|",
            recipeDefinitionId ?? string.Empty,
            recipeVersionId ?? string.Empty,
            recipeVariantId ?? string.Empty,
            trialKind,
            string.Join(",", variables.Select(item => item?.Signature ?? string.Empty).OrderBy(value => value, StringComparer.Ordinal)),
            string.Join(",", inputItemIds ?? Array.Empty<string>()),
            string.Join(",", toolIds ?? Array.Empty<string>()),
            string.Join(",", stationIds ?? Array.Empty<string>()));
    }

    [Serializable]
    public sealed class ExperimentMeasurementData
    {
        public string measurementId;
        public string experimentRunId;
        public string trialId;
        public ExperimentMeasurementKind kind = ExperimentMeasurementKind.Custom;
        public string subjectId;
        public string methodId;
        public ExperimentValueType valueType = ExperimentValueType.None;
        public float numericValue;
        public float minimumValue;
        public float maximumValue;
        public string stableValueId;
        public string qualitativeValue;
        public string unit;
        public int quality;
        public string observerPersonId;
        public string sourceId;
        public string worldTime;
        public string provenance;
        public long revision = 1L;

        public ExperimentMeasurementData Clone()
        {
            return new ExperimentMeasurementData
            {
                measurementId = measurementId ?? string.Empty,
                experimentRunId = experimentRunId ?? string.Empty,
                trialId = trialId ?? string.Empty,
                kind = kind,
                subjectId = subjectId ?? string.Empty,
                methodId = methodId ?? string.Empty,
                valueType = valueType,
                numericValue = numericValue,
                minimumValue = minimumValue,
                maximumValue = maximumValue,
                stableValueId = stableValueId ?? string.Empty,
                qualitativeValue = qualitativeValue ?? string.Empty,
                unit = unit ?? string.Empty,
                quality = Math.Max(0, Math.Min(1000, quality)),
                observerPersonId = observerPersonId ?? string.Empty,
                sourceId = sourceId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ExperimentInferenceData
    {
        public string inferenceId;
        public string experimentRunId;
        public ExperimentInferenceType inferenceType = ExperimentInferenceType.Custom;
        public string subjectId;
        public string inferredDefinitionId;
        public string[] evidenceIds = Array.Empty<string>();
        public string[] contradictingEvidenceIds = Array.Empty<string>();
        public float minimumValue;
        public float maximumValue;
        public string unit;
        public int confidence;
        public string message;
        public long revision = 1L;

        public ExperimentInferenceData Clone()
        {
            return new ExperimentInferenceData
            {
                inferenceId = inferenceId ?? string.Empty,
                experimentRunId = experimentRunId ?? string.Empty,
                inferenceType = inferenceType,
                subjectId = subjectId ?? string.Empty,
                inferredDefinitionId = inferredDefinitionId ?? string.Empty,
                evidenceIds = ExperimentVariableDefinitionData.NormalizeIds(evidenceIds),
                contradictingEvidenceIds = ExperimentVariableDefinitionData.NormalizeIds(contradictingEvidenceIds),
                minimumValue = minimumValue,
                maximumValue = maximumValue,
                unit = unit ?? string.Empty,
                confidence = Math.Max(0, Math.Min(1000, confidence)),
                message = message ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class DiscoveryClaimData
    {
        public string claimId;
        public string experimentRunId;
        public string inferenceId;
        public string hypothesisId;
        public DiscoveryClaimStatus status = DiscoveryClaimStatus.Draft;
        public string claimantPersonId;
        public string reviewerPersonId;
        public string[] evidenceIds = Array.Empty<string>();
        public int supportCount;
        public int contradictionCount;
        public int independentReproductionCount;
        public int confidence;
        public bool registrationProposed;
        public string accessPolicyId;
        public string worldTime;
        public string provenance;
        public long revision = 1L;

        public DiscoveryClaimData Clone()
        {
            return new DiscoveryClaimData
            {
                claimId = claimId ?? string.Empty,
                experimentRunId = experimentRunId ?? string.Empty,
                inferenceId = inferenceId ?? string.Empty,
                hypothesisId = hypothesisId ?? string.Empty,
                status = status,
                claimantPersonId = claimantPersonId ?? string.Empty,
                reviewerPersonId = reviewerPersonId ?? string.Empty,
                evidenceIds = ExperimentVariableDefinitionData.NormalizeIds(evidenceIds),
                supportCount = Math.Max(0, supportCount),
                contradictionCount = Math.Max(0, contradictionCount),
                independentReproductionCount = Math.Max(0, independentReproductionCount),
                confidence = Math.Max(0, Math.Min(1000, confidence)),
                registrationProposed = registrationProposed,
                accessPolicyId = accessPolicyId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class DiscoveryReviewData
    {
        public string reviewId;
        public string claimId;
        public string reviewerPersonId;
        public DiscoveryReviewDecision decision = DiscoveryReviewDecision.None;
        public string reason;
        public string worldTime;
        public long revision = 1L;

        public DiscoveryReviewData Clone()
        {
            return new DiscoveryReviewData
            {
                reviewId = reviewId ?? string.Empty,
                claimId = claimId ?? string.Empty,
                reviewerPersonId = reviewerPersonId ?? string.Empty,
                decision = decision,
                reason = reason ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class RecipeRegistrationProposalData
    {
        public string proposalId;
        public string claimId;
        public string proposedRecipeId;
        public string proposedVariantId;
        public string requesterPersonId;
        public string authorizationId;
        public bool authorized;
        public bool submitted;
        public string worldTime;
        public long revision = 1L;

        public RecipeRegistrationProposalData Clone()
        {
            return new RecipeRegistrationProposalData
            {
                proposalId = proposalId ?? string.Empty,
                claimId = claimId ?? string.Empty,
                proposedRecipeId = proposedRecipeId ?? string.Empty,
                proposedVariantId = proposedVariantId ?? string.Empty,
                requesterPersonId = requesterPersonId ?? string.Empty,
                authorizationId = authorizationId ?? string.Empty,
                authorized = authorized,
                submitted = submitted,
                worldTime = worldTime ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ExperimentLogRecordData
    {
        public string logId;
        public string experimentRunId;
        public string trialId;
        public string eventType;
        public string message;
        public string worldTime;
        public long sequence;

        public ExperimentLogRecordData Clone()
        {
            return new ExperimentLogRecordData
            {
                logId = logId ?? string.Empty,
                experimentRunId = experimentRunId ?? string.Empty,
                trialId = trialId ?? string.Empty,
                eventType = eventType ?? string.Empty,
                message = message ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                sequence = Math.Max(0L, sequence)
            };
        }
    }

    [Serializable]
    public sealed class ExperimentationRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long nextLogSequence;
        public List<ExperimentHypothesisData> hypotheses = new List<ExperimentHypothesisData>();
        public List<ExperimentPlanData> plans = new List<ExperimentPlanData>();
        public List<ExperimentRunData> runs = new List<ExperimentRunData>();
        public List<ExperimentTrialData> trials = new List<ExperimentTrialData>();
        public List<ExperimentMeasurementData> measurements = new List<ExperimentMeasurementData>();
        public List<ExperimentInferenceData> inferences = new List<ExperimentInferenceData>();
        public List<DiscoveryClaimData> claims = new List<DiscoveryClaimData>();
        public List<DiscoveryReviewData> reviews = new List<DiscoveryReviewData>();
        public List<RecipeRegistrationProposalData> registrationProposals = new List<RecipeRegistrationProposalData>();
        public List<ExperimentLogRecordData> logs = new List<ExperimentLogRecordData>();

        public ExperimentationRuntimeSaveData Clone()
        {
            return new ExperimentationRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = Math.Max(0L, revision),
                nextLogSequence = Math.Max(0L, nextLogSequence),
                hypotheses = (hypotheses ?? new List<ExperimentHypothesisData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                plans = (plans ?? new List<ExperimentPlanData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                runs = (runs ?? new List<ExperimentRunData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                trials = (trials ?? new List<ExperimentTrialData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                measurements = (measurements ?? new List<ExperimentMeasurementData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                inferences = (inferences ?? new List<ExperimentInferenceData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                claims = (claims ?? new List<DiscoveryClaimData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                reviews = (reviews ?? new List<DiscoveryReviewData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                registrationProposals = (registrationProposals ?? new List<RecipeRegistrationProposalData>()).Select(item => item?.Clone()).Where(item => item != null).ToList(),
                logs = (logs ?? new List<ExperimentLogRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class ExperimentationResult
    {
        private ExperimentationResult(bool succeeded, ExperimentOperationStatus status, string message, bool preview = false, bool duplicate = false)
        {
            Succeeded = succeeded;
            Status = duplicate ? ExperimentOperationStatus.Duplicate : preview ? ExperimentOperationStatus.Preview : status;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
        }

        public bool Succeeded { get; }
        public ExperimentOperationStatus Status { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public ExperimentHypothesisData Hypothesis { get; private set; }
        public ExperimentPlanData Plan { get; private set; }
        public ExperimentRunData Run { get; private set; }
        public ExperimentTrialData Trial { get; private set; }
        public ExperimentMeasurementData Measurement { get; private set; }
        public ExperimentInferenceData Inference { get; private set; }
        public DiscoveryClaimData Claim { get; private set; }
        public DiscoveryReviewData Review { get; private set; }
        public RecipeRegistrationProposalData RegistrationProposal { get; private set; }
        public CraftingExecutionResult CraftingResult { get; private set; }

        public static ExperimentationResult Success(string message, bool preview = false, bool duplicate = false)
        {
            return new ExperimentationResult(true, ExperimentOperationStatus.Success, message, preview, duplicate);
        }

        public static ExperimentationResult Failure(ExperimentOperationStatus status, string message)
        {
            return new ExperimentationResult(false, status, message);
        }

        public ExperimentationResult WithHypothesis(ExperimentHypothesisData value) { Hypothesis = value?.Clone(); return this; }
        public ExperimentationResult WithPlan(ExperimentPlanData value) { Plan = value?.Clone(); return this; }
        public ExperimentationResult WithRun(ExperimentRunData value) { Run = value?.Clone(); return this; }
        public ExperimentationResult WithTrial(ExperimentTrialData value) { Trial = value?.Clone(); return this; }
        public ExperimentationResult WithMeasurement(ExperimentMeasurementData value) { Measurement = value?.Clone(); return this; }
        public ExperimentationResult WithInference(ExperimentInferenceData value) { Inference = value?.Clone(); return this; }
        public ExperimentationResult WithClaim(DiscoveryClaimData value) { Claim = value?.Clone(); return this; }
        public ExperimentationResult WithReview(DiscoveryReviewData value) { Review = value?.Clone(); return this; }
        public ExperimentationResult WithRegistrationProposal(RecipeRegistrationProposalData value) { RegistrationProposal = value?.Clone(); return this; }
        public ExperimentationResult WithCrafting(CraftingExecutionResult value) { CraftingResult = value; return this; }
    }

    public sealed class ExperimentProjectionData
    {
        public string SubjectId { get; set; } = string.Empty;
        public ExperimentProjectionDecision Decision { get; set; } = ExperimentProjectionDecision.FullAccess;
        public bool Redacted { get; set; }
        public ExperimentRunData Run { get; set; }
        public string[] VisibleTrialIds { get; set; } = Array.Empty<string>();
        public string[] VisibleHypothesisIds { get; set; } = Array.Empty<string>();
        public string[] VisibleEvidenceIds { get; set; } = Array.Empty<string>();
        public string[] HiddenDetails { get; set; } = Array.Empty<string>();
    }

    public static class ExperimentInformationSubject
    {
        public const string ExperimentTag = "subject-type:experiment";
        public const string TrialTag = "subject-type:experiment-trial";
        public const string HypothesisTag = "subject-type:hypothesis";
        public const string ClaimTag = "subject-type:discovery-claim";

        public static InformationSubjectReferenceData Experiment(string runId, string definitionId, IEnumerable<string> tags = null)
        {
            return Create(runId, definitionId, ExperimentTag, tags);
        }

        public static InformationSubjectReferenceData Trial(string trialId, string runId, IEnumerable<string> tags = null)
        {
            return Create(trialId, runId, TrialTag, tags);
        }

        public static InformationSubjectReferenceData Hypothesis(string hypothesisId, string subjectId, IEnumerable<string> tags = null)
        {
            return Create(hypothesisId, subjectId, HypothesisTag, tags);
        }

        public static InformationSubjectReferenceData Claim(string claimId, string runId, IEnumerable<string> tags = null)
        {
            return Create(claimId, runId, ClaimTag, tags);
        }

        private static InformationSubjectReferenceData Create(string subjectId, string parentSubjectId, string typeTag, IEnumerable<string> tags)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                tags = (tags ?? Array.Empty<string>())
                    .Concat(new[] { "domain.production", "domain.experiment", typeTag })
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(tag => tag, StringComparer.Ordinal)
                    .ToArray()
            };
        }
    }
}
