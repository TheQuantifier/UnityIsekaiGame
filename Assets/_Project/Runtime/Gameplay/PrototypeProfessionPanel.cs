using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeProfessionPanel : MonoBehaviour
    {
        private static readonly (string name, string professionId, string entryPathId)[] Declarations =
        {
            ("Crafter", ProfessionContentIds.CrafterProfessionId, ProfessionContentIds.CrafterSelfDeclaredEntryPathId),
            ("Blacksmith", ProfessionContentIds.BlacksmithProfessionId, ProfessionContentIds.BlacksmithSelfDeclaredEntryPathId),
            ("Disassembler", ProfessionContentIds.DisassemblerProfessionId, ProfessionContentIds.DisassemblerSelfDeclaredEntryPathId),
            ("Salvager", ProfessionContentIds.SalvagerProfessionId, ProfessionContentIds.SalvagerSelfDeclaredEntryPathId)
        };

        private PrototypePersistenceServiceBehaviour services;
        private bool visible;
        private Vector2 scroll;
        private string status = "Choose a self-declared profession to begin recording work experience.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForPrototype(scene);
        }

        private static void InstallForPrototype(Scene scene)
        {
            if (!scene.IsValid() || scene.name.IndexOf("Prototype", StringComparison.OrdinalIgnoreCase) < 0
                || FindAnyObjectByType<PrototypeProfessionPanel>() != null)
            {
                return;
            }

            GameObject host = new GameObject("Prototype Profession Panel");
            host.AddComponent<PrototypeProfessionPanel>();
        }

        private void Awake()
        {
            services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
        }

        private void Update()
        {
            if (Keyboard.current?.f7Key.wasPressedThisFrame == true)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12f, Screen.height - 28f, 420f, 22f), "F7: Professions & Career");
            if (!visible)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(24f, 24f, Mathf.Min(540f, Screen.width - 48f), Mathf.Min(650f, Screen.height - 48f)), GUI.skin.window);
            GUILayout.Label("Professions & Career", new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold });
            if (services == null)
            {
                services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            }
            if (services == null)
            {
                GUILayout.Label("Persistence/gameplay services are unavailable.");
                GUILayout.EndArea();
                return;
            }

            string personId = services.PlayerPersonId;
            GUILayout.Label($"Character: {personId}");
            GUILayout.Space(6f);
            GUILayout.Label("Declare practice", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.BeginHorizontal();
            foreach ((string name, string professionId, string entryPathId) in Declarations)
            {
                bool active = services.Professions.QueryByPerson(personId, true).Any(item => item.ProfessionId == professionId);
                GUI.enabled = !active;
                if (GUILayout.Button(active ? $"{name} (Active)" : name, GUILayout.Height(30f)))
                {
                    string worldTime = Time.realtimeSinceStartupAsDouble.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    ProfessionEntryOperationResult result = services.ProfessionCoordinator.DeclareInformalProfession(
                        personId,
                        professionId,
                        entryPathId,
                        worldTime,
                        $"tx.ui.profession-entry.{Guid.NewGuid():N}");
                    status = result.Message;
                    if (result.Succeeded)
                    {
                        services.DirtyTracker?.MarkDirty($"Profession declared: {professionId}.");
                    }
                }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            scroll = GUILayout.BeginScrollView(scroll);
            DrawSection("Active professions", services.Professions.QueryByPerson(personId, true).Select(item =>
                $"{Display(item.ProfessionId)} — {(item.Recognized ? "formally recognized" : "self-declared")}{(item.Primary ? ", primary" : string.Empty)}"));
            DrawSection("Validated work", services.ProfessionalActivities.Activities
                .Where(item => item.personId == personId)
                .Select(item => $"{Display(item.professionId)}: {Display(item.activityDefinitionId)} — quality {item.quality}/1000, {item.difficulty}"));
            DrawSection("Training", services.Training.QueryByPerson(personId)
                .Select(item => $"{Display(item.Data.programId)} — {item.Data.state}"));
            DrawSection("Credentials", services.Credentials.QueryByRecipient(personId)
                .Select(item => $"{Display(item.credentialDefinitionId)} — {item.state}"));
            DrawSection("Ranks & mastery", services.ProfessionalRanks.QueryByPerson(personId)
                .Select(item => $"{Display(item.rankDefinitionId)} — {item.state}"));
            DrawSection("Employment", services.PositionEmployment.QueryEmploymentByPerson(personId)
                .Select(item => $"{Display(item.positionInstanceId)} — {item.state}"));
            DrawSection("Career timeline", services.CareerHistory.QueryEpisodesByPerson(personId)
                .Select(item => $"{Display(item.professionId)} — {item.category}, {item.state}"));
            DrawSection("Aspirations", services.LifePaths.QueryAspirationsByPerson(personId)
                .Select(item => $"{Display(item.aspirationDefinitionId)} — {item.state}"));
            DrawSection("Goals", services.LifePaths.QueryGoalsByPerson(personId)
                .Select(item => $"{Display(item.goalDefinitionId)} — {item.state}"));
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.Label(status, GUI.skin.box);
            if (GUILayout.Button("Close", GUILayout.Height(28f))) visible = false;
            GUILayout.EndArea();
        }

        private static void DrawSection(string title, System.Collections.Generic.IEnumerable<string> rows)
        {
            string[] values = rows.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            GUILayout.Label(title, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            if (values.Length == 0)
            {
                GUILayout.Label("  None");
                return;
            }
            foreach (string value in values) GUILayout.Label($"  • {value}");
        }

        private static string Display(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "Unknown";
            string leaf = id.Split('.').LastOrDefault() ?? id;
            return string.Join(" ", leaf.Split('-').Select(part => string.IsNullOrWhiteSpace(part) ? part : char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }
    }
}
