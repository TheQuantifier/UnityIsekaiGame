using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityIsekaiGame.Editor;
using UnityEngine;

namespace UnityIsekaiGame.Tests
{
    public sealed class ProjectStructureValidationTests
    {
        private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        private static readonly string ProtocolPackageRoot = RepositoryPath("Packages/com.thequantifier.isekai.protocol");
        private static readonly string SimulationPackageRoot = RepositoryPath("Packages/com.thequantifier.isekai.simulation");
        private static readonly string NetworkingPackageRoot = RepositoryPath("Packages/com.thequantifier.isekai.networking");
        private static readonly string ClientPackageRoot = RepositoryPath("Packages/com.thequantifier.isekai.client");
        private static readonly string ServerPackageRoot = RepositoryPath("Packages/com.thequantifier.isekai.server");
        private static readonly string ContentPackageRoot = RepositoryPath("Packages/com.thequantifier.isekai.content");

        [Test]
        public void CompleteProjectStructureValidatorHasNoFindings()
        {
            ProjectStructureValidationReport report = ProjectStructureValidationMenu.Validate();

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
        }

        [Test]
        public void ProjectOwnedAssetsLiveUnderProjectRoot()
        {
            string[] allowed = { "_Project", "ThirdParty", "StreamingAssets" };
            HashSet<string> allowedNames = new HashSet<string>(allowed, StringComparer.Ordinal);

            foreach (string directory in Directory.GetDirectories("Assets"))
            {
                string name = Path.GetFileName(directory);
                Assert.That(allowedNames, Does.Contain(name), $"Unexpected top-level Assets folder: {directory}");
            }
        }

        [Test]
        public void KnownMovedAssetsExistAtCanonicalPaths()
        {
            Assert.That(File.Exists(SimulationPackageRoot + "/Runtime/Core/Definitions/UnityIsekaiGame.GameData.asmdef"), Is.True);
            Assert.That(File.Exists(ContentPackageRoot + "/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset"), Is.True);
            Assert.That(File.Exists("Assets/_Project/Scenes/Prototype/PrototypeScene.unity"), Is.True);
            Assert.That(File.Exists(ContentPackageRoot + "/Content/Characters/Attributes/StrengthAttribute.asset"), Is.True);
            Assert.That(File.Exists(ContentPackageRoot + "/Content/Characters/CalculatedStats/Definitions/MaximumHealthCalculatedStat.asset"), Is.True);
            Assert.That(File.Exists(ContentPackageRoot + "/Content/Characters/Resources/HealthResource.asset"), Is.True);
            Assert.That(File.Exists(ContentPackageRoot + "/Content/Items/Definitions/HealthPotion.asset"), Is.True);
        }

        [Test]
        public void Client_server_and_shared_network_assemblies_use_physical_authority_roots()
        {
            Assert.That(Directory.Exists(SimulationPackageRoot + "/Runtime/Networking"), Is.False, "The former mixed networking root must not return.");
            Assert.That(File.Exists(ProtocolPackageRoot + "/Runtime/Protocol/UnityIsekaiGame.Networking.Protocol.asmdef"), Is.True);
            Assert.That(File.Exists(NetworkingPackageRoot + "/Runtime/Shared/Networking/Replication/UnityIsekaiGame.Networking.Shared.asmdef"), Is.True);
            Assert.That(File.Exists(ClientPackageRoot + "/Runtime/Networking/UnityIsekaiGame.Networking.Client.asmdef"), Is.True);
            Assert.That(File.Exists(ClientPackageRoot + "/Runtime/UI/UnityIsekaiGame.UI.asmdef"), Is.True);
            Assert.That(File.Exists(ServerPackageRoot + "/Runtime/Networking/UnityIsekaiGame.Networking.Server.asmdef"), Is.True);
            Assert.That(File.Exists("Assets/_Project/Editor/Networking/LocalNetworkFoundationAuthoring.cs"), Is.True);
            Assert.That(File.Exists("Assets/_Project/Tests/EditMode/Networking/LocalNetworkFoundationTests.cs"), Is.True);
        }

