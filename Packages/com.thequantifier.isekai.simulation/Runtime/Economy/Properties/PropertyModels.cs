using System;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using static UnityIsekaiGame.Economy.Properties.PropertyModelHelpers;

namespace UnityIsekaiGame.Economy.Properties
{
    [Serializable]
    public sealed class PropertySubjectReferenceData
    {
        public PropertySubjectKind kind = PropertySubjectKind.Person;
        public string subjectId;

        public string StableKey => $"{kind}:{subjectId ?? string.Empty}";

        public PropertySubjectReferenceData Clone()
        {
            return new PropertySubjectReferenceData
            {
                kind = kind,
                subjectId = subjectId ?? string.Empty
            };
        }

        public static PropertySubjectReferenceData Person(string personId)
        {
            return new PropertySubjectReferenceData { kind = PropertySubjectKind.Person, subjectId = personId ?? string.Empty };
        }

        public static PropertySubjectReferenceData Business(string businessId)
        {
            return new PropertySubjectReferenceData { kind = PropertySubjectKind.Business, subjectId = businessId ?? string.Empty };
        }
    }

    [Serializable]
    public sealed class PropertyShareData
    {
        public long units;
        public long totalUnits = 10000L;

        public PropertyShareData Clone()
        {
            return new PropertyShareData
            {
                units = Math.Max(0L, units),
                totalUnits = Math.Max(1L, totalUnits)
            };
        }

        public static PropertyShareData Full(long totalUnits = 10000L)
        {
            return new PropertyShareData { units = Math.Max(1L, totalUnits), totalUnits = Math.Max(1L, totalUnits) };
        }
    }

    [Serializable]
    public sealed class PropertyInstanceData
    {
        public string propertyId;
        public string propertyDefinitionId;
        public string displayName;
        public string recognizedName;
        public string parentPropertyId;
        public string[] childPropertyIds = Array.Empty<string>();
        public string spatialReferenceId;
        public string sceneObjectReferenceId;
        public PropertyState state = PropertyState.Available;
        public PropertyUseCategory[] currentUses = Array.Empty<PropertyUseCategory>();
        public PropertyOwnershipModel ownershipModel = PropertyOwnershipModel.Sole;
        public string currentTitleId;
        public string[] possessionRecordIds = Array.Empty<string>();
        public string[] occupancyRecordIds = Array.Empty<string>();
        public string[] tenancyIds = Array.Empty<string>();
        public string[] accessRightIds = Array.Empty<string>();
        public string[] businessEstablishmentIds = Array.Empty<string>();
        public string conditionRecordId;
        public string accessPolicyId;
        public string provenanceId;
        public double creationWorldTime;
        public double retiredWorldTime = -1d;
        public long revision = 1L;

