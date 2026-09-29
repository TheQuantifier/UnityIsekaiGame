using System.Collections.Generic;
using UnityIsekaiGame.Combat.Defense;

namespace UnityIsekaiGame.Combat.Integration
{
    public static class CombatTransactionValidator
    {
        public static CombatTransactionTraceSnapshot BuildTrace(AttackResolutionResult result)
        {
            if (result == null)
            {
                return new CombatTransactionTraceSnapshot(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null);
            }

            string attack = result.AttackTransactionId;
            string defense = result.DefenseResult == null ? string.Empty : result.DefenseResult.Request.TransactionId;
            string damage = result.DamageTransactionId;
            List<CombatRuntimeDiagnostic> diagnostics = new List<CombatRuntimeDiagnostic>();
            ValidateChild(attack, damage, "DamageTransaction", diagnostics);
            ValidateChild(attack, defense, "DefenseTransaction", diagnostics);
            return new CombatTransactionTraceSnapshot(attack, string.Empty, attack, defense, damage, string.Empty, string.Empty, diagnostics);
        }

        public static CombatTransactionTraceSnapshot BuildTrace(DamageApplicationResult result)
        {
            if (result == null)
            {
                return new CombatTransactionTraceSnapshot(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null);
            }

            string root = result.Request.TransactionId;
            return new CombatTransactionTraceSnapshot(root, string.Empty, string.Empty, string.Empty, root, string.Empty, string.Empty, null);
        }

        public static CombatIntegrityReport Validate(CombatTransactionTraceSnapshot trace)
        {
            List<CombatRuntimeDiagnostic> diagnostics = new List<CombatRuntimeDiagnostic>();
            if (trace != null)
            {
                ValidateChild(trace.RootTransactionId, trace.ExecutionTransactionId, "ExecutionTransaction", diagnostics);
                ValidateChild(trace.RootTransactionId, trace.AttackTransactionId, "AttackTransaction", diagnostics);
                ValidateChild(trace.RootTransactionId, trace.DefenseTransactionId, "DefenseTransaction", diagnostics);
                ValidateChild(trace.RootTransactionId, trace.DamageTransactionId, "DamageTransaction", diagnostics);
                ValidateChild(trace.RootTransactionId, trace.ReactionTransactionId, "ReactionTransaction", diagnostics);
                ValidateChild(trace.RootTransactionId, trace.ContributionTransactionId, "ContributionTransaction", diagnostics);
            }

            return new CombatIntegrityReport(diagnostics);
        }

        private static void ValidateChild(string root, string child, string label, List<CombatRuntimeDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(child) || string.Equals(root, child, System.StringComparison.Ordinal))
            {
                return;
            }

            if (!child.StartsWith(root + ".", System.StringComparison.Ordinal))
            {
                diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Warning, CombatSubsystem.Transactions, "UnexpectedTransactionAncestry", $"{label} '{child}' is not a deterministic child of root '{root}'."));
            }
        }
    }
}
