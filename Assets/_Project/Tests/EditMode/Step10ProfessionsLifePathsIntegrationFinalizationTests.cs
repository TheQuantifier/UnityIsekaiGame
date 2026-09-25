using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Professions.Integration;

namespace UnityIsekaiGame.Tests
{
    public sealed class Step10ProfessionsLifePathsIntegrationFinalizationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = "person.step10.integration";
        private const string OtherPersonId = "person.step10.other";
        private const string GuildOrganizationId = "organization.prototype.guild";
        private const string GuildAuthorityId = "authority.guild.prototype";

        [Test]
        public void AuthorityMapAndPersistenceDependenciesAreCompleteAndAcyclic()
        {
            Assert.That(Step10IntegrationValidator.AuthorityMap.Select(entry => entry.Domain).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(Step10IntegrationValidator.AuthorityMap.Count));

            Step10IntegrationValidationReport report = Step10IntegrationValidator.ValidateRuntimeGraph(new Step10IntegrationRuntimeSnapshot(), PrototypeRegistry());

            Assert.That(report.Diagnostics.Where(diagnostic => diagnostic.Domain == Step10IntegrationDiagnosticDomain.Authority && diagnostic.Severity == Step10IntegrationDiagnosticSeverity.Error), Is.Empty);
            Assert.That(report.Diagnostics.Where(diagnostic => diagnostic.Domain == Step10IntegrationDiagnosticDomain.Persistence && diagnostic.Severity == Step10IntegrationDiagnosticSeverity.Error), Is.Empty);
        }

        [Test]
        public void PrototypeCatalogProvidesStep10DefinitionsWithoutIntegrationErrors()
        {
            DefinitionRegistry registry = PrototypeRegistry();

            Step10IntegrationValidationReport report = Step10IntegrationValidator.ValidateDefinitions(registry);

            Assert.That(report.ErrorCount, Is.Zero, string.Join("\n", report.Diagnostics));
        }

        [Test]
        public void RuntimeGraphAcceptsCoherentCrossRuntimeCareerChain()
        {
            Step10IntegrationValidationReport report = Step10IntegrationValidator.ValidateRuntimeGraph(
                ValidSnapshot(),
                PrototypeRegistry(),
                new[] { PersonId, OtherPersonId },
                new[] { GuildOrganizationId },
                KnownAuthorities());

            Assert.That(report.ErrorCount, Is.Zero, string.Join("\n", report.Diagnostics));
        }

