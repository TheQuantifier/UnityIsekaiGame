namespace UnityIsekaiGame.Social.Networks
{
    public enum SocialGraphNodeKind { Person = 0 }
    public enum SocialGraphEdgeKind { ObjectiveRelationship = 0, DirectedAttitude = 1, MutualAttitude = 2, RecentInteraction = 3, RumorTransmission = 4, SharedGroupMembership = 5, CustomRegisteredProjection = 100 }
    public enum SocialGraphDirectionPolicy { PreserveDirection = 0, RequireMutual = 1, MaximumDirection = 2, MinimumDirection = 3, AverageDirections = 4, SumDirections = 5, ExplicitRelationshipSymmetry = 6 }
    public enum SocialGraphWeightPolicy { Authored = 0, RelationshipCategory = 1, AttitudeMagnitude = 2, InteractionFrequency = 3, RumorTransmissionCount = 4, SharedGroupMembership = 5, Composite = 6 }
    public enum SocialGraphValence { Neutral = 0, Positive = 1, Negative = 2, Mixed = 3, Unsigned = 4 }
    public enum SocialGraphVisibility { Authoritative = 0, ParticipantSafe = 1, KnowledgeSafe = 2, Development = 3 }
    public enum SocialNetworkOperationStatus { Succeeded = 0, Preview = 1, Duplicate = 2, InvalidRequest = 10, MissingDefinitionRegistry = 11, MissingDefinition = 12, DuplicateGroupId = 13, MissingGroup = 14, DuplicateMembershipId = 15, DuplicateActiveMembership = 16, UnknownPerson = 17, InvalidRole = 18, InvalidLifecycle = 19, GroupDissolved = 20, LimitExceeded = 21, RestoreFailed = 30, ValidationFailed = 31, RuntimeNotReady = 32 }
    public enum InformalSocialGroupCategory { FriendCircle = 0, AdventuringParty = 1, TravelingCompanions = 2, HouseholdCircle = 3, StudyGroup = 4, WorkPeerCircle = 5, CourtCircle = 6, NeighborhoodCircle = 7, Custom = 100 }
    public enum InformalSocialGroupVisibility { Public = 0, MembersOnly = 1, Secret = 2, Diagnostic = 3 }
    public enum InformalSocialGroupLifecycleStatus { Active = 0, Dormant = 1, Dissolved = 2 }
    public enum SocialGroupMembershipStatus { Candidate = 0, Invited = 1, Active = 2, Departed = 3, Removed = 4 }
    public enum SocialGroupMutationKind { CreateGroup = 0, AddMembership = 1, ChangeMembershipRole = 2, EndMembership = 3, DissolveGroup = 4, CreateGroupFromCandidate = 5 }
}
