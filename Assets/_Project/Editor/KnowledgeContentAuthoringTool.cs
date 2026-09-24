using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Editor
{
    public static class KnowledgeContentAuthoringTool
    {
        private const string Root = "Assets/_Project/Content/Knowledge";
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [MenuItem("Tools/Unity Isekai Game/Author Group 5 Knowledge Content")]
        public static void Generate()
        {
            List<ScriptableObject> policy = new() { CreateKnowledgePolicy() };
            List<ScriptableObject> sources = CreateSourceDefinitions().Cast<ScriptableObject>().ToList();
            List<ScriptableObject> transfers = CreateTransferDefinitions().Cast<ScriptableObject>().ToList();
            List<ScriptableObject> access = CreateAccessDefinitions().Cast<ScriptableObject>().ToList();
            List<ScriptableObject> records = CreateRecordDefinitions().Cast<ScriptableObject>().ToList();
            List<ScriptableObject> history = CreateLifeEventDefinitions().Cast<ScriptableObject>().ToList();

            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Definition catalog is missing at '{CatalogPath}'.");
            }

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            Set(serializedCatalog, "contentVersion", "phase-3.group-5");
            AddOrReplaceSection(serializedCatalog, "domain.knowledge-policy", policy);
            AddOrReplaceSection(serializedCatalog, "domain.information-source", sources);
            AddOrReplaceSection(serializedCatalog, "domain.information-transfer", transfers);
            AddOrReplaceSection(serializedCatalog, "domain.information-access", access);
            AddOrReplaceSection(serializedCatalog, "domain.knowledge-record", records);
            MergeSection(serializedCatalog, "domain.history-event", history);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            string manifest = string.Join("\n", catalog.GetDefinitions()
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .Select(definition => $"{definition.Id}|{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath((UnityEngine.Object)definition))}"));
            using (SHA256 sha = SHA256.Create())
            {
                serializedCatalog.Update();
                Set(serializedCatalog, "contentHash", BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest))).Replace("-", string.Empty).ToLowerInvariant());
                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Group 5 knowledge content authored: {policy.Count + sources.Count + transfers.Count + access.Count + records.Count + history.Count} definitions.");
        }

        private static KnowledgePolicyDefinition CreateKnowledgePolicy()
        {
            KnowledgePolicyDefinition asset = GetOrCreate<KnowledgePolicyDefinition>($"{Root}/Policies/DefaultKnowledgePolicy.asset");
            SerializedObject serialized = new SerializedObject(asset);
            Set(serialized, "policyId", KnowledgePolicyDefinition.DefaultPolicyId);
            Set(serialized, "displayName", "Default Knowledge Policy");
            Set(serialized, "suspectedThreshold", 1);
            Set(serialized, "believedThreshold", 400);
            Set(serialized, "stronglyBelievedThreshold", 600);
            Set(serialized, "defaultKnownThreshold", 700);
            Set(serialized, "defaultForgettingReduction", 250);
            Set(serialized, "defaultObservationQuality", 550);
            Set(serialized, "memoryMaintenanceIntervalSeconds", 60d);
            Set(serialized, "memoryConfidenceLossPerDay", 1);
            Set(serialized, "memoryClarityLossPerDay", 2);
            Set(serialized, "memorySalienceLossPerDay", 0);
            Save(serialized, asset);
            return asset;
        }

        private static IReadOnlyList<InformationSourceDefinition> CreateSourceDefinitions()
        {
            return new[]
            {
                Source("direct-observation", "Direct Observation", InformationSourceCategory.DirectObservation, 850, false, false, false, false),
                Source("direct-participation", "Direct Participation", InformationSourceCategory.DirectParticipation, 875, false, false, false, false),
                Source("personal-testimony", "Personal Testimony", InformationSourceCategory.PersonalTestimony, 650, false, true, true, true),
                Source("expert-testimony", "Expert Testimony", InformationSourceCategory.ExpertTestimony, 800, false, true, true, true),
                Source("anonymous-testimony", "Anonymous Testimony", InformationSourceCategory.AnonymousTestimony, 350, true, true, true, true),
                Source("hearsay", "Hearsay", InformationSourceCategory.Hearsay, 350, true, true, true, true),
                Source("written-record", "Written Record", InformationSourceCategory.WrittenRecord, 700, false, true, true, true),
                Source("official-record", "Official Record", InformationSourceCategory.OfficialRecord, 850, false, true, true, true, true),
                Source("historical-record", "Historical Record", InformationSourceCategory.HistoricalRecord, 725, false, true, true, true),
                Source("book", "Book", InformationSourceCategory.Book, 700, false, true, true, true),
                Source("journal", "Journal", InformationSourceCategory.Journal, 625, false, true, true, true),
                Source("letter", "Letter", InformationSourceCategory.Letter, 600, false, true, true, true),
                Source("map", "Map", InformationSourceCategory.Map, 700, false, true, true, true),
                Source("physical-evidence", "Physical Evidence", InformationSourceCategory.PhysicalEvidence, 825, false, false, false, false),
                Source("institutional-report", "Institutional Report", InformationSourceCategory.InstitutionalReport, 825, false, true, true, true, true),
                Source("public-announcement", "Public Announcement", InformationSourceCategory.PublicAnnouncement, 650, false, true, true, true),
                Source("copied-source", "Copied Source", InformationSourceCategory.CopiedSource, 600, false, true, true, true),
                Source("translation", "Translation", InformationSourceCategory.Translation, 550, false, true, true, true),
                Source("summary", "Summary", InformationSourceCategory.Summary, 500, false, true, true, true)
            };
        }

        private static InformationSourceDefinition Source(string slug, string label, InformationSourceCategory category, int reliability, bool anonymous, bool copy, bool translation, bool summary, bool verifyIdentity = false)
        {
            InformationSourceDefinition asset = GetOrCreate<InformationSourceDefinition>($"{Root}/Sources/{label.Replace(" ", string.Empty)}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            Set(serialized, "sourceDefinitionId", $"information-source.{slug}");
            Set(serialized, "displayName", label);
            Set(serialized, "description", $"Canonical {label.ToLowerInvariant()} information source.");
            Set(serialized, "category", (int)category);
            SerializedProperty reliabilityData = serialized.FindProperty("defaultReliability");
            foreach (string field in new[] { "generalDependability", "domainExpertise", "firsthandProximity", "methodQuality", "authenticity", "identityCertainty", "observationQuality", "recordIntegrity", "recency", "transmissionIntegrity", "independence", "corroboration", "internalConsistency", "completeness", "precision", "contextFit" })
            {
                reliabilityData.FindPropertyRelative(field).intValue = reliability;
            }
            reliabilityData.FindPropertyRelative("errorRisk").intValue = 1000 - reliability;
            reliabilityData.FindPropertyRelative("deceptionRisk").intValue = Math.Max(25, (1000 - reliability) / 2);
            reliabilityData.FindPropertyRelative("biasRisk").intValue = Math.Max(25, (1000 - reliability) / 2);
            SetEnumArray(serialized.FindProperty("supportedDomains"), Enumerable.Range(1, 19));
            SetStringArray(serialized.FindProperty("supportedMethodIds"), Array.Empty<string>());
            SetStringArray(serialized.FindProperty("authorityClassifications"), Array.Empty<string>());
            Set(serialized, "defaultErrorRisk", 1000 - reliability);
            Set(serialized, "defaultDeceptionRisk", Math.Max(25, (1000 - reliability) / 2));
            Set(serialized, "defaultBiasRisk", Math.Max(25, (1000 - reliability) / 2));
            Set(serialized, "stalenessPolicy", (int)KnowledgeStalenessPolicy.NeverStale);
            Set(serialized, "stalenessHalfLifeSeconds", 0d);
            Set(serialized, "transmissionPenaltyPerHop", 80);
            Set(serialized, "allowsAnonymous", anonymous);
            Set(serialized, "allowsCopying", copy);
            Set(serialized, "allowsTranslation", translation);
            Set(serialized, "allowsSummary", summary);
            Set(serialized, "requiresIdentityVerification", verifyIdentity);
            SetStringArray(serialized.FindProperty("tags"), new[] { "knowledge-source", $"source-category.{slug}" });
            Set(serialized, "version", 1);
            Save(serialized, asset);
            return asset;
        }

        private static IReadOnlyList<InformationTransferDefinition> CreateTransferDefinitions()
        {
            return new[]
            {
                Transfer("direct-testimony", "Direct Testimony", InformationTransferMode.DirectTestimony, false, false, false, false),
                Transfer("conversation", "Conversation Statement", InformationTransferMode.ConversationStatement, false, true, false, false),
                Transfer("written-message", "Written Message", InformationTransferMode.WrittenMessage, false, true, true, false),
                Transfer("public-announcement", "Public Announcement", InformationTransferMode.PublicAnnouncement, false, true, false, false),
                Transfer("training.prototype-lecture", "Formal Lesson", InformationTransferMode.Lecture, true, true, false, false),
                Transfer("training.prototype-demonstration", "Demonstration", InformationTransferMode.Demonstration, true, false, false, true),
                Transfer("training.prototype-guided-practice", "Guided Practice", InformationTransferMode.GuidedPractice, true, false, false, true),
                Transfer("translation", "Translation", InformationTransferMode.Translation, true, false, true, false),
                Transfer("summary", "Summary", InformationTransferMode.Summary, true, true, false, false),
                Transfer("rumor-retelling", "Rumor Retelling", InformationTransferMode.RumorRetelling, true, true, false, false)
            };
        }

        private static InformationTransferDefinition Transfer(string slug, string label, InformationTransferMode mode, bool recall, bool summary, bool translation, bool demonstration)
        {
            InformationTransferDefinition asset = GetOrCreate<InformationTransferDefinition>($"{Root}/Transfers/{label.Replace(" ", string.Empty)}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            Set(serialized, "transferDefinitionId", $"information-transfer.{slug}");
            Set(serialized, "displayName", label);
            Set(serialized, "description", $"Canonical {label.ToLowerInvariant()} transfer method.");
            Set(serialized, "mode", (int)mode);
            SetEnumArray(serialized.FindProperty("supportedDomains"), Enumerable.Range(1, 19));
            SetEnumArray(serialized.FindProperty("allowedSourceCategories"), Enumerable.Range(1, 32));
            Set(serialized, "recallRequired", recall);
            Set(serialized, "directKnowledgeAccessPermitted", true);
            Set(serialized, "writtenPersistenceInvolved", mode is InformationTransferMode.WrittenMessage or InformationTransferMode.Letter or InformationTransferMode.Report);
            Set(serialized, "publicAllowed", true);
            Set(serialized, "privateAllowed", true);
            Set(serialized, "secretAllowed", mode != InformationTransferMode.PublicAnnouncement);
            Set(serialized, "summarizationAllowed", summary);
            Set(serialized, "translationAllowed", translation);
            Set(serialized, "demonstrationAllowed", demonstration);
            Set(serialized, "defaultFidelity", 800);
            Set(serialized, "defaultCompleteness", 800);
            Set(serialized, "defaultTransmissionCost", 100);
            Set(serialized, "defaultEvidenceStrength", 650);
            Set(serialized, "inheritedConfidencePolicy", (int)TransferInheritedConfidencePolicy.SourceReliabilityAdjusted);
            Set(serialized, "memoryPolicy", (int)TransferMemoryPolicy.FormCommunicationMemory);
            Set(serialized, "evidencePolicy", (int)TransferEvidencePolicy.CreateRecipientEvidence);
            Set(serialized, "defaultPrivacy", (int)TransferPrivacyScope.RecipientOnly);
            Set(serialized, "requiredCapabilityId", string.Empty);
            Set(serialized, "requiredMethodId", string.Empty);
            Set(serialized, "versionMetadata", "1");
            SetStringArray(serialized.FindProperty("tags"), new[] { "knowledge-transfer", $"transfer-mode.{mode}" });
            Save(serialized, asset);
            return asset;
        }

        private static IReadOnlyList<InformationAccessPolicyDefinition> CreateAccessDefinitions()
        {
            return new[]
            {
                Access("public", "Public Information", InformationVisibilityClassification.Public, InformationDisclosurePolicy.FreelyDisclose, InformationResharingPolicy.FreelyReshareable, InformationAuditPolicy.AuditDenied),
                Access("personal", "Personal Information", InformationVisibilityClassification.Personal, InformationDisclosurePolicy.SameAsAccess, InformationResharingPolicy.NoResharing, InformationAuditPolicy.AuditDeniedAndGranted),
                Access("confidential", "Confidential Information", InformationVisibilityClassification.Confidential, InformationDisclosurePolicy.ApprovalRequired, InformationResharingPolicy.NoResharing, InformationAuditPolicy.AuditDeniedAndGranted),
                Access("medical", "Medical Information", InformationVisibilityClassification.Medical, InformationDisclosurePolicy.ApprovalRequired, InformationResharingPolicy.NoResharing, InformationAuditPolicy.AuditDeniedAndGranted),
                Access("profession.public", "Public Profession Information", InformationVisibilityClassification.Public, InformationDisclosurePolicy.FreelyDisclose, InformationResharingPolicy.FreelyReshareable, InformationAuditPolicy.AuditDenied),
                Access("profession.secret", "Restricted Profession Information", InformationVisibilityClassification.ProfessionRestricted, InformationDisclosurePolicy.ApprovalRequired, InformationResharingPolicy.NeedToKnowOnly, InformationAuditPolicy.AuditDeniedAndGranted)
            };
        }

        private static InformationAccessPolicyDefinition Access(string slug, string label, InformationVisibilityClassification classification, InformationDisclosurePolicy disclosure, InformationResharingPolicy resharing, InformationAuditPolicy audit)
        {
            InformationAccessPolicyDefinition asset = GetOrCreate<InformationAccessPolicyDefinition>($"{Root}/Access/{label.Replace(" ", string.Empty)}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            Set(serialized, "policyId", $"information-access.{slug}");
            Set(serialized, "displayName", label);
            Set(serialized, "description", $"Canonical {label.ToLowerInvariant()} access policy.");
            Set(serialized, "subjectType", (int)InformationSubjectType.Custom);
            Set(serialized, "classification", (int)classification);
            Set(serialized, "disclosurePolicy", (int)disclosure);
            Set(serialized, "resharingPolicy", (int)resharing);
            Set(serialized, "sourceVisibilityPolicy", (int)InformationSourceVisibilityPolicy.Reveal);
            Set(serialized, "detailVisibilityPolicy", (int)InformationDetailVisibilityPolicy.All);
            Set(serialized, "auditPolicy", (int)audit);
            SetStringArray(serialized.FindProperty("defaultVisibleDetails"), Array.Empty<string>());
            SetStringArray(serialized.FindProperty("defaultRedactedDetails"), Array.Empty<string>());
            SetStringArray(serialized.FindProperty("defaultHiddenDetails"), Array.Empty<string>());
            Set(serialized, "discoveryRequired", false);
            Set(serialized, "redactedAccessAcceptable", true);
            Save(serialized, asset);
            return asset;
        }

        private static IReadOnlyList<KnowledgeRecordDefinition> CreateRecordDefinitions()
        {
            return new[]
            {
                Record("journal-entry", "Journal Entry", KnowledgeRecordCategory.PersonalJournal, new[] { InformationSubjectType.HistoricalEvent, InformationSubjectType.Claim, InformationSubjectType.Memory }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.PrivateJournal }, "information-access.personal"),
                Record("historical-record", "Historical Record", KnowledgeRecordCategory.HistoricalRecord, new[] { InformationSubjectType.HistoricalEvent, InformationSubjectType.LifeEvent }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.PublicWorldRecord }, "information-access.public"),
                Record("biography-entry", "Biography Entry", KnowledgeRecordCategory.Biography, new[] { InformationSubjectType.PersonIdentity, InformationSubjectType.LifeEvent }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.PublicWorldRecord }, "information-access.personal"),
                Record("bestiary-entry", "Bestiary Entry", KnowledgeRecordCategory.Bestiary, new[] { InformationSubjectType.BodyIdentity, InformationSubjectType.Custom }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.SharedArchive }, "information-access.public"),
                Record("location-entry", "Location Entry", KnowledgeRecordCategory.LocationRecord, new[] { InformationSubjectType.Location }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.PublicWorldRecord }, "information-access.public"),
                Record("medical-record", "Medical Record", KnowledgeRecordCategory.MedicalRecord, new[] { InformationSubjectType.Diagnosis, InformationSubjectType.Condition, InformationSubjectType.Disease }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.Organization, KnowledgeRecordOwnerKind.Healer }, "information-access.medical"),
                Record("investigation-record", "Investigation Record", KnowledgeRecordCategory.InvestigationRecord, new[] { InformationSubjectType.SourceChain, InformationSubjectType.Evidence, InformationSubjectType.Claim }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.Organization }, "information-access.confidential"),
                Record("organization-entry", "Organization Entry", KnowledgeRecordCategory.OrganizationRecord, new[] { InformationSubjectType.Organization, InformationSubjectType.Affiliation }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.Organization }, "information-access.public"),
                Record("quest-note", "Quest Note", KnowledgeRecordCategory.QuestRelatedRecord, new[] { InformationSubjectType.Custom, InformationSubjectType.Claim }, new[] { KnowledgeRecordOwnerKind.Person }, "information-access.personal"),
                Record("custom-entry", "Custom Entry", KnowledgeRecordCategory.Custom, new[] { InformationSubjectType.Custom, InformationSubjectType.Document, InformationSubjectType.Claim, InformationSubjectType.KnowledgeRecord }, new[] { KnowledgeRecordOwnerKind.Person, KnowledgeRecordOwnerKind.SharedArchive }, "information-access.personal")
            };
        }

        private static KnowledgeRecordDefinition Record(string slug, string label, KnowledgeRecordCategory category, InformationSubjectType[] subjects, KnowledgeRecordOwnerKind[] owners, string accessPolicyId)
        {
            KnowledgeRecordDefinition asset = GetOrCreate<KnowledgeRecordDefinition>($"{Root}/Records/{label.Replace(" ", string.Empty)}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            Set(serialized, "recordDefinitionId", $"record-definition.{slug}");
            Set(serialized, "displayName", label);
            Set(serialized, "description", $"Canonical {label.ToLowerInvariant()} record type.");
            Set(serialized, "category", (int)category);
            SetEnumArray(serialized.FindProperty("allowedSubjectTypes"), subjects.Select(value => (int)value));
            SetEnumArray(serialized.FindProperty("allowedOwnerKinds"), owners.Select(value => (int)value));
            Set(serialized, "defaultProjectionKind", (int)KnowledgeRecordProjectionKind.ExplicitRecord);
            Set(serialized, "defaultAccessPolicyId", accessPolicyId);
            Set(serialized, "defaultPersistencePolicy", (int)KnowledgeRecordPersistencePolicy.ExplicitOnly);
            SetStringArray(serialized.FindProperty("defaultIndexingFields"), new[] { "subject", "category", "world-time" });
            Set(serialized, "defaultSortingPolicy", "time-then-id");
            Set(serialized, "defaultGroupingPolicy", "category");
            Set(serialized, "explicitDiscoveryRequired", false);
            Set(serialized, "explicitRecordingRequired", true);
            Set(serialized, "automaticProjectionAllowed", true);
            Set(serialized, "multipleEntriesPerSubjectAllowed", true);
            Set(serialized, "correctionsSupported", true);
            Set(serialized, "revisionsSupported", true);
            Set(serialized, "sourceReferencesRequired", false);
            Set(serialized, "evidenceReferencesRequired", false);
            Set(serialized, "uncertaintyShown", true);
            Set(serialized, "contradictionsShown", true);
            Set(serialized, "redactionSupported", true);
            SetStringArray(serialized.FindProperty("tags"), new[] { "knowledge-record", $"record-category.{slug}" });
            Set(serialized, "schemaVersion", 1);
            Save(serialized, asset);
            return asset;
        }

        private static IReadOnlyList<HistoricalEventDefinition> CreateLifeEventDefinitions()
        {
            return new[]
            {
                LifeEvent("life.birth", "Birth or Creation", HistoricalEventCategory.BirthOrCreation, LifeEventCategory.BirthOrCreation, LifeEventPayloadKind.BirthOrCreation),
                LifeEvent("life.death", "Death", HistoricalEventCategory.DeathOrDisappearance, LifeEventCategory.Death, LifeEventPayloadKind.DeathOrDisappearance),
                LifeEvent("life.presumed-death", "Presumed Death", HistoricalEventCategory.DeathOrDisappearance, LifeEventCategory.Disappearance, LifeEventPayloadKind.DeathOrDisappearance),
                LifeEvent("life.return-or-resurrection", "Return or Resurrection", HistoricalEventCategory.Recovery, LifeEventCategory.ReturnOrResurrection, LifeEventPayloadKind.Generic),
                LifeEvent("life.discovery", "Discovery", HistoricalEventCategory.Discovery, LifeEventCategory.Discovery, LifeEventPayloadKind.Discovery),
                LifeEvent("life.identity", "Identity Assignment", HistoricalEventCategory.Identity, LifeEventCategory.Identity, LifeEventPayloadKind.Generic),
                LifeEvent("life.travel", "Travel", HistoricalEventCategory.Travel, LifeEventCategory.Travel, LifeEventPayloadKind.TravelOrMigration),
                LifeEvent("life.quest", "Quest Event", HistoricalEventCategory.QuestRelevant, LifeEventCategory.QuestRelated, LifeEventPayloadKind.Generic)
            };
        }

        private static HistoricalEventDefinition LifeEvent(string slug, string label, HistoricalEventCategory historical, LifeEventCategory life, LifeEventPayloadKind payload)
        {
            HistoricalEventDefinition asset = GetOrCreate<HistoricalEventDefinition>($"{Root}/History/HistoricalEvents/{label.Replace(" ", string.Empty)}.asset");
            SerializedObject serialized = new SerializedObject(asset);
            Set(serialized, "eventDefinitionId", $"history-event.{slug}");
            Set(serialized, "displayName", label);
            Set(serialized, "description", $"Canonical {label.ToLowerInvariant()} life event.");
            Set(serialized, "category", (int)historical);
            Set(serialized, "defaultVisibility", (int)KnowledgeVisibility.Private);
            Set(serialized, "payloadKind", (int)HistoricalEventPayloadKind.Generic);
            Set(serialized, "lifeEventDefinition", true);
            Set(serialized, "lifeEventCategory", (int)life);
            Set(serialized, "lifeEventPayloadKind", (int)payload);
            SetEnumArray(serialized.FindProperty("requiredParticipantRoles"), new[] { (int)LifeEventParticipantRole.Subject });
            SetEnumArray(serialized.FindProperty("optionalParticipantRoles"), Array.Empty<int>());
            Set(serialized, "defaultSignificance", (int)LifeEventSignificance.Notable);
            Set(serialized, "defaultBiographyRelevance", (int)LifeEventBiographyRelevance.Optional);
            Set(serialized, "defaultPublicRecordRelevance", (int)LifeEventPublicRecordRelevance.PersonalOnly);
            Set(serialized, "mayBePrivate", true);
            Set(serialized, "mayBeSecret", true);
            Set(serialized, "mayBeCorrected", true);
            Set(serialized, "expectsCurrentStateTransition", false);
            SetStringArray(serialized.FindProperty("tags"), new[] { "life-event", $"life-event.{life}" });
            Save(serialized, asset);
            return asset;
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void AddOrReplaceSection(SerializedObject catalog, string domainId, IReadOnlyList<ScriptableObject> definitions)
        {
            SerializedProperty sections = catalog.FindProperty("sections");
            int index = FindSection(sections, domainId);
            if (index < 0)
            {
                index = sections.arraySize;
                sections.InsertArrayElementAtIndex(index);
            }

            SerializedProperty section = sections.GetArrayElementAtIndex(index);
            section.FindPropertyRelative("domainId").stringValue = domainId;
            SetObjectArray(section.FindPropertyRelative("definitions"), definitions);
        }

        private static void MergeSection(SerializedObject catalog, string domainId, IReadOnlyList<ScriptableObject> definitions)
        {
            SerializedProperty sections = catalog.FindProperty("sections");
            int index = FindSection(sections, domainId);
            List<ScriptableObject> merged = new List<ScriptableObject>();
            if (index >= 0)
            {
                SerializedProperty existing = sections.GetArrayElementAtIndex(index).FindPropertyRelative("definitions");
                for (int i = 0; i < existing.arraySize; i++)
                {
                    if (existing.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableObject item)
                    {
                        merged.Add(item);
                    }
                }
            }

            merged.AddRange(definitions);
            AddOrReplaceSection(catalog, domainId, merged.Where(item => item != null).Distinct().ToArray());
        }

        private static int FindSection(SerializedProperty sections, string domainId)
        {
            for (int i = 0; i < sections.arraySize; i++)
            {
                if (sections.GetArrayElementAtIndex(i).FindPropertyRelative("domainId").stringValue == domainId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SetObjectArray(SerializedProperty property, IReadOnlyList<ScriptableObject> values)
        {
            property.arraySize = values?.Count ?? 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void SetStringArray(SerializedProperty property, IEnumerable<string> values)
        {
            string[] array = (values ?? Array.Empty<string>()).ToArray();
            property.arraySize = array.Length;
            for (int i = 0; i < array.Length; i++) property.GetArrayElementAtIndex(i).stringValue = array[i];
        }

        private static void SetEnumArray(SerializedProperty property, IEnumerable<int> values)
        {
            int[] array = (values ?? Array.Empty<int>()).ToArray();
            property.arraySize = array.Length;
            for (int i = 0; i < array.Length; i++) property.GetArrayElementAtIndex(i).intValue = array[i];
        }

        private static void Set(SerializedObject serialized, string property, string value) => serialized.FindProperty(property).stringValue = value ?? string.Empty;
        private static void Set(SerializedObject serialized, string property, bool value) => serialized.FindProperty(property).boolValue = value;
        private static void Set(SerializedObject serialized, string property, int value) => serialized.FindProperty(property).intValue = value;
        private static void Set(SerializedObject serialized, string property, double value) => serialized.FindProperty(property).doubleValue = value;

        private static void Save(SerializedObject serialized, ScriptableObject asset)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