        [Test]
        public void RuntimeGraphDetectsCrossRuntimeConflictsAndMissingReferences()
        {
            Step10IntegrationRuntimeSnapshot snapshot = ValidSnapshot();
            snapshot.Positions.positions[0].holderPersonIds = Array.Empty<string>();
            snapshot.LifePaths.goals[0].targetCredentialDefinitionId = "credential.missing";
            snapshot.CareerHistory.episodes[0].employmentId = "employment.missing";

            Step10IntegrationValidationReport report = Step10IntegrationValidator.ValidateRuntimeGraph(
                snapshot,
                PrototypeRegistry(),
                new[] { PersonId, OtherPersonId },
                new[] { GuildOrganizationId },
                KnownAuthorities());

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "EmploymentHolderMissingFromPosition"), Is.True);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "GoalTargetCredentialMissing"), Is.True);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "CareerEmploymentMissing"), Is.True);
        }

        [Test]
        public void SaveSchemaValidationRejectsUnsupportedStep10VersionBeforeRuntimeRestore()
        {
            Step10IntegrationRuntimeSnapshot snapshot = ValidSnapshot();
            snapshot.LifePaths.schemaVersion = LifePathRuntimeSaveData.CurrentSchemaVersion + 1;

            Step10IntegrationValidationReport report = Step10IntegrationValidator.ValidateRuntimeGraph(
                snapshot,
                PrototypeRegistry(),
                new[] { PersonId, OtherPersonId },
                new[] { GuildOrganizationId },
                KnownAuthorities());

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "UnsupportedSchemaVersion" && diagnostic.SubjectId == "LifePathRuntime"), Is.True);
        }

        [Test]
        public void CanonicalFingerprintIsDeterministicAndOrderIndependent()
        {
            Step10IntegrationRuntimeSnapshot first = ValidSnapshot();
            Step10IntegrationRuntimeSnapshot second = first.Clone();
            second.LifePaths.goals.Reverse();
            second.Activities.activities.Reverse();
            second.Positions.employments.Reverse();

            string firstFingerprint = Step10IntegrationValidator.CreateCanonicalFingerprint(first);
            string secondFingerprint = Step10IntegrationValidator.CreateCanonicalFingerprint(second);

            Assert.That(firstFingerprint, Is.EqualTo(secondFingerprint));
        }

        [Test]
        public void RuntimeSnapshotClonesInputsAndRemainsImmutableAfterSourceMutation()
        {
            Step10IntegrationRuntimeSnapshot source = ValidSnapshot();
            Step10IntegrationRuntimeSnapshot snapshot = source.Clone();

            source.Professions.relationships[0].relationshipId = "profession.changed";
            source.LifePaths.goals[0].goalId = "goal.changed";

            Assert.That(snapshot.Professions.relationships.Single().relationshipId, Is.EqualTo("profession.step10.blacksmith"));
            Assert.That(snapshot.LifePaths.goals.Single().goalId, Is.EqualTo("goal.step10.credential"));
            Assert.That(Step10IntegrationValidator.CreateCanonicalFingerprint(snapshot), Is.EqualTo(Step10IntegrationValidator.CreateCanonicalFingerprint(snapshot.Clone())));
        }

        private static Step10IntegrationRuntimeSnapshot ValidSnapshot()
        {
            return new Step10IntegrationRuntimeSnapshot(
                professions: Professions(),
                training: Training(),
                activities: Activities(),
                credentials: Credentials(),
                ranks: Ranks(),
                positions: Positions(),
                careerHistory: CareerHistory(),
                lifePaths: LifePaths());
        }

        private static PersonProfessionRuntimeSaveData Professions()
        {
            return new PersonProfessionRuntimeSaveData
            {
                relationships =
                {
                    new PersonProfessionRelationshipData
                    {
                        relationshipId = "profession.step10.blacksmith",
                        personId = PersonId,
                        professionId = ProfessionContentIds.BlacksmithProfessionId,
                        specializationIds = new[] { ProfessionContentIds.WeaponsmithSpecializationId },
                        state = ProfessionRelationshipState.RecognizedPractitioner,
                        active = true,
                        primary = true,
                        formalPractice = true,
                        recognized = true,
                        recognizingAuthorityId = GuildAuthorityId,
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                }
            };
        }

        private static TrainingRuntimeSaveData Training()
        {
            return new TrainingRuntimeSaveData
            {
                enrollments =
                {
                    new TrainingEnrollmentData
                    {
                        enrollmentId = "training.step10.apprenticeship",
                        personId = PersonId,
                        programId = ProfessionContentIds.BlacksmithApprenticeshipProgramId,
                        relatedProfessionId = ProfessionContentIds.BlacksmithProfessionId,
                        relatedSpecializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                        state = TrainingEnrollmentState.Completed,
                        completedModuleIds = new[]
                        {
                            ProfessionContentIds.BlacksmithBasicsModuleId,
                            ProfessionContentIds.BlacksmithPracticeModuleId,
                            ProfessionContentIds.BlacksmithHiddenAssessmentModuleId
                        },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                }
            };
        }

        private static ProfessionalActivityRuntimeSaveData Activities()
        {
            return new ProfessionalActivityRuntimeSaveData
            {
                activities =
                {
                    new ProfessionalActivityRecordData
                    {
                        activityId = "activity.step10.crafting",
                        personId = PersonId,
                        professionId = ProfessionContentIds.BlacksmithProfessionId,
                        specializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                        activityDefinitionId = ProfessionContentIds.BlacksmithCraftingActivityDefinitionId,
                        source = new ProfessionalActivitySourceReferenceData
                        {
                            sourceType = ProfessionalActivitySourceType.CraftingOperation,
                            sourceId = "craft.step10.sword",
                            sourceRevision = 1L
                        },
                        category = ProfessionalActivityCategory.Crafting,
                        state = ProfessionalActivityState.Validated,
                        outcome = ProfessionalActivityOutcomeState.Successful,
                        quality = 800,
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                },
                evidence =
                {
                    new ProfessionalExperienceEvidenceData
                    {
                        evidenceId = "evidence.step10.crafting",
                        activityId = "activity.step10.crafting",
                        personId = PersonId,
                        professionId = ProfessionContentIds.BlacksmithProfessionId,
                        category = ProfessionalExperienceCategory.IndependentWork,
                        outcome = ProfessionalActivityOutcomeState.Successful,
                        source = new ProfessionalActivitySourceReferenceData
                        {
                            sourceType = ProfessionalActivitySourceType.CraftingOperation,
                            sourceId = "craft.step10.sword",
                            sourceRevision = 1L
                        },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                }
            };
        }

        private static CredentialRuntimeSaveData Credentials()
        {
            return new CredentialRuntimeSaveData
            {
                credentials =
                {
                    new CredentialRecordData
                    {
                        credentialId = "credential.step10.guild-license",
                        credentialDefinitionId = ProfessionContentIds.BlacksmithGuildLicenseCredentialId,
                        recipientPersonId = PersonId,
                        state = CredentialState.Active,
                        authenticityState = CredentialAuthenticityState.Authoritative,
                        issuer = new CredentialIssuerReferenceData
                        {
                            issuerId = GuildAuthorityId,
                            issuerKind = CredentialIssuerAuthorityKind.ProfessionalOrganization
                        },
                        supportingTrainingRecordIds = new[] { "training.step10.apprenticeship" },
                        supportingExperienceEvidenceIds = new[] { "evidence.step10.crafting" },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                }
            };
        }

        private static ProfessionalRankRuntimeSaveData Ranks()
        {
            return new ProfessionalRankRuntimeSaveData
            {
                ranks =
                {
                    new ProfessionalRankRecordData
                    {
                        rankRecordId = "rank.step10.journeyman",
                        personId = PersonId,
                        professionId = ProfessionContentIds.BlacksmithProfessionId,
                        specializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                        ladderDefinitionId = ProfessionContentIds.BlacksmithRankLadderId,
                        rankDefinitionId = ProfessionContentIds.BlacksmithRankJourneymanId,
                        state = ProfessionalRankState.Active,
                        recognizingAuthorityId = GuildAuthorityId,
                        supportingCredentialIds = new[] { "credential.step10.guild-license" },
                        supportingExperienceEvidenceIds = new[] { "evidence.step10.crafting" },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                }
            };
        }

        private static PositionEmploymentRuntimeSaveData Positions()
        {
            return new PositionEmploymentRuntimeSaveData
            {
                positions =
                {
                    new PositionInstanceData
                    {
                        positionInstanceId = "position.step10.guild-clerk",
                        positionDefinitionId = ProfessionContentIds.GuildClerkPositionId,
                        organizationId = GuildOrganizationId,
                        organizationTypeId = ProfessionContentIds.GuildOrganizationTypeId,
                        state = PositionInstanceState.Filled,
                        holderPersonIds = new[] { PersonId },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                },
                employments =
                {
                    new EmploymentRecordData
                    {
                        employmentId = "employment.step10.guild-clerk",
                        personId = PersonId,
                        employerOrganizationId = GuildOrganizationId,
                        positionInstanceId = "position.step10.guild-clerk",
                        positionDefinitionId = ProfessionContentIds.GuildClerkPositionId,
                        classification = EmploymentClassification.Permanent,
                        state = EmploymentState.Active,
                        appointmentAuthorityId = ProfessionContentIds.PositionAppointAuthorityId,
                        dutyAssignmentIds = new[] { "duty.step10.records" },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                },
                duties =
                {
                    new DutyAssignmentData
                    {
                        assignmentId = "duty.step10.records",
                        employmentId = "employment.step10.guild-clerk",
                        positionInstanceId = "position.step10.guild-clerk",
                        dutyDefinitionId = ProfessionContentIds.GuildClerkRecordDutyId,
                        assignedPersonId = PersonId,
                        state = DutyAssignmentState.Active,
                        completionEvidenceReferenceIds = new[] { "activity.step10.crafting" },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                }
            };
        }

        private static CareerHistoryRuntimeSaveData CareerHistory()
        {
            return new CareerHistoryRuntimeSaveData
            {
                episodes =
                {
                    new CareerEpisodeData
                    {
                        episodeId = "career.step10.guild",
                        personId = PersonId,
                        category = CareerEpisodeCategory.Employment,
                        professionId = ProfessionContentIds.BlacksmithProfessionId,
                        employmentId = "employment.step10.guild-clerk",
                        positionInstanceId = "position.step10.guild-clerk",
                        organizationId = GuildOrganizationId,
                        rankRecordId = "rank.step10.journeyman",
                        credentialId = "credential.step10.guild-license",
                        state = CareerEpisodeState.Active,
                        primaryCareer = true,
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                }
            };
        }

        private static LifePathRuntimeSaveData LifePaths()
        {
            return new LifePathRuntimeSaveData
            {
                lifePaths =
                {
                    new LifePathRecordData
                    {
                        lifePathId = "life-path.step10",
                        personId = PersonId,
                        state = LifePathState.Active,
                        professionRelationshipIds = new[] { "profession.step10.blacksmith" },
                        careerEpisodeIds = new[] { "career.step10.guild" },
                        credentialIds = new[] { "credential.step10.guild-license" },
                        rankRecordIds = new[] { "rank.step10.journeyman" },
                        positionInstanceIds = new[] { "position.step10.guild-clerk" },
                        employmentIds = new[] { "employment.step10.guild-clerk" },
                        activeAspirationIds = new[] { "aspiration.step10.guild-license" },
                        activeGoalIds = new[] { "goal.step10.credential" },
                        primaryProfessionalIdentityId = "identity.step10.blacksmith",
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                },
                aspirations =
                {
                    new PersonAspirationData
                    {
                        aspirationId = "aspiration.step10.guild-license",
                        personId = PersonId,
                        aspirationDefinitionId = ProfessionContentIds.AspirationEarnGuildLicenseId,
                        targetSubjectType = LifePathTargetSubjectType.Credential,
                        targetProfessionId = ProfessionContentIds.BlacksmithProfessionId,
                        targetCredentialDefinitionId = ProfessionContentIds.BlacksmithGuildLicenseCredentialId,
                        state = PersonAspirationState.Active,
                        relatedGoalIds = new[] { "goal.step10.credential" },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                },
                goals =
                {
                    new PersonGoalData
                    {
                        goalId = "goal.step10.credential",
                        personId = PersonId,
                        goalDefinitionId = ProfessionContentIds.GoalEarnBlacksmithGuildLicenseId,
                        parentAspirationId = "aspiration.step10.guild-license",
                        targetSubjectType = LifePathTargetSubjectType.Credential,
                        targetProfessionId = ProfessionContentIds.BlacksmithProfessionId,
                        targetCredentialDefinitionId = ProfessionContentIds.BlacksmithGuildLicenseCredentialId,
                        state = PersonGoalState.Completed,
                        progressState = LifeGoalProgressState.Satisfied,
                        completedRequirementIds = new[] { "credential.step10.guild-license" },
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                },
                identities =
                {
                    new ProfessionalIdentityData
                    {
                        identityId = "identity.step10.blacksmith",
                        personId = PersonId,
                        kind = ProfessionalIdentityKind.Primary,
                        alignment = ProfessionalIdentityAlignmentState.Aligned,
                        professionId = ProfessionContentIds.BlacksmithProfessionId,
                        professionRelationshipId = "profession.step10.blacksmith",
                        careerEpisodeId = "career.step10.guild",
                        active = true,
                        accessPolicyId = ProfessionContentIds.AccessPublicId
                    }
                }
            };
        }

        private static string[] KnownAuthorities()
        {
            return new[]
            {
                GuildAuthorityId,
                ProfessionContentIds.PositionAppointAuthorityId,
                ProfessionContentIds.PositionDutyAssignAuthorityId,
                ProfessionContentIds.PositionSuperviseAuthorityId,
                ProfessionContentIds.PositionRestrictedRecordsAuthorityId
            };
        }

        private static DefinitionRegistry PrototypeRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog.CreateRegistry();
        }
    }
}
