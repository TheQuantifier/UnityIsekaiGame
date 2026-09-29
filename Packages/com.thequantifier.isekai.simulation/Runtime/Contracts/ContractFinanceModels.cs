using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Contracts
{
    public static class ContractFinanceModelHelpers
    {
        public static string[] CleanIds(IEnumerable<string> ids)
        {
            return (ids ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        public static T[] CloneArray<T>(IEnumerable<T> values, Func<T, T> clone)
            where T : class
        {
            return (values ?? Array.Empty<T>())
                .Select(value => value == null ? null : clone(value))
                .Where(value => value != null)
                .ToArray();
        }

        public static long ClampNonNegative(long value) => value < 0L ? 0L : value;
    }

    [Serializable]
    public sealed class ContractPartyReferenceData
    {
        public ContractPartyKind kind = ContractPartyKind.Person;
        public string subjectId;

        public string StableKey => $"{kind}:{subjectId ?? string.Empty}";

        public ContractPartyReferenceData Clone()
        {
            return new ContractPartyReferenceData
            {
                kind = kind,
                subjectId = subjectId ?? string.Empty
            };
        }

        public static ContractPartyReferenceData Person(string personId)
        {
            return new ContractPartyReferenceData { kind = ContractPartyKind.Person, subjectId = personId ?? string.Empty };
        }

        public static ContractPartyReferenceData Business(string businessId)
        {
            return new ContractPartyReferenceData { kind = ContractPartyKind.Business, subjectId = businessId ?? string.Empty };
        }
    }

    [Serializable]
    public sealed class ContractPartyData
    {
        public string partyId;
        public ContractPartyRole role = ContractPartyRole.Unknown;
        public ContractPartyReferenceData reference = new ContractPartyReferenceData();
        public string authorityReferenceId;
        public string accountId;
        public bool accepted;
        public double acceptedWorldTime;

        public ContractPartyData Clone()
        {
            return new ContractPartyData
            {
                partyId = partyId ?? string.Empty,
                role = role,
                reference = reference?.Clone() ?? new ContractPartyReferenceData(),
                authorityReferenceId = authorityReferenceId ?? string.Empty,
                accountId = accountId ?? string.Empty,
                accepted = accepted,
                acceptedWorldTime = acceptedWorldTime
            };
        }
    }

    [Serializable]
    public sealed class ContractRationalData
    {
        public long numerator;
        public long denominator = 1L;
        public ContractRoundingMode rounding = ContractRoundingMode.Floor;

        public ContractRationalData Clone()
        {
            return new ContractRationalData
            {
                numerator = numerator,
                denominator = denominator <= 0L ? 1L : denominator,
                rounding = rounding
            };
        }
    }

    [Serializable]
    public sealed class ContractTermData
    {
        public string termId;
        public ContractTermCategory category = ContractTermCategory.General;
        public string description;
        public string responsiblePartyId;
        public string beneficiaryPartyId;
        public string currencyId;
        public long amountUnits;
        public long quantity;
        public double dueWorldTime = -1d;
        public double intervalWorldTime = -1d;
        public int maxOccurrences = 1;
        public string externalReferenceId;
        public string[] evidenceRequirementIds = Array.Empty<string>();

        public ContractTermData Clone()
        {
            return new ContractTermData
            {
                termId = termId ?? string.Empty,
                category = category,
                description = description ?? string.Empty,
                responsiblePartyId = responsiblePartyId ?? string.Empty,
                beneficiaryPartyId = beneficiaryPartyId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                amountUnits = ContractFinanceModelHelpers.ClampNonNegative(amountUnits),
                quantity = ContractFinanceModelHelpers.ClampNonNegative(quantity),
                dueWorldTime = dueWorldTime,
                intervalWorldTime = intervalWorldTime,
                maxOccurrences = Math.Max(0, maxOccurrences),
                externalReferenceId = externalReferenceId ?? string.Empty,
                evidenceRequirementIds = ContractFinanceModelHelpers.CleanIds(evidenceRequirementIds)
            };
        }
    }

    [Serializable]
    public sealed class ContractProposalData
    {
        public string proposalId;
        public string definitionId;
        public EconomicContractCategory category = EconomicContractCategory.General;
        public ContractProposalState state = ContractProposalState.Draft;
        public ContractPartyData[] parties = Array.Empty<ContractPartyData>();
        public ContractTermData[] terms = Array.Empty<ContractTermData>();
        public string createdByPartyId;
        public double createdWorldTime;
        public double expiresWorldTime = -1d;
        public string activatedContractId;
        public string accessPolicyId;
        public long revision = 1L;

        public ContractProposalData Clone()
        {
            return new ContractProposalData
            {
                proposalId = proposalId ?? string.Empty,
                definitionId = definitionId ?? string.Empty,
                category = category,
                state = state,
                parties = ContractFinanceModelHelpers.CloneArray(parties, party => party.Clone()),
                terms = ContractFinanceModelHelpers.CloneArray(terms, term => term.Clone()),
                createdByPartyId = createdByPartyId ?? string.Empty,
                createdWorldTime = createdWorldTime,
                expiresWorldTime = expiresWorldTime,
                activatedContractId = activatedContractId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EconomyContractData
    {
        public string contractId;
        public string definitionId;
        public string proposalId;
        public EconomicContractCategory category = EconomicContractCategory.General;
        public EconomicContractState state = EconomicContractState.Active;
        public int version = 1;
        public ContractPartyData[] parties = Array.Empty<ContractPartyData>();
        public ContractTermData[] terms = Array.Empty<ContractTermData>();
        public string[] obligationIds = Array.Empty<string>();
        public string[] amendmentIds = Array.Empty<string>();
        public string accessPolicyId;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public long revision = 1L;

        public EconomyContractData Clone()
        {
            return new EconomyContractData
            {
                contractId = contractId ?? string.Empty,
                definitionId = definitionId ?? string.Empty,
                proposalId = proposalId ?? string.Empty,
                category = category,
                state = state,
                version = Math.Max(1, version),
                parties = ContractFinanceModelHelpers.CloneArray(parties, party => party.Clone()),
                terms = ContractFinanceModelHelpers.CloneArray(terms, term => term.Clone()),
                obligationIds = ContractFinanceModelHelpers.CleanIds(obligationIds),
                amendmentIds = ContractFinanceModelHelpers.CleanIds(amendmentIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = contractId ?? string.Empty,
                ownerPersonId = FirstPersonPartyId(),
                tags = new[] { "contract", category.ToString() }
            };
        }

        private string FirstPersonPartyId()
        {
            ContractPartyData party = parties?.FirstOrDefault(candidate => candidate?.reference?.kind == ContractPartyKind.Person);
            return party?.reference?.subjectId ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class ContractAmendmentData
    {
        public string amendmentId;
        public string contractId;
        public int fromVersion = 1;
        public int toVersion = 2;
        public ContractAmendmentState state = ContractAmendmentState.Accepted;
        public ContractTermData[] replacementTerms = Array.Empty<ContractTermData>();
        public string reason;
        public double proposedWorldTime;
        public double acceptedWorldTime;
        public long revision = 1L;

        public ContractAmendmentData Clone()
        {
            return new ContractAmendmentData
            {
                amendmentId = amendmentId ?? string.Empty,
                contractId = contractId ?? string.Empty,
                fromVersion = Math.Max(1, fromVersion),
                toVersion = Math.Max(1, toVersion),
                state = state,
                replacementTerms = ContractFinanceModelHelpers.CloneArray(replacementTerms, term => term.Clone()),
                reason = reason ?? string.Empty,
                proposedWorldTime = proposedWorldTime,
                acceptedWorldTime = acceptedWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ContractObligationData
    {
        public string obligationId;
        public string contractId;
        public string termId;
        public ContractObligationCategory category = ContractObligationCategory.MonetaryPayment;
        public ContractObligationState state = ContractObligationState.Pending;
        public string obligorPartyId;
        public string beneficiaryPartyId;
        public string fromAccountId;
        public string toAccountId;
        public string currencyId;
        public long amountDueUnits;
        public long amountSatisfiedUnits;
        public double dueWorldTime = -1d;
        public string[] evidenceIds = Array.Empty<string>();
        public string[] paymentAllocationIds = Array.Empty<string>();
        public long revision = 1L;

        public long OutstandingUnits => Math.Max(0L, amountDueUnits - amountSatisfiedUnits);

        public ContractObligationData Clone()
        {
            return new ContractObligationData
            {
                obligationId = obligationId ?? string.Empty,
                contractId = contractId ?? string.Empty,
                termId = termId ?? string.Empty,
                category = category,
                state = state,
                obligorPartyId = obligorPartyId ?? string.Empty,
                beneficiaryPartyId = beneficiaryPartyId ?? string.Empty,
                fromAccountId = fromAccountId ?? string.Empty,
                toAccountId = toAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                amountDueUnits = ContractFinanceModelHelpers.ClampNonNegative(amountDueUnits),
                amountSatisfiedUnits = ContractFinanceModelHelpers.ClampNonNegative(amountSatisfiedUnits),
                dueWorldTime = dueWorldTime,
                evidenceIds = ContractFinanceModelHelpers.CleanIds(evidenceIds),
                paymentAllocationIds = ContractFinanceModelHelpers.CleanIds(paymentAllocationIds),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ContractPerformanceEvidenceData
    {
        public string evidenceId;
        public string contractId;
        public string obligationId;
        public ContractPerformanceState state = ContractPerformanceState.Submitted;
        public string sourceId;
        public string recordedBy;
        public string description;
        public long quantitySatisfied;
        public double recordedWorldTime;
        public long revision = 1L;

        public ContractPerformanceEvidenceData Clone()
        {
            return new ContractPerformanceEvidenceData
            {
                evidenceId = evidenceId ?? string.Empty,
                contractId = contractId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                state = state,
                sourceId = sourceId ?? string.Empty,
                recordedBy = recordedBy ?? string.Empty,
                description = description ?? string.Empty,
                quantitySatisfied = ContractFinanceModelHelpers.ClampNonNegative(quantitySatisfied),
                recordedWorldTime = recordedWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ContractPaymentAllocationData
    {
        public string allocationId;
        public string obligationId;
        public string contractId;
        public string economyTransactionId;
        public string currencyId;
        public long units;
        public double worldTime;
        public long revision = 1L;

        public ContractPaymentAllocationData Clone()
        {
            return new ContractPaymentAllocationData
            {
                allocationId = allocationId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                contractId = contractId ?? string.Empty,
                economyTransactionId = economyTransactionId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = ContractFinanceModelHelpers.ClampNonNegative(units),
                worldTime = worldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class CreditAgreementData
    {
        public string creditAgreementId;
        public string contractId;
        public string lenderPartyId;
        public string borrowerPartyId;
        public string lenderAccountId;
        public string borrowerAccountId;
        public string currencyId;
        public long creditLimitUnits;
        public long principalDrawnUnits;
        public CreditAgreementState state = CreditAgreementState.Active;
        public ContractRationalData interestRatePerPeriod = new ContractRationalData();
        public double startWorldTime;
        public long revision = 1L;

        public long AvailableCreditUnits => Math.Max(0L, creditLimitUnits - principalDrawnUnits);

        public CreditAgreementData Clone()
        {
            return new CreditAgreementData
            {
                creditAgreementId = creditAgreementId ?? string.Empty,
                contractId = contractId ?? string.Empty,
                lenderPartyId = lenderPartyId ?? string.Empty,
                borrowerPartyId = borrowerPartyId ?? string.Empty,
                lenderAccountId = lenderAccountId ?? string.Empty,
                borrowerAccountId = borrowerAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                creditLimitUnits = ContractFinanceModelHelpers.ClampNonNegative(creditLimitUnits),
                principalDrawnUnits = ContractFinanceModelHelpers.ClampNonNegative(principalDrawnUnits),
                state = state,
                interestRatePerPeriod = interestRatePerPeriod?.Clone() ?? new ContractRationalData(),
                startWorldTime = startWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class LoanData
    {
        public string loanId;
        public string contractId;
        public string lenderPartyId;
        public string borrowerPartyId;
        public string lenderAccountId;
        public string borrowerAccountId;
        public string currencyId;
        public long principalUnits;
        public long outstandingPrincipalUnits;
        public long accruedInterestOutstandingUnits;
        public ContractRationalData interestRatePerPeriod = new ContractRationalData();
        public LoanState state = LoanState.Approved;
        public string[] installmentIds = Array.Empty<string>();
        public string[] collateralIds = Array.Empty<string>();
        public string[] obligationIds = Array.Empty<string>();
        public double disbursedWorldTime = -1d;
        public long revision = 1L;

        public LoanData Clone()
        {
            return new LoanData
            {
                loanId = loanId ?? string.Empty,
                contractId = contractId ?? string.Empty,
                lenderPartyId = lenderPartyId ?? string.Empty,
                borrowerPartyId = borrowerPartyId ?? string.Empty,
                lenderAccountId = lenderAccountId ?? string.Empty,
                borrowerAccountId = borrowerAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                principalUnits = ContractFinanceModelHelpers.ClampNonNegative(principalUnits),
                outstandingPrincipalUnits = ContractFinanceModelHelpers.ClampNonNegative(outstandingPrincipalUnits),
                accruedInterestOutstandingUnits = ContractFinanceModelHelpers.ClampNonNegative(accruedInterestOutstandingUnits),
                interestRatePerPeriod = interestRatePerPeriod?.Clone() ?? new ContractRationalData(),
                state = state,
                installmentIds = ContractFinanceModelHelpers.CleanIds(installmentIds),
                collateralIds = ContractFinanceModelHelpers.CleanIds(collateralIds),
                obligationIds = ContractFinanceModelHelpers.CleanIds(obligationIds),
                disbursedWorldTime = disbursedWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class LoanInstallmentData
    {
        public string installmentId;
        public string loanId;
        public int sequence;
        public string currencyId;
        public long principalDueUnits;
        public long interestDueUnits;
        public long paidUnits;
        public double dueWorldTime;
        public LoanInstallmentState state = LoanInstallmentState.Scheduled;
        public string obligationId;
        public long revision = 1L;

        public long TotalDueUnits => Math.Max(0L, principalDueUnits + interestDueUnits);

        public LoanInstallmentData Clone()
        {
            return new LoanInstallmentData
            {
                installmentId = installmentId ?? string.Empty,
                loanId = loanId ?? string.Empty,
                sequence = Math.Max(0, sequence),
                currencyId = currencyId ?? string.Empty,
                principalDueUnits = ContractFinanceModelHelpers.ClampNonNegative(principalDueUnits),
                interestDueUnits = ContractFinanceModelHelpers.ClampNonNegative(interestDueUnits),
                paidUnits = ContractFinanceModelHelpers.ClampNonNegative(paidUnits),
                dueWorldTime = dueWorldTime,
                state = state,
                obligationId = obligationId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class CollateralDesignationData
    {
        public string collateralId;
        public string contractId;
        public string loanId;
        public CollateralAssetKind assetKind = CollateralAssetKind.ItemInstance;
        public string assetId;
        public string providerPartyId;
        public string currencyId;
        public long estimatedValueUnits;
        public CollateralState state = CollateralState.Pledged;
        public long revision = 1L;

        public CollateralDesignationData Clone()
        {
            return new CollateralDesignationData
            {
                collateralId = collateralId ?? string.Empty,
                contractId = contractId ?? string.Empty,
                loanId = loanId ?? string.Empty,
                assetKind = assetKind,
                assetId = assetId ?? string.Empty,
                providerPartyId = providerPartyId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                estimatedValueUnits = ContractFinanceModelHelpers.ClampNonNegative(estimatedValueUnits),
                state = state,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ContractProcessedTransactionData
    {
        public string transactionId;
        public string operationKey;
        public ContractOperationCode code;
        public long revision;

        public ContractProcessedTransactionData Clone()
        {
            return new ContractProcessedTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operationKey = operationKey ?? string.Empty,
                code = code,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ContractRuntimeSaveData
    {
        public int schemaVersion = ContractEconomyRuntime.CurrentSaveSchemaVersion;
        public long revision;
        public string worldId;
        public ContractProposalData[] proposals = Array.Empty<ContractProposalData>();
        public EconomyContractData[] contracts = Array.Empty<EconomyContractData>();
        public ContractAmendmentData[] amendments = Array.Empty<ContractAmendmentData>();
        public ContractObligationData[] obligations = Array.Empty<ContractObligationData>();
        public ContractPerformanceEvidenceData[] performanceEvidence = Array.Empty<ContractPerformanceEvidenceData>();
        public ContractPaymentAllocationData[] paymentAllocations = Array.Empty<ContractPaymentAllocationData>();
        public CreditAgreementData[] creditAgreements = Array.Empty<CreditAgreementData>();
        public LoanData[] loans = Array.Empty<LoanData>();
        public LoanInstallmentData[] installments = Array.Empty<LoanInstallmentData>();
        public CollateralDesignationData[] collateral = Array.Empty<CollateralDesignationData>();
        public ContractProcessedTransactionData[] processedTransactions = Array.Empty<ContractProcessedTransactionData>();

        public ContractRuntimeSaveData Clone()
        {
            return new ContractRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                worldId = worldId ?? string.Empty,
                proposals = ContractFinanceModelHelpers.CloneArray(proposals, proposal => proposal.Clone()),
                contracts = ContractFinanceModelHelpers.CloneArray(contracts, contract => contract.Clone()),
                amendments = ContractFinanceModelHelpers.CloneArray(amendments, amendment => amendment.Clone()),
                obligations = ContractFinanceModelHelpers.CloneArray(obligations, obligation => obligation.Clone()),
                performanceEvidence = ContractFinanceModelHelpers.CloneArray(performanceEvidence, evidence => evidence.Clone()),
                paymentAllocations = ContractFinanceModelHelpers.CloneArray(paymentAllocations, allocation => allocation.Clone()),
                creditAgreements = ContractFinanceModelHelpers.CloneArray(creditAgreements, agreement => agreement.Clone()),
                loans = ContractFinanceModelHelpers.CloneArray(loans, loan => loan.Clone()),
                installments = ContractFinanceModelHelpers.CloneArray(installments, installment => installment.Clone()),
                collateral = ContractFinanceModelHelpers.CloneArray(collateral, item => item.Clone()),
                processedTransactions = ContractFinanceModelHelpers.CloneArray(processedTransactions, processed => processed.Clone())
            };
        }
    }

    public sealed class ContractEconomyOperationResult
    {
        private ContractEconomyOperationResult(bool succeeded, bool preview, bool duplicate, ContractOperationCode code, string message, long before, long after)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public ContractOperationCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public ContractProposalData Proposal { get; private set; }
        public EconomyContractData Contract { get; private set; }
        public ContractObligationData Obligation { get; private set; }
        public LoanData Loan { get; private set; }
        public LoanInstallmentData Installment { get; private set; }
        public CollateralDesignationData Collateral { get; private set; }
        public ContractPaymentAllocationData PaymentAllocation { get; private set; }
        public ContractPerformanceEvidenceData Evidence { get; private set; }
        public EconomyTransactionSnapshot EconomyTransaction { get; private set; }

        public static ContractEconomyOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return new ContractEconomyOperationResult(true, preview, duplicate, preview ? ContractOperationCode.Preview : duplicate ? ContractOperationCode.Duplicate : ContractOperationCode.Succeeded, message, before, after);
        }

        public static ContractEconomyOperationResult Failure(ContractOperationCode code, string message, long revision, bool preview = false)
        {
            return new ContractEconomyOperationResult(false, preview, false, code, message, revision, revision);
        }

        public ContractEconomyOperationResult With(
            ContractProposalData proposal = null,
            EconomyContractData contract = null,
            ContractObligationData obligation = null,
            LoanData loan = null,
            LoanInstallmentData installment = null,
            CollateralDesignationData collateral = null,
            ContractPaymentAllocationData paymentAllocation = null,
            ContractPerformanceEvidenceData evidence = null,
            EconomyTransactionSnapshot economyTransaction = null)
        {
            Proposal = proposal?.Clone();
            Contract = contract?.Clone();
            Obligation = obligation?.Clone();
            Loan = loan?.Clone();
            Installment = installment?.Clone();
            Collateral = collateral?.Clone();
            PaymentAllocation = paymentAllocation?.Clone();
            Evidence = evidence?.Clone();
            EconomyTransaction = economyTransaction;
            return this;
        }
    }
}
