using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Editor
{
    public static class DefinitionValidationMenu
    {
        [MenuItem("Tools/Game Data/Validate Definitions")]
        public static void ValidateDefinitions()
        {
            DefinitionValidationReport combinedReport = ValidateAllDefinitionCatalogs();

            if (combinedReport.HasErrors)
            {
                Debug.LogError(combinedReport.GetSummary());
            }
            else if (combinedReport.WarningCount > 0)
            {
                Debug.LogWarning(combinedReport.GetSummary());
            }
            else
            {
                Debug.Log(combinedReport.GetSummary());
            }
        }

        public static DefinitionValidationReport ValidateAllDefinitionCatalogs()
        {
            DefinitionValidationReport combinedReport = new DefinitionValidationReport();
            string[] catalogGuids = AssetDatabase.FindAssets("t:DefinitionCatalog");

            if (catalogGuids.Length == 0)
            {
                combinedReport.AddWarning("No DefinitionCatalog assets were found. Create an explicit catalog before relying on stable-ID lookup.");
                return combinedReport;
            }

            for (int i = 0; i < catalogGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
                DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(path);
                DefinitionValidationReport catalogReport = DefinitionCatalogValidator.Validate(catalog);

                if (catalog == null)
                {
                    combinedReport.AddError($"Definition catalog at '{path}' could not be loaded.");
                    continue;
                }

                combinedReport.AddInfo($"Validated definition catalog '{catalog.name}' at '{path}'.");
                combinedReport.AddRange(catalogReport);
            }

            return combinedReport;
        }
    }
}
