using System;
using System.Linq;

namespace UnityIsekaiGame.Governments
{
    [Serializable]
    public sealed class GovernmentOfficeTenureRecordData
    {
        public string tenureId;
        public string governmentId;
        public string officeDefinitionId;
        public string organizationOfficeId;
        public string officeAssignmentId;
        public string holderPersonId;
        public GovernmentOfficeSelectionMethod selectionMethod;
        public GovernmentOfficeTenureState state;
        public string appointingAuthorityId;
        public string confirmationSourceId;
        public string successionSourceId;
        public string lineageAnchorPersonId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentOfficeTenureRecordData Clone() => (GovernmentOfficeTenureRecordData)MemberwiseClone();
        public bool IsActiveAt(double worldTime) => (state == GovernmentOfficeTenureState.Active || state == GovernmentOfficeTenureState.Acting) && startWorldTime <= worldTime && (endWorldTime < 0d || worldTime < endWorldTime);
    }

    [Serializable]
    public sealed class GovernmentElectionCandidateRecordData
    {
        public string personId;
        public string nominationSourceId;
        public double nominatedWorldTime;
        public bool withdrawn;
        public GovernmentElectionCandidateRecordData Clone() => (GovernmentElectionCandidateRecordData)MemberwiseClone();
    }

    [Serializable]
    public sealed class GovernmentElectionBallotRecordData
    {
        public string ballotId;
        public string voterPersonId;
        public string candidatePersonId;
        public double castWorldTime;
        public GovernmentElectionBallotRecordData Clone() => (GovernmentElectionBallotRecordData)MemberwiseClone();
    }

    [Serializable]
    public sealed class GovernmentElectionRecordData
    {
        public string electionId;
        public string governmentId;
        public string officeDefinitionId;
        public GovernmentOfficeSelectionMethod selectionMethod;
        public GovernmentElectionState state;
        public string administeringOrganizationId;
        public string[] eligibleVoterPersonIds = Array.Empty<string>();
        public GovernmentElectionCandidateRecordData[] candidates = Array.Empty<GovernmentElectionCandidateRecordData>();
        public GovernmentElectionBallotRecordData[] ballots = Array.Empty<GovernmentElectionBallotRecordData>();
        public string winningPersonId;
        public string linkedProposalId;
        public string linkedResolutionId;
        public double scheduledWorldTime;
        public double votingStartWorldTime;
        public double votingEndWorldTime;
        public double certifiedWorldTime = -1d;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentElectionRecordData Clone()
        {
            GovernmentElectionRecordData clone = (GovernmentElectionRecordData)MemberwiseClone();
            clone.eligibleVoterPersonIds = (eligibleVoterPersonIds ?? Array.Empty<string>()).ToArray();
            clone.candidates = (candidates ?? Array.Empty<GovernmentElectionCandidateRecordData>()).Select(value => value?.Clone()).Where(value => value != null).ToArray();
            clone.ballots = (ballots ?? Array.Empty<GovernmentElectionBallotRecordData>()).Select(value => value?.Clone()).Where(value => value != null).ToArray();
            return clone;
        }
    }

    [Serializable]
    public sealed class GovernmentPermitRecordData
    {
        public string permitId;
        public string permitDefinitionId;
        public string governmentId;
        public string jurisdictionId;
        public GovernmentPermitHolderCategory holderCategory;
        public string holderId;
        public GovernmentPermitState state;
        public string[] actionIds = Array.Empty<string>();
        public string issuedByPersonId;
        public string sourceAuthorityId;
        public string feeAssessmentId;
        public double issuedWorldTime;
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public string suspensionOrRevocationReason;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentPermitRecordData Clone()
        {
            GovernmentPermitRecordData clone = (GovernmentPermitRecordData)MemberwiseClone();
            clone.actionIds = (actionIds ?? Array.Empty<string>()).ToArray();
            return clone;
        }

        public bool IsActiveAt(double worldTime) => state == GovernmentPermitState.Active && effectiveWorldTime <= worldTime && (expirationWorldTime < 0d || worldTime < expirationWorldTime);
    }

    [Serializable]
    public sealed class GovernmentFiscalMandateRecordData
    {
        public string mandateId;
        public string fiscalPolicyDefinitionId;
        public string governmentId;
        public GovernmentFiscalMandateState state;
        public string[] revenueDefinitionIds = Array.Empty<string>();
        public string treasuryAccountId;
        public string organizationBudgetId;
        public string sourceAuthorityId;
        public string sourceDecisionId;
        public double effectiveWorldTime;
        public double endWorldTime = -1d;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentFiscalMandateRecordData Clone()
        {
            GovernmentFiscalMandateRecordData clone = (GovernmentFiscalMandateRecordData)MemberwiseClone();
            clone.revenueDefinitionIds = (revenueDefinitionIds ?? Array.Empty<string>()).ToArray();
            return clone;
        }
    }

