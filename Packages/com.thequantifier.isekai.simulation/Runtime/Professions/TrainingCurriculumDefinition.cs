using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.Sharing;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class TrainingInstructorRequirementData
    {
        public string requirementId;
        public TrainingInstructorRoleKind role = TrainingInstructorRoleKind.Instructor;
        public string requiredProfessionId;
        public string requiredSpecializationId;
        public string[] requiredSkillIds;
        public string[] requiredKnowledgeSubjectIds;
        public string requiredOrganizationId;
        public string requiredAuthorityId;
        public string accessPolicyId;
        public int maximumLearnerCapacity = 1;

        public TrainingInstructorRequirementData Clone()
        {
            return new TrainingInstructorRequirementData
            {
                requirementId = requirementId ?? string.Empty,
                role = role,
                requiredProfessionId = requiredProfessionId ?? string.Empty,
                requiredSpecializationId = requiredSpecializationId ?? string.Empty,
                requiredSkillIds = Clean(requiredSkillIds),
                requiredKnowledgeSubjectIds = Clean(requiredKnowledgeSubjectIds),
                requiredOrganizationId = requiredOrganizationId ?? string.Empty,
                requiredAuthorityId = requiredAuthorityId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                maximumLearnerCapacity = Math.Max(0, maximumLearnerCapacity)
            };
        }

        public void Validate(string context, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(requirementId))
            {
                report.AddError($"{context} is missing a stable requirement ID.");
            }

            if (!Enum.IsDefined(typeof(TrainingInstructorRoleKind), role))
            {
                report.AddError($"{context} has an invalid instructor role.");
            }

            ValidateReference<ProfessionDefinition>(requiredProfessionId, "requiredProfessionId", context, definitionsById, report, allowMissing: true);
            ValidateReference<ProfessionSpecializationDefinition>(requiredSpecializationId, "requiredSpecializationId", context, definitionsById, report, allowMissing: true);
            ValidateReferences<KnowledgeFactDefinition>(requiredKnowledgeSubjectIds, "requiredKnowledgeSubjectIds", context, definitionsById, report, allowMissing: true);
            ValidateReference<InformationAccessPolicyDefinition>(accessPolicyId, "accessPolicyId", context, definitionsById, report, allowMissing: true);
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static void ValidateReferences<TDefinition>(IEnumerable<string> ids, string field, string context, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, bool allowMissing = false)
            where TDefinition : class, IGameDefinition
        {
            foreach (string id in ids ?? Array.Empty<string>())
            {
                ValidateReference<TDefinition>(id, field, context, definitionsById, report, allowMissing);
            }
        }

        private static void ValidateReference<TDefinition>(string id, string field, string context, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, bool allowMissing = false)
            where TDefinition : class, IGameDefinition
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (definitionsById == null || !definitionsById.TryGetValue(id, out IGameDefinition definition))
            {
                if (!allowMissing)
                {
                    report.AddError($"{context} field '{field}' references missing definition '{id}'.");
                }

                return;
            }

            if (definition is not TDefinition)
            {
                report.AddError($"{context} field '{field}' references '{id}' as {typeof(TDefinition).Name}, but found {definition.GetType().Name}.");
            }
        }
    }

    [Serializable]
    public sealed class TrainingModuleDefinitionData
    {
        public string moduleId;
        public string displayName;
        public bool required = true;
        public bool hiddenFromLearner;
        public string[] dependencyModuleIds;
        public string[] lessonIds;
        public string[] practiceRequirementIds;
        public string[] assignmentIds;
        public string completionPolicyId;
        public string[] instructorRequirementIds;
        public string[] environmentRequirementIds;
        public string accessPolicyId;
        public double estimatedDurationHours;

        public TrainingModuleDefinitionData Clone()
        {
            return new TrainingModuleDefinitionData
            {
                moduleId = moduleId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                required = required,
                hiddenFromLearner = hiddenFromLearner,
                dependencyModuleIds = Clean(dependencyModuleIds),
                lessonIds = Clean(lessonIds),
                practiceRequirementIds = Clean(practiceRequirementIds),
                assignmentIds = Clean(assignmentIds),
                completionPolicyId = completionPolicyId ?? string.Empty,
                instructorRequirementIds = Clean(instructorRequirementIds),
                environmentRequirementIds = Clean(environmentRequirementIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                estimatedDurationHours = Math.Max(0d, estimatedDurationHours)
            };
        }

        internal static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class TrainingLessonDefinitionData
    {
        public string lessonId;
        public string moduleId;
        public string displayName;
        public TrainingTeachingMethod teachingMethod = TrainingTeachingMethod.Lecture;
        public string informationTransferDefinitionId;
        public string[] knowledgeSubjectIds;
        public string[] teacherRequirementIds;
        public string[] learnerRequirementIds;
        public string[] sourceOrRecordIds;
        public string observationFoundationId;
        public string practicalActivityFoundationId;
        public string completionEvidencePolicyId;
        public double durationHours;
        public string accessPolicyId;

        public TrainingLessonDefinitionData Clone()
        {
            return new TrainingLessonDefinitionData
            {
                lessonId = lessonId ?? string.Empty,
                moduleId = moduleId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                teachingMethod = teachingMethod,
                informationTransferDefinitionId = informationTransferDefinitionId ?? string.Empty,
                knowledgeSubjectIds = TrainingModuleDefinitionData.Clean(knowledgeSubjectIds),
                teacherRequirementIds = TrainingModuleDefinitionData.Clean(teacherRequirementIds),
                learnerRequirementIds = TrainingModuleDefinitionData.Clean(learnerRequirementIds),
                sourceOrRecordIds = TrainingModuleDefinitionData.Clean(sourceOrRecordIds),
                observationFoundationId = observationFoundationId ?? string.Empty,
                practicalActivityFoundationId = practicalActivityFoundationId ?? string.Empty,
                completionEvidencePolicyId = completionEvidencePolicyId ?? string.Empty,
                durationHours = Math.Max(0d, durationHours),
                accessPolicyId = accessPolicyId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class TrainingPracticalAssignmentDefinitionData
    {
        public string assignmentId;
        public string moduleId;
        public TrainingAssignmentActivityCategory activityCategory = TrainingAssignmentActivityCategory.Crafting;
        public string requiredProfessionId;
        public string requiredRecipeId;
        public string requiredProductionActivityId;
        public int requiredQuantity = 1;
        public int qualityThreshold;
        public int maximumFailureCount;
        public bool supervisionRequired;
        public string[] requiredToolIds;
        public string[] requiredStationIds;
        public string[] evidenceRequirementIds;
        public bool exclusiveActivityReference = true;
        public string completionPolicyId;
        public string accessPolicyId;

        public TrainingPracticalAssignmentDefinitionData Clone()
        {
            return new TrainingPracticalAssignmentDefinitionData
            {
                assignmentId = assignmentId ?? string.Empty,
                moduleId = moduleId ?? string.Empty,
                activityCategory = activityCategory,
                requiredProfessionId = requiredProfessionId ?? string.Empty,
                requiredRecipeId = requiredRecipeId ?? string.Empty,
                requiredProductionActivityId = requiredProductionActivityId ?? string.Empty,
                requiredQuantity = Math.Max(0, requiredQuantity),
                qualityThreshold = Math.Max(0, qualityThreshold),
                maximumFailureCount = Math.Max(0, maximumFailureCount),
                supervisionRequired = supervisionRequired,
                requiredToolIds = TrainingModuleDefinitionData.Clean(requiredToolIds),
                requiredStationIds = TrainingModuleDefinitionData.Clean(requiredStationIds),
                evidenceRequirementIds = TrainingModuleDefinitionData.Clean(evidenceRequirementIds),
                exclusiveActivityReference = exclusiveActivityReference,
                completionPolicyId = completionPolicyId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty
            };
        }
    }

    [CreateAssetMenu(fileName = "TrainingCurriculumDefinition", menuName = "Unity Isekai Game/Professions/Training Curriculum")]
    public sealed class TrainingCurriculumDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string curriculumId;
        [SerializeField] private string programId;
        [SerializeField] private string displayName;
        [SerializeField] private TrainingModuleDefinitionData[] modules;
        [SerializeField] private TrainingLessonDefinitionData[] lessons;
        [SerializeField] private TrainingPracticalAssignmentDefinitionData[] practicalAssignments;
        [SerializeField] private string[] knowledgeSubjectIds;
        [SerializeField] private string[] skillPracticeRequirementIds;
        [SerializeField] private string[] capabilityRequirementIds;
        [SerializeField] private string[] readingRecordRequirementIds;
        [SerializeField] private string[] observationRequirementIds;
        [SerializeField, Range(0, 1000)] private int minimumCompletionThreshold = 1000;
        [SerializeField] private string[] allowedAlternativeRequirementIds;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => curriculumId ?? string.Empty;
        public string ProgramId => programId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public IReadOnlyList<TrainingModuleDefinitionData> Modules => (modules ?? Array.Empty<TrainingModuleDefinitionData>()).Select(module => module?.Clone()).Where(module => module != null).ToArray();
        public IReadOnlyList<TrainingLessonDefinitionData> Lessons => (lessons ?? Array.Empty<TrainingLessonDefinitionData>()).Select(lesson => lesson?.Clone()).Where(lesson => lesson != null).ToArray();
        public IReadOnlyList<TrainingPracticalAssignmentDefinitionData> PracticalAssignments => (practicalAssignments ?? Array.Empty<TrainingPracticalAssignmentDefinitionData>()).Select(assignment => assignment?.Clone()).Where(assignment => assignment != null).ToArray();
        public IReadOnlyList<string> KnowledgeSubjectIds => knowledgeSubjectIds ?? Array.Empty<string>();
        public IReadOnlyList<string> SkillPracticeRequirementIds => skillPracticeRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> CapabilityRequirementIds => capabilityRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ReadingRecordRequirementIds => readingRecordRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ObservationRequirementIds => observationRequirementIds ?? Array.Empty<string>();
        public int MinimumCompletionThreshold => Mathf.Clamp(minimumCompletionThreshold, 0, 1000);
        public IReadOnlyList<string> AllowedAlternativeRequirementIds => allowedAlternativeRequirementIds ?? Array.Empty<string>();
        public int Version => Math.Max(1, version);

        private void OnValidate()
        {
            curriculumId = curriculumId?.Trim();
            programId = programId?.Trim();
            minimumCompletionThreshold = Mathf.Clamp(minimumCompletionThreshold, 0, 1000);
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string program,
            string label,
            TrainingModuleDefinitionData[] moduleDefinitions,
            TrainingLessonDefinitionData[] lessonDefinitions,
            TrainingPracticalAssignmentDefinitionData[] assignments = null,
            string[] knowledgeSubjects = null,
            string[] skillPractice = null,
            string[] capabilities = null,
            string[] readingRecords = null,
            string[] observations = null,
            int completionThreshold = 1000,
            string[] alternatives = null)
        {
            curriculumId = id?.Trim();
            programId = program?.Trim();
            displayName = string.IsNullOrWhiteSpace(label) ? id : label;
            modules = (moduleDefinitions ?? Array.Empty<TrainingModuleDefinitionData>()).Select(module => module?.Clone()).Where(module => module != null).ToArray();
            lessons = (lessonDefinitions ?? Array.Empty<TrainingLessonDefinitionData>()).Select(lesson => lesson?.Clone()).Where(lesson => lesson != null).ToArray();
            practicalAssignments = (assignments ?? Array.Empty<TrainingPracticalAssignmentDefinitionData>()).Select(assignment => assignment?.Clone()).Where(assignment => assignment != null).ToArray();
            knowledgeSubjectIds = TrainingModuleDefinitionData.Clean(knowledgeSubjects);
            skillPracticeRequirementIds = TrainingModuleDefinitionData.Clean(skillPractice);
            capabilityRequirementIds = TrainingModuleDefinitionData.Clean(capabilities);
            readingRecordRequirementIds = TrainingModuleDefinitionData.Clean(readingRecords);
            observationRequirementIds = TrainingModuleDefinitionData.Clean(observations);
            minimumCompletionThreshold = Mathf.Clamp(completionThreshold, 0, 1000);
            allowedAlternativeRequirementIds = TrainingModuleDefinitionData.Clean(alternatives);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Training Curriculum '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("training.curriculum.", StringComparison.Ordinal))
            {
                report.AddWarning($"Training Curriculum '{Id}' should use the 'training.curriculum.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(ProgramId))
            {
                report.AddError($"Training Curriculum '{DisplayName}' is missing a program ID.");
            }

            Dictionary<string, TrainingModuleDefinitionData> moduleMap = new Dictionary<string, TrainingModuleDefinitionData>(StringComparer.Ordinal);
            foreach (TrainingModuleDefinitionData module in Modules)
            {
                if (string.IsNullOrWhiteSpace(module.moduleId) || moduleMap.ContainsKey(module.moduleId))
                {
                    report.AddError($"Training Curriculum '{DisplayName}' has duplicate or blank module ID '{module.moduleId}'.");
                    continue;
                }

                moduleMap[module.moduleId] = module;
            }

            Dictionary<string, TrainingLessonDefinitionData> lessonMap = new Dictionary<string, TrainingLessonDefinitionData>(StringComparer.Ordinal);
            foreach (TrainingLessonDefinitionData lesson in Lessons)
            {
                if (string.IsNullOrWhiteSpace(lesson.lessonId) || lessonMap.ContainsKey(lesson.lessonId))
                {
                    report.AddError($"Training Curriculum '{DisplayName}' has duplicate or blank lesson ID '{lesson.lessonId}'.");
                    continue;
                }

                lessonMap[lesson.lessonId] = lesson;
                if (!moduleMap.ContainsKey(lesson.moduleId ?? string.Empty))
                {
                    report.AddError($"Training Lesson '{lesson.lessonId}' references missing module '{lesson.moduleId}'.");
                }

                if (!Enum.IsDefined(typeof(TrainingTeachingMethod), lesson.teachingMethod))
                {
                    report.AddError($"Training Lesson '{lesson.lessonId}' has invalid teaching method '{lesson.teachingMethod}'.");
                }

                if (!string.IsNullOrWhiteSpace(lesson.informationTransferDefinitionId))
                {
                    if (definitionsById == null || !definitionsById.TryGetValue(lesson.informationTransferDefinitionId, out IGameDefinition definition))
                    {
                        report.AddError($"Training Lesson '{lesson.lessonId}' references missing Information Transfer definition '{lesson.informationTransferDefinitionId}'.");
                    }
                    else if (definition is not InformationTransferDefinition transferDefinition)
                    {
                        report.AddError($"Training Lesson '{lesson.lessonId}' references '{lesson.informationTransferDefinitionId}' as InformationTransferDefinition, but found {definition.GetType().Name}.");
                    }
                    else
                    {
                        InformationTransferMode expectedMode = TransferModeFor(lesson.teachingMethod);
                        if (transferDefinition.Mode != InformationTransferMode.Unknown && transferDefinition.Mode != expectedMode)
                        {
                            report.AddError($"Training Lesson '{lesson.lessonId}' teaching method {lesson.teachingMethod} requires transfer mode {expectedMode}, but '{lesson.informationTransferDefinitionId}' uses {transferDefinition.Mode}.");
                        }
                    }
                }
            }

            Dictionary<string, TrainingPracticalAssignmentDefinitionData> assignmentMap = new Dictionary<string, TrainingPracticalAssignmentDefinitionData>(StringComparer.Ordinal);
            foreach (TrainingPracticalAssignmentDefinitionData assignment in PracticalAssignments)
            {
                if (string.IsNullOrWhiteSpace(assignment.assignmentId) || assignmentMap.ContainsKey(assignment.assignmentId))
                {
                    report.AddError($"Training Curriculum '{DisplayName}' has duplicate or blank assignment ID '{assignment.assignmentId}'.");
                    continue;
                }

                assignmentMap[assignment.assignmentId] = assignment;
                if (!moduleMap.ContainsKey(assignment.moduleId ?? string.Empty))
                {
                    report.AddError($"Training Assignment '{assignment.assignmentId}' references missing module '{assignment.moduleId}'.");
                }

                if (!Enum.IsDefined(typeof(TrainingAssignmentActivityCategory), assignment.activityCategory))
                {
                    report.AddError($"Training Assignment '{assignment.assignmentId}' has invalid activity category '{assignment.activityCategory}'.");
                }
            }

            foreach (TrainingModuleDefinitionData module in moduleMap.Values)
            {
                foreach (string dependency in module.dependencyModuleIds ?? Array.Empty<string>())
                {
                    if (!moduleMap.ContainsKey(dependency))
                    {
                        report.AddError($"Training Module '{module.moduleId}' references missing dependency '{dependency}'.");
                    }
                }

                foreach (string lessonId in module.lessonIds ?? Array.Empty<string>())
                {
                    if (!lessonMap.ContainsKey(lessonId))
                    {
                        report.AddError($"Training Module '{module.moduleId}' references missing lesson '{lessonId}'.");
                    }
                }

                foreach (string assignmentId in module.assignmentIds ?? Array.Empty<string>())
                {
                    if (!assignmentMap.ContainsKey(assignmentId))
                    {
                        report.AddError($"Training Module '{module.moduleId}' references missing assignment '{assignmentId}'.");
                    }
                }
            }

            if (HasCycle(moduleMap, out string cycle))
            {
                report.AddError($"Training Curriculum '{DisplayName}' has a dependency cycle: {cycle}.");
            }
        }

        public bool TryGetModule(string moduleId, out TrainingModuleDefinitionData module)
        {
            module = Modules.FirstOrDefault(item => string.Equals(item.moduleId, moduleId, StringComparison.Ordinal));
            return module != null;
        }

        public bool TryGetLesson(string lessonId, out TrainingLessonDefinitionData lesson)
        {
            lesson = Lessons.FirstOrDefault(item => string.Equals(item.lessonId, lessonId, StringComparison.Ordinal));
            return lesson != null;
        }

        public bool TryGetAssignment(string assignmentId, out TrainingPracticalAssignmentDefinitionData assignment)
        {
            assignment = PracticalAssignments.FirstOrDefault(item => string.Equals(item.assignmentId, assignmentId, StringComparison.Ordinal));
            return assignment != null;
        }

        private static bool HasCycle(Dictionary<string, TrainingModuleDefinitionData> modulesById, out string cycle)
        {
            cycle = string.Empty;
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string moduleId in modulesById.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (Visit(moduleId, modulesById, visiting, visited, new List<string>(), out cycle))
                {
                    return true;
                }
            }

            return false;
        }

        private static InformationTransferMode TransferModeFor(TrainingTeachingMethod method)
        {
            return method switch
            {
                TrainingTeachingMethod.Lecture => InformationTransferMode.Lecture,
                TrainingTeachingMethod.Reading => InformationTransferMode.BookReading,
                TrainingTeachingMethod.Demonstration => InformationTransferMode.Demonstration,
                TrainingTeachingMethod.GuidedPractice => InformationTransferMode.GuidedPractice,
                TrainingTeachingMethod.SupervisedWork => InformationTransferMode.Instruction,
                TrainingTeachingMethod.Discussion => InformationTransferMode.ConversationStatement,
                TrainingTeachingMethod.ExaminationPreparation => InformationTransferMode.FormalLesson,
                _ => InformationTransferMode.InformalTeaching
            };
        }

        private static bool Visit(string moduleId, Dictionary<string, TrainingModuleDefinitionData> modulesById, HashSet<string> visiting, HashSet<string> visited, List<string> path, out string cycle)
        {
            cycle = string.Empty;
            if (visited.Contains(moduleId) || !modulesById.TryGetValue(moduleId, out TrainingModuleDefinitionData module))
            {
                return false;
            }

            if (!visiting.Add(moduleId))
            {
                path.Add(moduleId);
                cycle = string.Join(" -> ", path);
                return true;
            }

            path.Add(moduleId);
            foreach (string dependency in module.dependencyModuleIds ?? Array.Empty<string>())
            {
                if (Visit(dependency, modulesById, visiting, visited, new List<string>(path), out cycle))
                {
                    return true;
                }
            }

            visiting.Remove(moduleId);
            visited.Add(moduleId);
            return false;
        }
    }
}
