namespace UnityIsekaiGame.GameData
{
    public readonly struct DefinitionIdValidationMessage
    {
        public DefinitionIdValidationMessage(DefinitionIdValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public DefinitionIdValidationSeverity Severity { get; }
        public string Message { get; }
    }
}
