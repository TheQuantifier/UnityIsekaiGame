using System.Text;

namespace UnityIsekaiGame.GameData
{
    public static class DefinitionIdValidator
    {
        public const string AllowedFormatDescription = "lowercase letters, digits, periods, and hyphens; no whitespace; no leading, trailing, or adjacent separators";

        public static DefinitionIdValidationResult Validate(string id, string context = null)
        {
            DefinitionIdValidationResult result = new DefinitionIdValidationResult();
            string prefix = string.IsNullOrWhiteSpace(context) ? "Definition ID" : context;

            if (id == null)
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} is null.");
                return result;
            }

            if (id.Length == 0)
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} is empty.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} contains only whitespace.");
                result.SetNormalizedSuggestion(CreateNormalizedSuggestion(id));
                return result;
            }

            if (id != id.Trim())
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} has leading or trailing whitespace.");
            }

            bool hasUppercase = false;
            bool hasSpace = false;
            bool hasUnsupported = false;
            bool hasRepeatedSeparator = false;

            char previous = '\0';
            for (int i = 0; i < id.Length; i++)
            {
                char character = id[i];

                if (char.IsUpper(character))
                {
                    hasUppercase = true;
                }

                if (char.IsWhiteSpace(character))
                {
                    hasSpace = true;
                }

                if (!IsAllowedCharacter(character))
                {
                    hasUnsupported = true;
                }

                if (IsSeparator(character) && IsSeparator(previous))
                {
                    hasRepeatedSeparator = true;
                }

                previous = character;
            }

            if (hasUppercase)
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} contains uppercase characters.");
            }

            if (hasSpace)
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} contains whitespace.");
            }

            if (hasUnsupported)
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} contains unsupported characters. Allowed format: {AllowedFormatDescription}.");
            }

            if (IsSeparator(id[0]))
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} starts with a separator.");
            }

            if (IsSeparator(id[id.Length - 1]))
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} ends with a separator.");
            }

            if (hasRepeatedSeparator)
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} contains repeated separators.");
            }

            if (!id.Contains("."))
            {
                result.Add(DefinitionIdValidationSeverity.Error, $"{prefix} has no namespace/domain prefix. Use IDs like item.health-potion.");
            }

            string suggestion = CreateNormalizedSuggestion(id);
            if (suggestion != id)
            {
                result.SetNormalizedSuggestion(suggestion);
            }

            return result;
        }

        public static bool IsValid(string id)
        {
            return Validate(id).IsValid;
        }

        public static string CreateNormalizedSuggestion(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(id.Trim().ToLowerInvariant());
            char previous = '\0';

            for (int i = 0; i < builder.Length; i++)
            {
                char character = builder[i];
                if (char.IsWhiteSpace(character))
                {
                    character = '-';
                }

                if (!IsAllowedCharacter(character))
                {
                    character = '-';
                }

                if (IsSeparator(character) && IsSeparator(previous))
                {
                    builder.Remove(i, 1);
                    i--;
                    continue;
                }

                builder[i] = character;
                previous = character;
            }

            while (builder.Length > 0 && IsSeparator(builder[0]))
            {
                builder.Remove(0, 1);
            }

            while (builder.Length > 0 && IsSeparator(builder[builder.Length - 1]))
            {
                builder.Remove(builder.Length - 1, 1);
            }

            return builder.ToString();
        }

        private static bool IsAllowedCharacter(char character)
        {
            return character >= 'a' && character <= 'z'
                || character >= '0' && character <= '9'
                || character == '.'
                || character == '-';
        }

        private static bool IsSeparator(char character)
        {
            return character == '.' || character == '-' || character == '_';
        }

    }
}