    [Serializable]
    public struct GovernmentLegitimacyComponentScore
    {
        public GovernmentLegitimacyComponent component;
        public int scoreBasisPoints;
        public string sourceId;
    }

    [Serializable]
    public sealed class GovernmentLegitimacyRecordData
    {
        public string assessmentId;
        public string governmentId;
        public string policyDefinitionId;
        public GovernmentLegitimacyComponentScore[] components = Array.Empty<GovernmentLegitimacyComponentScore>();
        public int totalScoreBasisPoints;
        public GovernmentLegitimacyBand band;
        public double assessedWorldTime;
        public string assessorId;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentLegitimacyRecordData Clone()
        {
            GovernmentLegitimacyRecordData clone = (GovernmentLegitimacyRecordData)MemberwiseClone();
            clone.components = (components ?? Array.Empty<GovernmentLegitimacyComponentScore>()).ToArray();
            return clone;
        }
    }

    public sealed class GovernmentOfficeInstallationRequest
    {
        public string transactionId;
        public string tenureId;
        public string governmentId;
        public string officeDefinitionId;
        public string organizationOfficeId;
        public string officeAssignmentId;
        public string holderPersonId;
        public GovernmentOfficeSelectionMethod selectionMethod;
        public GovernmentOfficeTenureState state = GovernmentOfficeTenureState.Active;
        public string appointingAuthorityId;
        public string confirmationSourceId;
        public string successionSourceId;
        public string lineageAnchorPersonId;
        public double worldTime;
        public double endWorldTime = -1d;
        public string provenanceId;
        public bool replaceCurrent;
        public bool preview;
    }

    public sealed class GovernmentElectionScheduleRequest
    {
        public string transactionId;
        public string electionId;
        public string governmentId;
        public string officeDefinitionId;
        public GovernmentOfficeSelectionMethod selectionMethod = GovernmentOfficeSelectionMethod.PopularElection;
        public string administeringOrganizationId;
        public string[] eligibleVoterPersonIds = Array.Empty<string>();
        public string linkedProposalId;
        public double scheduledWorldTime;
        public double votingStartWorldTime;
        public double votingEndWorldTime;
        public string provenanceId;
        public bool preview;
    }

    public sealed class GovernmentElectionNominationRequest
    {
        public string transactionId;
        public string electionId;
        public string candidatePersonId;
        public string nominationSourceId;
        public double worldTime;
    }

    public sealed class GovernmentElectionBallotRequest
    {
        public string transactionId;
        public string ballotId;
        public string electionId;
        public string voterPersonId;
        public string candidatePersonId;
        public double worldTime;
    }

    public sealed class GovernmentElectionCertificationRequest
    {
        public string transactionId;
        public string electionId;
        public string linkedResolutionId;
        public double worldTime;
    }

    public sealed class GovernmentPermitIssueRequest
    {
        public string transactionId;
        public string permitId;
        public string permitDefinitionId;
        public string governmentId;
        public string jurisdictionId;
        public GovernmentPermitHolderCategory holderCategory;
        public string holderId;
        public string issuedByPersonId;
        public string sourceAuthorityId;
        public string feeAssessmentId;
        public double worldTime;
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public string provenanceId;
        public bool preview;
    }

    public sealed class GovernmentPermitStateRequest
    {
        public string transactionId;
        public string permitId;
        public GovernmentPermitState targetState;
        public string reason;
        public double worldTime;
    }

    public sealed class GovernmentFiscalMandateRequest
    {
        public string transactionId;
        public string mandateId;
        public string fiscalPolicyDefinitionId;
        public string governmentId;
        public string organizationBudgetId;
        public string sourceAuthorityId;
        public string sourceDecisionId;
        public double worldTime;
        public double endWorldTime = -1d;
        public string provenanceId;
        public bool preview;
    }

    public sealed class GovernmentLegitimacyAssessmentRequest
    {
        public string transactionId;
        public string assessmentId;
        public string governmentId;
        public string policyDefinitionId;
        public GovernmentLegitimacyComponentScore[] components = Array.Empty<GovernmentLegitimacyComponentScore>();
        public double worldTime;
        public string assessorId;
        public string provenanceId;
        public bool preview;
    }
}
