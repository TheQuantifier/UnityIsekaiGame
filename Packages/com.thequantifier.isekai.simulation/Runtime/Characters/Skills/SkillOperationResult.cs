namespace UnityIsekaiGame.Skills
{
    public readonly struct SkillOperationResult
    {
        private SkillOperationResult(bool succeeded, string code, string message)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? (succeeded ? "Success" : "Failed") : code;
            Message = string.IsNullOrWhiteSpace(message) ? Code : message;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }

        public static SkillOperationResult Success(string message, string code = "Success")
        {
            return new SkillOperationResult(true, code, message);
        }

        public static SkillOperationResult Failure(string code, string message)
        {
            return new SkillOperationResult(false, code, message);
        }
    }
}
