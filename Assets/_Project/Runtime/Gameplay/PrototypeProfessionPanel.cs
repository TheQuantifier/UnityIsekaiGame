using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.Input;

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
        private PlayerInputReader input;
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
            input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            SetVisible(visible);
        }

        private void Update()
        {
            if (Keyboard.current?.f7Key.wasPressedThisFrame == true)
            {
                SetVisible(!visible);
            }
            else if (visible && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                SetVisible(false);
            }
        }

        private void OnDisable() => SetVisible(false);

        private void OnGUI()
        {
            GUI.Label(new Rect(12f, Screen.height - 30f, 420f, 24f), "[F7] Professions & Career", PrototypeUiTheme.MutedStyle);
            if (!visible)
            {
                return;
            }

            Rect window = new Rect(24f, 24f, Mathf.Min(620f, Screen.width - 48f), Mathf.Min(700f, Screen.height - 48f));
            PrototypeUiTheme.DrawPanelFrame(window);
            GUILayout.BeginArea(new Rect(window.x + 16f, window.y + 16f, window.width - 32f, window.height - 32f));
            GUILayout.Label("PROFESSIONS & CAREER", PrototypeUiTheme.TitleStyle);
            if (services == null)
            {
                services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            }
            if (services == null)
            {
                GUILayout.Label("Career services are unavailable.", PrototypeUiTheme.StatusStyle);
                GUILayout.EndArea();
                return;
            }

            string personId = services.PlayerPersonId;
            GUILayout.Label($"CHARACTER  |  {personId}", PrototypeUiTheme.MutedStyle);
            GUILayout.Space(6f);
            GUILayout.Label("DECLARE PRACTICE", PrototypeUiTheme.HeadingStyle);
            GUILayout.BeginHorizontal();
            foreach ((string name, string professionId, string entryPathId) in Declarations)
            {
                bool active = services.Professions.QueryByPerson(personId, true).Any(item => item.ProfessionId == professionId);
                GUI.enabled = !active;
                if (GUILayout.Button(active ? $"{name}  [Active]" : name, active ? PrototypeUiTheme.ButtonStyle : PrototypeUiTheme.PrimaryButtonStyle, GUILayout.Height(34f)))
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
            GUILayout.Label(status, PrototypeUiTheme.StatusStyle);
            if (GUILayout.Button("Close", PrototypeUiTheme.DangerButtonStyle, GUILayout.Height(34f))) SetVisible(false);
            GUILayout.EndArea();
        }

        private void SetVisible(bool value)
        {
            visible = value;
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            if (input != null) input.SetMenuInputBlocked(this, visible);
            else PlayerCursorMode.SetMenuOpen(this, visible);
        }

        private static void DrawSection(string title, System.Collections.Generic.IEnumerable<string> rows)
        {
            string[] values = rows.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            GUILayout.Label(title.ToUpperInvariant(), PrototypeUiTheme.HeadingStyle);
            if (values.Length == 0)
            {
                GUILayout.Label("None recorded yet.", PrototypeUiTheme.MutedStyle);
                return;
            }
            foreach (string value in values) GUILayout.Label($"• {value}", PrototypeUiTheme.BodyStyle);
        }

        private static string Display(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "Unknown";
            string leaf = id.Split('.').LastOrDefault() ?? id;
            return string.Join(" ", leaf.Split('-').Select(part => string.IsNullOrWhiteSpace(part) ? part : char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }
    }
}