        public PropertyInstanceData Clone()
        {
            return new PropertyInstanceData
            {
                propertyId = propertyId ?? string.Empty,
                propertyDefinitionId = propertyDefinitionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                recognizedName = recognizedName ?? string.Empty,
                parentPropertyId = parentPropertyId ?? string.Empty,
                childPropertyIds = CloneIds(childPropertyIds),
                spatialReferenceId = spatialReferenceId ?? string.Empty,
                sceneObjectReferenceId = sceneObjectReferenceId ?? string.Empty,
                state = state,
                currentUses = CloneEnums(currentUses),
                ownershipModel = ownershipModel,
                currentTitleId = currentTitleId ?? string.Empty,
                possessionRecordIds = CloneIds(possessionRecordIds),
                occupancyRecordIds = CloneIds(occupancyRecordIds),
                tenancyIds = CloneIds(tenancyIds),
                accessRightIds = CloneIds(accessRightIds),
                businessEstablishmentIds = CloneIds(businessEstablishmentIds),
                conditionRecordId = conditionRecordId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                creationWorldTime = creationWorldTime,
                retiredWorldTime = retiredWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyOwnershipInterestData
    {
        public string ownershipInterestId;
        public string propertyId;
        public PropertySubjectReferenceData owner = new PropertySubjectReferenceData();
        public PropertyOwnershipModel ownershipModel = PropertyOwnershipModel.Sole;
        public PropertyShareData ownershipShare = PropertyShareData.Full();
        public PropertyShareData votingShare = PropertyShareData.Full();
        public PropertyShareData economicBenefitShare = PropertyShareData.Full();
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string acquisitionSourceId;
        public string transferReferenceId;
        public PropertyAccessCategory[] rights = Array.Empty<PropertyAccessCategory>();
        public string[] restrictionIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => effectiveStartWorldTime <= worldTime && (effectiveEndWorldTime < 0d || effectiveEndWorldTime > worldTime);

        public PropertyOwnershipInterestData Clone()
        {
            return new PropertyOwnershipInterestData
            {
                ownershipInterestId = ownershipInterestId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                owner = owner?.Clone() ?? new PropertySubjectReferenceData(),
                ownershipModel = ownershipModel,
                ownershipShare = ownershipShare?.Clone() ?? PropertyShareData.Full(),
                votingShare = votingShare?.Clone() ?? PropertyShareData.Full(),
                economicBenefitShare = economicBenefitShare?.Clone() ?? PropertyShareData.Full(),
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                acquisitionSourceId = acquisitionSourceId ?? string.Empty,
                transferReferenceId = transferReferenceId ?? string.Empty,
                rights = CloneEnums(rights),
                restrictionIds = CloneIds(restrictionIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyTitleRecordData
    {
        public string titleId;
        public string propertyId;
        public string[] activeOwnershipInterestIds = Array.Empty<string>();
        public string recognizingAuthorityId;
        public double effectiveWorldTime;
        public string priorTitleId;
        public string transferSourceId;
        public bool disputed;
        public string[] restrictionIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public PropertyTitleRecordData Clone()
        {
            return new PropertyTitleRecordData
            {
                titleId = titleId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                activeOwnershipInterestIds = CloneIds(activeOwnershipInterestIds),
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime,
                priorTitleId = priorTitleId ?? string.Empty,
                transferSourceId = transferSourceId ?? string.Empty,
                disputed = disputed,
                restrictionIds = CloneIds(restrictionIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyRecordData
    {
        public string recordId;
        public string propertyId;
        public PropertyRecordCategory category = PropertyRecordCategory.Deed;
        public PropertyTransferCategory transferCategory = PropertyTransferCategory.Unknown;
        public PropertySubjectReferenceData transferor = new PropertySubjectReferenceData();
        public PropertySubjectReferenceData transferee = new PropertySubjectReferenceData();
        public PropertyShareData share = PropertyShareData.Full();
        public string transactionOrTradeReferenceId;
        public double effectiveWorldTime;
        public string recognizingAuthorityId;
        public string issuerOrWitnessId;
        public string sourceRecordId;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public PropertyRecordData Clone()
        {
            return new PropertyRecordData
            {
                recordId = recordId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                category = category,
                transferCategory = transferCategory,
                transferor = transferor?.Clone() ?? new PropertySubjectReferenceData(),
                transferee = transferee?.Clone() ?? new PropertySubjectReferenceData(),
                share = share?.Clone() ?? PropertyShareData.Full(),
                transactionOrTradeReferenceId = transactionOrTradeReferenceId ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime,
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                issuerOrWitnessId = issuerOrWitnessId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyPossessionRecordData
    {
        public string possessionId;
        public string propertyId;
        public PropertySubjectReferenceData possessor = new PropertySubjectReferenceData();
        public PossessionCategory category = PossessionCategory.OwnerPossession;
        public string sourceRecordId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public bool exclusive;
        public string responsibleCustodianId;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime);

        public PropertyPossessionRecordData Clone()
        {
            return new PropertyPossessionRecordData
            {
                possessionId = possessionId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                possessor = possessor?.Clone() ?? new PropertySubjectReferenceData(),
                category = category,
                sourceRecordId = sourceRecordId ?? string.Empty,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                exclusive = exclusive,
                responsibleCustodianId = responsibleCustodianId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyOccupancyRecordData
    {
        public string occupancyId;
        public string propertyId;
        public PropertySubjectReferenceData occupant = new PropertySubjectReferenceData();
        public OccupancyCategory category = OccupancyCategory.Residence;
        public string sourceRecordId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public bool primary;
        public bool exclusive;
        public PropertyUseCategory permittedUse = PropertyUseCategory.Residential;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime);

        public PropertyOccupancyRecordData Clone()
        {
            return new PropertyOccupancyRecordData
            {
                occupancyId = occupancyId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                occupant = occupant?.Clone() ?? new PropertySubjectReferenceData(),
                category = category,
                sourceRecordId = sourceRecordId ?? string.Empty,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                primary = primary,
                exclusive = exclusive,
                permittedUse = permittedUse,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyUseAssignmentData
    {
        public string assignmentId;
        public string propertyId;
        public PropertyUseCategory useCategory = PropertyUseCategory.Residential;
        public PropertySubjectReferenceData responsibleSubject = new PropertySubjectReferenceData();
        public string establishmentId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string approvalAuthorityId;
        public string[] restrictionIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public PropertyUseAssignmentData Clone()
        {
            return new PropertyUseAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                useCategory = useCategory,
                responsibleSubject = responsibleSubject?.Clone() ?? new PropertySubjectReferenceData(),
                establishmentId = establishmentId ?? string.Empty,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                approvalAuthorityId = approvalAuthorityId ?? string.Empty,
                restrictionIds = CloneIds(restrictionIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyRentTermsData
    {
        public string currencyId;
        public long rentUnitsPerPeriod;
        public long depositUnits;
        public double periodLengthWorldTime = 30d;

        public PropertyRentTermsData Clone()
        {
            return new PropertyRentTermsData
            {
                currencyId = currencyId ?? string.Empty,
                rentUnitsPerPeriod = Math.Max(0L, rentUnitsPerPeriod),
                depositUnits = Math.Max(0L, depositUnits),
                periodLengthWorldTime = Math.Max(0.0001d, periodLengthWorldTime)
            };
        }
    }

    [Serializable]
    public sealed class PropertyTenancyAgreementData
    {
        public string tenancyId;
        public string propertyId;
        public PropertySubjectReferenceData landlord = new PropertySubjectReferenceData();
        public PropertySubjectReferenceData tenant = new PropertySubjectReferenceData();
        public string[] propertyOwnerInterestIds = Array.Empty<string>();
        public PropertyUseCategory permittedUse = PropertyUseCategory.Residential;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public PropertyRentTermsData rentTerms = new PropertyRentTermsData();
        public string landlordAccountId;
        public string tenantAccountId;
        public PropertyAccessCategory[] grantedAccessCategories = { PropertyAccessCategory.Enter, PropertyAccessCategory.Occupy };
        public string[] maintenanceResponsibilityIds = Array.Empty<string>();
        public TenancyState state = TenancyState.Proposed;
        public string contractReferenceId;
        public string approvalAuthorityId;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => state == TenancyState.Active && startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime);

        public PropertyTenancyAgreementData Clone()
        {
            return new PropertyTenancyAgreementData
            {
                tenancyId = tenancyId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                landlord = landlord?.Clone() ?? new PropertySubjectReferenceData(),
                tenant = tenant?.Clone() ?? new PropertySubjectReferenceData(),
                propertyOwnerInterestIds = CloneIds(propertyOwnerInterestIds),
                permittedUse = permittedUse,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                rentTerms = rentTerms?.Clone() ?? new PropertyRentTermsData(),
                landlordAccountId = landlordAccountId ?? string.Empty,
                tenantAccountId = tenantAccountId ?? string.Empty,
                grantedAccessCategories = CloneEnums(grantedAccessCategories),
                maintenanceResponsibilityIds = CloneIds(maintenanceResponsibilityIds),
                state = state,
                contractReferenceId = contractReferenceId ?? string.Empty,
                approvalAuthorityId = approvalAuthorityId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyAccessRightData
    {
        public string accessRightId;
        public string propertyId;
        public PropertySubjectReferenceData holder = new PropertySubjectReferenceData();
        public PropertySubjectReferenceData grantingAuthority = new PropertySubjectReferenceData();
        public PropertyAccessCategory category = PropertyAccessCategory.Enter;
        public PropertyAccessCategory[] permittedActions = Array.Empty<PropertyAccessCategory>();
        public double startWorldTime;
        public double endWorldTime = -1d;
        public bool revoked;
        public string purposeRestriction;
        public string childPropertyScopeId;
        public string sourceRecordId;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => !revoked && startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime);

        public PropertyAccessRightData Clone()
        {
            return new PropertyAccessRightData
            {
                accessRightId = accessRightId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                holder = holder?.Clone() ?? new PropertySubjectReferenceData(),
                grantingAuthority = grantingAuthority?.Clone() ?? new PropertySubjectReferenceData(),
                category = category,
                permittedActions = CloneEnums(permittedActions),
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                revoked = revoked,
                purposeRestriction = purposeRestriction ?? string.Empty,
                childPropertyScopeId = childPropertyScopeId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyTransferRequestData
    {
        public string transferId;
        public string propertyId;
        public PropertyTransferCategory transferCategory = PropertyTransferCategory.Sale;
        public PropertySubjectReferenceData fromOwner = new PropertySubjectReferenceData();
        public PropertySubjectReferenceData toOwner = new PropertySubjectReferenceData();
        public PropertyShareData share = PropertyShareData.Full();
        public string buyerAccountId;
        public string sellerAccountId;
        public string currencyId;
        public long considerationUnits;
        public string economicReferenceId;
        public bool continueTenancy = true;
        public bool continuePossession = true;
        public bool continueAccess = true;
        public double effectiveWorldTime;
        public string approvalAuthorityId;
        public string injectFailureStage;
        public string accessPolicyId;
        public string provenanceId;

        public PropertyTransferRequestData Clone()
        {
            return new PropertyTransferRequestData
            {
                transferId = transferId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                transferCategory = transferCategory,
                fromOwner = fromOwner?.Clone() ?? new PropertySubjectReferenceData(),
                toOwner = toOwner?.Clone() ?? new PropertySubjectReferenceData(),
                share = share?.Clone() ?? PropertyShareData.Full(),
                buyerAccountId = buyerAccountId ?? string.Empty,
                sellerAccountId = sellerAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                considerationUnits = Math.Max(0L, considerationUnits),
                economicReferenceId = economicReferenceId ?? string.Empty,
                continueTenancy = continueTenancy,
                continuePossession = continuePossession,
                continueAccess = continueAccess,
                effectiveWorldTime = effectiveWorldTime,
                approvalAuthorityId = approvalAuthorityId ?? string.Empty,
                injectFailureStage = injectFailureStage ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class PropertyTransferRecordData
    {
        public string transferId;
        public string propertyId;
        public PropertyTransferCategory transferCategory;
        public string priorTitleId;
        public string newTitleId;
        public string deedRecordId;
        public string paymentTransactionId;
        public double effectiveWorldTime;
        public long revision = 1L;

        public PropertyTransferRecordData Clone()
        {
            return new PropertyTransferRecordData
            {
                transferId = transferId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                transferCategory = transferCategory,
                priorTitleId = priorTitleId ?? string.Empty,
                newTitleId = newTitleId ?? string.Empty,
                deedRecordId = deedRecordId ?? string.Empty,
                paymentTransactionId = paymentTransactionId ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RentObligationData
    {
        public string rentObligationId;
        public string tenancyId;
        public string propertyId;
        public string currencyId;
        public long totalDueUnits;
        public long paidUnits;
        public double periodStartWorldTime;
        public double periodEndWorldTime;
        public double dueWorldTime;
        public RentObligationState state = RentObligationState.Open;
        public string[] paymentTransactionIds = Array.Empty<string>();
        public string accessPolicyId;
        public long revision = 1L;

        public long OutstandingUnits => Math.Max(0L, totalDueUnits - paidUnits);

        public RentObligationData Clone()
        {
            return new RentObligationData
            {
                rentObligationId = rentObligationId ?? string.Empty,
                tenancyId = tenancyId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                totalDueUnits = Math.Max(0L, totalDueUnits),
                paidUnits = Math.Max(0L, paidUnits),
                periodStartWorldTime = periodStartWorldTime,
                periodEndWorldTime = periodEndWorldTime,
                dueWorldTime = dueWorldTime,
                state = state,
                paymentTransactionIds = CloneIds(paymentTransactionIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RentReceiptData
    {
        public string rentReceiptId;
        public string rentObligationId;
        public string transactionId;
        public string currencyId;
        public long paidUnits;
        public double paidWorldTime;
        public long revision = 1L;

        public RentReceiptData Clone()
        {
            return new RentReceiptData
            {
                rentReceiptId = rentReceiptId ?? string.Empty,
                rentObligationId = rentObligationId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                paidUnits = Math.Max(0L, paidUnits),
                paidWorldTime = paidWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyConditionRecordData
    {
        public string conditionRecordId;
        public string propertyId;
        public PropertyConditionState condition = PropertyConditionState.Good;
        public int severity;
        public string sourceRecordId;
        public double recordedWorldTime;
        public string accessPolicyId;
        public long revision = 1L;

        public PropertyConditionRecordData Clone()
        {
            return new PropertyConditionRecordData
            {
                conditionRecordId = conditionRecordId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                condition = condition,
                severity = Math.Max(0, severity),
                sourceRecordId = sourceRecordId ?? string.Empty,
                recordedWorldTime = recordedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyInspectionRecordData
    {
        public string inspectionId;
        public string propertyId;
        public PropertySubjectReferenceData inspector = new PropertySubjectReferenceData();
        public string[] findingIds = Array.Empty<string>();
        public string conditionRecordId;
        public double inspectedWorldTime;
        public string accessPolicyId;
        public long revision = 1L;

        public PropertyInspectionRecordData Clone()
        {
            return new PropertyInspectionRecordData
            {
                inspectionId = inspectionId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                inspector = inspector?.Clone() ?? new PropertySubjectReferenceData(),
                findingIds = CloneIds(findingIds),
                conditionRecordId = conditionRecordId ?? string.Empty,
                inspectedWorldTime = inspectedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyMaintenanceObligationData
    {
        public string maintenanceObligationId;
        public string propertyId;
        public PropertySubjectReferenceData responsibleSubject = new PropertySubjectReferenceData();
        public PropertySubjectReferenceData authorizedWorker = new PropertySubjectReferenceData();
        public string[] requiredProfessionOrCredentialIds = Array.Empty<string>();
        public string[] requiredToolItemInstanceIds = Array.Empty<string>();
        public string[] requiredMaterialItemInstanceIds = Array.Empty<string>();
        public string businessExpenseReferenceId;
        public string workEvidenceReferenceId;
        public MaintenanceObligationState state = MaintenanceObligationState.Required;
        public double dueWorldTime;
        public double completedWorldTime = -1d;
        public string accessPolicyId;
        public long revision = 1L;

        public PropertyMaintenanceObligationData Clone()
        {
            return new PropertyMaintenanceObligationData
            {
                maintenanceObligationId = maintenanceObligationId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                responsibleSubject = responsibleSubject?.Clone() ?? new PropertySubjectReferenceData(),
                authorizedWorker = authorizedWorker?.Clone() ?? new PropertySubjectReferenceData(),
                requiredProfessionOrCredentialIds = CloneIds(requiredProfessionOrCredentialIds),
                requiredToolItemInstanceIds = CloneIds(requiredToolItemInstanceIds),
                requiredMaterialItemInstanceIds = CloneIds(requiredMaterialItemInstanceIds),
                businessExpenseReferenceId = businessExpenseReferenceId ?? string.Empty,
                workEvidenceReferenceId = workEvidenceReferenceId ?? string.Empty,
                state = state,
                dueWorldTime = dueWorldTime,
                completedWorldTime = completedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PropertyMaintenanceRecordData
    {
        public string maintenanceRecordId;
        public string maintenanceObligationId;
        public string propertyId;
        public PropertySubjectReferenceData worker = new PropertySubjectReferenceData();
        public string[] toolItemInstanceIds = Array.Empty<string>();
        public string[] materialItemInstanceIds = Array.Empty<string>();
        public string workEvidenceReferenceId;
        public string expenseReferenceId;
        public string resultingConditionRecordId;
        public double performedWorldTime;
        public long revision = 1L;

        public PropertyMaintenanceRecordData Clone()
        {
            return new PropertyMaintenanceRecordData
            {
                maintenanceRecordId = maintenanceRecordId ?? string.Empty,
                maintenanceObligationId = maintenanceObligationId ?? string.Empty,
                propertyId = propertyId ?? string.Empty,
                worker = worker?.Clone() ?? new PropertySubjectReferenceData(),
                toolItemInstanceIds = CloneIds(toolItemInstanceIds),
                materialItemInstanceIds = CloneIds(materialItemInstanceIds),
                workEvidenceReferenceId = workEvidenceReferenceId ?? string.Empty,
                expenseReferenceId = expenseReferenceId ?? string.Empty,
                resultingConditionRecordId = resultingConditionRecordId ?? string.Empty,
                performedWorldTime = performedWorldTime,
                revision = revision
            };
        }
    }

    public sealed class PropertyAccessEvaluationResult
    {
        public PropertyAccessEvaluationResult(PropertyAccessDecision decision, string message, string[] matchingGrants = null, string[] sourceRecords = null)
        {
            Decision = decision;
            Message = message ?? string.Empty;
            MatchingGrantIds = CloneIds(matchingGrants);
            SourceRecordIds = CloneIds(sourceRecords);
        }

        public bool Allowed => Decision == PropertyAccessDecision.Allowed;
        public PropertyAccessDecision Decision { get; }
        public string Message { get; }
        public string[] MatchingGrantIds { get; }
        public string[] SourceRecordIds { get; }
    }

    public sealed class PropertyProjection
    {
        public PropertyProjection(PropertyProjectionKind kind, PropertyAccessDecision decision, PropertyInstanceData property, string[] visibleDetails, string message)
        {
            Kind = kind;
            Decision = decision;
            Property = property?.Clone();
            VisibleDetails = CloneIds(visibleDetails);
            Message = message ?? string.Empty;
        }

        public PropertyProjectionKind Kind { get; }
        public PropertyAccessDecision Decision { get; }
        public bool Redacted => Decision == PropertyAccessDecision.Redacted;
        public PropertyInstanceData Property { get; }
        public string[] VisibleDetails { get; }
        public string Message { get; }
    }

    public sealed class PropertyOperationResult
    {
        private PropertyOperationResult(PropertyOperationCode code, string message, long beforeRevision, long afterRevision, bool preview = false, bool duplicate = false)
        {
            Code = code;
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Preview = preview;
            Duplicate = duplicate;
        }

        public PropertyOperationCode Code { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Code == PropertyOperationCode.Success || Code == PropertyOperationCode.Preview || Duplicate;
        public PropertyInstanceData Property { get; private set; }
        public PropertyOwnershipInterestData Ownership { get; private set; }
        public PropertyTitleRecordData Title { get; private set; }
        public PropertyTenancyAgreementData Tenancy { get; private set; }
        public PropertyTransferRecordData Transfer { get; private set; }
        public RentObligationData Rent { get; private set; }
        public PropertyMaintenanceObligationData Maintenance { get; private set; }

        public static PropertyOperationResult Success(string message, long before, long after, bool duplicate = false) => new PropertyOperationResult(PropertyOperationCode.Success, message, before, after, duplicate: duplicate);
        public static PropertyOperationResult PreviewResult(string message, long revision) => new PropertyOperationResult(PropertyOperationCode.Preview, message, revision, revision, preview: true);
        public static PropertyOperationResult Failure(PropertyOperationCode code, string message, long revision = 0L) => new PropertyOperationResult(code, message, revision, revision);

        public PropertyOperationResult With(
            PropertyInstanceData property = null,
            PropertyOwnershipInterestData ownership = null,
            PropertyTitleRecordData title = null,
            PropertyTenancyAgreementData tenancy = null,
            PropertyTransferRecordData transfer = null,
            RentObligationData rent = null,
            PropertyMaintenanceObligationData maintenance = null)
        {
            Property = property?.Clone();
            Ownership = ownership?.Clone();
            Title = title?.Clone();
            Tenancy = tenancy?.Clone();
            Transfer = transfer?.Clone();
            Rent = rent?.Clone();
            Maintenance = maintenance?.Clone();
            return this;
        }
    }

    [Serializable]
    public sealed class PropertyRuntimeSaveData
    {
        public int schemaVersion = PropertyRuntime.CurrentSaveSchemaVersion;
        public long revision;
        public string worldId;
        public PropertyInstanceData[] properties = Array.Empty<PropertyInstanceData>();
        public PropertyOwnershipInterestData[] ownershipInterests = Array.Empty<PropertyOwnershipInterestData>();
        public PropertyTitleRecordData[] titles = Array.Empty<PropertyTitleRecordData>();
        public PropertyRecordData[] records = Array.Empty<PropertyRecordData>();
        public PropertyPossessionRecordData[] possessions = Array.Empty<PropertyPossessionRecordData>();
        public PropertyOccupancyRecordData[] occupancies = Array.Empty<PropertyOccupancyRecordData>();
        public PropertyUseAssignmentData[] uses = Array.Empty<PropertyUseAssignmentData>();
        public PropertyTenancyAgreementData[] tenancies = Array.Empty<PropertyTenancyAgreementData>();
        public PropertyAccessRightData[] accessRights = Array.Empty<PropertyAccessRightData>();
        public PropertyTransferRecordData[] transfers = Array.Empty<PropertyTransferRecordData>();
        public RentObligationData[] rentObligations = Array.Empty<RentObligationData>();
        public RentReceiptData[] rentReceipts = Array.Empty<RentReceiptData>();
        public PropertyConditionRecordData[] conditions = Array.Empty<PropertyConditionRecordData>();
        public PropertyInspectionRecordData[] inspections = Array.Empty<PropertyInspectionRecordData>();
        public PropertyMaintenanceObligationData[] maintenanceObligations = Array.Empty<PropertyMaintenanceObligationData>();
        public PropertyMaintenanceRecordData[] maintenanceRecords = Array.Empty<PropertyMaintenanceRecordData>();

        public PropertyRuntimeSaveData Clone()
        {
            return new PropertyRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                worldId = worldId ?? string.Empty,
                properties = CloneArray(properties),
                ownershipInterests = CloneArray(ownershipInterests),
                titles = CloneArray(titles),
                records = CloneArray(records),
                possessions = CloneArray(possessions),
                occupancies = CloneArray(occupancies),
                uses = CloneArray(uses),
                tenancies = CloneArray(tenancies),
                accessRights = CloneArray(accessRights),
                transfers = CloneArray(transfers),
                rentObligations = CloneArray(rentObligations),
                rentReceipts = CloneArray(rentReceipts),
                conditions = CloneArray(conditions),
                inspections = CloneArray(inspections),
                maintenanceObligations = CloneArray(maintenanceObligations),
                maintenanceRecords = CloneArray(maintenanceRecords)
            };
        }

        private static T[] CloneArray<T>(T[] values)
        {
            if (values == null)
            {
                return Array.Empty<T>();
            }

            return values.Select(value => (T)value?.GetType().GetMethod("Clone")?.Invoke(value, Array.Empty<object>())).ToArray();
        }
    }

    public static class PropertyInformationSubject
    {
        public const string PropertySubjectTag = "tag.economy.property";

        public static InformationSubjectReferenceData Create(string propertyId, string ownerOrScopeId = "")
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = propertyId ?? string.Empty,
                parentSubjectId = ownerOrScopeId ?? string.Empty,
                ownerPersonId = ownerOrScopeId ?? string.Empty,
                controllingEntityId = ownerOrScopeId ?? string.Empty,
                tags = new[] { PropertySubjectTag }
            };
        }
    }

    internal static class PropertyModelHelpers
    {
        public static string[] CloneIds(string[] ids)
        {
            return (ids ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        public static T[] CloneEnums<T>(T[] values)
            where T : Enum
        {
            return (values ?? Array.Empty<T>())
                .Where(value => Convert.ToInt32(value) != 0)
                .Distinct()
                .OrderBy(value => Convert.ToInt32(value))
                .ToArray();
        }
    }
}
