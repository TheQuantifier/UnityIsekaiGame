using System;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;

namespace UnityIsekaiGame.Organizations.Integration
{
    [Serializable]
    public sealed class Step13InstitutionalSimulationSettings
    {
        public bool enabled = true;
        public double evaluationIntervalSeconds = 1d;
        public int maximumOperationsPerRuntime = 64;
        public int maximumCatchUpBoundaries = 8;

        public void Normalize()
        {
            evaluationIntervalSeconds = Math.Max(0.1d, evaluationIntervalSeconds);
            maximumOperationsPerRuntime = Math.Max(1, Math.Min(512, maximumOperationsPerRuntime));
            maximumCatchUpBoundaries = Math.Max(1, Math.Min(64, maximumCatchUpBoundaries));
        }
    }

    public sealed class Step13InstitutionalSimulationCoordinator
    {
        private readonly Action<string> markDirty;
        private Step13InstitutionalSimulationSettings settings;
        private OrganizationRuntime organizations;
        private OrganizationMembershipRuntime memberships;
        private OrganizationAuthorityRuntime authority;
        private OrganizationResourceRuntime resources;
        private OrganizationDecisionRuntime decisions;
        private FactionRuntime factions;
        private DiplomacyRuntime diplomacy;
        private GovernmentRuntime governments;
        private LegalRuntime laws;
        private CrimeRuntime crimes;
        private JusticeRuntime justice;
        private long lastBoundary = -1L;
        private string lastRevisionFingerprint = string.Empty;

        public Step13InstitutionalSimulationCoordinator(Action<string> dirtyCallback, Step13InstitutionalSimulationSettings simulationSettings = null)
        {
            markDirty = dirtyCallback;
            settings = simulationSettings ?? new Step13InstitutionalSimulationSettings();
            settings.Normalize();
        }

        public long LastProcessedBoundary => lastBoundary;

        public void Rebind(
            OrganizationRuntime organizationRuntime,
            OrganizationMembershipRuntime membershipRuntime,
            OrganizationAuthorityRuntime authorityRuntime,
            OrganizationResourceRuntime resourceRuntime,
            OrganizationDecisionRuntime decisionRuntime,
            FactionRuntime factionRuntime,
            DiplomacyRuntime diplomacyRuntime,
            GovernmentRuntime governmentRuntime,
            LegalRuntime legalRuntime,
            CrimeRuntime crimeRuntime,
            JusticeRuntime justiceRuntime,
            Step13InstitutionalSimulationSettings simulationSettings = null)
        {
            organizations = organizationRuntime;
            memberships = membershipRuntime;
            authority = authorityRuntime;
            resources = resourceRuntime;
            decisions = decisionRuntime;
            factions = factionRuntime;
            diplomacy = diplomacyRuntime;
            governments = governmentRuntime;
            laws = legalRuntime;
            crimes = crimeRuntime;
            justice = justiceRuntime;
            if (simulationSettings != null) settings = simulationSettings;
            settings.Normalize();
            lastRevisionFingerprint = RevisionFingerprint();
        }

        public void Advance(double worldTime)
        {
            if (!settings.enabled || worldTime < 0d || organizations == null || justice == null) return;

            long boundary = (long)Math.Floor(worldTime / settings.evaluationIntervalSeconds);
            if (boundary <= lastBoundary) return;
            DetectExternalMutation();
            long first = lastBoundary < 0L ? boundary : Math.Max(lastBoundary + 1L, boundary - settings.maximumCatchUpBoundaries + 1L);
            for (long current = first; current <= boundary; current++) ProcessBoundary(current, current * settings.evaluationIntervalSeconds);
            lastBoundary = boundary;
            DetectExternalMutation();
        }

        private void ProcessBoundary(long boundary, double worldTime)
        {
            int max = settings.maximumOperationsPerRuntime;
            resources?.EvaluateTime(worldTime);
            decisions?.ProcessScheduled(worldTime, max);
            governments?.ProcessWorldTime(new PoliticalTimeEvaluationRequest { transactionId = $"institution.time.government.{boundary}", boundaryId = $"institution.boundary.{boundary}", worldTime = worldTime });
            laws?.ProcessWorldTime(new LegalTimeEvaluationRequest { transactionId = $"institution.time.law.{boundary}", boundaryId = $"institution.boundary.{boundary}", worldTime = worldTime, maximumOperations = max });
            crimes?.ProcessWorldTime(new CrimeTimeEvaluationRequest { transactionId = $"institution.time.crime.{boundary}", boundaryId = $"institution.boundary.{boundary}", worldTime = worldTime, maximumOperations = max });
            justice?.ProcessWorldTime(new JusticeTimeEvaluationRequest { transactionId = $"institution.time.justice.{boundary}", boundaryId = $"institution.boundary.{boundary}", worldTime = worldTime, maximumOperations = max });
        }

        private void DetectExternalMutation()
        {
            string current = RevisionFingerprint();
            if (!string.IsNullOrEmpty(lastRevisionFingerprint) && !string.Equals(current, lastRevisionFingerprint, StringComparison.Ordinal))
            {
                markDirty?.Invoke("Institutional world state changed.");
            }
            lastRevisionFingerprint = current;
        }

        private string RevisionFingerprint() => string.Join("|", new[]
        {
            organizations?.Revision ?? -1L,
            memberships?.Revision ?? -1L,
            authority?.Revision ?? -1L,
            resources?.Revision ?? -1L,
            decisions?.Revision ?? -1L,
            factions?.Revision ?? -1L,
            diplomacy?.Revision ?? -1L,
            governments?.Revision ?? -1L,
            laws?.Revision ?? -1L,
            crimes?.Revision ?? -1L,
            justice?.Revision ?? -1L
        });
    }
}
