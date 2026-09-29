using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    public static class PrototypeOrganizationAuthorityDefinitionFactory
    {
        public const string ViewPublicInformationPermissionId = "organization-permission.prototype.view-public-information";
        public const string ViewRestrictedInformationPermissionId = "organization-permission.prototype.view-restricted-information";
        public const string ViewSecretInformationPermissionId = "organization-permission.prototype.view-secret-information";
        public const string ReviewMembershipApplicationsPermissionId = "organization-permission.prototype.review-membership-applications";
        public const string InviteMembersPermissionId = "organization-permission.prototype.invite-members";
        public const string AdmitMembersPermissionId = "organization-permission.prototype.admit-members";
        public const string SuspendMembersPermissionId = "organization-permission.prototype.suspend-members";
        public const string ReinstateMembersPermissionId = "organization-permission.prototype.reinstate-members";
        public const string RemoveMembersPermissionId = "organization-permission.prototype.remove-members";
        public const string AssignRanksPermissionId = "organization-permission.prototype.assign-ranks";
        public const string PromoteMembersPermissionId = "organization-permission.prototype.promote-members";
        public const string DemoteMembersPermissionId = "organization-permission.prototype.demote-members";
        public const string CreateOfficesPermissionId = "organization-permission.prototype.create-offices";
        public const string AppointOfficeholdersPermissionId = "organization-permission.prototype.appoint-officeholders";
        public const string RemoveOfficeholdersPermissionId = "organization-permission.prototype.remove-officeholders";
        public const string AssignActingOfficeholdersPermissionId = "organization-permission.prototype.assign-acting-officeholders";
        public const string IssueOrdersPermissionId = "organization-permission.prototype.issue-orders";
        public const string RepresentExternallyPermissionId = "organization-permission.prototype.represent-externally";
        public const string ManageAliasesPermissionId = "organization-permission.prototype.manage-aliases";
        public const string ProposeHeadquartersPermissionId = "organization-permission.prototype.propose-headquarters-change";
        public const string AuthorizeHeadquartersPermissionId = "organization-permission.prototype.authorize-headquarters-change";
        public const string DelegatePermissionsPermissionId = "organization-permission.prototype.delegate-permissions";
        public const string RevokeDelegationsPermissionId = "organization-permission.prototype.revoke-delegations";
        public const string ViewTreasuryPermissionId = "organization-permission.prototype.view-treasury";
        public const string ViewRestrictedTreasuryPermissionId = "organization-permission.prototype.view-restricted-treasury";
        public const string CreateTreasuryAccountPermissionId = "organization-permission.prototype.create-treasury-account";
        public const string CloseTreasuryAccountPermissionId = "organization-permission.prototype.close-treasury-account";
        public const string DepositOrganizationFundsPermissionId = "organization-permission.prototype.deposit-organization-funds";
        public const string WithdrawOrganizationFundsPermissionId = "organization-permission.prototype.withdraw-organization-funds";
        public const string TransferOrganizationFundsPermissionId = "organization-permission.prototype.transfer-organization-funds";
        public const string ApproveOrganizationExpensePermissionId = "organization-permission.prototype.approve-organization-expense";
        public const string ManageRestrictedFundsPermissionId = "organization-permission.prototype.manage-restricted-funds";
        public const string ManageResourceReservationsPermissionId = "organization-permission.prototype.manage-resource-reservations";
        public const string ManageOrganizationInventoryPermissionId = "organization-permission.prototype.manage-organization-inventory";
        public const string AssignAssetCustodyPermissionId = "organization-permission.prototype.assign-asset-custody";
        public const string ManageBusinessAssociationPermissionId = "organization-permission.prototype.manage-business-association";
        public const string ViewFinancialAuditPermissionId = "organization-permission.prototype.view-financial-audit";
        public const string PerformFinancialCorrectionPermissionId = "organization-permission.prototype.perform-financial-correction";
        public const string SubmitDecisionProposalPermissionId = "organization-permission.prototype.submit-decision-proposal";
        public const string AmendDecisionProposalPermissionId = "organization-permission.prototype.amend-decision-proposal";
        public const string CastOrganizationVotePermissionId = "organization-permission.prototype.cast-organization-vote";
        public const string CloseOrganizationVotePermissionId = "organization-permission.prototype.close-organization-vote";
        public const string VetoOrganizationResolutionPermissionId = "organization-permission.prototype.veto-organization-resolution";
        public const string OverrideOrganizationVetoPermissionId = "organization-permission.prototype.override-organization-veto";
        public const string ExecuteOrganizationResolutionPermissionId = "organization-permission.prototype.execute-organization-resolution";
        public const string IssueEmergencyDecisionPermissionId = "organization-permission.prototype.issue-emergency-decision";
        public const string ViewConfidentialDecisionsPermissionId = "organization-permission.prototype.view-confidential-decisions";
        public const string EnactMunicipalLawPermissionId = "organization-permission.prototype.enact-municipal-law";
        public const string IssueWarrantPermissionId = "organization-permission.prototype.issue-warrant";
        public const string AdministerJusticePermissionId = "organization-permission.prototype.administer-justice";
        public const string RecordOfficialCrimeDiscoveryPermissionId = "organization-permission.prototype.record-official-crime-discovery";

        public const string ReviewMembershipApplicationActionId = "organization-action.prototype.review-membership-application";
        public const string InviteMemberActionId = "organization-action.prototype.invite-member";
        public const string AdmitMemberActionId = "organization-action.prototype.admit-member";
        public const string SuspendMemberActionId = "organization-action.prototype.suspend-member";
        public const string ReinstateMemberActionId = "organization-action.prototype.reinstate-member";
        public const string RemoveMemberActionId = "organization-action.prototype.remove-member";
        public const string AssignRankActionId = "organization-action.prototype.assign-rank";
        public const string PromoteMemberActionId = "organization-action.prototype.promote-member";
        public const string DemoteMemberActionId = "organization-action.prototype.demote-member";
        public const string CreateOfficeActionId = "organization-action.prototype.create-office";
        public const string AppointOfficeholderActionId = "organization-action.prototype.appoint-officeholder";
        public const string RemoveOfficeholderActionId = "organization-action.prototype.remove-officeholder";
        public const string AssignActingOfficeholderActionId = "organization-action.prototype.assign-acting-officeholder";
        public const string RenameOrganizationActionId = "organization-action.prototype.rename-organization";
        public const string ChangeHeadquartersActionId = "organization-action.prototype.change-headquarters";
        public const string CreateBranchActionId = "organization-action.prototype.create-branch";
        public const string EndBranchLinkActionId = "organization-action.prototype.end-branch-link";
        public const string IssueOrderActionId = "organization-action.prototype.issue-order";
        public const string AccessRestrictedRecordActionId = "organization-action.prototype.access-restricted-record";
        public const string AccessSecretRecordActionId = "organization-action.prototype.access-secret-record";
        public const string DelegatePermissionActionId = "organization-action.prototype.delegate-permission";
        public const string RevokeDelegationActionId = "organization-action.prototype.revoke-delegation";
        public const string CreateTreasuryActionId = "organization-action.prototype.create-treasury";
        public const string CreateTreasuryAccountActionId = "organization-action.prototype.create-treasury-account";
        public const string ManageTreasuryAccountActionId = "organization-action.prototype.manage-treasury-account";
        public const string DepositOrganizationFundsActionId = "organization-action.prototype.deposit-organization-funds";
        public const string WithdrawOrganizationFundsActionId = "organization-action.prototype.withdraw-organization-funds";
        public const string TransferOrganizationFundsActionId = "organization-action.prototype.transfer-organization-funds";
        public const string LargeOrganizationTransferActionId = "organization-action.prototype.large-organization-transfer";
        public const string ManageRestrictedFundsActionId = "organization-action.prototype.manage-restricted-funds";
        public const string ManageOrganizationBudgetActionId = "organization-action.prototype.manage-organization-budget";
        public const string ManageResourceReservationActionId = "organization-action.prototype.manage-resource-reservation";
        public const string ManageOrganizationInventoryActionId = "organization-action.prototype.manage-organization-inventory";
        public const string AssignAssetCustodyActionId = "organization-action.prototype.assign-asset-custody";
        public const string ManagePropertyAssociationActionId = "organization-action.prototype.manage-property-association";
        public const string ManageBusinessAssociationActionId = "organization-action.prototype.manage-business-association";
        public const string ViewFinancialAuditActionId = "organization-action.prototype.view-financial-audit";
        public const string PerformFinancialCorrectionActionId = "organization-action.prototype.perform-financial-correction";
        public const string SubmitDecisionProposalActionId = "organization-action.prototype.submit-decision-proposal";
        public const string AmendDecisionProposalActionId = "organization-action.prototype.amend-decision-proposal";
        public const string CastOrganizationVoteActionId = "organization-action.prototype.cast-organization-vote";
        public const string CloseOrganizationVoteActionId = "organization-action.prototype.close-organization-vote";
        public const string VetoOrganizationResolutionActionId = "organization-action.prototype.veto-organization-resolution";
        public const string OverrideOrganizationVetoActionId = "organization-action.prototype.override-organization-veto";
        public const string ExecuteOrganizationResolutionActionId = "organization-action.prototype.execute-organization-resolution";
        public const string IssueEmergencyDecisionActionId = "organization-action.prototype.issue-emergency-decision";
        public const string ViewConfidentialDecisionActionId = "organization-action.prototype.view-confidential-decision";
        public const string EnactMunicipalLawActionId = "organization-action.prototype.enact-municipal-law";
        public const string IssueWarrantActionId = "organization-action.prototype.issue-warrant";
        public const string AdministerJusticeActionId = "organization-action.prototype.administer-justice";
        public const string RecordOfficialCrimeDiscoveryActionId = "organization-action.prototype.record-official-crime-discovery";

        public const string GeneralMemberRoleId = "organization-authority-role.prototype.general-member";
        public const string MembershipOfficerRoleId = "organization-authority-role.prototype.membership-officer";
        public const string RankOfficerRoleId = "organization-authority-role.prototype.rank-officer";
        public const string TreasurerRoleId = "organization-authority-role.prototype.treasurer";
        public const string ChapterMasterRoleId = "organization-authority-role.prototype.chapter-master";
        public const string GuildmasterRoleId = "organization-authority-role.prototype.guildmaster";
        public const string GuildReceptionistRoleId = "organization-authority-role.prototype.guild-receptionist";
        public const string RecordClerkRoleId = "organization-authority-role.prototype.record-clerk";
        public const string HeadmasterRoleId = "organization-authority-role.prototype.headmaster";
        public const string ActingExecutiveRoleId = "organization-authority-role.prototype.acting-executive";
        public const string SecretMemberRoleId = "organization-authority-role.prototype.secret-member";
        public const string MilitaryOfficerRoleId = "organization-authority-role.prototype.military-officer";
        public const string NobleRoleId = "organization-authority-role.prototype.noble";
        public const string VillageChiefRoleId = "organization-authority-role.prototype.village-chief";
        public const string MayorRoleId = "organization-authority-role.prototype.mayor";
        public const string ManorLordRoleId = "organization-authority-role.prototype.manor-lord";
        public const string DukeRoleId = "organization-authority-role.prototype.duke";
        public const string MonarchRoleId = "organization-authority-role.prototype.monarch";
        public const string MagistrateRoleId = "organization-authority-role.prototype.magistrate";

        public const string GuildFullMemberBindingId = "organization-authority-binding.prototype.guild.full-member";
        public const string GuildSSSRankBindingId = "organization-authority-binding.prototype.guild.sss-rank";
        public const string GuildmasterOfficeBindingId = "organization-authority-binding.prototype.guild.guildmaster-office";
        public const string GuildReceptionistOfficeBindingId = "organization-authority-binding.prototype.guild.receptionist-office";
        public const string GuildTreasurerOfficeBindingId = "organization-authority-binding.prototype.guild.treasurer-office";
        public const string BranchChapterMasterOfficeBindingId = "organization-authority-binding.prototype.branch.chapter-master-office";
        public const string GuardCaptainOfficeBindingId = "organization-authority-binding.prototype.military.guard-captain-office";
        public const string SecretMemberBindingId = "organization-authority-binding.prototype.secret.member";
        public const string NobleOfficeBindingId = "organization-authority-binding.prototype.civic.noble-office";
        public const string VillageChiefOfficeBindingId = "organization-authority-binding.prototype.civic.village-chief-office";
        public const string MayorOfficeBindingId = "organization-authority-binding.prototype.civic.mayor-office";
        public const string ManorLordOfficeBindingId = "organization-authority-binding.prototype.civic.manor-lord-office";
        public const string DukeOfficeBindingId = "organization-authority-binding.prototype.civic.duke-office";
        public const string MonarchOfficeBindingId = "organization-authority-binding.prototype.civic.monarch-office";
        public const string MagistrateOfficeBindingId = "organization-authority-binding.prototype.civic.magistrate-office";
        public const string UniversityHeadmasterOfficeBindingId = "organization-authority-binding.prototype.university.headmaster-office";

        public static DefinitionRegistry AddMissingPrototypeOrganizationAuthorityDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            definitions.AddRange(CreateMissingPermissionDefinitions(ids));
            definitions.AddRange(CreateMissingActionDefinitions(ids));
            definitions.AddRange(CreateMissingRoleDefinitions(ids));
            definitions.AddRange(CreateMissingBindingDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<OrganizationPermissionDefinition> CreateMissingPermissionDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = Set(existingDefinitionIds);
            List<OrganizationPermissionDefinition> definitions = new List<OrganizationPermissionDefinition>();
            OrganizationAuthorityScopeType[] generalScopes = { OrganizationAuthorityScopeType.EntireOrganization, OrganizationAuthorityScopeType.OrganizationBranch, OrganizationAuthorityScopeType.SpecificOrganizationSubtree };
            AddPermission(definitions, ids, ViewPublicInformationPermissionId, "View Public Organization Information", OrganizationPermissionCategory.ViewInformation, generalScopes);
            AddPermission(definitions, ids, ViewRestrictedInformationPermissionId, "View Restricted Organization Information", OrganizationPermissionCategory.ViewInformation, generalScopes);
            AddPermission(definitions, ids, ViewSecretInformationPermissionId, "View Secret Organization Information", OrganizationPermissionCategory.ViewInformation, generalScopes, visibility: OrganizationVisibility.Restricted);
            AddPermission(definitions, ids, ReviewMembershipApplicationsPermissionId, "Review Membership Applications", OrganizationPermissionCategory.ManageMembership, generalScopes);
            AddPermission(definitions, ids, InviteMembersPermissionId, "Invite Organization Members", OrganizationPermissionCategory.ManageMembership, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, AdmitMembersPermissionId, "Admit Organization Members", OrganizationPermissionCategory.ManageMembership, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, SuspendMembersPermissionId, "Suspend Organization Members", OrganizationPermissionCategory.ManageMembership, generalScopes);
            AddPermission(definitions, ids, ReinstateMembersPermissionId, "Reinstate Organization Members", OrganizationPermissionCategory.ManageMembership, generalScopes);
            AddPermission(definitions, ids, RemoveMembersPermissionId, "Remove Organization Members", OrganizationPermissionCategory.ManageMembership, generalScopes);
            AddPermission(definitions, ids, AssignRanksPermissionId, "Assign Organization Ranks", OrganizationPermissionCategory.ManageRanks, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, PromoteMembersPermissionId, "Promote Organization Members", OrganizationPermissionCategory.ManageRanks, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, DemoteMembersPermissionId, "Demote Organization Members", OrganizationPermissionCategory.ManageRanks, generalScopes);
            AddPermission(definitions, ids, CreateOfficesPermissionId, "Create Organization Offices", OrganizationPermissionCategory.ManageOffices, generalScopes);
            AddPermission(definitions, ids, AppointOfficeholdersPermissionId, "Appoint Officeholders", OrganizationPermissionCategory.ManageOffices, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, RemoveOfficeholdersPermissionId, "Remove Officeholders", OrganizationPermissionCategory.ManageOffices, generalScopes);
            AddPermission(definitions, ids, AssignActingOfficeholdersPermissionId, "Assign Acting Officeholders", OrganizationPermissionCategory.ManageOffices, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, IssueOrdersPermissionId, "Issue Institutional Orders", OrganizationPermissionCategory.IssueInstitutionalOrders, generalScopes, canDelegate: true, canRedelegate: true);
            AddPermission(definitions, ids, RepresentExternallyPermissionId, "Represent Organization Externally", OrganizationPermissionCategory.RepresentOrganization, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, ManageAliasesPermissionId, "Manage Organization Aliases", OrganizationPermissionCategory.ManageInformation, generalScopes);
            AddPermission(definitions, ids, ProposeHeadquartersPermissionId, "Propose Headquarters Change", OrganizationPermissionCategory.ManagePropertyAssociation, generalScopes);
            AddPermission(definitions, ids, AuthorizeHeadquartersPermissionId, "Authorize Headquarters Change", OrganizationPermissionCategory.ManagePropertyAssociation, generalScopes, jointAllowed: true);
            AddPermission(definitions, ids, DelegatePermissionsPermissionId, "Delegate Institutional Permissions", OrganizationPermissionCategory.ManageAccess, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, RevokeDelegationsPermissionId, "Revoke Delegated Permissions", OrganizationPermissionCategory.ManageAccess, generalScopes);
            AddPermission(definitions, ids, ViewTreasuryPermissionId, "View Treasury", OrganizationPermissionCategory.ViewInformation, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, ViewRestrictedTreasuryPermissionId, "View Restricted Treasury", OrganizationPermissionCategory.ViewInformation, generalScopes, visibility: OrganizationVisibility.Restricted);
            AddPermission(definitions, ids, CreateTreasuryAccountPermissionId, "Create Treasury Accounts", OrganizationPermissionCategory.ManageResources, generalScopes);
            AddPermission(definitions, ids, CloseTreasuryAccountPermissionId, "Close Treasury Accounts", OrganizationPermissionCategory.ManageResources, generalScopes, jointAllowed: true);
            AddPermission(definitions, ids, DepositOrganizationFundsPermissionId, "Deposit Organization Funds", OrganizationPermissionCategory.ManageResources, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, WithdrawOrganizationFundsPermissionId, "Withdraw Organization Funds", OrganizationPermissionCategory.ManageResources, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, TransferOrganizationFundsPermissionId, "Transfer Organization Funds", OrganizationPermissionCategory.ManageResources, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, ApproveOrganizationExpensePermissionId, "Approve Organization Expense", OrganizationPermissionCategory.ManageResources, generalScopes, jointAllowed: true);
            AddPermission(definitions, ids, ManageRestrictedFundsPermissionId, "Manage Restricted Funds", OrganizationPermissionCategory.ManageResources, generalScopes);
            AddPermission(definitions, ids, ManageResourceReservationsPermissionId, "Manage Resource Reservations", OrganizationPermissionCategory.ManageResources, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, ManageOrganizationInventoryPermissionId, "Manage Organization Inventory", OrganizationPermissionCategory.ManageResources, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, AssignAssetCustodyPermissionId, "Assign Asset Custody", OrganizationPermissionCategory.ManageResources, generalScopes, canDelegate: true);
            AddPermission(definitions, ids, ManageBusinessAssociationPermissionId, "Manage Business Association", OrganizationPermissionCategory.ManageResources, generalScopes);
            AddPermission(definitions, ids, ViewFinancialAuditPermissionId, "View Financial Audit", OrganizationPermissionCategory.ViewInformation, generalScopes);
            AddPermission(definitions, ids, PerformFinancialCorrectionPermissionId, "Perform Financial Correction", OrganizationPermissionCategory.ManageResources, generalScopes, jointAllowed: true);
            AddPermission(definitions, ids, SubmitDecisionProposalPermissionId, "Submit Decision Proposal", OrganizationPermissionCategory.ParticipateInGovernance, generalScopes);
            AddPermission(definitions, ids, AmendDecisionProposalPermissionId, "Amend Decision Proposal", OrganizationPermissionCategory.ParticipateInGovernance, generalScopes);
            AddPermission(definitions, ids, CastOrganizationVotePermissionId, "Cast Organization Vote", OrganizationPermissionCategory.ParticipateInGovernance, generalScopes);
            AddPermission(definitions, ids, CloseOrganizationVotePermissionId, "Close Organization Vote", OrganizationPermissionCategory.ParticipateInGovernance, generalScopes);
            AddPermission(definitions, ids, VetoOrganizationResolutionPermissionId, "Veto Organization Resolution", OrganizationPermissionCategory.ManagePolicy, generalScopes, jointAllowed: true);
            AddPermission(definitions, ids, OverrideOrganizationVetoPermissionId, "Override Organization Veto", OrganizationPermissionCategory.ManagePolicy, generalScopes, jointAllowed: true);
            AddPermission(definitions, ids, ExecuteOrganizationResolutionPermissionId, "Execute Organization Resolution", OrganizationPermissionCategory.ManagePolicy, generalScopes);
            AddPermission(definitions, ids, IssueEmergencyDecisionPermissionId, "Issue Emergency Decision", OrganizationPermissionCategory.ManagePolicy, generalScopes, jointAllowed: true);
            AddPermission(definitions, ids, ViewConfidentialDecisionsPermissionId, "View Confidential Decisions", OrganizationPermissionCategory.ViewInformation, generalScopes);
            AddPermission(definitions, ids, EnactMunicipalLawPermissionId, "Enact Municipal Law", OrganizationPermissionCategory.ExerciseLegalAuthority, generalScopes);
            AddPermission(definitions, ids, IssueWarrantPermissionId, "Issue Warrant", OrganizationPermissionCategory.ExerciseLegalAuthority, generalScopes);
            AddPermission(definitions, ids, AdministerJusticePermissionId, "Administer Justice", OrganizationPermissionCategory.ExerciseLegalAuthority, generalScopes);
            AddPermission(definitions, ids, RecordOfficialCrimeDiscoveryPermissionId, "Record Official Crime Discovery", OrganizationPermissionCategory.ExerciseLegalAuthority, generalScopes, canDelegate: true);
            return definitions;
        }

        public static IReadOnlyList<InstitutionalActionDefinition> CreateMissingActionDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = Set(existingDefinitionIds);
            List<InstitutionalActionDefinition> definitions = new List<InstitutionalActionDefinition>();
            AddAction(definitions, ids, ReviewMembershipApplicationActionId, "Review Membership Application", InstitutionalActionCategory.Membership, new[] { ReviewMembershipApplicationsPermissionId }, target: "organization-membership");
            AddAction(definitions, ids, InviteMemberActionId, "Invite Member", InstitutionalActionCategory.Membership, new[] { InviteMembersPermissionId }, target: "person");
            AddAction(definitions, ids, AdmitMemberActionId, "Admit Member", InstitutionalActionCategory.Membership, new[] { AdmitMembersPermissionId }, target: "organization-membership");
            AddAction(definitions, ids, SuspendMemberActionId, "Suspend Member", InstitutionalActionCategory.Membership, new[] { SuspendMembersPermissionId }, target: "organization-membership");
            AddAction(definitions, ids, ReinstateMemberActionId, "Reinstate Member", InstitutionalActionCategory.Membership, new[] { ReinstateMembersPermissionId }, target: "organization-membership");
            AddAction(definitions, ids, RemoveMemberActionId, "Remove Member", InstitutionalActionCategory.Membership, new[] { RemoveMembersPermissionId }, target: "organization-membership");
            AddAction(definitions, ids, AssignRankActionId, "Assign Rank", InstitutionalActionCategory.Rank, new[] { AssignRanksPermissionId }, target: "organization-rank");
            AddAction(definitions, ids, PromoteMemberActionId, "Promote Member", InstitutionalActionCategory.Rank, new[] { PromoteMembersPermissionId }, target: "organization-rank");
            AddAction(definitions, ids, DemoteMemberActionId, "Demote Member", InstitutionalActionCategory.Rank, new[] { DemoteMembersPermissionId }, target: "organization-rank");
            AddAction(definitions, ids, CreateOfficeActionId, "Create Office", InstitutionalActionCategory.Office, new[] { CreateOfficesPermissionId }, target: "organization-office");
            AddAction(definitions, ids, AppointOfficeholderActionId, "Appoint Officeholder", InstitutionalActionCategory.Office, new[] { AppointOfficeholdersPermissionId }, target: "organization-office");
            AddAction(definitions, ids, RemoveOfficeholderActionId, "Remove Officeholder", InstitutionalActionCategory.Office, new[] { RemoveOfficeholdersPermissionId }, target: "organization-office");
            AddAction(definitions, ids, AssignActingOfficeholderActionId, "Assign Acting Officeholder", InstitutionalActionCategory.Office, new[] { AssignActingOfficeholdersPermissionId }, target: "organization-office");
            AddAction(definitions, ids, RenameOrganizationActionId, "Rename Organization", InstitutionalActionCategory.OrganizationIdentity, new[] { ManageAliasesPermissionId }, target: "organization");
            AddAction(definitions, ids, ChangeHeadquartersActionId, "Change Headquarters", InstitutionalActionCategory.OrganizationIdentity, new[] { ProposeHeadquartersPermissionId, AuthorizeHeadquartersPermissionId }, OrganizationPermissionCombinationPolicy.JointApproval, target: "place", approvals: 2);
            AddAction(definitions, ids, CreateBranchActionId, "Create Organization Branch", InstitutionalActionCategory.OrganizationHierarchy, new[] { IssueOrdersPermissionId }, target: "organization");
            AddAction(definitions, ids, EndBranchLinkActionId, "End Organization Branch Link", InstitutionalActionCategory.OrganizationHierarchy, new[] { IssueOrdersPermissionId }, target: "organization");
            AddAction(definitions, ids, IssueOrderActionId, "Issue Institutional Order", InstitutionalActionCategory.Command, new[] { IssueOrdersPermissionId }, target: "order");
            AddAction(definitions, ids, AccessRestrictedRecordActionId, "Access Restricted Record", InstitutionalActionCategory.InformationAccess, new[] { ViewRestrictedInformationPermissionId }, target: "record", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, AccessSecretRecordActionId, "Access Secret Record", InstitutionalActionCategory.InformationAccess, new[] { ViewSecretInformationPermissionId }, target: "record", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, DelegatePermissionActionId, "Delegate Permission", InstitutionalActionCategory.Delegation, new[] { DelegatePermissionsPermissionId }, target: "authority-grant");
            AddAction(definitions, ids, RevokeDelegationActionId, "Revoke Delegation", InstitutionalActionCategory.Delegation, new[] { RevokeDelegationsPermissionId }, target: "authority-grant");
            AddAction(definitions, ids, CreateTreasuryActionId, "Create Treasury", InstitutionalActionCategory.Financial, new[] { CreateTreasuryAccountPermissionId }, target: "organization-treasury");
            AddAction(definitions, ids, CreateTreasuryAccountActionId, "Create Treasury Account", InstitutionalActionCategory.Financial, new[] { CreateTreasuryAccountPermissionId }, target: "organization-account");
            AddAction(definitions, ids, ManageTreasuryAccountActionId, "Manage Treasury Account", InstitutionalActionCategory.Financial, new[] { CloseTreasuryAccountPermissionId }, target: "organization-account");
            AddAction(definitions, ids, DepositOrganizationFundsActionId, "Deposit Organization Funds", InstitutionalActionCategory.Financial, new[] { DepositOrganizationFundsPermissionId }, target: "organization-account");
            AddAction(definitions, ids, WithdrawOrganizationFundsActionId, "Withdraw Organization Funds", InstitutionalActionCategory.Financial, new[] { WithdrawOrganizationFundsPermissionId }, target: "organization-account");
            AddAction(definitions, ids, TransferOrganizationFundsActionId, "Transfer Organization Funds", InstitutionalActionCategory.Financial, new[] { TransferOrganizationFundsPermissionId }, target: "organization-account");
            AddAction(definitions, ids, LargeOrganizationTransferActionId, "Approve Large Organization Transfer", InstitutionalActionCategory.Financial, new[] { TransferOrganizationFundsPermissionId, ApproveOrganizationExpensePermissionId }, OrganizationPermissionCombinationPolicy.JointApproval, target: "organization-account", approvals: 2, audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, ManageRestrictedFundsActionId, "Manage Restricted Funds", InstitutionalActionCategory.Financial, new[] { ManageRestrictedFundsPermissionId }, target: "organization-fund-restriction");
            AddAction(definitions, ids, ManageOrganizationBudgetActionId, "Manage Organization Budget Record", InstitutionalActionCategory.Financial, new[] { ManageRestrictedFundsPermissionId }, target: "organization-budget");
            AddAction(definitions, ids, ManageResourceReservationActionId, "Manage Resource Reservation", InstitutionalActionCategory.Financial, new[] { ManageResourceReservationsPermissionId }, target: "organization-reservation");
            AddAction(definitions, ids, ManageOrganizationInventoryActionId, "Manage Organization Inventory", InstitutionalActionCategory.Financial, new[] { ManageOrganizationInventoryPermissionId }, target: "organization-inventory");
            AddAction(definitions, ids, AssignAssetCustodyActionId, "Assign Asset Custody", InstitutionalActionCategory.Financial, new[] { AssignAssetCustodyPermissionId }, target: "organization-custody");
            AddAction(definitions, ids, ManagePropertyAssociationActionId, "Manage Property Association", InstitutionalActionCategory.Financial, new[] { ProposeHeadquartersPermissionId }, target: "property");
            AddAction(definitions, ids, ManageBusinessAssociationActionId, "Manage Business Association", InstitutionalActionCategory.Financial, new[] { ManageBusinessAssociationPermissionId }, target: "business");
            AddAction(definitions, ids, ViewFinancialAuditActionId, "View Financial Audit", InstitutionalActionCategory.Financial, new[] { ViewFinancialAuditPermissionId }, target: "organization-financial-audit", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, PerformFinancialCorrectionActionId, "Perform Financial Correction", InstitutionalActionCategory.Financial, new[] { PerformFinancialCorrectionPermissionId, ApproveOrganizationExpensePermissionId }, OrganizationPermissionCombinationPolicy.JointApproval, target: "organization-account", approvals: 2, audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, SubmitDecisionProposalActionId, "Submit Decision Proposal", InstitutionalActionCategory.Governance, new[] { SubmitDecisionProposalPermissionId }, target: "organization-proposal");
            AddAction(definitions, ids, AmendDecisionProposalActionId, "Amend Decision Proposal", InstitutionalActionCategory.Governance, new[] { AmendDecisionProposalPermissionId }, target: "organization-proposal");
            AddAction(definitions, ids, CastOrganizationVoteActionId, "Cast Organization Vote", InstitutionalActionCategory.Governance, new[] { CastOrganizationVotePermissionId }, target: "organization-vote");
            AddAction(definitions, ids, CloseOrganizationVoteActionId, "Close Organization Vote", InstitutionalActionCategory.Governance, new[] { CloseOrganizationVotePermissionId }, target: "organization-proposal", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, VetoOrganizationResolutionActionId, "Veto Organization Resolution", InstitutionalActionCategory.Governance, new[] { VetoOrganizationResolutionPermissionId }, target: "organization-resolution", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, OverrideOrganizationVetoActionId, "Override Organization Veto", InstitutionalActionCategory.Governance, new[] { OverrideOrganizationVetoPermissionId, VetoOrganizationResolutionPermissionId }, OrganizationPermissionCombinationPolicy.JointApproval, target: "organization-resolution", approvals: 2, audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, ExecuteOrganizationResolutionActionId, "Execute Organization Resolution", InstitutionalActionCategory.Governance, new[] { ExecuteOrganizationResolutionPermissionId }, target: "organization-resolution", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, IssueEmergencyDecisionActionId, "Issue Emergency Decision", InstitutionalActionCategory.Governance, new[] { IssueEmergencyDecisionPermissionId, ExecuteOrganizationResolutionPermissionId }, OrganizationPermissionCombinationPolicy.JointApproval, target: "organization-emergency-decision", approvals: 2, audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, ViewConfidentialDecisionActionId, "View Confidential Decision", InstitutionalActionCategory.InformationAccess, new[] { ViewConfidentialDecisionsPermissionId }, target: "organization-decision");
            AddAction(definitions, ids, EnactMunicipalLawActionId, "Enact Municipal Law", InstitutionalActionCategory.Legal, new[] { EnactMunicipalLawPermissionId }, target: "legal-instrument", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, IssueWarrantActionId, "Issue Warrant", InstitutionalActionCategory.Legal, new[] { IssueWarrantPermissionId }, target: "warrant", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, AdministerJusticeActionId, "Administer Justice", InstitutionalActionCategory.Legal, new[] { AdministerJusticePermissionId }, target: "justice-record", audit: OrganizationAuthorityAuditPolicy.Always);
            AddAction(definitions, ids, RecordOfficialCrimeDiscoveryActionId, "Record Official Crime Discovery", InstitutionalActionCategory.Legal, new[] { RecordOfficialCrimeDiscoveryPermissionId }, target: "crime-incident", audit: OrganizationAuthorityAuditPolicy.Always);
            return definitions;
        }

        public static IReadOnlyList<OrganizationAuthorityRoleDefinition> CreateMissingRoleDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = Set(existingDefinitionIds);
            List<OrganizationAuthorityRoleDefinition> definitions = new List<OrganizationAuthorityRoleDefinition>();
            AddRole(definitions, ids, GeneralMemberRoleId, "General Member Authority", new[] { ViewPublicInformationPermissionId, SubmitDecisionProposalPermissionId, AmendDecisionProposalPermissionId, CastOrganizationVotePermissionId }, priority: 10);
            AddRole(definitions, ids, MembershipOfficerRoleId, "Membership Officer Authority", new[] { ViewPublicInformationPermissionId, ReviewMembershipApplicationsPermissionId, InviteMembersPermissionId, AdmitMembersPermissionId, SuspendMembersPermissionId, ReinstateMembersPermissionId, RemoveMembersPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 80);
            AddRole(definitions, ids, RankOfficerRoleId, "Rank Officer Authority", new[] { ViewPublicInformationPermissionId, AssignRanksPermissionId, PromoteMembersPermissionId, DemoteMembersPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 90);
            AddRole(definitions, ids, TreasurerRoleId, "Treasurer Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, ViewTreasuryPermissionId, ViewRestrictedTreasuryPermissionId, CreateTreasuryAccountPermissionId, CloseTreasuryAccountPermissionId, DepositOrganizationFundsPermissionId, WithdrawOrganizationFundsPermissionId, TransferOrganizationFundsPermissionId, ApproveOrganizationExpensePermissionId, ManageRestrictedFundsPermissionId, ManageResourceReservationsPermissionId, ManageOrganizationInventoryPermissionId, AssignAssetCustodyPermissionId, ManageBusinessAssociationPermissionId, ViewFinancialAuditPermissionId, ProposeHeadquartersPermissionId }, priority: 90);
            AddRole(definitions, ids, ChapterMasterRoleId, "Chapter Master Authority", new[] { ViewPublicInformationPermissionId, ReviewMembershipApplicationsPermissionId, InviteMembersPermissionId, AdmitMembersPermissionId, AssignActingOfficeholdersPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 100);
            AddRole(definitions, ids, GuildmasterRoleId, "Guildmaster Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, ReviewMembershipApplicationsPermissionId, InviteMembersPermissionId, AdmitMembersPermissionId, SuspendMembersPermissionId, ReinstateMembersPermissionId, RemoveMembersPermissionId, AssignRanksPermissionId, PromoteMembersPermissionId, DemoteMembersPermissionId, CreateOfficesPermissionId, AppointOfficeholdersPermissionId, RemoveOfficeholdersPermissionId, AssignActingOfficeholdersPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId, ManageAliasesPermissionId, ProposeHeadquartersPermissionId, AuthorizeHeadquartersPermissionId, DelegatePermissionsPermissionId, RevokeDelegationsPermissionId, ViewTreasuryPermissionId, ViewRestrictedTreasuryPermissionId, CreateTreasuryAccountPermissionId, CloseTreasuryAccountPermissionId, DepositOrganizationFundsPermissionId, WithdrawOrganizationFundsPermissionId, TransferOrganizationFundsPermissionId, ApproveOrganizationExpensePermissionId, ManageRestrictedFundsPermissionId, ManageResourceReservationsPermissionId, ManageOrganizationInventoryPermissionId, AssignAssetCustodyPermissionId, ManageBusinessAssociationPermissionId, ViewFinancialAuditPermissionId, PerformFinancialCorrectionPermissionId, SubmitDecisionProposalPermissionId, AmendDecisionProposalPermissionId, CastOrganizationVotePermissionId, CloseOrganizationVotePermissionId, VetoOrganizationResolutionPermissionId, OverrideOrganizationVetoPermissionId, ExecuteOrganizationResolutionPermissionId, IssueEmergencyDecisionPermissionId, ViewConfidentialDecisionsPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.Redelegable, priority: 150);
            AddRole(definitions, ids, GuildReceptionistRoleId, "Guild Receptionist Authority", new[] { ViewPublicInformationPermissionId, ReviewMembershipApplicationsPermissionId, AdmitMembersPermissionId }, priority: 60);
            AddRole(definitions, ids, RecordClerkRoleId, "Record Clerk Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId }, priority: 70);
            AddRole(definitions, ids, HeadmasterRoleId, "Headmaster Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, InviteMembersPermissionId, AdmitMembersPermissionId, CreateOfficesPermissionId, AppointOfficeholdersPermissionId, RemoveOfficeholdersPermissionId, AssignActingOfficeholdersPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId, SubmitDecisionProposalPermissionId, AmendDecisionProposalPermissionId, CastOrganizationVotePermissionId, ExecuteOrganizationResolutionPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 140);
            AddRole(definitions, ids, ActingExecutiveRoleId, "Acting Executive Authority", new[] { ViewPublicInformationPermissionId, ReviewMembershipApplicationsPermissionId, InviteMembersPermissionId, AdmitMembersPermissionId, AssignActingOfficeholdersPermissionId, IssueOrdersPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 120);
            AddRole(definitions, ids, SecretMemberRoleId, "Secret Member Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, ViewSecretInformationPermissionId }, visibility: OrganizationVisibility.Hidden, priority: 60);
            AddRole(definitions, ids, MilitaryOfficerRoleId, "Military Officer Authority", new[] { ViewPublicInformationPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId, RecordOfficialCrimeDiscoveryPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 100);
            AddRole(definitions, ids, NobleRoleId, "Noble Authority", new[] { ViewPublicInformationPermissionId, RepresentExternallyPermissionId }, priority: 60);
            AddRole(definitions, ids, VillageChiefRoleId, "Village Chief Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId, SubmitDecisionProposalPermissionId, CastOrganizationVotePermissionId, RecordOfficialCrimeDiscoveryPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 140);
            AddRole(definitions, ids, MayorRoleId, "Mayor Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, CreateOfficesPermissionId, AppointOfficeholdersPermissionId, RemoveOfficeholdersPermissionId, AssignActingOfficeholdersPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId, ViewTreasuryPermissionId, CreateTreasuryAccountPermissionId, ApproveOrganizationExpensePermissionId, ManageRestrictedFundsPermissionId, SubmitDecisionProposalPermissionId, CastOrganizationVotePermissionId, ExecuteOrganizationResolutionPermissionId, EnactMunicipalLawPermissionId, RecordOfficialCrimeDiscoveryPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 160);
            AddRole(definitions, ids, ManorLordRoleId, "Manor Lord Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, CreateOfficesPermissionId, AppointOfficeholdersPermissionId, RemoveOfficeholdersPermissionId, AssignActingOfficeholdersPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId, DelegatePermissionsPermissionId, ViewTreasuryPermissionId, ViewRestrictedTreasuryPermissionId, CreateTreasuryAccountPermissionId, ApproveOrganizationExpensePermissionId, ManageRestrictedFundsPermissionId, SubmitDecisionProposalPermissionId, CastOrganizationVotePermissionId, VetoOrganizationResolutionPermissionId, ExecuteOrganizationResolutionPermissionId, EnactMunicipalLawPermissionId, RecordOfficialCrimeDiscoveryPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation, priority: 170);
            AddRole(definitions, ids, DukeRoleId, "Ducal Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, CreateOfficesPermissionId, AppointOfficeholdersPermissionId, RemoveOfficeholdersPermissionId, AssignActingOfficeholdersPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId, DelegatePermissionsPermissionId, RevokeDelegationsPermissionId, ViewTreasuryPermissionId, ViewRestrictedTreasuryPermissionId, CreateTreasuryAccountPermissionId, ApproveOrganizationExpensePermissionId, ManageRestrictedFundsPermissionId, SubmitDecisionProposalPermissionId, CastOrganizationVotePermissionId, VetoOrganizationResolutionPermissionId, ExecuteOrganizationResolutionPermissionId, IssueEmergencyDecisionPermissionId, EnactMunicipalLawPermissionId, RecordOfficialCrimeDiscoveryPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.Redelegable, priority: 180);
            AddRole(definitions, ids, MonarchRoleId, "Royal Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, ViewSecretInformationPermissionId, CreateOfficesPermissionId, AppointOfficeholdersPermissionId, RemoveOfficeholdersPermissionId, AssignActingOfficeholdersPermissionId, IssueOrdersPermissionId, RepresentExternallyPermissionId, DelegatePermissionsPermissionId, RevokeDelegationsPermissionId, ViewTreasuryPermissionId, ViewRestrictedTreasuryPermissionId, CreateTreasuryAccountPermissionId, CloseTreasuryAccountPermissionId, ApproveOrganizationExpensePermissionId, ManageRestrictedFundsPermissionId, SubmitDecisionProposalPermissionId, CastOrganizationVotePermissionId, VetoOrganizationResolutionPermissionId, OverrideOrganizationVetoPermissionId, ExecuteOrganizationResolutionPermissionId, IssueEmergencyDecisionPermissionId, EnactMunicipalLawPermissionId, RecordOfficialCrimeDiscoveryPermissionId }, delegation: OrganizationAuthorityDelegationPolicy.Redelegable, priority: 200);
            AddRole(definitions, ids, MagistrateRoleId, "Magistrate Authority", new[] { ViewPublicInformationPermissionId, ViewRestrictedInformationPermissionId, IssueWarrantPermissionId, AdministerJusticePermissionId, RecordOfficialCrimeDiscoveryPermissionId }, priority: 150);
            return definitions;
        }

        public static IReadOnlyList<OrganizationAuthorityBindingDefinition> CreateMissingBindingDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = Set(existingDefinitionIds);
            List<OrganizationAuthorityBindingDefinition> definitions = new List<OrganizationAuthorityBindingDefinition>();
            AddBinding(definitions, ids, GuildFullMemberBindingId, "Guild Full Member Authority", OrganizationAuthorityBindingSourceType.MembershipDefinition, PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, GeneralMemberRoleId, OrganizationAuthorityScopeType.EntireOrganization);
            AddBinding(definitions, ids, GuildSSSRankBindingId, "Guild SSS Rank Authority", OrganizationAuthorityBindingSourceType.RankDefinition, PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId, RankOfficerRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 95);
            AddBinding(definitions, ids, GuildmasterOfficeBindingId, "Guildmaster Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, GuildmasterRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 160);
            AddBinding(definitions, ids, GuildReceptionistOfficeBindingId, "Guild Receptionist Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.GuildReceptionistOfficeId, GuildReceptionistRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 70);
            AddBinding(definitions, ids, GuildTreasurerOfficeBindingId, "Guild Treasurer Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.GuildTreasurerOfficeId, TreasurerRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 95);
            AddBinding(definitions, ids, BranchChapterMasterOfficeBindingId, "Branch Chapter Master Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.BranchChapterMasterOfficeId, ChapterMasterRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 110);
            AddBinding(definitions, ids, GuardCaptainOfficeBindingId, "Guard Captain Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.GuardCaptainOfficeId, MilitaryOfficerRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 110);
            AddBinding(definitions, ids, SecretMemberBindingId, "Secret Membership Authority", OrganizationAuthorityBindingSourceType.MembershipDefinition, PrototypeOrganizationMembershipDefinitionFactory.SecretMemberId, SecretMemberRoleId, OrganizationAuthorityScopeType.EntireOrganization, bindingVisibility: OrganizationVisibility.Hidden, priority: 60);
            AddBinding(definitions, ids, NobleOfficeBindingId, "Noble Title Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.NobleTitleOfficeId, NobleRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 70);
            AddBinding(definitions, ids, VillageChiefOfficeBindingId, "Village Chief Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.VillageChiefOfficeId, VillageChiefRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 150);
            AddBinding(definitions, ids, MayorOfficeBindingId, "Mayor Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.MayorOfficeId, MayorRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 170);
            AddBinding(definitions, ids, ManorLordOfficeBindingId, "Manor Lord Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.ManorLordOfficeId, ManorLordRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 180);
            AddBinding(definitions, ids, DukeOfficeBindingId, "Duke or Duchess Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.DukeOfficeId, DukeRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 190);
            AddBinding(definitions, ids, MonarchOfficeBindingId, "King or Queen Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.MonarchOfficeId, MonarchRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 210);
            AddBinding(definitions, ids, MagistrateOfficeBindingId, "Magistrate Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.MagistrateOfficeId, MagistrateRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 160);
            AddBinding(definitions, ids, UniversityHeadmasterOfficeBindingId, "University Headmaster Office Authority", OrganizationAuthorityBindingSourceType.OfficeDefinition, PrototypeOrganizationMembershipDefinitionFactory.UniversityHeadmasterOfficeId, HeadmasterRoleId, OrganizationAuthorityScopeType.EntireOrganization, priority: 150);
            return definitions;
        }

        private static void AddPermission(ICollection<OrganizationPermissionDefinition> definitions, ISet<string> ids, string id, string name, OrganizationPermissionCategory category, IEnumerable<OrganizationAuthorityScopeType> scopes, bool canDelegate = false, bool canRedelegate = false, bool jointAllowed = false, OrganizationVisibility visibility = OrganizationVisibility.Public)
        {
            if (ids.Contains(id))
            {
                return;
            }

            OrganizationPermissionDefinition definition = ScriptableObject.CreateInstance<OrganizationPermissionDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, scopeTypes: scopes, canDelegate: canDelegate, canRedelegate: canRedelegate, jointAllowed: jointAllowed, permissionVisibility: visibility, tagIds: new[] { "prototype", "organization", "authority" });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddAction(ICollection<InstitutionalActionDefinition> definitions, ISet<string> ids, string id, string name, InstitutionalActionCategory category, IEnumerable<string> permissions, OrganizationPermissionCombinationPolicy policy = OrganizationPermissionCombinationPolicy.AllRequiredPermissions, string target = "", int approvals = 1, OrganizationAuthorityAuditPolicy audit = OrganizationAuthorityAuditPolicy.SuccessfulActions)
        {
            if (ids.Contains(id))
            {
                return;
            }

            InstitutionalActionDefinition definition = ScriptableObject.CreateInstance<InstitutionalActionDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, permissions, policy, target, approvals: approvals, audit: audit, tagIds: new[] { "prototype", "organization", "authority" });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddRole(ICollection<OrganizationAuthorityRoleDefinition> definitions, ISet<string> ids, string id, string name, IEnumerable<string> grants, OrganizationAuthorityDelegationPolicy delegation = OrganizationAuthorityDelegationPolicy.NonDelegable, int priority = 100, OrganizationVisibility visibility = OrganizationVisibility.Public)
        {
            if (ids.Contains(id))
            {
                return;
            }

            OrganizationAuthorityRoleDefinition definition = ScriptableObject.CreateInstance<OrganizationAuthorityRoleDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, grants, scopeType: OrganizationAuthorityScopeType.EntireOrganization, delegation: delegation, rolePriority: priority, roleVisibility: visibility, tagIds: new[] { "prototype", "organization", "authority" });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddBinding(ICollection<OrganizationAuthorityBindingDefinition> definitions, ISet<string> ids, string id, string name, OrganizationAuthorityBindingSourceType sourceType, string sourceDefinition, string authorityRole, OrganizationAuthorityScopeType scopeType, int priority = 100, OrganizationVisibility bindingVisibility = OrganizationVisibility.Public)
        {
            if (ids.Contains(id))
            {
                return;
            }

            OrganizationAuthorityBindingDefinition definition = ScriptableObject.CreateInstance<OrganizationAuthorityBindingDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, sourceType, sourceDefinition, authorityRole, authorityScopeType: scopeType, bindingPriority: priority, bindingVisibility: bindingVisibility, tagIds: new[] { "prototype", "organization", "authority" });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static HashSet<string> Set(IEnumerable<string> ids)
        {
            return ids == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(ids.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }
    }
}
