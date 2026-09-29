using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Economy.Properties
{
    public sealed class PropertyRuntime
    {
        public const int CurrentSaveSchemaVersion = 1;

        private readonly Dictionary<string, PropertyInstanceData> propertiesById = new Dictionary<string, PropertyInstanceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyOwnershipInterestData> ownershipById = new Dictionary<string, PropertyOwnershipInterestData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyTitleRecordData> titlesById = new Dictionary<string, PropertyTitleRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyRecordData> recordsById = new Dictionary<string, PropertyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyPossessionRecordData> possessionsById = new Dictionary<string, PropertyPossessionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyOccupancyRecordData> occupanciesById = new Dictionary<string, PropertyOccupancyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyUseAssignmentData> usesById = new Dictionary<string, PropertyUseAssignmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyTenancyAgreementData> tenanciesById = new Dictionary<string, PropertyTenancyAgreementData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyAccessRightData> accessById = new Dictionary<string, PropertyAccessRightData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyTransferRecordData> transfersById = new Dictionary<string, PropertyTransferRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RentObligationData> rentsById = new Dictionary<string, RentObligationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RentReceiptData> receiptsById = new Dictionary<string, RentReceiptData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyConditionRecordData> conditionsById = new Dictionary<string, PropertyConditionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyInspectionRecordData> inspectionsById = new Dictionary<string, PropertyInspectionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyMaintenanceObligationData> maintenanceById = new Dictionary<string, PropertyMaintenanceObligationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyMaintenanceRecordData> maintenanceRecordsById = new Dictionary<string, PropertyMaintenanceRecordData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string worldId;

        public long Revision { get; private set; }
        public int PropertyCount => propertiesById.Count;
        public int OwnershipCount => ownershipById.Count;
        public int TenancyCount => tenanciesById.Count;
        public int RentCount => rentsById.Count;
        public int MaintenanceCount => maintenanceById.Count;

        public IReadOnlyList<PropertyInstanceData> Properties => Ordered(propertiesById.Values, item => item.propertyId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyOwnershipInterestData> OwnershipInterests => Ordered(ownershipById.Values, item => item.ownershipInterestId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyTitleRecordData> Titles => Ordered(titlesById.Values, item => item.effectiveWorldTime, item => item.titleId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyRecordData> Records => Ordered(recordsById.Values, item => item.recordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyPossessionRecordData> Possessions => Ordered(possessionsById.Values, item => item.possessionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyOccupancyRecordData> Occupancies => Ordered(occupanciesById.Values, item => item.occupancyId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyTenancyAgreementData> Tenancies => Ordered(tenanciesById.Values, item => item.tenancyId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyAccessRightData> AccessRights => Ordered(accessById.Values, item => item.accessRightId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyTransferRecordData> Transfers => Ordered(transfersById.Values, item => item.transferId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<RentObligationData> RentObligations => Ordered(rentsById.Values, item => item.periodStartWorldTime, item => item.rentObligationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyConditionRecordData> Conditions => Ordered(conditionsById.Values, item => item.recordedWorldTime, item => item.conditionRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PropertyMaintenanceObligationData> MaintenanceObligations => Ordered(maintenanceById.Values, item => item.maintenanceObligationId).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, string world)
        {
            registry = definitionRegistry ?? registry;
            worldId = world ?? worldId ?? string.Empty;
        }

        public bool TryGetProperty(string propertyId, out PropertyInstanceData property)
        {
            if (!string.IsNullOrWhiteSpace(propertyId) && propertiesById.TryGetValue(propertyId, out PropertyInstanceData found))
            {
                property = found.Clone();
                return true;
            }

            property = null;
            return false;
        }

        public bool TryGetTitle(string titleId, out PropertyTitleRecordData title)
        {
            if (!string.IsNullOrWhiteSpace(titleId) && titlesById.TryGetValue(titleId, out PropertyTitleRecordData found))
            {
                title = found.Clone();
                return true;
            }

            title = null;
            return false;
        }

        public PropertyOperationResult RegisterProperty(PropertyInstanceData request, bool preview = false)
        {
            long before = Revision;
            PropertyInstanceData property = request?.Clone();
            if (property == null || string.IsNullOrWhiteSpace(property.propertyId) || string.IsNullOrWhiteSpace(property.propertyDefinitionId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Property ID and definition ID are required.");
            }

            if (!TryGetPropertyDefinition(property.propertyDefinitionId, out PropertyDefinition definition))
            {
                return Fail(PropertyOperationCode.MissingDefinition, $"Property definition '{property.propertyDefinitionId}' was not found.");
            }

            if (propertiesById.TryGetValue(property.propertyId, out PropertyInstanceData existing))
            {
                return SameProperty(existing, property)
                    ? PropertyOperationResult.Success("Property already exists.", before, before, duplicate: true).With(property: existing)
                    : Fail(PropertyOperationCode.Duplicate, $"Property '{property.propertyId}' already exists with different data.");
            }

            if (definition.RequiresSpatialReference && string.IsNullOrWhiteSpace(property.spatialReferenceId) && string.IsNullOrWhiteSpace(property.sceneObjectReferenceId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Property definition requires a spatial or scene reference.");
            }

            if (!string.IsNullOrWhiteSpace(property.parentPropertyId))
            {
                if (!propertiesById.TryGetValue(property.parentPropertyId, out PropertyInstanceData parent))
                {
                    return Fail(PropertyOperationCode.InvalidHierarchy, $"Parent property '{property.parentPropertyId}' was not found.");
                }

                if (!TryGetPropertyDefinition(parent.propertyDefinitionId, out PropertyDefinition parentDefinition))
                {
                    return Fail(PropertyOperationCode.MissingDefinition, $"Parent property definition '{parent.propertyDefinitionId}' was not found.");
                }

                if (parentDefinition.PermittedChildCategories.Count == 0)
                {
                    return Fail(PropertyOperationCode.InvalidHierarchy, $"Parent property category {parentDefinition.Category} does not permit child properties.");
                }

                if (!parentDefinition.PermittedChildCategories.Contains(definition.Category))
                {
                    return Fail(PropertyOperationCode.InvalidHierarchy, $"Parent property does not permit child category {definition.Category}.");
                }

                if (WouldCreateCycle(property.propertyId, property.parentPropertyId))
                {
                    return Fail(PropertyOperationCode.InvalidHierarchy, "Property hierarchy would create a cycle.");
                }
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Property registration preview succeeded.", before).With(property: property);
            }

            property.displayName = string.IsNullOrWhiteSpace(property.displayName) ? definition.DisplayName : property.displayName;
            property.recognizedName = string.IsNullOrWhiteSpace(property.recognizedName) ? property.displayName : property.recognizedName;
            property.state = property.state == PropertyState.Unknown ? PropertyState.Available : property.state;
            property.currentUses = property.currentUses.Length == 0 ? new[] { definition.SupportedUseCategories.FirstOrDefault() } : property.currentUses;
            property.revision = 1L;
            propertiesById.Add(property.propertyId, property);
            if (!string.IsNullOrWhiteSpace(property.parentPropertyId) && propertiesById.TryGetValue(property.parentPropertyId, out PropertyInstanceData mutableParent))
            {
                mutableParent.childPropertyIds = AddId(mutableParent.childPropertyIds, property.propertyId);
                mutableParent.revision++;
            }

            Touch();
            return PropertyOperationResult.Success("Property registered.", before, Revision).With(property: property);
        }

        public PropertyOperationResult CreateOwnership(PropertyOwnershipInterestData request, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            PropertyOwnershipInterestData ownership = request?.Clone();
            if (!ValidateOwnership(ownership, worldTime, out PropertyOperationCode code, out string failure))
            {
                return Fail(code, failure);
            }

            if (ownershipById.TryGetValue(ownership.ownershipInterestId, out PropertyOwnershipInterestData existing))
            {
                return SameOwnership(existing, ownership)
                    ? PropertyOperationResult.Success("Ownership interest already exists.", before, before, duplicate: true).With(ownership: existing)
                    : Fail(PropertyOperationCode.Duplicate, $"Ownership interest '{ownership.ownershipInterestId}' already exists with different data.");
            }

            if (ownershipById.Values.Any(item => item.propertyId == ownership.propertyId
                && item.owner.StableKey == ownership.owner.StableKey
                && item.IsActiveAt(worldTime)
                && ownership.IsActiveAt(worldTime)))
            {
                return Fail(PropertyOperationCode.PolicyViolation, "Duplicate active ownership for the same subject is not allowed.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Ownership preview succeeded.", before).With(ownership: ownership);
            }

            ownership.revision = 1L;
            ownershipById.Add(ownership.ownershipInterestId, ownership);
            Touch();
            return PropertyOperationResult.Success("Ownership interest created.", before, Revision).With(ownership: ownership);
        }

        public PropertyOperationResult CreateTitle(string titleId, string propertyId, string[] activeOwnershipInterestIds, double worldTime, string transferSourceId = "", string priorTitleId = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(titleId) || string.IsNullOrWhiteSpace(propertyId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Title ID and property ID are required.");
            }

            if (!propertiesById.TryGetValue(propertyId, out PropertyInstanceData property))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{propertyId}' was not found.");
            }

            string[] ownershipIds = CleanIds(activeOwnershipInterestIds);
            if (ownershipIds.Length == 0 || ownershipIds.Any(id => !ownershipById.TryGetValue(id, out PropertyOwnershipInterestData item) || item.propertyId != propertyId || !item.IsActiveAt(worldTime)))
            {
                return Fail(PropertyOperationCode.MissingOwnership, "Title references missing or inactive ownership interests.");
            }

            if (!ValidateActiveShareTotal(property, ownershipIds, worldTime, out string shareFailure))
            {
                return Fail(PropertyOperationCode.InvalidShare, shareFailure);
            }

            if (titlesById.ContainsKey(titleId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Title '{titleId}' already exists.");
            }

            PropertyTitleRecordData title = new PropertyTitleRecordData
            {
                titleId = titleId,
                propertyId = propertyId,
                activeOwnershipInterestIds = ownershipIds,
                effectiveWorldTime = worldTime,
                priorTitleId = priorTitleId ?? property.currentTitleId ?? string.Empty,
                transferSourceId = transferSourceId ?? string.Empty,
                revision = 1L
            };

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Title preview succeeded.", before).With(title: title);
            }

            titlesById.Add(title.titleId, title);
            property.currentTitleId = title.titleId;
            property.revision++;
            Touch();
            return PropertyOperationResult.Success("Title created.", before, Revision).With(title: title);
        }

        public PropertyOperationResult BeginPossession(PropertyPossessionRecordData request, bool preview = false)
        {
            long before = Revision;
            PropertyPossessionRecordData possession = request?.Clone();
            if (possession == null || string.IsNullOrWhiteSpace(possession.possessionId) || string.IsNullOrWhiteSpace(possession.propertyId) || string.IsNullOrWhiteSpace(possession.possessor?.subjectId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Possession ID, property ID, and possessor are required.");
            }

            if (!propertiesById.TryGetValue(possession.propertyId, out PropertyInstanceData property))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{possession.propertyId}' was not found.");
            }

            if (possessionsById.ContainsKey(possession.possessionId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Possession '{possession.possessionId}' already exists.");
            }

            if (possession.exclusive && possessionsById.Values.Any(item => item.propertyId == possession.propertyId && item.exclusive && item.IsActiveAt(possession.startWorldTime)))
            {
                return Fail(PropertyOperationCode.PolicyViolation, "Exclusive possession conflicts with an active exclusive possession.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Possession preview succeeded.", before);
            }

            possession.revision = 1L;
            possessionsById.Add(possession.possessionId, possession);
            property.possessionRecordIds = AddId(property.possessionRecordIds, possession.possessionId);
            property.revision++;
            Touch();
            return PropertyOperationResult.Success("Possession begun.", before, Revision);
        }

        public PropertyOperationResult BeginOccupancy(PropertyOccupancyRecordData request, bool preview = false)
        {
            long before = Revision;
            PropertyOccupancyRecordData occupancy = request?.Clone();
            if (occupancy == null || string.IsNullOrWhiteSpace(occupancy.occupancyId) || string.IsNullOrWhiteSpace(occupancy.propertyId) || string.IsNullOrWhiteSpace(occupancy.occupant?.subjectId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Occupancy ID, property ID, and occupant are required.");
            }

            if (!propertiesById.TryGetValue(occupancy.propertyId, out PropertyInstanceData property))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{occupancy.propertyId}' was not found.");
            }

            if (property.state == PropertyState.UninhabitableFoundation || property.state == PropertyState.CondemnedFoundation || property.state == PropertyState.DestroyedFoundation)
            {
                return Fail(PropertyOperationCode.PolicyViolation, "Property state blocks occupancy.");
            }

            if (occupanciesById.ContainsKey(occupancy.occupancyId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Occupancy '{occupancy.occupancyId}' already exists.");
            }

            if (occupancy.exclusive && occupanciesById.Values.Any(item => item.propertyId == occupancy.propertyId && item.exclusive && item.IsActiveAt(occupancy.startWorldTime)))
            {
                return Fail(PropertyOperationCode.PolicyViolation, "Exclusive occupancy conflicts with an active exclusive occupancy.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Occupancy preview succeeded.", before);
            }

            occupancy.revision = 1L;
            occupanciesById.Add(occupancy.occupancyId, occupancy);
            property.occupancyRecordIds = AddId(property.occupancyRecordIds, occupancy.occupancyId);
            property.state = PropertyState.Occupied;
            property.revision++;
            Touch();
            return PropertyOperationResult.Success("Occupancy begun.", before, Revision);
        }

        public PropertyOperationResult AssignUse(PropertyUseAssignmentData request, bool preview = false)
        {
            long before = Revision;
            PropertyUseAssignmentData use = request?.Clone();
            if (use == null || string.IsNullOrWhiteSpace(use.assignmentId) || string.IsNullOrWhiteSpace(use.propertyId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Use assignment ID and property ID are required.");
            }

            if (!propertiesById.TryGetValue(use.propertyId, out PropertyInstanceData property))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{use.propertyId}' was not found.");
            }

            if (!TryGetPropertyDefinition(property.propertyDefinitionId, out PropertyDefinition definition) || !definition.SupportedUseCategories.Contains(use.useCategory))
            {
                return Fail(PropertyOperationCode.PolicyViolation, $"Property definition does not support use {use.useCategory}.");
            }

            if (usesById.ContainsKey(use.assignmentId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Use assignment '{use.assignmentId}' already exists.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Use assignment preview succeeded.", before);
            }

            usesById.Add(use.assignmentId, use);
            property.currentUses = AddEnum(property.currentUses, use.useCategory);
            if (!string.IsNullOrWhiteSpace(use.establishmentId))
            {
                property.businessEstablishmentIds = AddId(property.businessEstablishmentIds, use.establishmentId);
            }

            property.revision++;
            Touch();
            return PropertyOperationResult.Success("Property use assigned.", before, Revision).With(property: property);
        }

        public PropertyOperationResult CreateTenancy(PropertyTenancyAgreementData request, bool preview = false)
        {
            long before = Revision;
            PropertyTenancyAgreementData tenancy = request?.Clone();
            if (tenancy == null || string.IsNullOrWhiteSpace(tenancy.tenancyId) || string.IsNullOrWhiteSpace(tenancy.propertyId) || string.IsNullOrWhiteSpace(tenancy.tenant?.subjectId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Tenancy ID, property ID, and tenant are required.");
            }

            if (!propertiesById.TryGetValue(tenancy.propertyId, out PropertyInstanceData property))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{tenancy.propertyId}' was not found.");
            }

            if (!IsCurrentOwner(property.propertyId, tenancy.landlord, tenancy.startWorldTime))
            {
                return Fail(PropertyOperationCode.MissingAuthority, "Landlord is not a current property owner.");
            }

            if (!TryGetPropertyDefinition(property.propertyDefinitionId, out PropertyDefinition definition) || !definition.SupportedUseCategories.Contains(tenancy.permittedUse))
            {
                return Fail(PropertyOperationCode.PolicyViolation, $"Property definition does not permit tenancy use {tenancy.permittedUse}.");
            }

            if (tenanciesById.ContainsKey(tenancy.tenancyId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Tenancy '{tenancy.tenancyId}' already exists.");
            }

            if (tenanciesById.Values.Any(item => item.propertyId == tenancy.propertyId && item.state == TenancyState.Active && item.IsActiveAt(tenancy.startWorldTime)))
            {
                return Fail(PropertyOperationCode.PolicyViolation, "Duplicate incompatible active tenancy exists.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Tenancy preview succeeded.", before).With(tenancy: tenancy);
            }

            tenancy.state = tenancy.state == TenancyState.Unknown ? TenancyState.Proposed : tenancy.state;
            tenancy.revision = 1L;
            tenanciesById.Add(tenancy.tenancyId, tenancy);
            property.tenancyIds = AddId(property.tenancyIds, tenancy.tenancyId);
            property.revision++;
            Touch();
            return PropertyOperationResult.Success("Tenancy created.", before, Revision).With(tenancy: tenancy);
        }

        public PropertyOperationResult ActivateTenancy(string tenancyId, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!tenanciesById.TryGetValue(tenancyId ?? string.Empty, out PropertyTenancyAgreementData tenancy))
            {
                return Fail(PropertyOperationCode.MissingTenancy, $"Tenancy '{tenancyId}' was not found.");
            }

            if (tenancy.state != TenancyState.Proposed && tenancy.state != TenancyState.Suspended)
            {
                return Fail(PropertyOperationCode.InvalidState, $"Tenancy cannot activate from {tenancy.state}.");
            }

            if (preview)
            {
                PropertyTenancyAgreementData projected = tenancy.Clone();
                projected.state = TenancyState.Active;
                return PropertyOperationResult.PreviewResult("Tenancy activation preview succeeded.", before).With(tenancy: projected);
            }

            tenancy.state = TenancyState.Active;
            tenancy.startWorldTime = tenancy.startWorldTime <= 0d ? worldTime : tenancy.startWorldTime;
            tenancy.revision++;
            BeginPossession(new PropertyPossessionRecordData
            {
                possessionId = $"{tenancy.tenancyId}.possession",
                propertyId = tenancy.propertyId,
                possessor = tenancy.tenant.Clone(),
                category = PossessionCategory.TenantPossession,
                sourceRecordId = tenancy.tenancyId,
                startWorldTime = tenancy.startWorldTime,
                exclusive = true
            });
            BeginOccupancy(new PropertyOccupancyRecordData
            {
                occupancyId = $"{tenancy.tenancyId}.occupancy",
                propertyId = tenancy.propertyId,
                occupant = tenancy.tenant.Clone(),
                category = tenancy.permittedUse == PropertyUseCategory.Commercial ? OccupancyCategory.BusinessOperation : OccupancyCategory.Residence,
                sourceRecordId = tenancy.tenancyId,
                startWorldTime = tenancy.startWorldTime,
                permittedUse = tenancy.permittedUse,
                exclusive = true,
                primary = true
            });
            foreach (PropertyAccessCategory action in tenancy.grantedAccessCategories.Length == 0 ? new[] { PropertyAccessCategory.Enter, PropertyAccessCategory.Occupy } : tenancy.grantedAccessCategories)
            {
                GrantAccess(new PropertyAccessRightData
                {
                    accessRightId = $"{tenancy.tenancyId}.access.{action}",
                    propertyId = tenancy.propertyId,
                    holder = tenancy.tenant.Clone(),
                    grantingAuthority = tenancy.landlord.Clone(),
                    category = action,
                    permittedActions = new[] { action },
                    startWorldTime = tenancy.startWorldTime,
                    endWorldTime = tenancy.endWorldTime,
                    sourceRecordId = tenancy.tenancyId
                });
            }

            Touch();
            return PropertyOperationResult.Success("Tenancy activated.", before, Revision).With(tenancy: tenancy);
        }

        public PropertyOperationResult EndTenancy(string tenancyId, double endWorldTime, bool preview = false)
        {
            long before = Revision;
            if (!tenanciesById.TryGetValue(tenancyId ?? string.Empty, out PropertyTenancyAgreementData tenancy))
            {
                return Fail(PropertyOperationCode.MissingTenancy, $"Tenancy '{tenancyId}' was not found.");
            }

            if (preview)
            {
                PropertyTenancyAgreementData projected = tenancy.Clone();
                projected.state = TenancyState.Ended;
                projected.endWorldTime = endWorldTime;
                return PropertyOperationResult.PreviewResult("Tenancy end preview succeeded.", before).With(tenancy: projected);
            }

            tenancy.state = TenancyState.Ended;
            tenancy.endWorldTime = endWorldTime;
            tenancy.revision++;
            foreach (PropertyAccessRightData access in accessById.Values.Where(item => item.sourceRecordId == tenancy.tenancyId && !item.revoked).ToArray())
            {
                access.revoked = true;
                access.endWorldTime = endWorldTime;
                access.revision++;
            }

            foreach (PropertyPossessionRecordData possession in possessionsById.Values.Where(item => item.sourceRecordId == tenancy.tenancyId && item.endWorldTime < 0d).ToArray())
            {
                possession.endWorldTime = endWorldTime;
                possession.revision++;
            }

            foreach (PropertyOccupancyRecordData occupancy in occupanciesById.Values.Where(item => item.sourceRecordId == tenancy.tenancyId && item.endWorldTime < 0d).ToArray())
            {
                occupancy.endWorldTime = endWorldTime;
                occupancy.revision++;
            }

            Touch();
            return PropertyOperationResult.Success("Tenancy ended.", before, Revision).With(tenancy: tenancy);
        }

        public PropertyOperationResult GrantAccess(PropertyAccessRightData request, bool preview = false)
        {
            long before = Revision;
            PropertyAccessRightData access = request?.Clone();
            if (access == null || string.IsNullOrWhiteSpace(access.accessRightId) || string.IsNullOrWhiteSpace(access.propertyId) || string.IsNullOrWhiteSpace(access.holder?.subjectId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Access right ID, property ID, and holder are required.");
            }

            if (!propertiesById.TryGetValue(access.propertyId, out PropertyInstanceData property))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{access.propertyId}' was not found.");
            }

            if (accessById.ContainsKey(access.accessRightId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Access right '{access.accessRightId}' already exists.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Access grant preview succeeded.", before);
            }

            access.revision = 1L;
            accessById.Add(access.accessRightId, access);
            property.accessRightIds = AddId(property.accessRightIds, access.accessRightId);
            property.revision++;
            Touch();
            return PropertyOperationResult.Success("Access granted.", before, Revision);
        }

        public PropertyOperationResult RevokeAccess(string accessRightId, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            if (!accessById.TryGetValue(accessRightId ?? string.Empty, out PropertyAccessRightData access))
            {
                return Fail(PropertyOperationCode.MissingAccessRight, $"Access right '{accessRightId}' was not found.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Access revoke preview succeeded.", before);
            }

            access.revoked = true;
            access.endWorldTime = worldTime;
            access.revision++;
            Touch();
            return PropertyOperationResult.Success("Access revoked.", before, Revision);
        }

        public PropertyAccessEvaluationResult EvaluateAccess(string propertyId, PropertySubjectReferenceData requester, PropertyAccessCategory action, double worldTime)
        {
            if (!propertiesById.ContainsKey(propertyId ?? string.Empty))
            {
                return new PropertyAccessEvaluationResult(PropertyAccessDecision.MissingAuthority, $"Property '{propertyId}' was not found.");
            }

            string subjectKey = requester?.StableKey ?? string.Empty;
            string[] grants = accessById.Values
                .Where(item => item.propertyId == propertyId && item.holder.StableKey == subjectKey && item.IsActiveAt(worldTime) && (item.category == action || item.permittedActions.Contains(action)))
                .Select(item => item.accessRightId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (grants.Length > 0)
            {
                return new PropertyAccessEvaluationResult(PropertyAccessDecision.Allowed, "Access allowed by explicit property grant.", grants, grants);
            }

            bool owner = IsCurrentOwner(propertyId, requester, worldTime);
            if (owner && (action == PropertyAccessCategory.Manage || action == PropertyAccessCategory.TransferProperty || action == PropertyAccessCategory.Inspect))
            {
                return new PropertyAccessEvaluationResult(PropertyAccessDecision.Allowed, "Access allowed by current ownership.");
            }

            return new PropertyAccessEvaluationResult(PropertyAccessDecision.MissingAuthority, "No current property access source grants the requested action.");
        }

        public PropertyOperationResult TransferProperty(PropertyTransferRequestData request, EconomyRuntime economy = null, bool preview = false)
        {
            long before = Revision;
            PropertyTransferRequestData transfer = request?.Clone();
            if (!ValidateTransfer(transfer, economy, out PropertyOperationCode code, out string failure))
            {
                return Fail(code, failure);
            }

            if (transfersById.ContainsKey(transfer.transferId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Transfer '{transfer.transferId}' already exists.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Property transfer preview succeeded.", before);
            }

            PropertyRuntimeSaveData rollback = CreateSaveData();
            try
            {
                string paymentTransactionId = string.Empty;
                string pendingPaymentTransactionId = string.Empty;
                bool requiresPayment = transfer.transferCategory == PropertyTransferCategory.Sale && transfer.considerationUnits > 0L;
                if (requiresPayment)
                {
                    if (transfer.injectFailureStage == "buyer-fund-reservation" || transfer.injectFailureStage == "buyer-debit" || transfer.injectFailureStage == "seller-credit")
                    {
                        RestoreFromSaveData(rollback, registry);
                        return Fail(PropertyOperationCode.PaymentFailed, $"Injected transfer failure at '{transfer.injectFailureStage}'.");
                    }

                    pendingPaymentTransactionId = $"{transfer.transferId}.payment";
                }

                if (transfer.injectFailureStage == "ownership-interest-ending")
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(PropertyOperationCode.TransferFailed, "Injected transfer failure at ownership-interest-ending.");
                }

                PropertyOwnershipInterestData from = ownershipById.Values
                    .Where(item => item.propertyId == transfer.propertyId && item.owner.StableKey == transfer.fromOwner.StableKey && item.IsActiveAt(transfer.effectiveWorldTime))
                    .OrderBy(item => item.ownershipInterestId, StringComparer.Ordinal)
                    .First();
                if (transfer.share.units > from.ownershipShare.units || transfer.share.totalUnits != from.ownershipShare.totalUnits)
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(PropertyOperationCode.InvalidShare, "Transfer share must use the current owner's share denominator and cannot exceed owned units.");
                }

                from.effectiveEndWorldTime = transfer.effectiveWorldTime;
                from.revision++;

                if (transfer.injectFailureStage == "new-ownership-creation")
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(PropertyOperationCode.TransferFailed, "Injected transfer failure at new-ownership-creation.");
                }

                PropertyOwnershipInterestData to = new PropertyOwnershipInterestData
                {
                    ownershipInterestId = $"{transfer.transferId}.ownership",
                    propertyId = transfer.propertyId,
                    owner = transfer.toOwner.Clone(),
                    ownershipModel = transfer.toOwner.kind == PropertySubjectKind.Business ? PropertyOwnershipModel.Business : PropertyOwnershipModel.Sole,
                    ownershipShare = transfer.share.Clone(),
                    votingShare = transfer.share.Clone(),
                    economicBenefitShare = transfer.share.Clone(),
                    effectiveStartWorldTime = transfer.effectiveWorldTime,
                    acquisitionSourceId = transfer.transferId,
                    transferReferenceId = transfer.transferId,
                    rights = new[] { PropertyAccessCategory.Manage, PropertyAccessCategory.TransferProperty }
                };
                ownershipById.Add(to.ownershipInterestId, to);
                if (from.ownershipShare.units > transfer.share.units)
                {
                    PropertyOwnershipInterestData remaining = from.Clone();
                    remaining.ownershipInterestId = $"{transfer.transferId}.ownership.remaining";
                    remaining.effectiveStartWorldTime = transfer.effectiveWorldTime;
                    remaining.effectiveEndWorldTime = -1d;
                    remaining.ownershipShare.units = from.ownershipShare.units - transfer.share.units;
                    remaining.votingShare.units = Math.Min(remaining.votingShare.units, remaining.ownershipShare.units);
                    remaining.economicBenefitShare.units = Math.Min(remaining.economicBenefitShare.units, remaining.ownershipShare.units);
                    remaining.acquisitionSourceId = from.acquisitionSourceId;
                    remaining.transferReferenceId = transfer.transferId;
                    remaining.revision = 1L;
                    ownershipById.Add(remaining.ownershipInterestId, remaining);
                }

                string[] activeOwnershipIds = ownershipById.Values
                    .Where(item => item.propertyId == transfer.propertyId && item.IsActiveAt(transfer.effectiveWorldTime))
                    .Select(item => item.ownershipInterestId)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                if (!ValidateActiveShareTotal(propertiesById[transfer.propertyId], activeOwnershipIds, transfer.effectiveWorldTime, out string activeShareFailure))
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(PropertyOperationCode.InvalidShare, activeShareFailure);
                }

                if (transfer.injectFailureStage == "deed-creation")
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(PropertyOperationCode.TransferFailed, "Injected transfer failure at deed-creation.");
                }

                string deedId = $"{transfer.transferId}.deed";
                recordsById.Add(deedId, new PropertyRecordData
                {
                    recordId = deedId,
                    propertyId = transfer.propertyId,
                    category = transfer.transferCategory == PropertyTransferCategory.Inheritance ? PropertyRecordCategory.InheritanceRecord : PropertyRecordCategory.Deed,
                    transferCategory = transfer.transferCategory,
                    transferor = transfer.fromOwner.Clone(),
                    transferee = transfer.toOwner.Clone(),
                    share = transfer.share.Clone(),
                    transactionOrTradeReferenceId = FirstNonEmpty(pendingPaymentTransactionId, transfer.economicReferenceId),
                    effectiveWorldTime = transfer.effectiveWorldTime,
                    recognizingAuthorityId = transfer.approvalAuthorityId,
                    revision = 1L
                });

                if (transfer.injectFailureStage == "title-creation")
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(PropertyOperationCode.TransferFailed, "Injected transfer failure at title-creation.");
                }

                PropertyInstanceData property = propertiesById[transfer.propertyId];
                string priorTitle = property.currentTitleId ?? string.Empty;
                string titleId = $"{transfer.transferId}.title";
                titlesById.Add(titleId, new PropertyTitleRecordData
                {
                    titleId = titleId,
                    propertyId = transfer.propertyId,
                    activeOwnershipInterestIds = activeOwnershipIds,
                    priorTitleId = priorTitle,
                    transferSourceId = transfer.transferId,
                    effectiveWorldTime = transfer.effectiveWorldTime,
                    recognizingAuthorityId = transfer.approvalAuthorityId,
                    revision = 1L
                });
                property.currentTitleId = titleId;
                property.revision++;

                if (!transfer.continueAccess)
                {
                    foreach (PropertyAccessRightData access in accessById.Values.Where(item => item.propertyId == transfer.propertyId && !item.revoked).ToArray())
                    {
                        access.revoked = true;
                        access.endWorldTime = transfer.effectiveWorldTime;
                        access.revision++;
                    }
                }

                if (transfer.injectFailureStage == "final-transfer-commit")
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(PropertyOperationCode.TransferFailed, "Injected transfer failure at final-transfer-commit.");
                }

                if (requiresPayment)
                {
                    EconomyOperationResult payment = economy.Transfer(pendingPaymentTransactionId, transfer.buyerAccountId, transfer.sellerAccountId, new MoneyAmount(transfer.currencyId, transfer.considerationUnits), EconomyTransactionKind.Payment, actorId: transfer.toOwner.subjectId);
                    if (!payment.Succeeded)
                    {
                        RestoreFromSaveData(rollback, registry);
                        return Fail(PropertyOperationCode.PaymentFailed, payment.Message);
                    }

                    paymentTransactionId = payment.Transaction?.TransactionId ?? pendingPaymentTransactionId;
                }

                PropertyTransferRecordData committed = new PropertyTransferRecordData
                {
                    transferId = transfer.transferId,
                    propertyId = transfer.propertyId,
                    transferCategory = transfer.transferCategory,
                    priorTitleId = priorTitle,
                    newTitleId = titleId,
                    deedRecordId = deedId,
                    paymentTransactionId = paymentTransactionId,
                    effectiveWorldTime = transfer.effectiveWorldTime,
                    revision = 1L
                };
                transfersById.Add(committed.transferId, committed);
                Touch();
                return PropertyOperationResult.Success("Property transferred.", before, Revision).With(transfer: committed);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry);
                return Fail(PropertyOperationCode.TransferFailed, exception.Message);
            }
        }

        public PropertyOperationResult GenerateRentObligation(string rentObligationId, string tenancyId, double periodStart, double periodEnd, double dueWorldTime, bool preview = false)
        {
            long before = Revision;
            if (!tenanciesById.TryGetValue(tenancyId ?? string.Empty, out PropertyTenancyAgreementData tenancy))
            {
                return Fail(PropertyOperationCode.MissingTenancy, $"Tenancy '{tenancyId}' was not found.");
            }

            if (tenancy.state == TenancyState.Ended && periodStart >= tenancy.endWorldTime)
            {
                return Fail(PropertyOperationCode.PolicyViolation, "Ended tenancy cannot accrue future rent.");
            }

            RentObligationData rent = new RentObligationData
            {
                rentObligationId = rentObligationId ?? string.Empty,
                tenancyId = tenancyId ?? string.Empty,
                propertyId = tenancy.propertyId,
                currencyId = tenancy.rentTerms.currencyId,
                totalDueUnits = tenancy.rentTerms.rentUnitsPerPeriod,
                periodStartWorldTime = periodStart,
                periodEndWorldTime = periodEnd,
                dueWorldTime = dueWorldTime,
                state = RentObligationState.Open,
                revision = 1L
            };

            if (string.IsNullOrWhiteSpace(rent.rentObligationId) || string.IsNullOrWhiteSpace(rent.currencyId) || rent.totalDueUnits <= 0L)
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Rent obligation requires ID, currency, and positive amount.");
            }

            if (rentsById.TryGetValue(rent.rentObligationId, out RentObligationData existing))
            {
                return SameRent(existing, rent)
                    ? PropertyOperationResult.Success("Rent obligation already exists.", before, before, duplicate: true).With(rent: existing)
                    : Fail(PropertyOperationCode.Duplicate, $"Rent obligation '{rent.rentObligationId}' exists with different data.");
            }

            if (rentsById.Values.Any(item => item.tenancyId == tenancyId && Math.Abs(item.periodStartWorldTime - periodStart) < 0.00001d))
            {
                return Fail(PropertyOperationCode.Duplicate, "Rent for the same tenancy period already exists.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Rent generation preview succeeded.", before).With(rent: rent);
            }

            rentsById.Add(rent.rentObligationId, rent);
            Touch();
            return PropertyOperationResult.Success("Rent obligation generated.", before, Revision).With(rent: rent);
        }

        public PropertyOperationResult PayRent(string rentObligationId, EconomyRuntime economy, string transactionId, long units, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!rentsById.TryGetValue(rentObligationId ?? string.Empty, out RentObligationData rent))
            {
                return Fail(PropertyOperationCode.MissingRent, $"Rent obligation '{rentObligationId}' was not found.");
            }

            if (!tenanciesById.TryGetValue(rent.tenancyId, out PropertyTenancyAgreementData tenancy))
            {
                return Fail(PropertyOperationCode.MissingTenancy, $"Tenancy '{rent.tenancyId}' was not found.");
            }

            if (units <= 0L || units > rent.OutstandingUnits)
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Rent payment must be positive and cannot exceed outstanding rent.");
            }

            if (economy == null)
            {
                return Fail(PropertyOperationCode.PaymentFailed, "Economy runtime is required for rent payment.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Rent payment preview succeeded.", before).With(rent: rent);
            }

            EconomyOperationResult payment = economy.Transfer(transactionId, tenancy.tenantAccountId, tenancy.landlordAccountId, new MoneyAmount(rent.currencyId, units), EconomyTransactionKind.Payment, actorId: tenancy.tenant.subjectId);
            if (!payment.Succeeded)
            {
                return Fail(PropertyOperationCode.PaymentFailed, payment.Message);
            }

            rent.paidUnits += units;
            rent.paymentTransactionIds = AddId(rent.paymentTransactionIds, payment.Transaction?.TransactionId ?? transactionId);
            rent.state = rent.OutstandingUnits == 0L ? RentObligationState.Paid : RentObligationState.PartiallyPaid;
            rent.revision++;
            string receiptId = $"{rent.rentObligationId}.receipt.{rent.paymentTransactionIds.Length}";
            receiptsById.Add(receiptId, new RentReceiptData
            {
                rentReceiptId = receiptId,
                rentObligationId = rent.rentObligationId,
                transactionId = payment.Transaction?.TransactionId ?? transactionId,
                currencyId = rent.currencyId,
                paidUnits = units,
                paidWorldTime = worldTime
            });
            Touch();
            return PropertyOperationResult.Success("Rent paid.", before, Revision).With(rent: rent);
        }

        public PropertyOperationResult MarkOverdueRent(string rentObligationId, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!rentsById.TryGetValue(rentObligationId ?? string.Empty, out RentObligationData rent))
            {
                return Fail(PropertyOperationCode.MissingRent, $"Rent obligation '{rentObligationId}' was not found.");
            }

            if (rent.state == RentObligationState.Paid || worldTime <= rent.dueWorldTime)
            {
                return Fail(PropertyOperationCode.InvalidState, "Rent is not overdue.");
            }

            if (preview)
            {
                RentObligationData projected = rent.Clone();
                projected.state = RentObligationState.Overdue;
                return PropertyOperationResult.PreviewResult("Overdue preview succeeded.", before).With(rent: projected);
            }

            rent.state = RentObligationState.Overdue;
            rent.revision++;
            Touch();
            return PropertyOperationResult.Success("Rent marked overdue.", before, Revision).With(rent: rent);
        }

        public PropertyOperationResult RecordCondition(PropertyConditionRecordData request, bool preview = false)
        {
            long before = Revision;
            PropertyConditionRecordData condition = request?.Clone();
            if (condition == null || string.IsNullOrWhiteSpace(condition.conditionRecordId) || string.IsNullOrWhiteSpace(condition.propertyId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Condition record ID and property ID are required.");
            }

            if (!propertiesById.TryGetValue(condition.propertyId, out PropertyInstanceData property))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{condition.propertyId}' was not found.");
            }

            if (conditionsById.ContainsKey(condition.conditionRecordId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Condition '{condition.conditionRecordId}' already exists.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Condition preview succeeded.", before);
            }

            conditionsById.Add(condition.conditionRecordId, condition);
            property.conditionRecordId = condition.conditionRecordId;
            property.state = condition.condition switch
            {
                PropertyConditionState.Damaged => PropertyState.Damaged,
                PropertyConditionState.Unsafe => PropertyState.Restricted,
                PropertyConditionState.Uninhabitable => PropertyState.UninhabitableFoundation,
                PropertyConditionState.DestroyedFoundation => PropertyState.DestroyedFoundation,
                _ => property.state == PropertyState.Damaged || property.state == PropertyState.UnderMaintenance ? PropertyState.Available : property.state
            };
            property.revision++;
            Touch();
            return PropertyOperationResult.Success("Condition recorded.", before, Revision).With(property: property);
        }

        public PropertyOperationResult PerformInspection(PropertyInspectionRecordData request, bool preview = false)
        {
            long before = Revision;
            PropertyInspectionRecordData inspection = request?.Clone();
            if (inspection == null || string.IsNullOrWhiteSpace(inspection.inspectionId) || string.IsNullOrWhiteSpace(inspection.propertyId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Inspection ID and property ID are required.");
            }

            if (!propertiesById.ContainsKey(inspection.propertyId))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{inspection.propertyId}' was not found.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Inspection preview succeeded.", before);
            }

            inspectionsById.Add(inspection.inspectionId, inspection);
            recordsById[$"{inspection.inspectionId}.record"] = new PropertyRecordData
            {
                recordId = $"{inspection.inspectionId}.record",
                propertyId = inspection.propertyId,
                category = PropertyRecordCategory.InspectionRecord,
                sourceRecordId = inspection.inspectionId,
                effectiveWorldTime = inspection.inspectedWorldTime
            };
            Touch();
            return PropertyOperationResult.Success("Inspection recorded.", before, Revision);
        }

        public PropertyOperationResult CreateMaintenanceObligation(PropertyMaintenanceObligationData request, bool preview = false)
        {
            long before = Revision;
            PropertyMaintenanceObligationData maintenance = request?.Clone();
            if (maintenance == null || string.IsNullOrWhiteSpace(maintenance.maintenanceObligationId) || string.IsNullOrWhiteSpace(maintenance.propertyId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Maintenance obligation ID and property ID are required.");
            }

            if (!propertiesById.ContainsKey(maintenance.propertyId))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{maintenance.propertyId}' was not found.");
            }

            if (maintenanceById.ContainsKey(maintenance.maintenanceObligationId))
            {
                return Fail(PropertyOperationCode.Duplicate, $"Maintenance obligation '{maintenance.maintenanceObligationId}' already exists.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Maintenance obligation preview succeeded.", before).With(maintenance: maintenance);
            }

            maintenance.state = maintenance.state == MaintenanceObligationState.Unknown ? MaintenanceObligationState.Required : maintenance.state;
            maintenanceById.Add(maintenance.maintenanceObligationId, maintenance);
            Touch();
            return PropertyOperationResult.Success("Maintenance obligation created.", before, Revision).With(maintenance: maintenance);
        }

        public PropertyOperationResult ExecuteMaintenance(string maintenanceObligationId, PropertySubjectReferenceData worker, ItemInstanceIdentityRuntime items, string[] toolItemInstanceIds, string[] materialItemInstanceIds, string workEvidenceReferenceId, string expenseReferenceId, double worldTime, string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!maintenanceById.TryGetValue(maintenanceObligationId ?? string.Empty, out PropertyMaintenanceObligationData obligation))
            {
                return Fail(PropertyOperationCode.MissingMaintenance, $"Maintenance obligation '{maintenanceObligationId}' was not found.");
            }

            if (obligation.state == MaintenanceObligationState.Completed)
            {
                return Fail(PropertyOperationCode.InvalidState, "Maintenance is already complete.");
            }

            if (!string.IsNullOrWhiteSpace(obligation.authorizedWorker?.subjectId) && obligation.authorizedWorker.StableKey != (worker?.StableKey ?? string.Empty))
            {
                return Fail(PropertyOperationCode.MissingAuthority, "Worker is not authorized for this maintenance obligation.");
            }

            if (obligation.requiredToolItemInstanceIds.Length > 0 && !obligation.requiredToolItemInstanceIds.All(id => (toolItemInstanceIds ?? Array.Empty<string>()).Contains(id)))
            {
                return Fail(PropertyOperationCode.MissingExternalReference, "Required maintenance tools are missing.");
            }

            if (obligation.requiredMaterialItemInstanceIds.Length > 0 && !obligation.requiredMaterialItemInstanceIds.All(id => (materialItemInstanceIds ?? Array.Empty<string>()).Contains(id)))
            {
                return Fail(PropertyOperationCode.MissingExternalReference, "Required maintenance materials are missing.");
            }

            if (items != null)
            {
                foreach (string id in CleanIds(toolItemInstanceIds).Concat(CleanIds(materialItemInstanceIds)))
                {
                    if (!items.TryGetSnapshot(id, out _))
                    {
                        return Fail(PropertyOperationCode.MissingExternalReference, $"Maintenance item '{id}' was not found.");
                    }
                }
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Maintenance execution preview succeeded.", before).With(maintenance: obligation);
            }

            PropertyRuntimeSaveData rollback = CreateSaveData();
            if (!string.IsNullOrWhiteSpace(injectFailureStage))
            {
                RestoreFromSaveData(rollback, registry);
                return Fail(PropertyOperationCode.MaintenanceFailed, $"Injected maintenance failure at '{injectFailureStage}'.");
            }

            string conditionId = $"{maintenanceObligationId}.condition.completed";
            RecordCondition(new PropertyConditionRecordData
            {
                conditionRecordId = conditionId,
                propertyId = obligation.propertyId,
                condition = PropertyConditionState.Good,
                sourceRecordId = maintenanceObligationId,
                recordedWorldTime = worldTime
            });
            string recordId = $"{maintenanceObligationId}.record";
            maintenanceRecordsById.Add(recordId, new PropertyMaintenanceRecordData
            {
                maintenanceRecordId = recordId,
                maintenanceObligationId = obligation.maintenanceObligationId,
                propertyId = obligation.propertyId,
                worker = worker?.Clone() ?? new PropertySubjectReferenceData(),
                toolItemInstanceIds = CleanIds(toolItemInstanceIds),
                materialItemInstanceIds = CleanIds(materialItemInstanceIds),
                workEvidenceReferenceId = workEvidenceReferenceId ?? string.Empty,
                expenseReferenceId = expenseReferenceId ?? string.Empty,
                resultingConditionRecordId = conditionId,
                performedWorldTime = worldTime
            });
            obligation.state = MaintenanceObligationState.Completed;
            obligation.completedWorldTime = worldTime;
            obligation.workEvidenceReferenceId = workEvidenceReferenceId ?? string.Empty;
            obligation.businessExpenseReferenceId = expenseReferenceId ?? string.Empty;
            obligation.revision++;
            Touch();
            return PropertyOperationResult.Success("Maintenance executed.", before, Revision).With(maintenance: obligation);
        }

        public PropertyOperationResult LinkBusinessEstablishment(string propertyId, string establishmentId, BusinessRuntime businesses = null, bool preview = false)
        {
            long before = Revision;
            if (!propertiesById.TryGetValue(propertyId ?? string.Empty, out PropertyInstanceData property))
            {
                return Fail(PropertyOperationCode.MissingProperty, $"Property '{propertyId}' was not found.");
            }

            if (string.IsNullOrWhiteSpace(establishmentId))
            {
                return Fail(PropertyOperationCode.InvalidRequest, "Establishment ID is required.");
            }

            if (businesses != null && !businesses.Establishments.Any(item => item.establishmentId == establishmentId))
            {
                return Fail(PropertyOperationCode.MissingExternalReference, $"Business establishment '{establishmentId}' was not found.");
            }

            if (preview)
            {
                return PropertyOperationResult.PreviewResult("Establishment link preview succeeded.", before).With(property: property);
            }

            property.businessEstablishmentIds = AddId(property.businessEstablishmentIds, establishmentId);
            property.revision++;
            Touch();
            return PropertyOperationResult.Success("Business establishment linked.", before, Revision).With(property: property);
        }

        public PropertyProjection ProjectProperty(string propertyId, PropertyProjectionKind kind, InformationAccessRuntime accessRuntime = null, InformationAccessContext context = null)
        {
            if (!propertiesById.TryGetValue(propertyId ?? string.Empty, out PropertyInstanceData property))
            {
                return new PropertyProjection(kind, PropertyAccessDecision.MissingAuthority, null, Array.Empty<string>(), $"Property '{propertyId}' was not found.");
            }

            string[] details = kind == PropertyProjectionKind.Privileged
                ? new[] { "detail.property", "detail.title", "detail.owner", "detail.tenant", "detail.accounts", "detail.maintenance" }
                : kind == PropertyProjectionKind.Public
                    ? new[] { "detail.property" }
                    : new[] { "detail.property", "detail.title" };
            PropertyAccessDecision decision = kind == PropertyProjectionKind.Public ? PropertyAccessDecision.Redacted : PropertyAccessDecision.Allowed;

            if (accessRuntime != null && context != null)
            {
                InformationAccessContext projectedContext = InformationAccessProjectionUtility.BuildContext(context, PropertyInformationSubject.Create(property.propertyId), InformationAccessMode.Query, InformationAccessPurpose.Gameplay, details, property.accessPolicyId);
                RedactedInformationProjection redaction = accessRuntime.Project(projectedContext, details);
                if (redaction.Decision == null || redaction.Decision.Denied)
                {
                    return new PropertyProjection(kind, PropertyAccessDecision.Denied, null, Array.Empty<string>(), redaction.Decision?.DiagnosticReason ?? "Property access denied.");
                }

                string[] requestedDetails = details;
                details = requestedDetails.Where(detail => InformationAccessProjectionUtility.IsVisible(redaction.Details, detail)).ToArray();
                decision = details.Length == requestedDetails.Length ? PropertyAccessDecision.Allowed : PropertyAccessDecision.Redacted;
            }

            PropertyInstanceData projected = property.Clone();
            if (!details.Contains("detail.title"))
            {
                projected.currentTitleId = string.Empty;
            }

            if (!details.Contains("detail.tenant"))
            {
                projected.tenancyIds = Array.Empty<string>();
                projected.occupancyRecordIds = Array.Empty<string>();
            }

            if (!details.Contains("detail.maintenance"))
            {
                projected.conditionRecordId = string.Empty;
            }

            return new PropertyProjection(kind, decision, projected, details, decision == PropertyAccessDecision.Allowed ? "Property projection visible." : "Property projection redacted.");
        }

        public PropertyRuntimeSaveData CreateSaveData()
        {
            return new PropertyRuntimeSaveData
            {
                schemaVersion = CurrentSaveSchemaVersion,
                revision = Revision,
                worldId = worldId ?? string.Empty,
                properties = Properties.ToArray(),
                ownershipInterests = OwnershipInterests.ToArray(),
                titles = Titles.ToArray(),
                records = Records.ToArray(),
                possessions = Possessions.ToArray(),
                occupancies = Occupancies.ToArray(),
                uses = Ordered(usesById.Values, item => item.assignmentId).Select(item => item.Clone()).ToArray(),
                tenancies = Tenancies.ToArray(),
                accessRights = AccessRights.ToArray(),
                transfers = Transfers.ToArray(),
                rentObligations = RentObligations.ToArray(),
                rentReceipts = Ordered(receiptsById.Values, item => item.rentReceiptId).Select(item => item.Clone()).ToArray(),
                conditions = Conditions.ToArray(),
                inspections = Ordered(inspectionsById.Values, item => item.inspectionId).Select(item => item.Clone()).ToArray(),
                maintenanceObligations = MaintenanceObligations.ToArray(),
                maintenanceRecords = Ordered(maintenanceRecordsById.Values, item => item.maintenanceRecordId).Select(item => item.Clone()).ToArray()
            };
        }

        public PropertyOperationResult RestoreFromSaveData(PropertyRuntimeSaveData saveData, DefinitionRegistry definitionRegistry = null)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, out string failure))
            {
                return PropertyOperationResult.Failure(PropertyOperationCode.RestoreFailed, failure, before);
            }

            propertiesById.Clear();
            ownershipById.Clear();
            titlesById.Clear();
            recordsById.Clear();
            possessionsById.Clear();
            occupanciesById.Clear();
            usesById.Clear();
            tenanciesById.Clear();
            accessById.Clear();
            transfersById.Clear();
            rentsById.Clear();
            receiptsById.Clear();
            conditionsById.Clear();
            inspectionsById.Clear();
            maintenanceById.Clear();
            maintenanceRecordsById.Clear();

            foreach (PropertyInstanceData item in saveData.properties ?? Array.Empty<PropertyInstanceData>()) propertiesById[item.propertyId] = item.Clone();
            foreach (PropertyOwnershipInterestData item in saveData.ownershipInterests ?? Array.Empty<PropertyOwnershipInterestData>()) ownershipById[item.ownershipInterestId] = item.Clone();
            foreach (PropertyTitleRecordData item in saveData.titles ?? Array.Empty<PropertyTitleRecordData>()) titlesById[item.titleId] = item.Clone();
            foreach (PropertyRecordData item in saveData.records ?? Array.Empty<PropertyRecordData>()) recordsById[item.recordId] = item.Clone();
            foreach (PropertyPossessionRecordData item in saveData.possessions ?? Array.Empty<PropertyPossessionRecordData>()) possessionsById[item.possessionId] = item.Clone();
            foreach (PropertyOccupancyRecordData item in saveData.occupancies ?? Array.Empty<PropertyOccupancyRecordData>()) occupanciesById[item.occupancyId] = item.Clone();
            foreach (PropertyUseAssignmentData item in saveData.uses ?? Array.Empty<PropertyUseAssignmentData>()) usesById[item.assignmentId] = item.Clone();
            foreach (PropertyTenancyAgreementData item in saveData.tenancies ?? Array.Empty<PropertyTenancyAgreementData>()) tenanciesById[item.tenancyId] = item.Clone();
            foreach (PropertyAccessRightData item in saveData.accessRights ?? Array.Empty<PropertyAccessRightData>()) accessById[item.accessRightId] = item.Clone();
            foreach (PropertyTransferRecordData item in saveData.transfers ?? Array.Empty<PropertyTransferRecordData>()) transfersById[item.transferId] = item.Clone();
            foreach (RentObligationData item in saveData.rentObligations ?? Array.Empty<RentObligationData>()) rentsById[item.rentObligationId] = item.Clone();
            foreach (RentReceiptData item in saveData.rentReceipts ?? Array.Empty<RentReceiptData>()) receiptsById[item.rentReceiptId] = item.Clone();
            foreach (PropertyConditionRecordData item in saveData.conditions ?? Array.Empty<PropertyConditionRecordData>()) conditionsById[item.conditionRecordId] = item.Clone();
            foreach (PropertyInspectionRecordData item in saveData.inspections ?? Array.Empty<PropertyInspectionRecordData>()) inspectionsById[item.inspectionId] = item.Clone();
            foreach (PropertyMaintenanceObligationData item in saveData.maintenanceObligations ?? Array.Empty<PropertyMaintenanceObligationData>()) maintenanceById[item.maintenanceObligationId] = item.Clone();
            foreach (PropertyMaintenanceRecordData item in saveData.maintenanceRecords ?? Array.Empty<PropertyMaintenanceRecordData>()) maintenanceRecordsById[item.maintenanceRecordId] = item.Clone();

            Revision = Math.Max(0L, saveData.revision);
            worldId = saveData.worldId ?? worldId ?? string.Empty;
            return PropertyOperationResult.Success("Property runtime restored.", before, Revision);
        }

        public static bool ValidateSaveData(PropertyRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Property save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != CurrentSaveSchemaVersion)
            {
                failure = $"Unsupported property save schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> properties = new HashSet<string>((saveData.properties ?? Array.Empty<PropertyInstanceData>()).Select(item => item.propertyId ?? string.Empty), StringComparer.Ordinal);
            if (properties.Count != (saveData.properties ?? Array.Empty<PropertyInstanceData>()).Length || properties.Contains(string.Empty))
            {
                failure = "Property save data has duplicate or empty property IDs.";
                return false;
            }

            foreach (PropertyInstanceData property in saveData.properties ?? Array.Empty<PropertyInstanceData>())
            {
                if (registry != null && !registry.TryGet(property.propertyDefinitionId, out PropertyDefinition _))
                {
                    failure = $"Property '{property.propertyId}' references missing definition '{property.propertyDefinitionId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(property.parentPropertyId) && !properties.Contains(property.parentPropertyId))
                {
                    failure = $"Property '{property.propertyId}' references missing parent '{property.parentPropertyId}'.";
                    return false;
                }
            }

            if (!ValidateRecordSet(saveData.ownershipInterests, item => item.ownershipInterestId, item => item.propertyId, properties, "ownership interest", out failure)) return false;
            if (!ValidateRecordSet(saveData.titles, item => item.titleId, item => item.propertyId, properties, "title", out failure)) return false;
            if (!ValidateRecordSet(saveData.records, item => item.recordId, item => item.propertyId, properties, "property record", out failure)) return false;
            if (!ValidateRecordSet(saveData.possessions, item => item.possessionId, item => item.propertyId, properties, "possession", out failure)) return false;
            if (!ValidateRecordSet(saveData.occupancies, item => item.occupancyId, item => item.propertyId, properties, "occupancy", out failure)) return false;
            if (!ValidateRecordSet(saveData.tenancies, item => item.tenancyId, item => item.propertyId, properties, "tenancy", out failure)) return false;
            if (!ValidateRecordSet(saveData.accessRights, item => item.accessRightId, item => item.propertyId, properties, "access right", out failure)) return false;
            if (!ValidateRecordSet(saveData.rentObligations, item => item.rentObligationId, item => item.propertyId, properties, "rent obligation", out failure)) return false;
            if (!ValidateRecordSet(saveData.conditions, item => item.conditionRecordId, item => item.propertyId, properties, "condition", out failure)) return false;
            if (!ValidateRecordSet(saveData.maintenanceObligations, item => item.maintenanceObligationId, item => item.propertyId, properties, "maintenance obligation", out failure)) return false;
            return true;
        }

        private bool ValidateTransfer(PropertyTransferRequestData transfer, EconomyRuntime economy, out PropertyOperationCode code, out string failure)
        {
            code = PropertyOperationCode.Success;
            failure = string.Empty;
            if (transfer == null || string.IsNullOrWhiteSpace(transfer.transferId) || string.IsNullOrWhiteSpace(transfer.propertyId) || string.IsNullOrWhiteSpace(transfer.fromOwner?.subjectId) || string.IsNullOrWhiteSpace(transfer.toOwner?.subjectId))
            {
                code = PropertyOperationCode.InvalidRequest;
                failure = "Transfer ID, property ID, transferor, and transferee are required.";
                return false;
            }

            if (!propertiesById.ContainsKey(transfer.propertyId))
            {
                code = PropertyOperationCode.MissingProperty;
                failure = $"Property '{transfer.propertyId}' was not found.";
                return false;
            }

            if (!IsCurrentOwner(transfer.propertyId, transfer.fromOwner, transfer.effectiveWorldTime))
            {
                code = PropertyOperationCode.MissingAuthority;
                failure = "Transferor is not a current owner.";
                return false;
            }

            if (!ValidShare(transfer.share))
            {
                code = PropertyOperationCode.InvalidShare;
                failure = "Transfer share is invalid.";
                return false;
            }

            if (transfer.transferCategory == PropertyTransferCategory.Sale)
            {
                if (economy == null)
                {
                    code = PropertyOperationCode.PaymentFailed;
                    failure = "Economy runtime is required for property sale.";
                    return false;
                }

                if (transfer.considerationUnits <= 0L || string.IsNullOrWhiteSpace(transfer.currencyId) || string.IsNullOrWhiteSpace(transfer.buyerAccountId) || string.IsNullOrWhiteSpace(transfer.sellerAccountId))
                {
                    code = PropertyOperationCode.InvalidRequest;
                    failure = "Sale requires price, currency, buyer account, and seller account.";
                    return false;
                }
            }

            return true;
        }

        private bool ValidateOwnership(PropertyOwnershipInterestData ownership, double worldTime, out PropertyOperationCode code, out string failure)
        {
            code = PropertyOperationCode.Success;
            failure = string.Empty;
            if (ownership == null || string.IsNullOrWhiteSpace(ownership.ownershipInterestId) || string.IsNullOrWhiteSpace(ownership.propertyId) || string.IsNullOrWhiteSpace(ownership.owner?.subjectId))
            {
                code = PropertyOperationCode.InvalidRequest;
                failure = "Ownership ID, property ID, and owner are required.";
                return false;
            }

            if (!propertiesById.TryGetValue(ownership.propertyId, out PropertyInstanceData property))
            {
                code = PropertyOperationCode.MissingProperty;
                failure = $"Property '{ownership.propertyId}' was not found.";
                return false;
            }

            if (!ValidShare(ownership.ownershipShare))
            {
                code = PropertyOperationCode.InvalidShare;
                failure = "Ownership share is invalid.";
                return false;
            }

            if (TryGetPropertyDefinition(property.propertyDefinitionId, out PropertyDefinition definition) && !definition.PermittedOwnershipModels.Contains(ownership.ownershipModel))
            {
                code = PropertyOperationCode.PolicyViolation;
                failure = $"Property definition does not permit ownership model {ownership.ownershipModel}.";
                return false;
            }

            return true;
        }

        private bool ValidateActiveShareTotal(PropertyInstanceData property, string[] ownershipIds, double worldTime, out string failure)
        {
            failure = string.Empty;
            if (!TryGetPropertyDefinition(property.propertyDefinitionId, out PropertyDefinition definition))
            {
                return true;
            }

            PropertySharePolicyData policy = definition.OwnershipPolicy;
            if (!policy.requireExactTotalActiveShares)
            {
                return true;
            }

            long total = ownershipIds.Select(id => ownershipById[id]).Where(item => item.IsActiveAt(worldTime)).Sum(item => item.ownershipShare.units);
            if (total != policy.requiredTotalUnits)
            {
                failure = $"Active ownership shares total {total}, expected {policy.requiredTotalUnits}.";
                return false;
            }

            return true;
        }

        private bool TryGetPropertyDefinition(string definitionId, out PropertyDefinition definition)
        {
            definition = null;
            return registry != null && registry.TryGet(definitionId, out definition);
        }

        private bool IsCurrentOwner(string propertyId, PropertySubjectReferenceData subject, double worldTime)
        {
            string key = subject?.StableKey ?? string.Empty;
            return ownershipById.Values.Any(item => item.propertyId == propertyId && item.owner.StableKey == key && item.IsActiveAt(worldTime));
        }

        private bool WouldCreateCycle(string propertyId, string parentPropertyId)
        {
            string cursor = parentPropertyId;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(cursor))
            {
                if (!seen.Add(cursor) || string.Equals(cursor, propertyId, StringComparison.Ordinal))
                {
                    return true;
                }

                cursor = propertiesById.TryGetValue(cursor, out PropertyInstanceData parent) ? parent.parentPropertyId : string.Empty;
            }

            return false;
        }

        private void Touch()
        {
            Revision++;
        }

        private PropertyOperationResult Fail(PropertyOperationCode code, string message)
        {
            return PropertyOperationResult.Failure(code, message, Revision);
        }

        private static bool ValidShare(PropertyShareData share)
        {
            return share != null && share.totalUnits > 0L && share.units > 0L && share.units <= share.totalUnits;
        }

        private static bool SameProperty(PropertyInstanceData left, PropertyInstanceData right)
        {
            return left.propertyDefinitionId == right.propertyDefinitionId && left.parentPropertyId == right.parentPropertyId && left.spatialReferenceId == right.spatialReferenceId && left.sceneObjectReferenceId == right.sceneObjectReferenceId;
        }

        private static bool SameOwnership(PropertyOwnershipInterestData left, PropertyOwnershipInterestData right)
        {
            return left.propertyId == right.propertyId && left.owner.StableKey == right.owner.StableKey && left.ownershipShare.units == right.ownershipShare.units && left.ownershipShare.totalUnits == right.ownershipShare.totalUnits;
        }

        private static bool SameRent(RentObligationData left, RentObligationData right)
        {
            return left.tenancyId == right.tenancyId && left.currencyId == right.currencyId && left.totalDueUnits == right.totalDueUnits && Math.Abs(left.periodStartWorldTime - right.periodStartWorldTime) < 0.00001d;
        }

        private static string[] CleanIds(string[] values)
        {
            return PropertyModelHelpers.CloneIds(values);
        }

        private static string[] AddId(string[] values, string id)
        {
            return CleanIds((values ?? Array.Empty<string>()).Concat(new[] { id }).ToArray());
        }

        private static T[] AddEnum<T>(T[] values, T value)
            where T : Enum
        {
            return (values ?? Array.Empty<T>()).Concat(new[] { value }).Distinct().OrderBy(item => Convert.ToInt32(item)).ToArray();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static IEnumerable<T> Ordered<T>(IEnumerable<T> values, Func<T, string> key)
        {
            return (values ?? Enumerable.Empty<T>()).OrderBy(key, StringComparer.Ordinal);
        }

        private static IEnumerable<T> Ordered<T>(IEnumerable<T> values, Func<T, double> first, Func<T, string> second)
        {
            return (values ?? Enumerable.Empty<T>()).OrderBy(first).ThenBy(second, StringComparer.Ordinal);
        }

        private static bool ValidateRecordSet<T>(T[] records, Func<T, string> idSelector, Func<T, string> propertySelector, HashSet<string> propertyIds, string label, out string failure)
        {
            failure = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T record in records ?? Array.Empty<T>())
            {
                string id = idSelector(record) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    failure = $"Property save data has duplicate or empty {label} IDs.";
                    return false;
                }

                string propertyId = propertySelector(record) ?? string.Empty;
                if (!propertyIds.Contains(propertyId))
                {
                    failure = $"{label} '{id}' references missing property '{propertyId}'.";
                    return false;
                }
            }

            return true;
        }
    }
}
