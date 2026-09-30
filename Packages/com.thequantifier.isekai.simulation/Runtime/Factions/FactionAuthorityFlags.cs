using System;

namespace UnityIsekaiGame.Factions
{
    [Flags]
    public enum FactionAuthorityFlags
    {
        None = 0,
        IssueContracts = 1 << 0,
        ApproveContracts = 1 << 1,
        AssignRanks = 1 << 2,
        SetLocalRules = 1 << 3,
        CollectTaxes = 1 << 4,
        EnforceLaw = 1 << 5,
        OperateMarkets = 1 << 6,
        CommandGuards = 1 << 7,
        OwnProperty = 1 << 8,
        GrantLicenses = 1 << 9,
        ReviewReports = 1 << 10
    }
}
