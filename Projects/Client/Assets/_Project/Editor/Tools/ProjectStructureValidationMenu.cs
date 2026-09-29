using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityIsekaiGame.Editor
{
    public static class ProjectStructureValidationMenu
    {
        private static readonly string[] AllowedAssetsRoots =
        {
            "Assets/_Project",
            "Assets/ThirdParty",
            "Assets/StreamingAssets"
        };

        private const string ProtocolPackagePath = "Packages/com.thequantifier.isekai.protocol";
        private const string SimulationPackagePath = "Packages/com.thequantifier.isekai.simulation";
        private const string NetworkingPackagePath = "Packages/com.thequantifier.isekai.networking";
        private const string ClientPackagePath = "Packages/com.thequantifier.isekai.client";
        private const string ServerPackagePath = "Packages/com.thequantifier.isekai.server";
        private const string ContentPackagePath = "Packages/com.thequantifier.isekai.content";
        private const string ProjectToolsPackagePath = "Packages/com.thequantifier.isekai.project-tools";
        private const string SceneZonePackagePath = "Packages/com.thequantifier.scene-zone-tool";
        private const string PackageVersion = "0.1.0";

        private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        private static readonly string ProtocolPackageRoot = RepositoryPath(ProtocolPackagePath);
        private static readonly string SimulationPackageRoot = RepositoryPath(SimulationPackagePath);
        private static readonly string NetworkingPackageRoot = RepositoryPath(NetworkingPackagePath);
        private static readonly string ClientPackageRoot = RepositoryPath(ClientPackagePath);
        private static readonly string ServerPackageRoot = RepositoryPath(ServerPackagePath);
        private static readonly string ContentPackageRoot = RepositoryPath(ContentPackagePath);
        private static readonly string ProjectToolsPackageRoot = RepositoryPath(ProjectToolsPackagePath);
        private static readonly string SceneZonePackageRoot = RepositoryPath(SceneZonePackagePath);

        private static readonly string[] UnityAssetRoots =
        {
            "Assets",
            ProtocolPackageRoot,
            SimulationPackageRoot,
            NetworkingPackageRoot,
            ClientPackageRoot,
            ServerPackageRoot,
            ContentPackageRoot,
            ProjectToolsPackageRoot,
            SceneZonePackageRoot
        };

        private static readonly string[] CodeRoots =
        {
            "Assets/_Project",
            ProtocolPackageRoot + "/Runtime",
            SimulationPackageRoot + "/Runtime",
            NetworkingPackageRoot + "/Runtime",
            ClientPackageRoot + "/Runtime",
            ServerPackageRoot + "/Runtime",
            ContentPackageRoot + "/Content"
        };

        private static readonly string[] ObsoletePathFragments =
        {
            "Assets/Scripts/",
            "Assets/GameData/",
            "Assets/Items/",
            "Assets/StatusEffects/",
            "Assets/Materials/",
            "Assets/Abilities/",
            "Assets/Contracts/",
            "Assets/Dialogue/",
            "Assets/Input/",
            "Assets/Loot/",
            "Assets/People/",
            "Assets/Prefabs/",
            "Assets/Quests/",
            "Assets/Settings/",
            "Assets/Spells/",
            "Assets/_Project/Runtime/",
            "Assets/_Project/Content/"
        };

        private static readonly string[] CanonicalDefinitionNames =
        {
            "StrengthAttribute.asset",
            "VitalityAttribute.asset",
            "MaximumHealthCalculatedStat.asset",
            "HealthResource.asset",
            "SwordsmanshipSkill.asset",
            "ArcaneMagicSkill.asset",
            "CommonerRole.asset",
            "CitizenStatus.asset",
            "LivingTrait.asset"
        };

        private static readonly string[] TextScanRoots =
        {
            "Assets/_Project",
            RepositoryPath("Documentation"),
            "ProjectSettings",
            RepositoryPath("Packages")
        };

        [MenuItem("Tools/Project Maintenance/Validate Project Structure")]
        public static void ValidateProjectStructure()
        {
            ProjectStructureValidationReport report = Validate();

            if (report.ErrorCount > 0)
            {
                Debug.LogError(report.GetSummary());
            }
            else if (report.WarningCount > 0)
            {
                Debug.LogWarning(report.GetSummary());
            }
            else
            {
                Debug.Log(report.GetSummary());
            }
        }

        public static ProjectStructureValidationReport Validate()
        {
            ProjectStructureValidationReport report = new ProjectStructureValidationReport();

            ValidateAssetsRoot(report);
            ValidatePackageContracts(report);
            ValidateMetaFiles(report);
            ValidateDuplicateGuids(report);
            ValidateCodePlacement(report);
            ValidateContentPlacement(report);
            ValidateHardcodedObsoletePaths(report);
            ValidateMissingScripts(report);
            ValidateAsmdefs(report);
            ValidateProductionBoundaryAssets(report);
            ValidateBuildSceneCategories(report);
            ValidateKnownMovedAssets(report);

            if (report.ErrorCount == 0 && report.WarningCount == 0)
            {
                report.AddInfo("Project structure validation completed with no errors or warnings.");
            }

            return report;
        }

        private static void ValidateAssetsRoot(ProjectStructureValidationReport report)
        {
            foreach (string path in Directory.GetDirectories("Assets"))
            {
                string normalized = NormalizePath(path);
                bool allowed = false;

                for (int i = 0; i < AllowedAssetsRoots.Length; i++)
                {
                    if (string.Equals(normalized, AllowedAssetsRoots[i], StringComparison.Ordinal))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    report.AddError($"Unexpected top-level Assets folder '{normalized}'. Project-owned assets should live under Assets/_Project.");
                }
            }
        }

        private static void ValidateMetaFiles(ProjectStructureValidationReport report)
        {
            foreach (string root in UnityAssetRoots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
                {
                    string normalized = NormalizePath(path);
                    if (normalized.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        string assetPath = path.Substring(0, path.Length - ".meta".Length);
                        if (!File.Exists(assetPath) && !Directory.Exists(assetPath))
                        {
                            report.AddWarning($"Orphan meta file '{normalized}' has no matching asset or folder.");
                        }

                        continue;
                    }

                    if (!File.Exists(path + ".meta"))
                    {
                        report.AddError($"Missing meta file for '{normalized}'.");
                    }
                }
            }
        }

        private static void ValidatePackageContracts(ProjectStructureValidationReport report)
        {
            ValidatePackageManifest(
                report,
                ProtocolPackageRoot,
                "com.thequantifier.isekai.protocol",
                Array.Empty<string>());
            ValidatePackageManifest(
                report,
                SimulationPackageRoot,
                "com.thequantifier.isekai.simulation",
                Array.Empty<string>());
            ValidatePackageManifest(
                report,
                NetworkingPackageRoot,
                "com.thequantifier.isekai.networking",
                new[] { "com.thequantifier.isekai.protocol", "com.thequantifier.isekai.simulation" });
            ValidatePackageManifest(
                report,
                ClientPackageRoot,
                "com.thequantifier.isekai.client",
                new[] { "com.thequantifier.isekai.networking", "com.thequantifier.isekai.protocol", "com.thequantifier.isekai.simulation" });
            ValidatePackageManifest(
                report,
                ServerPackageRoot,
                "com.thequantifier.isekai.server",
                new[] { "com.thequantifier.isekai.networking", "com.thequantifier.isekai.protocol", "com.thequantifier.isekai.simulation" });
            ValidatePackageManifest(
                report,
                ContentPackageRoot,
                "com.thequantifier.isekai.content",
                new[] { "com.thequantifier.isekai.networking", "com.thequantifier.isekai.simulation" });
            ValidatePackageManifest(
                report,
                ProjectToolsPackageRoot,
                "com.thequantifier.isekai.project-tools",
                new[] { "com.thequantifier.isekai.simulation" });
            ValidatePackageIdentity(report, SceneZonePackageRoot, "com.thequantifier.scene-zone-tool", "1.0.3");

            if (Directory.Exists(ContentPackageRoot + "/Content"))
            {
                foreach (string scriptPath in Directory.EnumerateFiles(ContentPackageRoot + "/Content", "*.cs", SearchOption.AllDirectories))
                {
                    report.AddError($"Shared content package contains runtime code '{NormalizePath(scriptPath)}'. Code belongs in protocol, simulation, or networking.");
                }
            }

            if (Directory.Exists("Assets/_Project/Runtime") &&
                Directory.EnumerateFiles("Assets/_Project/Runtime", "*.cs", SearchOption.AllDirectories).GetEnumerator().MoveNext())
            {
                report.AddError("Legacy Assets/_Project/Runtime still contains C# source after package extraction.");
            }

            if (Directory.Exists("Assets/_Project/Content") &&
                Directory.EnumerateFileSystemEntries("Assets/_Project/Content", "*", SearchOption.AllDirectories).GetEnumerator().MoveNext())
            {
                report.AddError("Legacy Assets/_Project/Content still contains authored assets after package extraction.");
            }
        }

        private static void ValidatePackageManifest(
            ProjectStructureValidationReport report,
            string packageRoot,
            string expectedName,
            string[] requiredDependencies)
        {
            string manifestPath = packageRoot + "/package.json";
            if (!File.Exists(manifestPath))
            {
                report.AddError($"Required package manifest is missing at '{manifestPath}'.");
                return;
            }

            string contents = File.ReadAllText(manifestPath);
            string packageName = ReadJsonString(contents, "name");
            string version = ReadJsonString(contents, "version");
            if (!string.Equals(packageName, expectedName, StringComparison.Ordinal))
            {
                report.AddError($"Package at '{packageRoot}' must be named '{expectedName}', not '{packageName}'.");
            }

            if (!string.Equals(version, PackageVersion, StringComparison.Ordinal))
            {
                report.AddError($"Package '{expectedName}' must use coordinated version '{PackageVersion}', not '{version}'.");
            }

            for (int i = 0; i < requiredDependencies.Length; i++)
            {
                string dependency = requiredDependencies[i];
                if (!Regex.IsMatch(contents, $@"""{Regex.Escape(dependency)}""\s*:\s*""{Regex.Escape(PackageVersion)}"""))
                {
                    report.AddError($"Package '{expectedName}' must depend on '{dependency}' version '{PackageVersion}'.");
                }
            }
        }

        private static string ReadJsonString(string contents, string propertyName)
        {
            Match match = Regex.Match(contents, $@"""{Regex.Escape(propertyName)}""\s*:\s*""([^""]*)""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static void ValidatePackageIdentity(
            ProjectStructureValidationReport report,
            string packageRoot,
            string expectedName,
            string expectedVersion)
        {
            string manifestPath = packageRoot + "/package.json";
            if (!File.Exists(manifestPath))
            {
                report.AddError($"Required package manifest is missing at '{manifestPath}'.");
                return;
            }

            string contents = File.ReadAllText(manifestPath);
            string packageName = ReadJsonString(contents, "name");
            string version = ReadJsonString(contents, "version");
            if (!string.Equals(packageName, expectedName, StringComparison.Ordinal))
            {
                report.AddError($"Package at '{packageRoot}' must be named '{expectedName}', not '{packageName}'.");
            }

            if (!string.Equals(version, expectedVersion, StringComparison.Ordinal))
            {
                report.AddError($"Package '{expectedName}' must use version '{expectedVersion}', not '{version}'.");
            }
        }

        private static void ValidateDuplicateGuids(ProjectStructureValidationReport report)
        {
            Dictionary<string, string> seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string metaPath in EnumerateFilesUnderRoots(UnityAssetRoots, "*.meta"))
            {
                string guid = ReadGuid(metaPath);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    report.AddWarning($"Meta file '{NormalizePath(metaPath)}' has no readable GUID.");
                    continue;
                }

                if (seen.TryGetValue(guid, out string existingPath))
                {
                    report.AddError($"Duplicate Unity GUID '{guid}' in '{existingPath}' and '{NormalizePath(metaPath)}'.");
                    continue;
                }

                seen.Add(guid, NormalizePath(metaPath));
            }
        }

        private static void ValidateCodePlacement(ProjectStructureValidationReport report)
        {
            foreach (string scriptPath in EnumerateFilesUnderRoots(CodeRoots, "*.cs"))
            {
                string normalized = NormalizePath(scriptPath);
                string contents = null;

                if (normalized.Contains("/Tests/", StringComparison.Ordinal) && !normalized.StartsWith("Assets/_Project/Tests/", StringComparison.Ordinal))
                {
                    report.AddError($"Test code '{normalized}' is outside Assets/_Project/Tests.");
                }

                if (normalized.Contains("/Editor/", StringComparison.Ordinal) && !normalized.StartsWith("Assets/_Project/Editor/", StringComparison.Ordinal))
                {
                    report.AddError($"Editor code '{normalized}' is inside a runtime-owned folder.");
                }

                if (normalized.StartsWith(ContentPackagePath + "/Content/", StringComparison.Ordinal) ||
                    normalized.StartsWith("Assets/_Project/Presentation/", StringComparison.Ordinal) ||
                    normalized.StartsWith("Assets/_Project/Configuration/", StringComparison.Ordinal) ||
                    normalized.StartsWith(ContentPackagePath + "/Content/Prototype/", StringComparison.Ordinal))
                {
                    report.AddError($"Code file '{normalized}' is in an authored-content, presentation, configuration, or prototype-content folder.");
                }

                bool isPackagedRuntime = normalized.StartsWith(ProtocolPackagePath + "/Runtime/", StringComparison.Ordinal) ||
                    normalized.StartsWith(SimulationPackagePath + "/Runtime/", StringComparison.Ordinal) ||
                    normalized.StartsWith(NetworkingPackagePath + "/Runtime/", StringComparison.Ordinal) ||
                    normalized.StartsWith(ClientPackagePath + "/Runtime/", StringComparison.Ordinal) ||
                    normalized.StartsWith(ServerPackagePath + "/Runtime/", StringComparison.Ordinal);
                if (isPackagedRuntime)
                {
                    contents ??= File.ReadAllText(scriptPath);
                    if (contents.Contains("UnityEditor", StringComparison.Ordinal))
                    {
                        report.AddError($"Runtime script '{normalized}' references UnityEditor. Editor-only code belongs under Assets/_Project/Editor.");
                    }

                    if (Regex.IsMatch(contents, @"using\s+UnityIsekaiGame\.Development\s*;"))
                    {
                        report.AddError($"Runtime script '{normalized}' imports Development. Runtime assemblies must not depend on development tooling.");
                    }

                    bool isSimulationRuntime = normalized.StartsWith(SimulationPackagePath + "/Runtime/", StringComparison.Ordinal);
                    if (isSimulationRuntime && Regex.IsMatch(contents, @"using\s+UnityIsekaiGame\.UI(?:\.|\s*;)"))
                    {
                        report.AddError($"Simulation script '{normalized}' imports UI. Simulation code must communicate through simulation-owned contracts.");
                    }
                }

                if (normalized.StartsWith("Assets/_Project/Development/", StringComparison.Ordinal))
                {
                    contents ??= File.ReadAllText(scriptPath);
                    if (!contents.Contains("UNITY_EDITOR", StringComparison.Ordinal) && !contents.Contains("DEVELOPMENT_BUILD", StringComparison.Ordinal))
                    {
                        report.AddWarning($"Development script '{normalized}' is not visibly guarded by UNITY_EDITOR or DEVELOPMENT_BUILD.");
                    }
                }
            }
        }

        private static void ValidateContentPlacement(ProjectStructureValidationReport report)
        {
            foreach (string assetPath in EnumerateFilesUnderRoots(
                new[] { ProtocolPackageRoot + "/Runtime", SimulationPackageRoot + "/Runtime", NetworkingPackageRoot + "/Runtime" },
                "*.asset"))
            {
                report.AddError($"ScriptableObject asset '{NormalizePath(assetPath)}' is under Runtime code.");
            }

            foreach (string fileName in CanonicalDefinitionNames)
            {
                foreach (string assetPath in Directory.EnumerateFiles("Assets/_Project/Prototype", fileName, SearchOption.AllDirectories))
                {
                    report.AddError($"Canonical definition '{NormalizePath(assetPath)}' is still under Prototype.");
                }
            }
        }

        private static void ValidateHardcodedObsoletePaths(ProjectStructureValidationReport report)
        {
            foreach (string root in TextScanRoots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string normalized = NormalizePath(path);
                    if (normalized.EndsWith("/ProjectStructureValidationMenu.cs", StringComparison.Ordinal) ||
                        normalized.EndsWith("/ProjectStructureValidationTests.cs", StringComparison.Ordinal) ||
                        normalized.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                        normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                        normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string contents;
                    try
                    {
                        contents = File.ReadAllText(path);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    for (int i = 0; i < ObsoletePathFragments.Length; i++)
                    {
                        if (contents.Contains(ObsoletePathFragments[i], StringComparison.Ordinal))
                        {
                            report.AddWarning($"File '{normalized}' contains obsolete path fragment '{ObsoletePathFragments[i]}'.");
                        }
                    }
                }
            }
        }

        private static void ValidateMissingScripts(ProjectStructureValidationReport report)
        {
            foreach (string path in EnumerateFilesUnderRoots(new[] { "Assets/_Project", ContentPackageRoot + "/Content" }, "*.*"))
            {
                string normalized = NormalizePath(path);
                if (!normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                    !normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string contents = File.ReadAllText(path);
                if (contents.Contains("m_Script: {fileID: 0}", StringComparison.Ordinal))
                {
                    report.AddError($"Asset '{normalized}' contains a missing script reference.");
                }
            }
        }

        private static void ValidateAsmdefs(ProjectStructureValidationReport report)
        {
            Dictionary<string, AsmdefInfo> asmdefs = new Dictionary<string, AsmdefInfo>(StringComparer.Ordinal);

            foreach (string asmdefPath in EnumerateFilesUnderRoots(CodeRoots, "*.asmdef"))
            {
                string normalized = NormalizePath(asmdefPath);
                string contents = File.ReadAllText(asmdefPath);
                Match nameMatch = Regex.Match(contents, @"""name""\s*:\s*""([^""]+)""");
                if (!nameMatch.Success)
                {
                    report.AddError($"Asmdef '{normalized}' is missing a name.");
                    continue;
                }

                string name = nameMatch.Groups[1].Value;
                if (asmdefs.TryGetValue(name, out AsmdefInfo existing))
                {
                    report.AddError($"Duplicate asmdef name '{name}' in '{existing.Path}' and '{normalized}'.");
                    continue;
                }

                AsmdefInfo info = new AsmdefInfo(
                    name,
                    normalized,
                    ExtractStringArray(contents, "references"),
                    ExtractStringArray(contents, "includePlatforms"));
                asmdefs.Add(name, info);

                if (normalized.Contains("/Editor/", StringComparison.Ordinal) && !info.IncludePlatforms.Contains("Editor"))
                {
                    report.AddWarning($"Editor asmdef '{normalized}' should include the Editor platform.");
                }
            }

            RequireAsmdef(report, asmdefs, "UnityIsekaiGame.GameData");
            RequireAsmdef(report, asmdefs, "UnityIsekaiGame.Gameplay");
            RequireAsmdef(report, asmdefs, "UnityIsekaiGame.UI");
            RequireAsmdef(report, asmdefs, "UnityIsekaiGame.Development");
            RequireAsmdef(report, asmdefs, "UnityIsekaiGame.Editor");
            RequireAsmdef(report, asmdefs, "UnityIsekaiGame.EditModeTests");
            RequireAsmdefAtPath(report, asmdefs, "UnityIsekaiGame.UI", ClientPackagePath + "/Runtime/UI/UnityIsekaiGame.UI.asmdef");
            RequireAsmdefAtPath(report, asmdefs, "UnityIsekaiGame.Networking.Protocol", ProtocolPackagePath + "/Runtime/Protocol/UnityIsekaiGame.Networking.Protocol.asmdef");
            RequireAsmdefAtPath(report, asmdefs, "UnityIsekaiGame.Networking.Shared", NetworkingPackagePath + "/Runtime/Shared/Networking/Replication/UnityIsekaiGame.Networking.Shared.asmdef");
            RequireAsmdefAtPath(report, asmdefs, "UnityIsekaiGame.Networking.Client", ClientPackagePath + "/Runtime/Networking/UnityIsekaiGame.Networking.Client.asmdef");
            RequireAsmdefAtPath(report, asmdefs, "UnityIsekaiGame.Networking.Server", ServerPackagePath + "/Runtime/Networking/UnityIsekaiGame.Networking.Server.asmdef");

            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.GameData", "UnityIsekaiGame.Gameplay");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.GameData", "UnityIsekaiGame.UI");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.GameData", "UnityIsekaiGame.Development");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.GameData", "UnityIsekaiGame.Editor");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.GameData", "UnityIsekaiGame.EditModeTests");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Gameplay", "UnityIsekaiGame.UI");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Gameplay", "UnityIsekaiGame.Development");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Gameplay", "UnityIsekaiGame.Editor");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Gameplay", "UnityIsekaiGame.EditModeTests");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.UI", "UnityIsekaiGame.Development");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.UI", "UnityIsekaiGame.Editor");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.UI", "UnityIsekaiGame.EditModeTests");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Development", "UnityIsekaiGame.Editor");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Development", "UnityIsekaiGame.EditModeTests");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Protocol", "UnityIsekaiGame.Networking.Shared");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Protocol", "UnityIsekaiGame.Networking.Client");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Protocol", "UnityIsekaiGame.Networking.Server");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Protocol", "UnityIsekaiGame.Gameplay");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Protocol", "UnityIsekaiGame.UI");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Shared", "UnityIsekaiGame.Networking.Client");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Shared", "UnityIsekaiGame.Networking.Server");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Shared", "UnityIsekaiGame.Gameplay");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Shared", "UnityIsekaiGame.UI");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Client", "UnityIsekaiGame.Networking.Server");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Server", "UnityIsekaiGame.Networking.Client");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Networking.Server", "UnityIsekaiGame.UI");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Gameplay", "UnityIsekaiGame.Networking.Client");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Gameplay", "UnityIsekaiGame.Networking.Server");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Gameplay", "UnityIsekaiGame.Networking.Shared");
            ValidateForbiddenAsmdefReference(report, asmdefs, "UnityIsekaiGame.Gameplay", "UnityIsekaiGame.Networking.Protocol");

            if (asmdefs.TryGetValue("UnityIsekaiGame.Editor", out AsmdefInfo editorInfo) && !editorInfo.IncludePlatforms.Contains("Editor"))
            {
                report.AddError("UnityIsekaiGame.Editor must be restricted to the Editor platform.");
            }

            if (asmdefs.TryGetValue("UnityIsekaiGame.EditModeTests", out AsmdefInfo testsInfo) && !testsInfo.IncludePlatforms.Contains("Editor"))
            {
                report.AddError("UnityIsekaiGame.EditModeTests must be restricted to the Editor platform.");
            }

            ValidateAsmdefCycles(report, asmdefs);
        }

        private static void ValidateProductionBoundaryAssets(ProjectStructureValidationReport report)
        {
            HashSet<string> developmentScriptGuids = ReadGuidsUnder("Assets/_Project/Development", ".cs.meta");
            HashSet<string> testLabScriptGuids = ReadGuidsUnder("Assets/_Project/Development/TestLab", ".cs.meta");
            // Production data must not depend on prototype-only data definitions. Presentation
            // placeholders (models, materials, and prefabs) may still be shared while the game is
            // in vertical-slice development, so only the Prototype/Content boundary is enforced here.
            HashSet<string> prototypeAssetGuids = ReadGuidsUnder("Packages/com.thequantifier.isekai.content/Content/Prototype", ".meta");

            string[] productionRoots = { "Assets/_Project", ContentPackageRoot + "/Content" };
            foreach (string prefabPath in EnumerateFilesUnderRoots(productionRoots, "*.prefab"))
            {
                string normalized = NormalizePath(prefabPath);
                if (!IsProductionAssetPath(normalized))
                {
                    continue;
                }

                string contents = File.ReadAllText(prefabPath);
                if (ContainsAnyGuid(contents, developmentScriptGuids))
                {
                    report.AddError($"Production prefab '{normalized}' has a Development component.");
                }
            }

            foreach (string scenePath in Directory.EnumerateFiles("Assets/_Project", "*.unity", SearchOption.AllDirectories))
            {
                string normalized = NormalizePath(scenePath);
                if (!IsProductionAssetPath(normalized))
                {
                    continue;
                }

                string contents = File.ReadAllText(scenePath);
                if (ContainsAnyGuid(contents, testLabScriptGuids) || contents.Contains("UnityIsekaiGame.Development.PrototypeTest", StringComparison.Ordinal))
                {
                    report.AddError($"Production scene '{normalized}' contains a Test Lab component.");
                }
            }

            foreach (string assetPath in EnumerateFilesUnderRoots(productionRoots, "*.asset"))
            {
                string normalized = NormalizePath(assetPath);
                if (!IsProductionAssetPath(normalized))
                {
                    continue;
                }

                string contents = File.ReadAllText(assetPath);
                if (ContainsAnyGuid(contents, prototypeAssetGuids))
                {
                    report.AddError($"Production ScriptableObject '{normalized}' references prototype-only game data.");
                }
            }
        }

        private static void ValidateBuildSceneCategories(ProjectStructureValidationReport report)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene == null || string.IsNullOrWhiteSpace(scene.path))
                {
                    continue;
                }

                string normalized = NormalizePath(scene.path);
                string category = CategorizeScenePath(normalized);
                if (category == null)
                {
                    report.AddError($"Build scene '{normalized}' is not categorized as production or development/prototype.");
                    continue;
                }

                string enabled = scene.enabled ? "enabled" : "disabled";
                report.AddInfo($"Build scene '{normalized}' is categorized as {category} and is {enabled}.");
            }
        }

        private static string CategorizeScenePath(string normalizedPath)
        {
            if (normalizedPath.StartsWith("Assets/_Project/Scenes/Production/", StringComparison.Ordinal))
            {
                return "Production";
            }

            if (normalizedPath.StartsWith("Assets/_Project/Scenes/Development/", StringComparison.Ordinal) ||
                normalizedPath.StartsWith("Assets/_Project/Scenes/Prototype/", StringComparison.Ordinal) ||
                normalizedPath.StartsWith("Assets/_Project/Prototype/", StringComparison.Ordinal))
            {
                return "Development";
            }

            return null;
        }

        private static bool IsProductionAssetPath(string normalizedPath)
        {
            if (normalizedPath.StartsWith(ContentPackagePath + "/Content/Prototype/", StringComparison.Ordinal))
            {
                return false;
            }

            if (normalizedPath.StartsWith(ContentPackagePath + "/Content/", StringComparison.Ordinal))
            {
                return true;
            }

            return normalizedPath.StartsWith("Assets/_Project/", StringComparison.Ordinal) &&
                !normalizedPath.StartsWith("Assets/_Project/Prototype/", StringComparison.Ordinal) &&
                !normalizedPath.StartsWith("Assets/_Project/Development/", StringComparison.Ordinal) &&
                !normalizedPath.StartsWith("Assets/_Project/Editor/", StringComparison.Ordinal) &&
                !normalizedPath.StartsWith("Assets/_Project/Tests/", StringComparison.Ordinal) &&
                !normalizedPath.StartsWith("Assets/_Project/Scenes/Prototype/", StringComparison.Ordinal) &&
                !normalizedPath.StartsWith("Assets/_Project/Scenes/Development/", StringComparison.Ordinal);
        }

        private static HashSet<string> ReadGuidsUnder(string root, string metaSuffix)
        {
            HashSet<string> guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            root = ResolvePath(root);
            if (!Directory.Exists(root))
            {
                return guids;
            }

            foreach (string metaPath in Directory.EnumerateFiles(root, "*" + metaSuffix, SearchOption.AllDirectories))
            {
                string guid = ReadGuid(metaPath);
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    guids.Add(guid);
                }
            }

            return guids;
        }

        private static bool ContainsAnyGuid(string contents, HashSet<string> guids)
        {
            foreach (string guid in guids)
            {
                if (contents.Contains(guid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RequireAsmdef(ProjectStructureValidationReport report, Dictionary<string, AsmdefInfo> asmdefs, string name)
        {
            if (!asmdefs.ContainsKey(name))
            {
                report.AddError($"Required asmdef '{name}' is missing.");
            }
        }

        private static void RequireAsmdefAtPath(ProjectStructureValidationReport report, Dictionary<string, AsmdefInfo> asmdefs, string name, string expectedPath)
        {
            if (!asmdefs.TryGetValue(name, out AsmdefInfo info))
            {
                report.AddError($"Required asmdef '{name}' is missing from '{expectedPath}'.");
                return;
            }

            if (!string.Equals(info.Path, expectedPath, StringComparison.Ordinal))
            {
                report.AddError($"Asmdef '{name}' belongs at '{expectedPath}', not '{info.Path}'.");
            }
        }

        private static void ValidateForbiddenAsmdefReference(ProjectStructureValidationReport report, Dictionary<string, AsmdefInfo> asmdefs, string source, string forbiddenTarget)
        {
            if (!asmdefs.TryGetValue(source, out AsmdefInfo info))
            {
                return;
            }

            if (info.References.Contains(forbiddenTarget))
            {
                report.AddError($"Asmdef '{source}' must not reference '{forbiddenTarget}'.");
            }
        }

        private static void ValidateAsmdefCycles(ProjectStructureValidationReport report, Dictionary<string, AsmdefInfo> asmdefs)
        {
            Dictionary<string, bool> visiting = new Dictionary<string, bool>(StringComparer.Ordinal);
            Dictionary<string, bool> visited = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (string name in asmdefs.Keys)
            {
                if (HasAsmdefCycle(name, asmdefs, visiting, visited))
                {
                    report.AddError($"Asmdef dependency cycle detected at '{name}'.");
                    return;
                }
            }
        }

        private static bool HasAsmdefCycle(string name, Dictionary<string, AsmdefInfo> asmdefs, Dictionary<string, bool> visiting, Dictionary<string, bool> visited)
        {
            if (visited.ContainsKey(name))
            {
                return false;
            }

            if (visiting.ContainsKey(name))
            {
                return true;
            }

            visiting[name] = true;
            if (asmdefs.TryGetValue(name, out AsmdefInfo info))
            {
                foreach (string reference in info.References)
                {
                    if (asmdefs.ContainsKey(reference) && HasAsmdefCycle(reference, asmdefs, visiting, visited))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(name);
            visited[name] = true;
            return false;
        }

        private static HashSet<string> ExtractStringArray(string contents, string propertyName)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
            Match arrayMatch = Regex.Match(contents, $@"""{Regex.Escape(propertyName)}""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
            if (!arrayMatch.Success)
            {
                return values;
            }

            foreach (Match valueMatch in Regex.Matches(arrayMatch.Groups[1].Value, @"""([^""]+)"""))
            {
                values.Add(valueMatch.Groups[1].Value);
            }

            return values;
        }

        private static IEnumerable<string> EnumerateFilesUnderRoots(IEnumerable<string> roots, string searchPattern)
        {
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories))
                {
                    yield return path;
                }
            }
        }

        private static void ValidateKnownMovedAssets(ProjectStructureValidationReport report)
        {
            RequireAsset(report, ContentPackagePath + "/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            RequireAsset(report, "Assets/_Project/Scenes/Prototype/PrototypeScene.unity");
            RequireAsset(report, ContentPackagePath + "/Content/Characters/Attributes/StrengthAttribute.asset");
            RequireAsset(report, ContentPackagePath + "/Content/Characters/CalculatedStats/Definitions/MaximumHealthCalculatedStat.asset");
            RequireAsset(report, ContentPackagePath + "/Content/Characters/Resources/HealthResource.asset");
            RequireAsset(report, ContentPackagePath + "/Content/Items/Definitions/HealthPotion.asset");
        }

        private static void RequireAsset(ProjectStructureValidationReport report, string path)
        {
            if (!File.Exists(ResolvePath(path)))
            {
                report.AddError($"Required project asset is missing at '{path}'.");
            }
        }

        private static string ReadGuid(string metaPath)
        {
            foreach (string line in File.ReadLines(metaPath))
            {
                if (line.StartsWith("guid: ", StringComparison.Ordinal))
                {
                    return line.Substring("guid: ".Length).Trim();
                }
            }

            return string.Empty;
        }

        private static string RepositoryPath(string relativePath)
        {
            return Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ResolvePath(string path)
        {
            return path.StartsWith("Packages/", StringComparison.Ordinal) ||
                path.StartsWith("Documentation/", StringComparison.Ordinal) ||
                path.Equals(".gitattributes", StringComparison.Ordinal)
                ? RepositoryPath(path)
                : path;
        }

        private static string NormalizePath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string repositoryPrefix = RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(projectPrefix.Length).Replace('\\', '/');
            }

            if (fullPath.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(repositoryPrefix.Length).Replace('\\', '/');
            }

            return path.Replace('\\', '/');
        }
    }

    internal sealed class AsmdefInfo
    {
        public AsmdefInfo(string name, string path, HashSet<string> references, HashSet<string> includePlatforms)
        {
            Name = name;
            Path = path;
            References = references;
            IncludePlatforms = includePlatforms;
        }

        public string Name { get; }
        public string Path { get; }
        public HashSet<string> References { get; }
        public HashSet<string> IncludePlatforms { get; }
    }

    public sealed class ProjectStructureValidationReport
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> infos = new List<string>();

        public int ErrorCount => errors.Count;
        public int WarningCount => warnings.Count;
        public int InfoCount => infos.Count;

        public void AddError(string message)
        {
            errors.Add(message);
        }

        public void AddWarning(string message)
        {
            warnings.Add(message);
        }

        public void AddInfo(string message)
        {
            infos.Add(message);
        }

        public string GetSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Project structure validation finished with ");
            builder.Append(ErrorCount);
            builder.Append(" error(s), ");
            builder.Append(WarningCount);
            builder.Append(" warning(s), and ");
            builder.Append(InfoCount);
            builder.AppendLine(" info message(s).");

            AppendMessages(builder, "Error", errors);
            AppendMessages(builder, "Warning", warnings);
            AppendMessages(builder, "Info", infos);

            return builder.ToString();
        }

        private static void AppendMessages(StringBuilder builder, string label, List<string> messages)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                builder.Append(label);
                builder.Append(": ");
                builder.AppendLine(messages[i]);
            }
        }
    }
}
