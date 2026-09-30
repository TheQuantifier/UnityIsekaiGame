using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.Input;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeProfessionPanel : MonoBehaviour
    {
        private const float DataRefreshIntervalSeconds = 0.5f;

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
        private readonly HashSet<string> activeProfessionIds = new HashSet<string>(StringComparer.Ordinal);
        private string[] activeProfessionRows = Array.Empty<string>();
        private string[] activityRows = Array.Empty<string>();
        private string[] trainingRows = Array.Empty<string>();
        private string[] credentialRows = Array.Empty<string>();
        private string[] rankRows = Array.Empty<string>();
        private string[] employmentRows = Array.Empty<string>();
        private string[] careerRows = Array.Empty<string>();
        private string[] aspirationRows = Array.Empty<string>();
        private string[] goalRows = Array.Empty<string>();
        private float nextDataRefreshAt;

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
            if (Application.isBatchMode
                || !scene.IsValid()
                || scene.name.IndexOf("Prototype", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PrototypePersistenceServiceBehaviour persistence in root.GetComponentsInChildren<PrototypePersistenceServiceBehaviour>(true))
                {
                    PrototypeProfessionPanel panel = persistence.GetComponent<PrototypeProfessionPanel>();
                    if (panel == null) panel = persistence.gameObject.AddComponent<PrototypeProfessionPanel>();
                    panel.Configure(persistence);
                }
            }
        }

        public void Configure(PrototypePersistenceServiceBehaviour persistence)
        {
            services = persistence;
            input = services == null ? null : services.PlayerInput;
        }

        private void Awake()
        {
            services ??= GetComponent<PrototypePersistenceServiceBehaviour>();
            input = services == null ? null : services.PlayerInput;
            SetVisible(visible);
        }

        private void Update()
        {
            if (input != null && input.ProfessionTogglePressedThisFrame)
            {
                SetVisible(!visible);
            }

            if (visible && Time.unscaledTime >= nextDataRefreshAt)
            {
                RefreshCachedData();
            }
        }

        private void OnDisable() => SetVisible(false);

        private void OnGUI()
        {
            GUI.Label(new Rect(12f, Screen.height - 30f, 420f, 24f), "[F7] Professions & Career", GameUiTheme.MutedStyle);
            if (!visible)
            {
                return;
            }

            Rect window = new Rect(24f, 24f, Mathf.Min(620f, Screen.width - 48f), Mathf.Min(700f, Screen.height - 48f));
            GameUiTheme.DrawPanelFrame(window);
            GUILayout.BeginArea(new Rect(window.x + 16f, window.y + 16f, window.width - 32f, window.height - 32f));
            GUILayout.Label("PROFESSIONS & CAREER", GameUiTheme.TitleStyle);
            if (services == null)
            {
                GUILayout.Label("Career services are unavailable.", GameUiTheme.StatusStyle);
                GUILayout.EndArea();
                return;
            }

            string personId = services.PlayerPersonId;
            GUILayout.Label($"CHARACTER  |  {personId}", GameUiTheme.MutedStyle);
            GUILayout.Space(6f);
            GUILayout.Label("DECLARE PRACTICE", GameUiTheme.HeadingStyle);
            GUILayout.BeginHorizontal();
            foreach ((string name, string professionId, string entryPathId) in Declarations)
            {
                bool active = activeProfessionIds.Contains(professionId);
                GUI.enabled = !active;
                if (GUILayout.Button(active ? $"{name}  [Active]" : name, active ? GameUiTheme.ButtonStyle : GameUiTheme.PrimaryButtonStyle, GUILayout.Height(34f)))
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
                        RefreshCachedData();
                    }
                }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            scroll = GUILayout.BeginScrollView(scroll);
            DrawSection("Active professions", activeProfessionRows);
            DrawSection("Validated work", activityRows);
            DrawSection("Training", trainingRows);
            DrawSection("Credentials", credentialRows);
            DrawSection("Ranks & mastery", rankRows);
            DrawSection("Employment", employmentRows);
            DrawSection("Career timeline", careerRows);
            DrawSection("Aspirations", aspirationRows);
            DrawSection("Goals", goalRows);
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.Label(status, GameUiTheme.StatusStyle);
            if (GUILayout.Button("Close", GameUiTheme.DangerButtonStyle, GUILayout.Height(34f))) SetVisible(false);
            GUILayout.EndArea();
        }

        private void SetVisible(bool value)
        {
            visible = value;
            if (visible)
            {
                RefreshCachedData();
            }
            if (input == null && services != null) input = services.PlayerInput;
            if (input != null) input.SetMenuInputBlocked(this, visible, visible ? CloseFromCancel : null);
            else PlayerCursorMode.SetMenuOpen(this, visible, visible ? CloseFromCancel : null);
        }

        private void CloseFromCancel()
        {
            SetVisible(false);
        }

        private void RefreshCachedData()
        {
            nextDataRefreshAt = Time.unscaledTime + DataRefreshIntervalSeconds;
            if (services == null) return;

            string personId = services.PlayerPersonId;
            var professions = services.Professions.QueryByPerson(personId, true);
            activeProfessionIds.Clear();
            foreach (var profession in professions) activeProfessionIds.Add(profession.ProfessionId);
            activeProfessionRows = Rows(professions.Select(item =>
                $"{Display(item.ProfessionId)} — {(item.Recognized ? "formally recognized" : "self-declared")}{(item.Primary ? ", primary" : string.Empty)}"));
            activityRows = Rows(services.ProfessionalActivities.Activities
                .Where(item => item.personId == personId)
                .Select(item => $"{Display(item.professionId)}: {Display(item.activityDefinitionId)} — quality {item.quality}/1000, {item.difficulty}"));
            trainingRows = Rows(services.Training.QueryByPerson(personId).Select(item => $"{Display(item.Data.programId)} — {item.Data.state}"));
            credentialRows = Rows(services.Credentials.QueryByRecipient(personId).Select(item => $"{Display(item.credentialDefinitionId)} — {item.state}"));
            rankRows = Rows(services.ProfessionalRanks.QueryByPerson(personId).Select(item => $"{Display(item.rankDefinitionId)} — {item.state}"));
            employmentRows = Rows(services.PositionEmployment.QueryEmploymentByPerson(personId).Select(item => $"{Display(item.positionInstanceId)} — {item.state}"));
            careerRows = Rows(services.CareerHistory.QueryEpisodesByPerson(personId).Select(item => $"{Display(item.professionId)} — {item.category}, {item.state}"));
            aspirationRows = Rows(services.LifePaths.QueryAspirationsByPerson(personId).Select(item => $"{Display(item.aspirationDefinitionId)} — {item.state}"));
            goalRows = Rows(services.LifePaths.QueryGoalsByPerson(personId).Select(item => $"{Display(item.goalDefinitionId)} — {item.state}"));
        }

        private static string[] Rows(IEnumerable<string> rows)
        {
            return rows.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        private static void DrawSection(string title, IReadOnlyList<string> values)
        {
            GUILayout.Label(title.ToUpperInvariant(), GameUiTheme.HeadingStyle);
            if (values == null || values.Count == 0)
            {
                GUILayout.Label("None recorded yet.", GameUiTheme.MutedStyle);
                return;
            }
            for (int i = 0; i < values.Count; i++) GUILayout.Label($"• {values[i]}", GameUiTheme.BodyStyle);
        }

        private static string Display(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "Unknown";
            string leaf = id.Split('.').LastOrDefault() ?? id;
            return string.Join(" ", leaf.Split('-').Select(part => string.IsNullOrWhiteSpace(part) ? part : char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }
    }
}