        [Test]
        public void Shared_packages_have_coordinated_names_versions_and_dependencies()
        {
            AssertPackageManifest(ProtocolPackageRoot, "com.thequantifier.isekai.protocol");
            AssertPackageManifest(SimulationPackageRoot, "com.thequantifier.isekai.simulation");
            AssertPackageManifest(
                NetworkingPackageRoot,
                "com.thequantifier.isekai.networking",
                "com.thequantifier.isekai.protocol",
                "com.thequantifier.isekai.simulation");
            AssertPackageManifest(
                ContentPackageRoot,
                "com.thequantifier.isekai.content",
                "com.thequantifier.isekai.networking",
                "com.thequantifier.isekai.simulation");
            AssertPackageManifest(
                ClientPackageRoot,
                "com.thequantifier.isekai.client",
                "com.thequantifier.isekai.networking",
                "com.thequantifier.isekai.protocol",
                "com.thequantifier.isekai.simulation");
            AssertPackageManifest(
                ServerPackageRoot,
                "com.thequantifier.isekai.server",
                "com.thequantifier.isekai.networking",
                "com.thequantifier.isekai.protocol",
                "com.thequantifier.isekai.simulation");

            string projectManifest = File.ReadAllText("Packages/manifest.json");
            Assert.That(projectManifest, Does.Contain("\"com.thequantifier.isekai.protocol\""));
            Assert.That(projectManifest, Does.Contain("\"com.thequantifier.isekai.simulation\""));
            Assert.That(projectManifest, Does.Contain("\"com.thequantifier.isekai.networking\""));
            Assert.That(projectManifest, Does.Contain("\"com.thequantifier.isekai.content\""));
            Assert.That(projectManifest, Does.Contain("\"com.thequantifier.isekai.client\""));
            Assert.That(projectManifest, Does.Not.Contain("\"com.thequantifier.isekai.server\""));
        }

        [Test]
        public void Package_boundaries_do_not_leave_legacy_code_or_mix_content_and_code()
        {
            Assert.That(Directory.GetFiles(ContentPackageRoot + "/Content", "*.cs", SearchOption.AllDirectories), Is.Empty);

            if (Directory.Exists("Assets/_Project/Runtime"))
            {
                Assert.That(Directory.GetFiles("Assets/_Project/Runtime", "*.cs", SearchOption.AllDirectories), Is.Empty);
            }

            Assert.That(Directory.Exists(ProtocolPackageRoot + "/Runtime/Client"), Is.False);
            Assert.That(Directory.Exists(ProtocolPackageRoot + "/Runtime/Server"), Is.False);
            Assert.That(Directory.Exists(SimulationPackageRoot + "/Runtime/Client"), Is.False);
            Assert.That(Directory.Exists(SimulationPackageRoot + "/Runtime/Server"), Is.False);
            Assert.That(Directory.Exists(NetworkingPackageRoot + "/Runtime/Client"), Is.False);
            Assert.That(Directory.Exists(NetworkingPackageRoot + "/Runtime/Server"), Is.False);
            Assert.That(Directory.Exists(ClientPackageRoot + "/Runtime"), Is.True);
            Assert.That(Directory.Exists(ServerPackageRoot + "/Runtime"), Is.True);
        }

        [Test]
        public void Online_narrative_client_does_not_mutate_authoritative_party_services()
        {
            string bridgePath = ClientPackageRoot + "/Runtime/Networking/LocalNarrativeAuthorityBridge.cs";
            string source = File.ReadAllText(bridgePath);

            Assert.That(source, Does.Not.Contain("ApplyPartyReplica"), bridgePath);
            Assert.That(source, Does.Not.Contain("persistence.AdventuringParties"), bridgePath);
            Assert.That(source, Does.Not.Contain("persistence.PartyOperations"), bridgePath);
        }

        [Test]
        public void Runtime_menus_and_clients_do_not_pause_the_shared_simulation_clock()
        {
            string[] runtimeRoots =
            {
                SimulationPackageRoot + "/Runtime",
                NetworkingPackageRoot + "/Runtime",
                ClientPackageRoot + "/Runtime",
                ServerPackageRoot + "/Runtime"
            };

            foreach (string root in runtimeRoots)
            {
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(path);
                    Assert.That(source, Does.Not.Contain("Time.timeScale"), $"{path} must not globally pause server or shared simulation time.");
                }
            }
        }

        [Test]
        public void Client_build_output_resolves_inside_the_repository()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string actual = Path.GetFullPath(Path.Combine(projectRoot, LocalNetworkBuildAutomation.DefaultClientPath));
            string expected = RepositoryPath("Builds/LocalClient/UnityIsekaiClient.exe");

