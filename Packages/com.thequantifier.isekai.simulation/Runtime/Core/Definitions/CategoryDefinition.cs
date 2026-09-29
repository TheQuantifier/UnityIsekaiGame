using UnityEngine;

namespace UnityIsekaiGame.GameData
{
    [CreateAssetMenu(fileName = "Category", menuName = "Unity Isekai Game/Game Data/Category")]
    public sealed class CategoryDefinition : ScriptableObject, IGameDefinition
    {
        [SerializeField] private string categoryId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition parentCategory;
        [SerializeField] private CategoryDomain domain = CategoryDomain.General;
        [SerializeField] private int sortOrder;

        public string CategoryId => categoryId;
        public string Id => categoryId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition ParentCategory => parentCategory;
        public CategoryDomain Domain => domain;
        public int SortOrder => sortOrder;
    }
}
