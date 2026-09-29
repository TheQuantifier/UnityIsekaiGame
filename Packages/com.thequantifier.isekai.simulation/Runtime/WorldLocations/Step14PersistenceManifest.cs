using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public enum Step14PersistenceOwnerKind
    {
        Authoritative,
        Derived,
        External
    }

    public enum Step14PersistenceValidationSeverity
    {
        Info,
        Warning,
        Error,
        Fatal
    }

    public sealed class Step14PersistenceOwnerRecord
    {
        public Step14PersistenceOwnerRecord(string category, string ownerParticipantId, Step14PersistenceOwnerKind ownerKind, string notes)
        {
            Category = N(category);
            OwnerParticipantId = N(ownerParticipantId);
            OwnerKind = ownerKind;
            Notes = notes ?? string.Empty;
        }

        public string Category { get; }
        public string OwnerParticipantId { get; }
        public Step14PersistenceOwnerKind OwnerKind { get; }
        public string Notes { get; }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step14PersistenceParticipantManifest
    {
        public Step14PersistenceParticipantManifest(string participantId, string ownerRuntime, int schemaVersion, int expectedSchemaVersion, long revision, int authoritativeRecordCount, int historicalRecordCount, int transactionCount, IEnumerable<string> requiredDependencies, IEnumerable<string> optionalDependencies, bool present)
        {
            ParticipantId = N(participantId);
            OwnerRuntime = N(ownerRuntime);
            SchemaVersion = schemaVersion;
            ExpectedSchemaVersion = expectedSchemaVersion;
            Revision = revision;
            AuthoritativeRecordCount = Math.Max(0, authoritativeRecordCount);
            HistoricalRecordCount = Math.Max(0, historicalRecordCount);
            TransactionCount = Math.Max(0, transactionCount);
            RequiredDependencies = C(requiredDependencies);
            OptionalDependencies = C(optionalDependencies);
            Present = present;
        }

        public string ParticipantId { get; }
        public string OwnerRuntime { get; }
        public int SchemaVersion { get; }
        public int ExpectedSchemaVersion { get; }
        public long Revision { get; }
        public int AuthoritativeRecordCount { get; }
        public int HistoricalRecordCount { get; }
        public int TransactionCount { get; }
        public IReadOnlyList<string> RequiredDependencies { get; }
        public IReadOnlyList<string> OptionalDependencies { get; }
        public bool Present { get; }
        public bool SchemaCompatible => Present && SchemaVersion == ExpectedSchemaVersion;

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static IReadOnlyList<string> C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class Step14PersistenceValidationIssue
    {
        public Step14PersistenceValidationIssue(Step14PersistenceValidationSeverity severity, string participantId, string recordId, string message)
        {
            Severity = severity;
            ParticipantId = participantId ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public Step14PersistenceValidationSeverity Severity { get; }
        public string ParticipantId { get; }
        public string RecordId { get; }
        public string Message { get; }
    }

    public sealed class Step14PersistenceValidationReport
    {
        private readonly Step14PersistenceValidationIssue[] issues;

        public Step14PersistenceValidationReport(IEnumerable<Step14PersistenceValidationIssue> issues)
        {
            this.issues = (issues ?? Array.Empty<Step14PersistenceValidationIssue>())
                .Where(issue => issue != null)
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.ParticipantId, StringComparer.Ordinal)
                .ThenBy(issue => issue.RecordId, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<Step14PersistenceValidationIssue> Issues => issues.ToArray();
        public IReadOnlyList<Step14PersistenceValidationIssue> Errors => issues.Where(issue => issue.Severity == Step14PersistenceValidationSeverity.Error || issue.Severity == Step14PersistenceValidationSeverity.Fatal).ToArray();
        public IReadOnlyList<Step14PersistenceValidationIssue> Warnings => issues.Where(issue => issue.Severity == Step14PersistenceValidationSeverity.Warning).ToArray();
        public bool Succeeded => Errors.Count == 0;
        public string Summary => Succeeded ? "Step 14 persistence validation passed." : string.Join(" | ", Errors.Select(issue => issue.Message));
    }

    public sealed class Step14PersistenceManifest
    {
        public const int CurrentSchemaVersion = 1;

        public Step14PersistenceManifest(string worldId, string saveSlotId, double authoritativeWorldTime, IEnumerable<Step14PersistenceParticipantManifest> participants, IEnumerable<Step14PersistenceOwnerRecord> ownership, Step14PersistenceValidationReport validationReport)
        {
            WorldId = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim();
            SaveSlotId = saveSlotId ?? string.Empty;
            AuthoritativeWorldTime = authoritativeWorldTime;
            Participants = (participants ?? Array.Empty<Step14PersistenceParticipantManifest>()).Where(item => item != null).OrderBy(item => item.ParticipantId, StringComparer.Ordinal).ToArray();
            Ownership = (ownership ?? Array.Empty<Step14PersistenceOwnerRecord>()).Where(item => item != null).OrderBy(item => item.Category, StringComparer.Ordinal).ToArray();
            ValidationReport = validationReport ?? new Step14PersistenceValidationReport(Array.Empty<Step14PersistenceValidationIssue>());
        }

        public int SchemaVersion => CurrentSchemaVersion;
        public string WorldId { get; }
        public string SaveSlotId { get; }
        public double AuthoritativeWorldTime { get; }
        public IReadOnlyList<Step14PersistenceParticipantManifest> Participants { get; }
        public IReadOnlyList<Step14PersistenceOwnerRecord> Ownership { get; }
        public Step14PersistenceValidationReport ValidationReport { get; }
        public bool Succeeded => ValidationReport.Succeeded;
    }

    public sealed class Step14PersistenceSnapshotSource
    {
        public string worldId;
        public string saveSlotId;
        public double authoritativeWorldTime;
        public LocationRuntimeSaveData locations;
        public EntityLocationRuntimeSaveData entityLocations;
        public InteractionPointRuntimeSaveData interactionPoints;
        public LocationConnectionRuntimeSaveData connections;
        public LocationRouteRuntimeSaveData routes;
        public TravelJourneyRuntimeSaveData journeys;
        public TravelConditionRuntimeSaveData travelConditions;
        public PoliticalTravelRuntimeSaveData politicalTravel;

        public Step14PersistenceSnapshotSource Clone()
        {
            return new Step14PersistenceSnapshotSource
            {
                worldId = N(worldId),
                saveSlotId = saveSlotId ?? string.Empty,
                authoritativeWorldTime = authoritativeWorldTime,
                locations = locations?.Clone(),
                entityLocations = entityLocations?.Clone(),
                interactionPoints = interactionPoints?.Clone(),
                connections = connections?.Clone(),
                routes = routes?.Clone(),
                journeys = journeys?.Clone(),
                travelConditions = travelConditions?.Clone(),
                politicalTravel = politicalTravel?.Clone()
            };
        }

        public static Step14PersistenceSnapshotSource FromRuntimes(LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactionPoints, LocationConnectionRuntime connections, LocationRouteRuntime routes, TravelJourneyRuntime journeys, TravelConditionRuntime travelConditions, PoliticalTravelRuntime politicalTravel, string worldId, string saveSlotId = "", double authoritativeWorldTime = 0d)
        {
            return new Step14PersistenceSnapshotSource
            {
                worldId = N(worldId),
                saveSlotId = saveSlotId ?? string.Empty,
                authoritativeWorldTime = authoritativeWorldTime,
                locations = locations?.CreateSaveData(),
                entityLocations = entityLocations?.CreateSaveData(),
                interactionPoints = interactionPoints?.CreateSaveData(),
                connections = connections?.CreateSaveData(),
                routes = routes?.CreateSaveData(),
                journeys = journeys?.CreateSaveData(),
                travelConditions = travelConditions?.CreateSaveData(),
                politicalTravel = politicalTravel?.CreateSaveData()
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? PersistenceService.LocalWorldId : value.Trim();
    }

    public static class Step14PersistenceManifestBuilder
    {
        public const string LocationParticipantId = "step14.locations";
        public const string EntityLocationParticipantId = "step14.entity-locations";
        public const string InteractionPointParticipantId = "step14.interaction-points";
        public const string ConnectionParticipantId = "step14.connections";
        public const string RouteParticipantId = "step14.routes";
        public const string JourneyParticipantId = "step14.journeys";
        public const string TravelConditionParticipantId = "step14.travel-conditions";
        public const string PoliticalTravelParticipantId = "step14.political-travel";

        public static IReadOnlyList<Step14PersistenceOwnerRecord> OwnershipMap { get; } = BuildOwnershipMap();

        public static Step14PersistenceManifest Build(Step14PersistenceSnapshotSource source)
        {
            Step14PersistenceSnapshotSource snapshot = source?.Clone() ?? new Step14PersistenceSnapshotSource();
            string expectedWorldId = string.IsNullOrWhiteSpace(snapshot.worldId) ? PersistenceService.LocalWorldId : snapshot.worldId.Trim();
            List<Step14PersistenceParticipantManifest> participants = new List<Step14PersistenceParticipantManifest>
            {
                P(LocationParticipantId, "LocationRuntime", snapshot.locations?.schemaVersion ?? 0, LocationRuntimeSaveData.CurrentSchemaVersion, snapshot.locations?.revision ?? 0L, snapshot.locations?.records?.Count ?? 0, (snapshot.locations?.names?.Count ?? 0) + (snapshot.locations?.containmentLinks?.Count ?? 0) + (snapshot.locations?.spatialRelationships?.Count ?? 0), snapshot.locations?.transactions?.Count ?? 0, Array.Empty<string>(), Array.Empty<string>(), snapshot.locations != null),
                P(EntityLocationParticipantId, "EntityLocationRuntime", snapshot.entityLocations?.schemaVersion ?? 0, EntityLocationRuntimeSaveData.CurrentSchemaVersion, snapshot.entityLocations?.revision ?? 0L, snapshot.entityLocations?.placements?.Count ?? 0, snapshot.entityLocations?.placements?.Count ?? 0, snapshot.entityLocations?.transactions?.Count ?? 0, new [] { LocationParticipantId }, Array.Empty<string>(), snapshot.entityLocations != null),
                P(InteractionPointParticipantId, "InteractionPointRuntime", snapshot.interactionPoints?.schemaVersion ?? 0, InteractionPointRuntimeSaveData.CurrentSchemaVersion, snapshot.interactionPoints?.revision ?? 0L, (snapshot.interactionPoints?.points?.Count ?? 0) + (snapshot.interactionPoints?.hostAssignments?.Count ?? 0) + (snapshot.interactionPoints?.providerAssignments?.Count ?? 0) + (snapshot.interactionPoints?.reservations?.Count ?? 0) + (snapshot.interactionPoints?.useSessions?.Count ?? 0), 0, snapshot.interactionPoints?.transactions?.Count ?? 0, new [] { LocationParticipantId }, new [] { EntityLocationParticipantId }, snapshot.interactionPoints != null),
                P(ConnectionParticipantId, "LocationConnectionRuntime", snapshot.connections?.schemaVersion ?? 0, LocationConnectionRuntimeSaveData.CurrentSchemaVersion, snapshot.connections?.revision ?? 0L, (snapshot.connections?.connections?.Length ?? 0) + (snapshot.connections?.endpoints?.Length ?? 0) + (snapshot.connections?.grants?.Length ?? 0), snapshot.connections?.history?.Length ?? 0, snapshot.connections?.transactions?.Length ?? 0, new [] { LocationParticipantId, EntityLocationParticipantId, InteractionPointParticipantId }, Array.Empty<string>(), snapshot.connections != null),
                P(RouteParticipantId, "LocationRouteRuntime", snapshot.routes?.schemaVersion ?? 0, LocationRouteRuntimeSaveData.CurrentSchemaVersion, snapshot.routes?.revision ?? 0L, (snapshot.routes?.segments?.Length ?? 0) + (snapshot.routes?.networks?.Length ?? 0), snapshot.routes?.history?.Length ?? 0, snapshot.routes?.transactions?.Length ?? 0, new [] { LocationParticipantId, ConnectionParticipantId }, Array.Empty<string>(), snapshot.routes != null),
                P(JourneyParticipantId, "TravelJourneyRuntime", snapshot.journeys?.schemaVersion ?? 0, TravelJourneyRuntimeSaveData.CurrentSchemaVersion, snapshot.journeys?.revision ?? 0L, (snapshot.journeys?.journeys?.Length ?? 0) + (snapshot.journeys?.steps?.Length ?? 0), snapshot.journeys?.history?.Length ?? 0, snapshot.journeys?.transactions?.Length ?? 0, new [] { LocationParticipantId, EntityLocationParticipantId, ConnectionParticipantId, RouteParticipantId }, new [] { TravelConditionParticipantId, PoliticalTravelParticipantId }, snapshot.journeys != null),
                P(TravelConditionParticipantId, "TravelConditionRuntime", snapshot.travelConditions?.schemaVersion ?? 0, TravelConditionRuntimeSaveData.CurrentSchemaVersion, snapshot.travelConditions?.revision ?? 0L, (snapshot.travelConditions?.conditions?.Length ?? 0) + (snapshot.travelConditions?.hazardExposures?.Length ?? 0) + (snapshot.travelConditions?.encounters?.Length ?? 0), (snapshot.travelConditions?.hazardExposures?.Length ?? 0) + (snapshot.travelConditions?.encounters?.Length ?? 0), snapshot.travelConditions?.transactions?.Length ?? 0, new [] { RouteParticipantId, JourneyParticipantId }, Array.Empty<string>(), snapshot.travelConditions != null),
                P(PoliticalTravelParticipantId, "PoliticalTravelRuntime", snapshot.politicalTravel?.schemaVersion ?? 0, PoliticalTravelRuntimeSaveData.CurrentSchemaVersion, snapshot.politicalTravel?.revision ?? 0L, (snapshot.politicalTravel?.checkpoints?.Length ?? 0) + (snapshot.politicalTravel?.authorizations?.Length ?? 0) + (snapshot.politicalTravel?.crossings?.Length ?? 0), snapshot.politicalTravel?.crossings?.Length ?? 0, snapshot.politicalTravel?.transactions?.Length ?? 0, new [] { LocationParticipantId, RouteParticipantId }, new [] { JourneyParticipantId, "step13.governments", "step13.laws", "step13.crimes" }, snapshot.politicalTravel != null)
            };

            Step14PersistenceValidationReport report = Validate(snapshot, participants, expectedWorldId);
            return new Step14PersistenceManifest(expectedWorldId, snapshot.saveSlotId, snapshot.authoritativeWorldTime, participants, OwnershipMap, report);
        }

        public static Step14PersistenceValidationReport Validate(Step14PersistenceSnapshotSource source)
        {
            return Build(source).ValidationReport;
        }

        private static Step14PersistenceValidationReport Validate(Step14PersistenceSnapshotSource source, IReadOnlyList<Step14PersistenceParticipantManifest> participants, string expectedWorldId)
        {
            List<Step14PersistenceValidationIssue> issues = new List<Step14PersistenceValidationIssue>();
            foreach (Step14PersistenceParticipantManifest participant in participants)
            {
                if (!participant.Present)
                {
                    issues.Add(I(Step14PersistenceValidationSeverity.Error, participant.ParticipantId, string.Empty, $"Required Step 14 participant '{participant.ParticipantId}' is missing."));
                }
                else if (!participant.SchemaCompatible)
                {
                    issues.Add(I(Step14PersistenceValidationSeverity.Fatal, participant.ParticipantId, string.Empty, $"Participant '{participant.ParticipantId}' schema {participant.SchemaVersion} is not compatible with expected schema {participant.ExpectedSchemaVersion}."));
                }

                foreach (string required in participant.RequiredDependencies)
                {
                    Step14PersistenceParticipantManifest dependency = participants.FirstOrDefault(item => item.ParticipantId == required);
                    if (dependency == null || !dependency.Present)
                    {
                        issues.Add(I(Step14PersistenceValidationSeverity.Error, participant.ParticipantId, required, $"Participant '{participant.ParticipantId}' requires missing dependency '{required}'."));
                    }
                }
            }

            ValidateWorld(source.locations?.worldId, expectedWorldId, LocationParticipantId, "runtime", issues);
            ValidateWorld(source.entityLocations?.worldId, expectedWorldId, EntityLocationParticipantId, "runtime", issues);
            ValidateWorld(source.interactionPoints?.worldId, expectedWorldId, InteractionPointParticipantId, "runtime", issues);
            ValidateWorld(source.connections?.worldId, expectedWorldId, ConnectionParticipantId, "runtime", issues);
            ValidateWorld(source.routes?.worldId, expectedWorldId, RouteParticipantId, "runtime", issues);
            ValidateWorld(source.journeys?.worldId, expectedWorldId, JourneyParticipantId, "runtime", issues);
            ValidateWorld(source.travelConditions?.worldId, expectedWorldId, TravelConditionParticipantId, "runtime", issues);
            ValidateWorld(source.politicalTravel?.worldId, expectedWorldId, PoliticalTravelParticipantId, "runtime", issues);

            HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (Step14PersistenceOwnerRecord owner in OwnershipMap.Where(item => item.OwnerKind == Step14PersistenceOwnerKind.Authoritative))
            {
                string key = owner.Category;
                if (!owners.Add(key))
                {
                    issues.Add(I(Step14PersistenceValidationSeverity.Fatal, owner.OwnerParticipantId, key, $"Persistence ownership category '{key}' has multiple authoritative owners."));
                }
            }

            ValidateSourceReferences(source, issues);
            ValidateMovementIntervals(source, issues);
            return new Step14PersistenceValidationReport(issues);
        }

        private static void ValidateSourceReferences(Step14PersistenceSnapshotSource source, List<Step14PersistenceValidationIssue> issues)
        {
            HashSet<string> locationIds = new HashSet<string>((source.locations?.records ?? new List<LocationRecordData>()).Select(item => item?.locationId).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            foreach (LocationContainmentLinkData link in source.locations?.containmentLinks ?? new List<LocationContainmentLinkData>())
            {
                if (link == null) continue;
                if (!locationIds.Contains(link.parentLocationId)) issues.Add(I(Step14PersistenceValidationSeverity.Error, LocationParticipantId, link.linkId, $"Containment link '{link.linkId}' references missing parent location '{link.parentLocationId}'."));
                if (!locationIds.Contains(link.childLocationId)) issues.Add(I(Step14PersistenceValidationSeverity.Error, LocationParticipantId, link.linkId, $"Containment link '{link.linkId}' references missing child location '{link.childLocationId}'."));
            }

            foreach (EntityPlacementRecordData placement in source.entityLocations?.placements ?? new List<EntityPlacementRecordData>())
            {
                if (placement == null) continue;
                if (!locationIds.Contains(placement.exactLocationId)) issues.Add(I(Step14PersistenceValidationSeverity.Error, EntityLocationParticipantId, placement.placementId, $"Entity placement '{placement.placementId}' references missing exact location '{placement.exactLocationId}'."));
            }

            HashSet<string> segmentIds = new HashSet<string>((source.routes?.segments ?? Array.Empty<LocationRouteSegmentRecordData>()).Select(item => item?.segmentId).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            foreach (TravelJourneyStepRecordData step in source.journeys?.steps ?? Array.Empty<TravelJourneyStepRecordData>())
            {
                if (step == null) continue;
                if (!string.IsNullOrWhiteSpace(step.edgeId) && step.edgeKind == RouteEdgeKind.RouteSegment && !segmentIds.Contains(step.edgeId))
                {
                    issues.Add(I(Step14PersistenceValidationSeverity.Warning, JourneyParticipantId, step.journeyStepId, $"Journey step '{step.journeyStepId}' references unavailable route segment '{step.edgeId}'. Historical projection can still expose the stable edge ID."));
                }
            }

            HashSet<string> journeyIds = new HashSet<string>((source.journeys?.journeys ?? Array.Empty<TravelJourneyRecordData>()).Select(item => item?.journeyId).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            foreach (TravelEncounterRecordData encounter in source.travelConditions?.encounters ?? Array.Empty<TravelEncounterRecordData>())
            {
                if (encounter == null || string.IsNullOrWhiteSpace(encounter.journeyId)) continue;
                if (!journeyIds.Contains(encounter.journeyId)) issues.Add(I(Step14PersistenceValidationSeverity.Error, TravelConditionParticipantId, encounter.encounterId, $"Travel encounter '{encounter.encounterId}' references missing journey '{encounter.journeyId}'."));
            }
        }

        private static void ValidateMovementIntervals(Step14PersistenceSnapshotSource source, List<Step14PersistenceValidationIssue> issues)
        {
            foreach (EntityPlacementRecordData placement in source.entityLocations?.placements ?? new List<EntityPlacementRecordData>())
            {
                if (placement == null) continue;
                if (placement.endWorldTime >= 0d && placement.endWorldTime < placement.startWorldTime)
                {
                    issues.Add(I(Step14PersistenceValidationSeverity.Error, EntityLocationParticipantId, placement.placementId, $"Entity placement '{placement.placementId}' ends before it starts."));
                }
            }

            IEnumerable<IGrouping<string, EntityPlacementRecordData>> byEntity = (source.entityLocations?.placements ?? new List<EntityPlacementRecordData>())
                .Where(item => item?.entity != null)
                .GroupBy(item => item.entity.StableKey, StringComparer.Ordinal);
            foreach (IGrouping<string, EntityPlacementRecordData> group in byEntity)
            {
                EntityPlacementRecordData[] placements = group.OrderBy(item => item.startWorldTime).ThenBy(item => item.placementId, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < placements.Length; i++)
                {
                    for (int j = i + 1; j < placements.Length; j++)
                    {
                        if (Overlaps(placements[i].startWorldTime, placements[i].endWorldTime, placements[j].startWorldTime, placements[j].endWorldTime))
                        {
                            issues.Add(I(Step14PersistenceValidationSeverity.Error, EntityLocationParticipantId, placements[j].placementId, $"Entity '{group.Key}' has overlapping placement intervals '{placements[i].placementId}' and '{placements[j].placementId}'."));
                        }
                    }
                }
            }
        }

        private static Step14PersistenceParticipantManifest P(string id, string runtime, int schema, int expected, long revision, int authoritative, int historical, int transactions, IEnumerable<string> required, IEnumerable<string> optional, bool present)
        {
            return new Step14PersistenceParticipantManifest(id, runtime, schema, expected, revision, authoritative, historical, transactions, required, optional, present);
        }

        private static Step14PersistenceValidationIssue I(Step14PersistenceValidationSeverity severity, string participant, string record, string message)
        {
            return new Step14PersistenceValidationIssue(severity, participant, record, message);
        }

        private static void ValidateWorld(string actualWorldId, string expectedWorldId, string participantId, string recordId, List<Step14PersistenceValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(actualWorldId)) return;
            if (!string.Equals(actualWorldId.Trim(), expectedWorldId, StringComparison.Ordinal))
            {
                issues.Add(I(Step14PersistenceValidationSeverity.Fatal, participantId, recordId, $"Participant '{participantId}' belongs to world '{actualWorldId}', expected '{expectedWorldId}'."));
            }
        }

        private static bool Overlaps(double aStart, double aEnd, double bStart, double bEnd)
        {
            double aStop = aEnd < 0d ? double.PositiveInfinity : aEnd;
            double bStop = bEnd < 0d ? double.PositiveInfinity : bEnd;
            return aStart <= bStop && bStart <= aStop;
        }

        private static IReadOnlyList<Step14PersistenceOwnerRecord> BuildOwnershipMap()
        {
            return new[]
            {
                O("world identity", LocationParticipantId, Step14PersistenceOwnerKind.Authoritative, "LocationRuntime anchors Step 14 world identity and location records."),
                O("runtime location record", LocationParticipantId, Step14PersistenceOwnerKind.Authoritative, "Stable LocationId, lifecycle, names, associations, and visibility."),
                O("location names", LocationParticipantId, Step14PersistenceOwnerKind.Authoritative, "Historical names remain stable and time-scoped."),
                O("location lifecycle", LocationParticipantId, Step14PersistenceOwnerKind.Authoritative, "Created/ended lifecycle state is not replayed from movement history."),
                O("containment links", LocationParticipantId, Step14PersistenceOwnerKind.Authoritative, "Historical hierarchy source records."),
                O("spatial relationships", LocationParticipantId, Step14PersistenceOwnerKind.Authoritative, "Non-traversal spatial facts."),
                O("entity placement", EntityLocationParticipantId, Step14PersistenceOwnerKind.Authoritative, "Exact placement and active placement state."),
                O("placement intervals/history", EntityLocationParticipantId, Step14PersistenceOwnerKind.Authoritative, "Placement intervals are the source for occupancy and visits."),
                O("occupancy indexes", EntityLocationParticipantId, Step14PersistenceOwnerKind.Derived, "Rebuilt from active placement intervals and historical hierarchy."),
                O("interaction points", InteractionPointParticipantId, Step14PersistenceOwnerKind.Authoritative, "Functional locations and point state."),
                O("interaction host assignments", InteractionPointParticipantId, Step14PersistenceOwnerKind.Authoritative, "Host/point linkage."),
                O("provider assignments", InteractionPointParticipantId, Step14PersistenceOwnerKind.Authoritative, "Service/provider records."),
                O("reservations", InteractionPointParticipantId, Step14PersistenceOwnerKind.Authoritative, "Reservation lifecycle and access."),
                O("connection identity", ConnectionParticipantId, Step14PersistenceOwnerKind.Authoritative, "Door/gate/path connection records."),
                O("connection open/lock/blockage state", ConnectionParticipantId, Step14PersistenceOwnerKind.Authoritative, "State plus state history."),
                O("access grants", ConnectionParticipantId, Step14PersistenceOwnerKind.Authoritative, "Connection access grants."),
                O("route segments", RouteParticipantId, Step14PersistenceOwnerKind.Authoritative, "Route edge records and route history."),
                O("route networks", RouteParticipantId, Step14PersistenceOwnerKind.Authoritative, "Network membership and categories."),
                O("journey records", JourneyParticipantId, Step14PersistenceOwnerKind.Authoritative, "Journey lifecycle and current progress."),
                O("journey steps", JourneyParticipantId, Step14PersistenceOwnerKind.Authoritative, "Route-plan steps and per-step progress."),
                O("route-plan assignment history", JourneyParticipantId, Step14PersistenceOwnerKind.Authoritative, "Accepted route snapshot and replan history."),
                O("journey progress", JourneyParticipantId, Step14PersistenceOwnerKind.Authoritative, "Current step, completed distance, paused/blocked state."),
                O("journey scheduler state", JourneyParticipantId, Step14PersistenceOwnerKind.Derived, "Scheduler jobs are reconciled from journey logical state."),
                O("travel conditions", TravelConditionParticipantId, Step14PersistenceOwnerKind.Authoritative, "Dynamic route/travel condition records."),
                O("hazard exposures", TravelConditionParticipantId, Step14PersistenceOwnerKind.Authoritative, "Hazard exposure state and trigger records."),
                O("travel encounters", TravelConditionParticipantId, Step14PersistenceOwnerKind.Authoritative, "Encounter opportunities, triggers, and interruptions."),
                O("encounter checkpoint state", TravelConditionParticipantId, Step14PersistenceOwnerKind.Authoritative, "Processed encounter/hazard trigger state."),
                O("political checkpoints", PoliticalTravelParticipantId, Step14PersistenceOwnerKind.Authoritative, "Checkpoint locations and policies."),
                O("crossing authorizations", PoliticalTravelParticipantId, Step14PersistenceOwnerKind.Authoritative, "Travel permits and checkpoint authorizations."),
                O("border/political crossing records", PoliticalTravelParticipantId, Step14PersistenceOwnerKind.Authoritative, "Recorded territory and jurisdiction crossings."),
                O("movement historical projections", "step14.movement-history", Step14PersistenceOwnerKind.Derived, "Read-only projection over placements, journeys, encounters, routes, and crossings."),
                O("current location summaries", "step14.movement-history", Step14PersistenceOwnerKind.Derived, "Derived from active placement or active journey."),
                O("last-known location", "step14.movement-history", Step14PersistenceOwnerKind.Derived, "Derived from placement intervals at or before query time."),
                O("derived indexes", "step14.derived-indexes", Step14PersistenceOwnerKind.Derived, "Ancestor, descendant, occupancy, route adjacency, lookup, and aggregate caches are rebuilt."),
                O("caches", "step14.derived-indexes", Step14PersistenceOwnerKind.Derived, "Never an authoritative persistence owner."),
                O("territory/government/law state", "step13.governments-laws-crimes", Step14PersistenceOwnerKind.External, "Step 14 references Step 13 stable records and does not duplicate them.")
            };
        }

        private static Step14PersistenceOwnerRecord O(string category, string owner, Step14PersistenceOwnerKind kind, string notes)
        {
            return new Step14PersistenceOwnerRecord(category, owner, kind, notes);
        }
    }
}