            Assert.That(actual, Is.EqualTo(expected).IgnoreCase);
        }

        [Test]
        public void TerrainDataAssetsAreTrackedAsBinary()
        {
            string attributesPath = RepositoryPath(".gitattributes");
            Assert.That(File.Exists(attributesPath), Is.True, "Missing repository .gitattributes.");

            string[] lines = File.ReadAllLines(attributesPath);
            bool hasTerrainDataBinaryRule = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Equals("Projects/Client/Assets/_Project/Prototype/Environment/Terrain/Data/*.asset binary", StringComparison.Ordinal))
                {
                    hasTerrainDataBinaryRule = true;
                    break;
                }
            }

            Assert.That(hasTerrainDataBinaryRule, Is.True, "Prototype TerrainData assets must be binary so Git does not normalize heightmaps, splatmaps, tree instances, or terrain metadata as YAML text.");
        }

        [Test]
        public void MovedAssetsPreserveKnownGuids()
        {
            AssertMetaGuid("Assets/_Project/Scenes/Prototype/PrototypeScene.unity.meta", "e05b77e4d2cc25845adb762e95d51873");
            AssertMetaGuid(ContentPackageRoot + "/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset.meta", "357d3d18865946889262f9bf55802d62");
            AssertMetaGuid(ContentPackageRoot + "/Content/Characters/Attributes/StrengthAttribute.asset.meta", "a1000000000000000000000000000052");
            AssertMetaGuid(ContentPackageRoot + "/Content/Networking/Prefabs/NetworkPlayerActor.prefab.meta", "ffacee9f9a9cbb3409b0980ceb89fe69");
            AssertMetaGuid(ContentPackageRoot + "/Content/Networking/Prefabs/NetworkCombatWorldState.prefab.meta", "d433e9cb8b04d544ba2d9e1bad797394");
        }

        [Test]
        public void NoDuplicateAssetGuidsExist()
        {
            Dictionary<string, string> seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] roots = { "Assets", ProtocolPackageRoot, SimulationPackageRoot, NetworkingPackageRoot, ClientPackageRoot, ServerPackageRoot, ContentPackageRoot };
            foreach (string root in roots)
            {
                foreach (string metaPath in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
                {
                    string guid = ReadGuid(metaPath);
                    Assert.That(guid, Is.Not.Empty, $"Missing GUID in {metaPath}");

                    if (seen.TryGetValue(guid, out string existingPath))
                    {
                        Assert.Fail($"Duplicate GUID {guid} in {existingPath} and {metaPath}");
                    }

                    seen.Add(guid, metaPath);
                }
            }
        }

        [Test]
        public void NoObsoleteHardcodedProjectPathsRemain()
        {
            string[] obsoleteFragments =
            {
                "Assets/Scripts/",
                "Assets/GameData/",
                "Assets/Items/",
                "Assets/StatusEffects/",
                "Assets/Materials/"
            };

            string[] roots = { "Assets/_Project", RepositoryPath("Documentation"), "ProjectSettings", RepositoryPath("Packages") };

            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string normalized = file.Replace('\\', '/');
                    if (!ShouldScanForObsoletePaths(normalized))
                    {
                        continue;
                    }

                    string text;
                    try
                    {
                        text = File.ReadAllText(file);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    for (int i = 0; i < obsoleteFragments.Length; i++)
                    {
                        Assert.That(text, Does.Not.Contain(obsoleteFragments[i]), $"{normalized} contains obsolete path {obsoleteFragments[i]}");
                    }
                }
            }
        }

        private static bool ShouldScanForObsoletePaths(string normalizedPath)
        {
            if (normalizedPath.EndsWith("/ProjectStructureValidationMenu.cs", StringComparison.Ordinal) ||
                normalizedPath.EndsWith("/ProjectStructureValidationTests.cs", StringComparison.Ordinal) ||
                normalizedPath.Contains("/Models/", StringComparison.Ordinal) ||
                normalizedPath.Contains("/Textures/", StringComparison.Ordinal) ||
                normalizedPath.Contains("/Materials/", StringComparison.Ordinal))
            {
                return false;
            }

            string extension = Path.GetExtension(normalizedPath);
            return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
        }

        private static void AssertMetaGuid(string metaPath, string expectedGuid)
        {
            Assert.That(File.Exists(metaPath), Is.True, $"Missing meta file: {metaPath}");
            Assert.That(ReadGuid(metaPath), Is.EqualTo(expectedGuid), metaPath);
        }

        private static void AssertPackageManifest(string packageRoot, string packageName, params string[] requiredDependencies)
        {
            string manifestPath = packageRoot + "/package.json";
            Assert.That(File.Exists(manifestPath), Is.True, manifestPath);

            string manifest = File.ReadAllText(manifestPath);
            Assert.That(manifest, Does.Match($@"""name""\s*:\s*""{Regex.Escape(packageName)}"""));
            Assert.That(manifest, Does.Match(@"""version""\s*:\s*""0\.1\.0"""));
            for (int i = 0; i < requiredDependencies.Length; i++)
            {
                Assert.That(manifest, Does.Match($@"""{Regex.Escape(requiredDependencies[i])}""\s*:\s*""0\.1\.0"""));
            }
        }

        private static string ReadGuid(string metaPath)
        {
            string text = File.ReadAllText(metaPath);
            Match match = Regex.Match(text, @"^guid:\s*(\w+)", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string RepositoryPath(string relativePath)
        {
            return Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
