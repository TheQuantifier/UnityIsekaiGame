using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Contracts
{
    public sealed class ContractEconomyRuntime
    {
        public const int CurrentSaveSchemaVersion = 1;

        private readonly Dictionary<string, ContractProposalData> proposalsById = new Dictionary<string, ContractProposalData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomyContractData> contractsById = new Dictionary<string, EconomyContractData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContractAmendmentData> amendmentsById = new Dictionary<string, ContractAmendmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContractObligationData> obligationsById = new Dictionary<string, ContractObligationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContractPerformanceEvidenceData> evidenceById = new Dictionary<string, ContractPerformanceEvidenceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContractPaymentAllocationData> allocationsById = new Dictionary<string, ContractPaymentAllocationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CreditAgreementData> creditAgreementsById = new Dictionary<string, CreditAgreementData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LoanData> loansById = new Dictionary<string, LoanData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LoanInstallmentData> installmentsById = new Dictionary<string, LoanInstallmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CollateralDesignationData> collateralById = new Dictionary<string, CollateralDesignationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContractProcessedTransactionData> processedByTransactionId = new Dictionary<string, ContractProcessedTransactionData>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private string worldId;

        public long Revision { get; private set; }
        public int ContractCount => contractsById.Count;
        public int ObligationCount => obligationsById.Count;
        public int LoanCount => loansById.Count;

        public void Configure(DefinitionRegistry definitionRegistry, string world)
        {
            registry = definitionRegistry ?? registry;
            worldId = world ?? string.Empty;
        }

        public IReadOnlyList<ContractProposalData> Proposals => proposalsById.Values.OrderBy(item => item.proposalId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<EconomyContractData> Contracts => contractsById.Values.OrderBy(item => item.contractId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ContractAmendmentData> Amendments => amendmentsById.Values.OrderBy(item => item.amendmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ContractObligationData> Obligations => obligationsById.Values.OrderBy(item => item.dueWorldTime).ThenBy(item => item.obligationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ContractPerformanceEvidenceData> PerformanceEvidence => evidenceById.Values.OrderBy(item => item.recordedWorldTime).ThenBy(item => item.evidenceId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ContractPaymentAllocationData> PaymentAllocations => allocationsById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.allocationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CreditAgreementData> CreditAgreements => creditAgreementsById.Values.OrderBy(item => item.creditAgreementId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<LoanData> Loans => loansById.Values.OrderBy(item => item.loanId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<LoanInstallmentData> Installments => installmentsById.Values.OrderBy(item => item.loanId, StringComparer.Ordinal).ThenBy(item => item.sequence).ThenBy(item => item.installmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CollateralDesignationData> Collateral => collateralById.Values.OrderBy(item => item.collateralId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public bool TryGetContract(string contractId, out EconomyContractData contract)
        {
            contract = null;
            if (string.IsNullOrWhiteSpace(contractId) || !contractsById.TryGetValue(contractId, out EconomyContractData found))
            {
                return false;
            }

            contract = found.Clone();
            return true;
        }

        public bool TryGetObligation(string obligationId, out ContractObligationData obligation)
        {
            obligation = null;
            if (string.IsNullOrWhiteSpace(obligationId) || !obligationsById.TryGetValue(obligationId, out ContractObligationData found))
            {
                return false;
            }

            obligation = found.Clone();
            return true;
        }

        public bool TryGetLoan(string loanId, out LoanData loan)
        {
            loan = null;
            if (string.IsNullOrWhiteSpace(loanId) || !loansById.TryGetValue(loanId, out LoanData found))
            {
                return false;
            }

            loan = found.Clone();
            return true;
        }

        public ContractEconomyOperationResult CreateProposal(ContractProposalData proposal, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!ValidateProposal(proposal, registry, requireDefinition: !string.IsNullOrWhiteSpace(proposal?.definitionId), out string failure))
            {
                ContractOperationCode code = failure.Contains("definition", StringComparison.OrdinalIgnoreCase) ? ContractOperationCode.MissingDefinition : ContractOperationCode.InvalidRequest;
                return Fail(code, failure, preview);
            }

            ContractProposalData clean = proposal.Clone();
            clean.state = clean.state == ContractProposalState.Draft ? ContractProposalState.Offered : clean.state;
            string key = $"proposal:{clean.proposalId}";
            if (!preview && IsDuplicate(transactionId, key, out ContractEconomyOperationResult duplicate))
            {
                return duplicate.With(proposal: clean);
            }

            if (proposalsById.ContainsKey(clean.proposalId))
            {
                return Fail(ContractOperationCode.InvalidRequest, $"Contract proposal '{clean.proposalId}' already exists.", preview);
            }

            if (preview)
            {
                return ContractEconomyOperationResult.Success("Contract proposal preview succeeded.", before, before, preview: true).With(proposal: clean);
            }

            proposalsById.Add(clean.proposalId, clean);
            Revision++;
            Remember(transactionId, key, ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Contract proposal created.", before, Revision).With(proposal: clean);
        }

        public ContractEconomyOperationResult AcceptProposal(string proposalId, string partyId, double worldTime = 0d, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!proposalsById.TryGetValue(proposalId ?? string.Empty, out ContractProposalData proposal))
            {
                return Fail(ContractOperationCode.MissingProposal, $"Contract proposal '{proposalId}' is missing.", preview);
            }

            if (proposal.state != ContractProposalState.Offered && proposal.state != ContractProposalState.Draft)
            {
                return Fail(ContractOperationCode.InvalidState, $"Contract proposal '{proposalId}' cannot be accepted from state {proposal.state}.", preview);
            }

            ContractProposalData updated = proposal.Clone();
            bool matched = false;
            for (int i = 0; i < updated.parties.Length; i++)
            {
                if (string.Equals(updated.parties[i].partyId, partyId, StringComparison.Ordinal))
                {
                    updated.parties[i].accepted = true;
                    updated.parties[i].acceptedWorldTime = worldTime;
                    matched = true;
                }
            }

            if (!matched)
            {
                return Fail(ContractOperationCode.InvalidRequest, $"Party '{partyId}' is not part of proposal '{proposalId}'.", preview);
            }

            if (updated.parties.Where(party => party.role != ContractPartyRole.Witness && party.role != ContractPartyRole.Administrator).All(party => party.accepted))
            {
                updated.state = ContractProposalState.Accepted;
            }
            else
            {
                updated.state = ContractProposalState.Offered;
            }

            if (preview)
            {
                return ContractEconomyOperationResult.Success("Proposal acceptance preview succeeded.", before, before, preview: true).With(proposal: updated);
            }

            updated.revision++;
            proposalsById[proposalId] = updated;
            Revision++;
            Remember(transactionId, $"accept:{proposalId}:{partyId}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Proposal accepted.", before, Revision).With(proposal: updated);
        }

        public ContractEconomyOperationResult ActivateProposal(string proposalId, string contractId, double worldTime = 0d, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!proposalsById.TryGetValue(proposalId ?? string.Empty, out ContractProposalData proposal))
            {
                return Fail(ContractOperationCode.MissingProposal, $"Contract proposal '{proposalId}' is missing.", preview);
            }

            if (proposal.state != ContractProposalState.Accepted)
            {
                return Fail(ContractOperationCode.InvalidState, $"Contract proposal '{proposalId}' must be accepted before activation.", preview);
            }

            string resolvedContractId = string.IsNullOrWhiteSpace(contractId) ? $"contract.{proposal.proposalId}" : contractId.Trim();
            if (contractsById.ContainsKey(resolvedContractId))
            {
                return Fail(ContractOperationCode.InvalidRequest, $"Contract '{resolvedContractId}' already exists.", preview);
            }

            EconomyContractData contract = new EconomyContractData
            {
                contractId = resolvedContractId,
                definitionId = proposal.definitionId,
                proposalId = proposal.proposalId,
                category = proposal.category,
                state = EconomicContractState.Active,
                parties = ContractFinanceModelHelpers.CloneArray(proposal.parties, party => party.Clone()),
                terms = ContractFinanceModelHelpers.CloneArray(proposal.terms, term => term.Clone()),
                accessPolicyId = proposal.accessPolicyId,
                effectiveStartWorldTime = worldTime,
                revision = 1L
            };

            List<ContractObligationData> generatedObligations = BuildInitialObligations(contract);
            contract.obligationIds = generatedObligations.Select(obligation => obligation.obligationId).ToArray();

            if (preview)
            {
                return ContractEconomyOperationResult.Success("Contract activation preview succeeded.", before, before, preview: true).With(contract: contract, obligation: generatedObligations.FirstOrDefault());
            }

            ContractProposalData updatedProposal = proposal.Clone();
            updatedProposal.state = ContractProposalState.Activated;
            updatedProposal.activatedContractId = contract.contractId;
            updatedProposal.revision++;
            proposalsById[proposal.proposalId] = updatedProposal;
            contractsById.Add(contract.contractId, contract);
            foreach (ContractObligationData obligation in generatedObligations)
            {
                obligationsById.Add(obligation.obligationId, obligation);
            }

            Revision++;
            Remember(transactionId, $"activate:{proposalId}:{contract.contractId}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Contract activated.", before, Revision).With(contract: contract, obligation: generatedObligations.FirstOrDefault());
        }

        public ContractEconomyOperationResult AmendContract(ContractAmendmentData amendment, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (amendment == null || string.IsNullOrWhiteSpace(amendment.amendmentId) || string.IsNullOrWhiteSpace(amendment.contractId))
            {
                return Fail(ContractOperationCode.InvalidRequest, "Amendment ID and contract ID are required.", preview);
            }

            if (!contractsById.TryGetValue(amendment.contractId, out EconomyContractData contract))
            {
                return Fail(ContractOperationCode.MissingContract, $"Contract '{amendment.contractId}' is missing.", preview);
            }

            if (amendmentsById.ContainsKey(amendment.amendmentId))
            {
                return Fail(ContractOperationCode.InvalidRequest, $"Amendment '{amendment.amendmentId}' already exists.", preview);
            }

            ContractAmendmentData clean = amendment.Clone();
            clean.fromVersion = contract.version;
            clean.toVersion = contract.version + 1;
            clean.state = ContractAmendmentState.Accepted;
            EconomyContractData updated = contract.Clone();
            updated.version = clean.toVersion;
            updated.terms = clean.replacementTerms.Length == 0 ? updated.terms : ContractFinanceModelHelpers.CloneArray(clean.replacementTerms, term => term.Clone());
            updated.amendmentIds = updated.amendmentIds.Concat(new[] { clean.amendmentId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            updated.revision++;

            if (preview)
            {
                return ContractEconomyOperationResult.Success("Contract amendment preview succeeded.", before, before, preview: true).With(contract: updated);
            }

            amendmentsById.Add(clean.amendmentId, clean);
            contractsById[updated.contractId] = updated;
            Revision++;
            Remember(transactionId, $"amend:{clean.amendmentId}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Contract amended.", before, Revision).With(contract: updated);
        }

        public ContractEconomyOperationResult RecordPerformanceEvidence(ContractPerformanceEvidenceData evidence, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (evidence == null || string.IsNullOrWhiteSpace(evidence.evidenceId) || string.IsNullOrWhiteSpace(evidence.obligationId))
            {
                return Fail(ContractOperationCode.InvalidRequest, "Performance evidence ID and obligation ID are required.", preview);
            }

            if (!obligationsById.TryGetValue(evidence.obligationId, out ContractObligationData obligation))
            {
                return Fail(ContractOperationCode.MissingObligation, $"Obligation '{evidence.obligationId}' is missing.", preview);
            }

            if (evidenceById.ContainsKey(evidence.evidenceId))
            {
                return Fail(ContractOperationCode.InvalidRequest, $"Performance evidence '{evidence.evidenceId}' already exists.", preview);
            }

            ContractPerformanceEvidenceData clean = evidence.Clone();
            clean.contractId = string.IsNullOrWhiteSpace(clean.contractId) ? obligation.contractId : clean.contractId;
            ContractObligationData updated = obligation.Clone();
            updated.evidenceIds = updated.evidenceIds.Concat(new[] { clean.evidenceId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (clean.state == ContractPerformanceState.Accepted && updated.amountDueUnits == 0L)
            {
                updated.state = ContractObligationState.Satisfied;
            }

            if (preview)
            {
                return ContractEconomyOperationResult.Success("Performance evidence preview succeeded.", before, before, preview: true).With(evidence: clean, obligation: updated);
            }

            evidenceById.Add(clean.evidenceId, clean);
            obligationsById[updated.obligationId] = updated;
            Revision++;
            Remember(transactionId, $"evidence:{clean.evidenceId}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Performance evidence recorded.", before, Revision).With(evidence: clean, obligation: updated);
        }

        public ContractEconomyOperationResult AllocatePaymentToObligation(string obligationId, EconomyRuntime economy, string transactionId, long units, double worldTime = 0d, string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!obligationsById.TryGetValue(obligationId ?? string.Empty, out ContractObligationData obligation))
            {
                return Fail(ContractOperationCode.MissingObligation, $"Obligation '{obligationId}' is missing.", preview);
            }

            if (economy == null)
            {
                return Fail(ContractOperationCode.MissingAccount, "Economy runtime is missing.", preview);
            }

            long requestedUnits = Math.Max(0L, units);
            long payable = Math.Min(requestedUnits, obligation.OutstandingUnits);
            if (payable <= 0L || string.IsNullOrWhiteSpace(obligation.currencyId))
            {
                return Fail(ContractOperationCode.InvalidRequest, "Payment amount and obligation currency must be positive.", preview);
            }

            string operationKey = $"obligation-payment:{obligationId}:{requestedUnits}";
            if (!preview && IsDuplicate(transactionId, operationKey, out ContractEconomyOperationResult duplicate))
            {
                return duplicate.With(obligation: obligation);
            }

            ContractRuntimeSaveData contractRollback = CreateSaveData();
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData();
            try
            {
                EconomyOperationResult transfer = economy.Transfer(transactionId, obligation.fromAccountId, obligation.toAccountId, new MoneyAmount(obligation.currencyId, payable), EconomyTransactionKind.Payment, actorId: obligation.obligorPartyId, preview: preview);
                if (!transfer.Succeeded)
                {
                    return EconomyFailure(transfer, before, preview);
                }

                ContractPaymentAllocationData allocation = new ContractPaymentAllocationData
                {
                    allocationId = $"allocation.{obligationId}.{Sanitize(transactionId)}",
                    obligationId = obligationId,
                    contractId = obligation.contractId,
                    economyTransactionId = transfer.Transaction?.TransactionId ?? transactionId,
                    currencyId = obligation.currencyId,
                    units = payable,
                    worldTime = worldTime,
                    revision = 1L
                };

                ContractObligationData updated = obligation.Clone();
                updated.amountSatisfiedUnits = checked(updated.amountSatisfiedUnits + payable);
                updated.state = updated.OutstandingUnits == 0L ? ContractObligationState.Satisfied : ContractObligationState.PartiallySatisfied;
                updated.paymentAllocationIds = updated.paymentAllocationIds.Concat(new[] { allocation.allocationId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                updated.revision++;

                if (preview)
                {
                    return ContractEconomyOperationResult.Success("Obligation payment preview succeeded.", before, before, preview: true).With(obligation: updated, paymentAllocation: allocation, economyTransaction: transfer.Transaction);
                }

                if (FailAt(injectFailureStage, "after-economy-transfer"))
                {
                    throw new InvalidOperationException("Injected contract failure after economy transfer.");
                }

                allocationsById.Add(allocation.allocationId, allocation);
                obligationsById[updated.obligationId] = updated;
                Revision++;
                Remember(transactionId, operationKey, ContractOperationCode.Succeeded);
                return ContractEconomyOperationResult.Success("Obligation payment allocated.", before, Revision).With(obligation: updated, paymentAllocation: allocation, economyTransaction: transfer.Transaction);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
            {
                RestoreFromSaveData(contractRollback, registry);
                economy.RestoreFromSaveData(economyRollback, registry);
                return ContractEconomyOperationResult.Failure(exception is OverflowException ? ContractOperationCode.ArithmeticOverflow : ContractOperationCode.RolledBack, exception.Message, before);
            }
        }

        public ContractEconomyOperationResult CreateLoan(LoanData loan, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!ValidateLoan(loan, out string failure))
            {
                return Fail(ContractOperationCode.InvalidRequest, failure, preview);
            }

            LoanData clean = loan.Clone();
            clean.outstandingPrincipalUnits = clean.outstandingPrincipalUnits <= 0L ? clean.principalUnits : clean.outstandingPrincipalUnits;
            clean.state = clean.state == LoanState.Draft ? LoanState.Approved : clean.state;
            if (!contractsById.ContainsKey(clean.contractId))
            {
                return Fail(ContractOperationCode.MissingContract, $"Contract '{clean.contractId}' is missing.", preview);
            }

            if (loansById.ContainsKey(clean.loanId))
            {
                return Fail(ContractOperationCode.InvalidRequest, $"Loan '{clean.loanId}' already exists.", preview);
            }

            if (preview)
            {
                return ContractEconomyOperationResult.Success("Loan creation preview succeeded.", before, before, preview: true).With(loan: clean);
            }

            loansById.Add(clean.loanId, clean);
            Revision++;
            Remember(transactionId, $"loan:{clean.loanId}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Loan created.", before, Revision).With(loan: clean);
        }

        public ContractEconomyOperationResult DisburseLoan(string loanId, EconomyRuntime economy, string transactionId, double worldTime = 0d, string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!loansById.TryGetValue(loanId ?? string.Empty, out LoanData loan))
            {
                return Fail(ContractOperationCode.MissingLoan, $"Loan '{loanId}' is missing.", preview);
            }

            if (loan.state != LoanState.Approved)
            {
                return Fail(ContractOperationCode.InvalidState, $"Loan '{loanId}' cannot be disbursed from state {loan.state}.", preview);
            }

            if (economy == null)
            {
                return Fail(ContractOperationCode.MissingAccount, "Economy runtime is missing.", preview);
            }

            if (!preview && IsDuplicate(transactionId, $"loan-disburse:{loanId}", out ContractEconomyOperationResult duplicate))
            {
                return duplicate.With(loan: loan);
            }

            ContractRuntimeSaveData contractRollback = CreateSaveData();
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData();
            try
            {
                EconomyOperationResult transfer = economy.Transfer(transactionId, loan.lenderAccountId, loan.borrowerAccountId, new MoneyAmount(loan.currencyId, loan.principalUnits), EconomyTransactionKind.Transfer, actorId: loan.lenderPartyId, preview: preview);
                if (!transfer.Succeeded)
                {
                    return EconomyFailure(transfer, before, preview);
                }

                LoanData updated = loan.Clone();
                updated.state = LoanState.Current;
                updated.disbursedWorldTime = worldTime;
                updated.outstandingPrincipalUnits = loan.principalUnits;
                updated.revision++;
                if (preview)
                {
                    return ContractEconomyOperationResult.Success("Loan disbursement preview succeeded.", before, before, preview: true).With(loan: updated, economyTransaction: transfer.Transaction);
                }

                if (FailAt(injectFailureStage, "after-economy-transfer"))
                {
                    throw new InvalidOperationException("Injected loan disbursement failure after economy transfer.");
                }

                loansById[loanId] = updated;
                Revision++;
                Remember(transactionId, $"loan-disburse:{loanId}", ContractOperationCode.Succeeded);
                return ContractEconomyOperationResult.Success("Loan disbursed.", before, Revision).With(loan: updated, economyTransaction: transfer.Transaction);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
            {
                RestoreFromSaveData(contractRollback, registry);
                economy.RestoreFromSaveData(economyRollback, registry);
                return ContractEconomyOperationResult.Failure(exception is OverflowException ? ContractOperationCode.ArithmeticOverflow : ContractOperationCode.RolledBack, exception.Message, before);
            }
        }

        public ContractEconomyOperationResult GenerateRepaymentSchedule(string loanId, int installmentCount, double firstDueWorldTime, double intervalWorldTime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!loansById.TryGetValue(loanId ?? string.Empty, out LoanData loan))
            {
                return Fail(ContractOperationCode.MissingLoan, $"Loan '{loanId}' is missing.", preview);
            }

            if (installmentCount <= 0)
            {
                return Fail(ContractOperationCode.InvalidRequest, "Installment count must be positive.", preview);
            }

            if (loan.installmentIds.Length > 0)
            {
                return ContractEconomyOperationResult.Success("Repayment schedule already exists.", before, before, duplicate: true).With(loan: loan);
            }

            long basePrincipal = loan.principalUnits / installmentCount;
            long remainder = loan.principalUnits % installmentCount;
            List<LoanInstallmentData> created = new List<LoanInstallmentData>();
            List<string> obligationIds = new List<string>();
            for (int index = 0; index < installmentCount; index++)
            {
                long principalDue = basePrincipal + (index < remainder ? 1L : 0L);
                long interestDue = ApplyRatio(principalDue, loan.interestRatePerPeriod);
                string installmentId = $"installment.{loan.loanId}.{index + 1:D3}";
                string obligationId = $"obligation.{installmentId}";
                created.Add(new LoanInstallmentData
                {
                    installmentId = installmentId,
                    loanId = loan.loanId,
                    sequence = index + 1,
                    currencyId = loan.currencyId,
                    principalDueUnits = principalDue,
                    interestDueUnits = interestDue,
                    dueWorldTime = firstDueWorldTime + Math.Max(0d, intervalWorldTime) * index,
                    obligationId = obligationId
                });
                obligationIds.Add(obligationId);
            }

            if (preview)
            {
                return ContractEconomyOperationResult.Success("Repayment schedule preview succeeded.", before, before, preview: true).With(installment: created.FirstOrDefault());
            }

            LoanData updatedLoan = loan.Clone();
            updatedLoan.installmentIds = created.Select(item => item.installmentId).ToArray();
            updatedLoan.obligationIds = obligationIds.ToArray();
            updatedLoan.revision++;
            loansById[loan.loanId] = updatedLoan;
            foreach (LoanInstallmentData installment in created)
            {
                installmentsById.Add(installment.installmentId, installment);
                ContractObligationData obligation = new ContractObligationData
                {
                    obligationId = installment.obligationId,
                    contractId = loan.contractId,
                    termId = $"loan-repayment.{installment.sequence:D3}",
                    category = ContractObligationCategory.LoanRepayment,
                    obligorPartyId = loan.borrowerPartyId,
                    beneficiaryPartyId = loan.lenderPartyId,
                    fromAccountId = loan.borrowerAccountId,
                    toAccountId = loan.lenderAccountId,
                    currencyId = loan.currencyId,
                    amountDueUnits = installment.TotalDueUnits,
                    dueWorldTime = installment.dueWorldTime
                };
                obligationsById.Add(obligation.obligationId, obligation);
            }

            Revision++;
            Remember(transactionId, $"schedule:{loanId}:{installmentCount}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Repayment schedule generated.", before, Revision).With(loan: updatedLoan, installment: created.FirstOrDefault());
        }

        public ContractEconomyOperationResult AccrueLoanInterest(string loanId, string accrualId, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!loansById.TryGetValue(loanId ?? string.Empty, out LoanData loan))
            {
                return Fail(ContractOperationCode.MissingLoan, $"Loan '{loanId}' is missing.", preview);
            }

            if (string.IsNullOrWhiteSpace(accrualId))
            {
                return Fail(ContractOperationCode.InvalidRequest, "Accrual ID is required.", preview);
            }

            if (!preview && IsDuplicate(transactionId, $"interest:{loanId}:{accrualId}", out ContractEconomyOperationResult duplicate))
            {
                return duplicate.With(loan: loan);
            }

            long interest = ApplyRatio(loan.outstandingPrincipalUnits, loan.interestRatePerPeriod);
            LoanData updated = loan.Clone();
            updated.accruedInterestOutstandingUnits = checked(updated.accruedInterestOutstandingUnits + interest);
            updated.revision++;
            if (preview)
            {
                return ContractEconomyOperationResult.Success("Loan interest accrual preview succeeded.", before, before, preview: true).With(loan: updated);
            }

            loansById[loanId] = updated;
            Revision++;
            Remember(transactionId, $"interest:{loanId}:{accrualId}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Loan interest accrued.", before, Revision).With(loan: updated);
        }

        public ContractEconomyOperationResult RepayLoan(string loanId, EconomyRuntime economy, string transactionId, long units, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            if (!loansById.TryGetValue(loanId ?? string.Empty, out LoanData loan))
            {
                return Fail(ContractOperationCode.MissingLoan, $"Loan '{loanId}' is missing.", preview);
            }

            ContractEconomyOperationResult allocationResult = AllocatePaymentToSyntheticLoanObligation(loan, economy, transactionId, units, worldTime, preview);
            if (!allocationResult.Succeeded || preview)
            {
                return allocationResult;
            }

            long applied = allocationResult.PaymentAllocation?.units ?? 0L;
            LoanData updated = loansById[loanId].Clone();
            long interestPaid = Math.Min(updated.accruedInterestOutstandingUnits, applied);
            updated.accruedInterestOutstandingUnits -= interestPaid;
            long principalPaid = Math.Min(updated.outstandingPrincipalUnits, applied - interestPaid);
            updated.outstandingPrincipalUnits -= principalPaid;
            updated.state = updated.outstandingPrincipalUnits == 0L && updated.accruedInterestOutstandingUnits == 0L ? LoanState.PaidOff : LoanState.Current;
            updated.revision++;
            loansById[loanId] = updated;
            Revision++;
            return ContractEconomyOperationResult.Success("Loan repayment allocated.", before, Revision).With(loan: updated, paymentAllocation: allocationResult.PaymentAllocation, economyTransaction: allocationResult.EconomyTransaction);
        }

        public ContractEconomyOperationResult AddCollateral(CollateralDesignationData collateral, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (collateral == null || string.IsNullOrWhiteSpace(collateral.collateralId) || string.IsNullOrWhiteSpace(collateral.assetId))
            {
                return Fail(ContractOperationCode.InvalidRequest, "Collateral ID and asset ID are required.", preview);
            }

            if (!string.IsNullOrWhiteSpace(collateral.loanId) && !loansById.TryGetValue(collateral.loanId, out LoanData loan))
            {
                return Fail(ContractOperationCode.MissingLoan, $"Loan '{collateral.loanId}' is missing.", preview);
            }

            if (collateralById.ContainsKey(collateral.collateralId))
            {
                return Fail(ContractOperationCode.InvalidRequest, $"Collateral '{collateral.collateralId}' already exists.", preview);
            }

            CollateralDesignationData clean = collateral.Clone();
            clean.state = CollateralState.Pledged;
            if (preview)
            {
                return ContractEconomyOperationResult.Success("Collateral preview succeeded.", before, before, preview: true).With(collateral: clean);
            }

            collateralById.Add(clean.collateralId, clean);
            if (!string.IsNullOrWhiteSpace(clean.loanId))
            {
                LoanData updatedLoan = loansById[clean.loanId].Clone();
                updatedLoan.collateralIds = updatedLoan.collateralIds.Concat(new[] { clean.collateralId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                updatedLoan.revision++;
                loansById[updatedLoan.loanId] = updatedLoan;
            }

            Revision++;
            Remember(transactionId, $"collateral:{clean.collateralId}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Collateral pledged.", before, Revision).With(collateral: clean);
        }

        public ContractEconomyOperationResult UpdateLoanState(string loanId, LoanState state, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!loansById.TryGetValue(loanId ?? string.Empty, out LoanData loan))
            {
                return Fail(ContractOperationCode.MissingLoan, $"Loan '{loanId}' is missing.", preview);
            }

            LoanData updated = loan.Clone();
            updated.state = state;
            updated.revision++;
            if (preview)
            {
                return ContractEconomyOperationResult.Success("Loan state update preview succeeded.", before, before, preview: true).With(loan: updated);
            }

            loansById[loanId] = updated;
            Revision++;
            Remember(transactionId, $"loan-state:{loanId}:{state}", ContractOperationCode.Succeeded);
            return ContractEconomyOperationResult.Success("Loan state updated.", before, Revision).With(loan: updated);
        }

        public ContractRuntimeSaveData CreateSaveData()
        {
            return new ContractRuntimeSaveData
            {
                schemaVersion = CurrentSaveSchemaVersion,
                revision = Revision,
                worldId = worldId ?? string.Empty,
                proposals = Proposals.ToArray(),
                contracts = Contracts.ToArray(),
                amendments = Amendments.ToArray(),
                obligations = Obligations.ToArray(),
                performanceEvidence = PerformanceEvidence.ToArray(),
                paymentAllocations = PaymentAllocations.ToArray(),
                creditAgreements = CreditAgreements.ToArray(),
                loans = Loans.ToArray(),
                installments = Installments.ToArray(),
                collateral = Collateral.ToArray(),
                processedTransactions = processedByTransactionId.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray()
            };
        }

        public ContractEconomyOperationResult RestoreFromSaveData(ContractRuntimeSaveData saveData, DefinitionRegistry definitionRegistry = null)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, out string failure))
            {
                return ContractEconomyOperationResult.Failure(ContractOperationCode.PersistenceRejected, failure, before);
            }

            proposalsById.Clear();
            contractsById.Clear();
            amendmentsById.Clear();
            obligationsById.Clear();
            evidenceById.Clear();
            allocationsById.Clear();
            creditAgreementsById.Clear();
            loansById.Clear();
            installmentsById.Clear();
            collateralById.Clear();
            processedByTransactionId.Clear();

            foreach (ContractProposalData proposal in saveData.proposals ?? Array.Empty<ContractProposalData>()) proposalsById.Add(proposal.proposalId, proposal.Clone());
            foreach (EconomyContractData contract in saveData.contracts ?? Array.Empty<EconomyContractData>()) contractsById.Add(contract.contractId, contract.Clone());
            foreach (ContractAmendmentData amendment in saveData.amendments ?? Array.Empty<ContractAmendmentData>()) amendmentsById.Add(amendment.amendmentId, amendment.Clone());
            foreach (ContractObligationData obligation in saveData.obligations ?? Array.Empty<ContractObligationData>()) obligationsById.Add(obligation.obligationId, obligation.Clone());
            foreach (ContractPerformanceEvidenceData evidence in saveData.performanceEvidence ?? Array.Empty<ContractPerformanceEvidenceData>()) evidenceById.Add(evidence.evidenceId, evidence.Clone());
            foreach (ContractPaymentAllocationData allocation in saveData.paymentAllocations ?? Array.Empty<ContractPaymentAllocationData>()) allocationsById.Add(allocation.allocationId, allocation.Clone());
            foreach (CreditAgreementData agreement in saveData.creditAgreements ?? Array.Empty<CreditAgreementData>()) creditAgreementsById.Add(agreement.creditAgreementId, agreement.Clone());
            foreach (LoanData loan in saveData.loans ?? Array.Empty<LoanData>()) loansById.Add(loan.loanId, loan.Clone());
            foreach (LoanInstallmentData installment in saveData.installments ?? Array.Empty<LoanInstallmentData>()) installmentsById.Add(installment.installmentId, installment.Clone());
            foreach (CollateralDesignationData collateral in saveData.collateral ?? Array.Empty<CollateralDesignationData>()) collateralById.Add(collateral.collateralId, collateral.Clone());
            foreach (ContractProcessedTransactionData processed in saveData.processedTransactions ?? Array.Empty<ContractProcessedTransactionData>()) processedByTransactionId.Add(processed.transactionId, processed.Clone());
            Revision = Math.Max(0L, saveData.revision);
            worldId = saveData.worldId ?? worldId;
            return ContractEconomyOperationResult.Success("Contract runtime restored.", before, Revision);
        }

        public static bool ValidateSaveData(ContractRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Contract save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != CurrentSaveSchemaVersion)
            {
                failure = $"Unsupported contract save schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!ValidateUnique(saveData.proposals, item => item?.proposalId, "proposal", out failure)) return false;
            if (!ValidateUnique(saveData.contracts, item => item?.contractId, "contract", out failure)) return false;
            if (!ValidateUnique(saveData.amendments, item => item?.amendmentId, "amendment", out failure)) return false;
            if (!ValidateUnique(saveData.obligations, item => item?.obligationId, "obligation", out failure)) return false;
            if (!ValidateUnique(saveData.performanceEvidence, item => item?.evidenceId, "performance evidence", out failure)) return false;
            if (!ValidateUnique(saveData.paymentAllocations, item => item?.allocationId, "payment allocation", out failure)) return false;
            if (!ValidateUnique(saveData.creditAgreements, item => item?.creditAgreementId, "credit agreement", out failure)) return false;
            if (!ValidateUnique(saveData.loans, item => item?.loanId, "loan", out failure)) return false;
            if (!ValidateUnique(saveData.installments, item => item?.installmentId, "installment", out failure)) return false;
            if (!ValidateUnique(saveData.collateral, item => item?.collateralId, "collateral", out failure)) return false;

            HashSet<string> contractIds = new HashSet<string>((saveData.contracts ?? Array.Empty<EconomyContractData>()).Select(item => item.contractId), StringComparer.Ordinal);
            HashSet<string> obligationIds = new HashSet<string>((saveData.obligations ?? Array.Empty<ContractObligationData>()).Select(item => item.obligationId), StringComparer.Ordinal);
            HashSet<string> proposalIds = new HashSet<string>((saveData.proposals ?? Array.Empty<ContractProposalData>()).Select(item => item.proposalId), StringComparer.Ordinal);
            HashSet<string> loanIds = new HashSet<string>((saveData.loans ?? Array.Empty<LoanData>()).Select(item => item.loanId), StringComparer.Ordinal);
            HashSet<string> installmentIds = new HashSet<string>((saveData.installments ?? Array.Empty<LoanInstallmentData>()).Select(item => item.installmentId), StringComparer.Ordinal);
            HashSet<string> collateralIds = new HashSet<string>((saveData.collateral ?? Array.Empty<CollateralDesignationData>()).Select(item => item.collateralId), StringComparer.Ordinal);

            foreach (ContractProposalData proposal in saveData.proposals ?? Array.Empty<ContractProposalData>())
            {
                if (!ValidateProposal(proposal, registry, requireDefinition: false, out failure))
                {
                    return false;
                }
            }

            foreach (EconomyContractData contract in saveData.contracts ?? Array.Empty<EconomyContractData>())
            {
                if (string.IsNullOrWhiteSpace(contract.contractId))
                {
                    failure = "Contract save data contains a contract without an ID.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(contract.proposalId) && !proposalIds.Contains(contract.proposalId))
                {
                    failure = $"Contract '{contract.contractId}' references missing proposal '{contract.proposalId}'.";
                    return false;
                }

                foreach (string obligationId in contract.obligationIds ?? Array.Empty<string>())
                {
                    if (!obligationIds.Contains(obligationId))
                    {
                        failure = $"Contract '{contract.contractId}' references missing obligation '{obligationId}'.";
                        return false;
                    }
                }
            }

            foreach (ContractObligationData obligation in saveData.obligations ?? Array.Empty<ContractObligationData>())
            {
                if (!contractIds.Contains(obligation.contractId))
                {
                    failure = $"Obligation '{obligation.obligationId}' references missing contract '{obligation.contractId}'.";
                    return false;
                }
            }

            foreach (LoanData loan in saveData.loans ?? Array.Empty<LoanData>())
            {
                if (!contractIds.Contains(loan.contractId))
                {
                    failure = $"Loan '{loan.loanId}' references missing contract '{loan.contractId}'.";
                    return false;
                }

                foreach (string installmentId in loan.installmentIds ?? Array.Empty<string>())
                {
                    if (!installmentIds.Contains(installmentId))
                    {
                        failure = $"Loan '{loan.loanId}' references missing installment '{installmentId}'.";
                        return false;
                    }
                }

                foreach (string collateralId in loan.collateralIds ?? Array.Empty<string>())
                {
                    if (!collateralIds.Contains(collateralId))
                    {
                        failure = $"Loan '{loan.loanId}' references missing collateral '{collateralId}'.";
                        return false;
                    }
                }
            }

            foreach (LoanInstallmentData installment in saveData.installments ?? Array.Empty<LoanInstallmentData>())
            {
                if (!loanIds.Contains(installment.loanId))
                {
                    failure = $"Installment '{installment.installmentId}' references missing loan '{installment.loanId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(installment.obligationId) && !obligationIds.Contains(installment.obligationId))
                {
                    failure = $"Installment '{installment.installmentId}' references missing obligation '{installment.obligationId}'.";
                    return false;
                }
            }

            foreach (CollateralDesignationData collateral in saveData.collateral ?? Array.Empty<CollateralDesignationData>())
            {
                if (!string.IsNullOrWhiteSpace(collateral.loanId) && !loanIds.Contains(collateral.loanId))
                {
                    failure = $"Collateral '{collateral.collateralId}' references missing loan '{collateral.loanId}'.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateProposal(ContractProposalData proposal, DefinitionRegistry registry, bool requireDefinition, out string failure)
        {
            failure = string.Empty;
            if (proposal == null || string.IsNullOrWhiteSpace(proposal.proposalId))
            {
                failure = "Contract proposal ID is required.";
                return false;
            }

            if (requireDefinition && (registry == null || !registry.TryGet(proposal.definitionId, out _)))
            {
                failure = $"Contract proposal '{proposal.proposalId}' references missing definition '{proposal.definitionId}'.";
                return false;
            }

            ContractPartyData[] parties = proposal.parties ?? Array.Empty<ContractPartyData>();
            ContractTermData[] terms = proposal.terms ?? Array.Empty<ContractTermData>();
            if (parties.Length < 2)
            {
                failure = $"Contract proposal '{proposal.proposalId}' must include at least two parties.";
                return false;
            }

            if (terms.Length == 0)
            {
                failure = $"Contract proposal '{proposal.proposalId}' must include at least one term.";
                return false;
            }

            if (!ValidateUnique(parties, party => party?.partyId, "proposal party", out failure))
            {
                return false;
            }

            if (!ValidateUnique(terms, term => term?.termId, "proposal term", out failure))
            {
                return false;
            }

            return true;
        }

        private static bool ValidateLoan(LoanData loan, out string failure)
        {
            failure = string.Empty;
            if (loan == null || string.IsNullOrWhiteSpace(loan.loanId) || string.IsNullOrWhiteSpace(loan.contractId))
            {
                failure = "Loan ID and contract ID are required.";
                return false;
            }

            if (loan.principalUnits <= 0L || string.IsNullOrWhiteSpace(loan.currencyId))
            {
                failure = "Loan principal and currency must be positive.";
                return false;
            }

            if (loan.interestRatePerPeriod == null || loan.interestRatePerPeriod.denominator <= 0L)
            {
                failure = "Loan interest ratio denominator must be positive.";
                return false;
            }

            return true;
        }

        private static bool ValidateUnique<T>(IEnumerable<T> values, Func<T, string> idSelector, string label, out string failure)
        {
            failure = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T value in values ?? Array.Empty<T>())
            {
                string id = idSelector(value);
                if (string.IsNullOrWhiteSpace(id))
                {
                    failure = $"Contract save data contains a {label} without an ID.";
                    return false;
                }

                if (!ids.Add(id))
                {
                    failure = $"Contract save data contains duplicate {label} '{id}'.";
                    return false;
                }
            }

            return true;
        }

        private List<ContractObligationData> BuildInitialObligations(EconomyContractData contract)
        {
            List<ContractObligationData> obligations = new List<ContractObligationData>();
            foreach (ContractTermData term in contract.terms ?? Array.Empty<ContractTermData>())
            {
                if (term.category != ContractTermCategory.Payment && term.category != ContractTermCategory.Repayment && term.category != ContractTermCategory.Rent)
                {
                    continue;
                }

                ContractPartyData obligor = contract.parties.FirstOrDefault(party => string.Equals(party.partyId, term.responsiblePartyId, StringComparison.Ordinal));
                ContractPartyData beneficiary = contract.parties.FirstOrDefault(party => string.Equals(party.partyId, term.beneficiaryPartyId, StringComparison.Ordinal));
                obligations.Add(new ContractObligationData
                {
                    obligationId = $"obligation.{contract.contractId}.{term.termId}",
                    contractId = contract.contractId,
                    termId = term.termId,
                    category = term.category == ContractTermCategory.Rent ? ContractObligationCategory.RentPayment : ContractObligationCategory.MonetaryPayment,
                    obligorPartyId = term.responsiblePartyId,
                    beneficiaryPartyId = term.beneficiaryPartyId,
                    fromAccountId = obligor?.accountId ?? string.Empty,
                    toAccountId = beneficiary?.accountId ?? string.Empty,
                    currencyId = term.currencyId,
                    amountDueUnits = term.amountUnits,
                    dueWorldTime = term.dueWorldTime
                });
            }

            return obligations.OrderBy(item => item.obligationId, StringComparer.Ordinal).ToList();
        }

        private ContractEconomyOperationResult AllocatePaymentToSyntheticLoanObligation(LoanData loan, EconomyRuntime economy, string transactionId, long units, double worldTime, bool preview)
        {
            string obligationId = $"obligation.{loan.loanId}.direct-repayment";
            ContractObligationData obligation = obligationsById.TryGetValue(obligationId, out ContractObligationData existing)
                ? existing.Clone()
                : new ContractObligationData
                {
                    obligationId = obligationId,
                    contractId = loan.contractId,
                    termId = "direct-loan-repayment",
                    category = ContractObligationCategory.LoanRepayment,
                    state = ContractObligationState.Pending,
                    obligorPartyId = loan.borrowerPartyId,
                    beneficiaryPartyId = loan.lenderPartyId,
                    fromAccountId = loan.borrowerAccountId,
                    toAccountId = loan.lenderAccountId,
                    currencyId = loan.currencyId,
                    dueWorldTime = worldTime
                };

            obligation.amountDueUnits = checked(loan.outstandingPrincipalUnits + loan.accruedInterestOutstandingUnits);
            if (preview)
            {
                EconomyOperationResult previewTransfer = economy.Transfer(transactionId, loan.borrowerAccountId, loan.lenderAccountId, new MoneyAmount(loan.currencyId, Math.Min(Math.Max(0L, units), obligation.OutstandingUnits)), EconomyTransactionKind.Payment, actorId: loan.borrowerPartyId, preview: true);
                if (!previewTransfer.Succeeded)
                {
                    return EconomyFailure(previewTransfer, Revision, preview: true);
                }

                ContractPaymentAllocationData previewAllocation = new ContractPaymentAllocationData
                {
                    allocationId = $"allocation.{obligationId}.{Sanitize(transactionId)}",
                    obligationId = obligationId,
                    contractId = loan.contractId,
                    economyTransactionId = transactionId,
                    currencyId = loan.currencyId,
                    units = Math.Min(Math.Max(0L, units), obligation.OutstandingUnits),
                    worldTime = worldTime
                };
                return ContractEconomyOperationResult.Success("Loan repayment preview succeeded.", Revision, Revision, preview: true).With(obligation: obligation, paymentAllocation: previewAllocation, economyTransaction: previewTransfer.Transaction);
            }

            obligationsById[obligationId] = obligation;
            return AllocatePaymentToObligation(obligationId, economy, transactionId, units, worldTime, preview: preview);
        }

        private ContractEconomyOperationResult EconomyFailure(EconomyOperationResult economyResult, long revision, bool preview)
        {
            ContractOperationCode code = economyResult.Code == EconomyResultCode.InsufficientFunds
                ? ContractOperationCode.InsufficientFunds
                : economyResult.Code == EconomyResultCode.MissingAccount
                    ? ContractOperationCode.MissingAccount
                    : ContractOperationCode.InvalidRequest;
            return ContractEconomyOperationResult.Failure(code, economyResult.Message, revision, preview);
        }

        private ContractEconomyOperationResult Fail(ContractOperationCode code, string message, bool preview)
        {
            return ContractEconomyOperationResult.Failure(code, message, Revision, preview);
        }

        private bool IsDuplicate(string transactionId, string operationKey, out ContractEconomyOperationResult duplicate)
        {
            duplicate = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (!processedByTransactionId.TryGetValue(transactionId, out ContractProcessedTransactionData processed))
            {
                return false;
            }

            if (string.Equals(processed.operationKey, operationKey, StringComparison.Ordinal))
            {
                duplicate = ContractEconomyOperationResult.Success("Duplicate contract transaction ignored.", Revision, Revision, duplicate: true);
                return true;
            }

            duplicate = ContractEconomyOperationResult.Failure(
                ContractOperationCode.InvalidRequest,
                $"Transaction ID '{transactionId}' was already used for contract operation '{processed.operationKey}', not '{operationKey}'.",
                Revision);
            return true;
        }

        private void Remember(string transactionId, string operationKey, ContractOperationCode code)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            processedByTransactionId[transactionId] = new ContractProcessedTransactionData
            {
                transactionId = transactionId,
                operationKey = operationKey ?? string.Empty,
                code = code,
                revision = Revision
            };
        }

        private static long ApplyRatio(long value, ContractRationalData ratio)
        {
            if (value <= 0L || ratio == null || ratio.numerator <= 0L)
            {
                return 0L;
            }

            if (ratio.denominator <= 0L)
            {
                throw new InvalidOperationException("Ratio denominator must be positive.");
            }

            checked
            {
                long numerator = value * ratio.numerator;
                long denominator = ratio.denominator;
                return ratio.rounding switch
                {
                    ContractRoundingMode.Ceiling => (numerator + denominator - 1L) / denominator,
                    ContractRoundingMode.Nearest => (numerator + denominator / 2L) / denominator,
                    _ => numerator / denominator
                };
            }
        }

        private static bool FailAt(string stage, string expected)
        {
            return !string.IsNullOrWhiteSpace(stage) && string.Equals(stage, expected, StringComparison.Ordinal);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "generated";
            }

            char[] chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            return new string(chars).Trim('-');
        }
    }
}
