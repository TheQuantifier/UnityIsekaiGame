using System.Collections.Generic;

namespace UnityIsekaiGame.GameData
{
    public sealed class DefinitionRegistry
    {
        private readonly Dictionary<string, IGameDefinition> definitionsById = new Dictionary<string, IGameDefinition>();

        public DefinitionRegistry(IEnumerable<IGameDefinition> definitions, DefinitionValidationReport report = null, GameDataDefaultsDefinition defaults = null)
        {
            Defaults = defaults;
            if (definitions == null)
            {
                report?.AddError("Cannot initialize definition registry from a null definition collection.");
                return;
            }

            foreach (IGameDefinition definition in definitions)
            {
                Register(definition, report);
            }
        }

        public IReadOnlyDictionary<string, IGameDefinition> DefinitionsById => definitionsById;
        public GameDataDefaultsDefinition Defaults { get; }
        public int Count => definitionsById.Count;

        public bool TryGet(string id, out IGameDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(id) && definitionsById.TryGetValue(id, out definition);
        }

        public bool TryGet<TDefinition>(string id, out TDefinition definition)
            where TDefinition : class, IGameDefinition
        {
            definition = null;

            if (!TryGet(id, out IGameDefinition found) || found is not TDefinition typedDefinition)
            {
                return false;
            }

            definition = typedDefinition;
            return true;
        }

        public bool Contains(string id)
        {
            return TryGet(id, out _);
        }

        private void Register(IGameDefinition definition, DefinitionValidationReport report)
        {
            if (definition == null)
            {
                report?.AddError("Definition registry received a null definition.");
                return;
            }

            DefinitionIdValidationResult idResult = DefinitionIdValidator.Validate(definition.Id, $"{definition.GetType().Name} ID");
            foreach (DefinitionIdValidationMessage message in idResult.Messages)
            {
                report?.Add(message.Severity, $"{definition.GetType().Name}: {message.Message}");
            }

            if (!idResult.IsValid)
            {
                return;
            }

            if (definitionsById.TryGetValue(definition.Id, out IGameDefinition existing))
            {
                report?.AddError($"Duplicate definition ID '{definition.Id}' found on {existing.GetType().Name} and {definition.GetType().Name}.");
                return;
            }

            definitionsById.Add(definition.Id, definition);
        }
    }
}
