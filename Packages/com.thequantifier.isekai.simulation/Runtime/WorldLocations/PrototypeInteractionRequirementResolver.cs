using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class PrototypeInteractionRequirementResolver : IInteractionRequirementResolver
    {
        private readonly OrganizationMembershipRuntime memberships;
        private readonly OrganizationAuthorityRuntime authority;
        private readonly Func<string, string, bool> legalRequirementCheck;
        private readonly Func<string, string, bool> itemRequirementCheck;
        private readonly Func<string, string, bool> statusRequirementCheck;

        public PrototypeInteractionRequirementResolver(
            OrganizationMembershipRuntime membershipRuntime,
            OrganizationAuthorityRuntime authorityRuntime,
            Func<string, string, bool> legalCheck = null,
            Func<string, string, bool> itemCheck = null,
            Func<string, string, bool> statusCheck = null)
        {
            memberships = membershipRuntime;
            authority = authorityRuntime;
            legalRequirementCheck = legalCheck;
            itemRequirementCheck = itemCheck;
            statusRequirementCheck = statusCheck;
        }

        public InteractionRequirementResolution Evaluate(InteractionRequirementContext context)
        {
            if (context?.Service == null) return new InteractionRequirementResolution(new[] { "service.missing" });
            string providerPersonId = PersonId(context.Provider);
            string consumerPersonId = PersonId(context.Consumer);
            List<string> unmet = new List<string>();
            OrganizationMembershipSnapshot[] providerMemberships = string.IsNullOrWhiteSpace(providerPersonId) || memberships == null
                ? Array.Empty<OrganizationMembershipSnapshot>()
                : memberships.QueryMemberships(providerPersonId, activeOnly: true).ToArray();

            foreach (string requirement in context.Service.MembershipRequirementIds)
            {
                if (!providerMemberships.Any(value => value.Data.membershipDefinitionId == requirement)) unmet.Add($"membership.{requirement}");
            }

            foreach (string requirement in context.Service.RankRequirementIds)
            {
                if (!providerMemberships.SelectMany(value => value.RankAssignments).Any(value => value.IsActive && value.rankDefinitionId == requirement)) unmet.Add($"rank.{requirement}");
            }

            foreach (string requirement in context.Service.OfficeRequirementIds)
            {
                bool satisfied = providerMemberships.SelectMany(value => value.OfficeAssignments)
                    .Where(value => value.IsActive)
                    .Any(value => value.officeId == requirement || memberships.TryGetOffice(value.officeId, out OrganizationOfficeSnapshot office) && office.Data.officeDefinitionId == requirement);
                if (!satisfied) unmet.Add($"office.{requirement}");
            }

            foreach (string requirement in context.Service.AuthorityRequirementIds)
            {
                bool satisfied = providerMemberships.Any(membership => authority != null
                    && authority.QueryEffectiveAuthority(providerPersonId, membership.OrganizationId, context.WorldTime, includeDelegated: true, privileged: false)
                        .Sources.Any(source => source.permissionDefinitionId == requirement && !source.denied));
                if (!satisfied) unmet.Add($"authority.{requirement}");
            }

            foreach (string requirement in context.Service.LegalRequirementIds)
            {
                if (legalRequirementCheck == null || !legalRequirementCheck(providerPersonId, requirement)) unmet.Add($"legal.{requirement}");
            }

            foreach (string requirement in context.Service.ItemRequirementIds)
            {
                if (itemRequirementCheck == null || !itemRequirementCheck(consumerPersonId, requirement)) unmet.Add($"item.{requirement}");
            }

            foreach (string requirement in context.Service.StatusRequirementIds)
            {
                if (statusRequirementCheck == null || !statusRequirementCheck(consumerPersonId, requirement)) unmet.Add($"status.{requirement}");
            }

            return new InteractionRequirementResolution(unmet, unmet.Count == 0 ? "Interaction requirements satisfied." : "One or more interaction requirements are not satisfied.");
        }

        private static string PersonId(EntityLocationReferenceData entity)
        {
            return entity?.entityType == LocationOccupantEntityType.Person ? entity.entityId ?? string.Empty : string.Empty;
        }
    }
}
