using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Relationships
{
    public static class PrototypeRelationshipDefinitionFactory
    {
        public const string FriendRelationshipId = "relationship.prototype.friend";
        public const string ParentChildRelationshipId = "relationship.prototype.parent-child";
        public const string MentorStudentRelationshipId = "relationship.prototype.mentor-student";
        public const string RivalRelationshipId = "relationship.prototype.rival";
        public const string BiologicalParentChildRelationshipId = "relationship.family.biological-parent-child";
        public const string AdoptiveParentChildRelationshipId = "relationship.family.adoptive-parent-child";
        public const string LegalGuardianDependentRelationshipId = "relationship.family.legal-guardian-dependent";
        public const string FosterGuardianDependentRelationshipId = "relationship.family.foster-guardian-dependent";
        public const string SpouseRelationshipId = "relationship.family.spouse";
        public const string DomesticPartnerRelationshipId = "relationship.family.domestic-partner";
        public const string CourtshipPartnerRelationshipId = "relationship.family.courtship-partner";
        public const string EngagedPartnerRelationshipId = "relationship.family.engaged-partner";
        public const string SeparatedPartnerRelationshipId = "relationship.family.separated-partner";
        public const string FormerRomanticPartnerRelationshipId = "relationship.family.former-romantic-partner";

        public static IReadOnlyList<ScriptableObject> CreateDefinitions()
        {
            return new ScriptableObject[]
            {
                Definition(FriendRelationshipId, "Prototype Friendship", RelationshipCategory.Personal, RelationshipDirectionality.Symmetric, RelationshipDuplicatePolicy.OneActiveBetweenParticipants, true, Role("friend", "Friend")),
                Definition(ParentChildRelationshipId, "Prototype Parent Child", RelationshipCategory.Family, RelationshipDirectionality.Directed, RelationshipDuplicatePolicy.OneActivePerDefinitionAndRoles, false, Role("parent", "Parent"), Role("child", "Child")),
                Definition(MentorStudentRelationshipId, "Prototype Mentor Student", RelationshipCategory.Professional, RelationshipDirectionality.Directed, RelationshipDuplicatePolicy.OneActivePerDefinitionAndRoles, true, Role("mentor", "Mentor"), Role("student", "Student")),
                Definition(RivalRelationshipId, "Prototype Rivalry", RelationshipCategory.Conflict, RelationshipDirectionality.Symmetric, RelationshipDuplicatePolicy.OneActiveBetweenParticipants, true, Role("rival", "Rival")),
                Definition(BiologicalParentChildRelationshipId, "Biological Parent Child", RelationshipCategory.Family, RelationshipDirectionality.Directed, RelationshipDuplicatePolicy.OneActivePerDefinitionAndRoles, false, Role("parent", "Biological Parent"), Role("child", "Child"), "family", "parentage", "parentage:Biological", "visibility:Public"),
                Definition(AdoptiveParentChildRelationshipId, "Adoptive Parent Child", RelationshipCategory.Family, RelationshipDirectionality.Directed, RelationshipDuplicatePolicy.OneActivePerDefinitionAndRoles, false, Role("parent", "Adoptive Parent"), Role("child", "Child"), "family", "parentage", "parentage:Adoptive", "visibility:FamilyKnown"),
                Definition(LegalGuardianDependentRelationshipId, "Legal Guardian Dependent", RelationshipCategory.Family, RelationshipDirectionality.Directed, RelationshipDuplicatePolicy.OneActivePerDefinitionAndRoles, true, Role("guardian", "Legal Guardian"), Role("dependent", "Dependent"), "family", "guardian", "parentage:Legal", "visibility:ParticipantKnown"),
                Definition(FosterGuardianDependentRelationshipId, "Foster Guardian Dependent", RelationshipCategory.Family, RelationshipDirectionality.Directed, RelationshipDuplicatePolicy.OneActivePerDefinitionAndRoles, true, Role("guardian", "Foster Guardian"), Role("dependent", "Dependent"), "family", "guardian", "parentage:Foster", "visibility:ParticipantKnown"),
                Definition(SpouseRelationshipId, "Spouse", RelationshipCategory.Family, RelationshipDirectionality.Symmetric, RelationshipDuplicatePolicy.OneActiveBetweenParticipants, true, Role("partner", "Spouse"), "family", "romantic", "partnership", "visibility:Public"),
                Definition(DomesticPartnerRelationshipId, "Domestic Partner", RelationshipCategory.Family, RelationshipDirectionality.Symmetric, RelationshipDuplicatePolicy.OneActiveBetweenParticipants, true, Role("partner", "Domestic Partner"), "family", "romantic", "partnership", "visibility:ParticipantKnown"),
                Definition(CourtshipPartnerRelationshipId, "Courtship Partner", RelationshipCategory.Personal, RelationshipDirectionality.Symmetric, RelationshipDuplicatePolicy.OneActiveBetweenParticipants, true, Role("partner", "Courtship Partner"), "family", "romantic", "courtship", "visibility:ParticipantKnown"),
                Definition(EngagedPartnerRelationshipId, "Engaged Partner", RelationshipCategory.Personal, RelationshipDirectionality.Symmetric, RelationshipDuplicatePolicy.OneActiveBetweenParticipants, true, Role("partner", "Engaged Partner"), "family", "romantic", "engagement", "visibility:ParticipantKnown"),
                Definition(SeparatedPartnerRelationshipId, "Separated Partner", RelationshipCategory.Personal, RelationshipDirectionality.Symmetric, RelationshipDuplicatePolicy.OneActiveBetweenParticipants, true, Role("partner", "Separated Partner"), "family", "romantic", "separated", "visibility:Confidential"),
                Definition(FormerRomanticPartnerRelationshipId, "Former Romantic Partner", RelationshipCategory.Personal, RelationshipDirectionality.Symmetric, RelationshipDuplicatePolicy.AllowMultipleActive, true, Role("partner", "Former Partner"), "family", "romantic", "former", "historical", "visibility:ParticipantKnown")
            };
        }

        public static DefinitionRegistry AddMissingPrototypeRelationshipDefinitions(DefinitionRegistry baseRegistry)
        {
            IGameDefinition[] existing = baseRegistry == null
                ? new IGameDefinition[0]
                : baseRegistry.DefinitionsById.Values.ToArray();
            IGameDefinition[] additions = CreateDefinitions()
                .OfType<IGameDefinition>()
                .Where(definition => baseRegistry == null || !baseRegistry.Contains(definition.Id))
                .ToArray();
            return new DefinitionRegistry(existing.Concat(additions));
        }

        private static RelationshipDefinition Definition(string id, string name, RelationshipCategory category, RelationshipDirectionality directionality, RelationshipDuplicatePolicy duplicatePolicy, bool mayEnd, params RelationshipRoleDefinitionData[] roles)
        {
            return Definition(id, name, category, directionality, duplicatePolicy, mayEnd, roles, Array.Empty<string>());
        }

        private static RelationshipDefinition Definition(string id, string name, RelationshipCategory category, RelationshipDirectionality directionality, RelationshipDuplicatePolicy duplicatePolicy, bool mayEnd, RelationshipRoleDefinitionData role, params string[] tags)
        {
            return Definition(id, name, category, directionality, duplicatePolicy, mayEnd, new[] { role }, tags);
        }

        private static RelationshipDefinition Definition(string id, string name, RelationshipCategory category, RelationshipDirectionality directionality, RelationshipDuplicatePolicy duplicatePolicy, bool mayEnd, RelationshipRoleDefinitionData firstRole, RelationshipRoleDefinitionData secondRole, params string[] tags)
        {
            return Definition(id, name, category, directionality, duplicatePolicy, mayEnd, new[] { firstRole, secondRole }, tags);
        }

        private static RelationshipDefinition Definition(string id, string name, RelationshipCategory category, RelationshipDirectionality directionality, RelationshipDuplicatePolicy duplicatePolicy, bool mayEnd, RelationshipRoleDefinitionData[] roles, params string[] tags)
        {
            RelationshipDefinition definition = ScriptableObject.CreateInstance<RelationshipDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, directionality, duplicatePolicy, roles, mayEnd, tagIds: tags);
            return definition;
        }

        private static RelationshipRoleDefinitionData Role(string id, string name)
        {
            return new RelationshipRoleDefinitionData
            {
                roleId = id,
                displayName = name,
                required = true
            };
        }
    }
}
