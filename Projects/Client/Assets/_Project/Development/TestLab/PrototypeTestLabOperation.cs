#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UnityIsekaiGame.Development
{
    public readonly struct PrototypeTestLabOperation
    {
        public PrototypeTestLabOperation(DateTime timestamp, string operationName, bool succeeded, string code, string message)
        {
            Timestamp = timestamp;
            OperationName = operationName;
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? (succeeded ? "Success" : "Failure") : code;
            Message = string.IsNullOrWhiteSpace(message) ? Code : message;
        }

        public DateTime Timestamp { get; }
        public string OperationName { get; }
        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }

        public static PrototypeTestLabOperation Success(string operationName, string message, string code = "Success")
        {
            return new PrototypeTestLabOperation(DateTime.Now, operationName, true, code, message);
        }

        public static PrototypeTestLabOperation Failure(string operationName, string message, string code = "Failure")
        {
            return new PrototypeTestLabOperation(DateTime.Now, operationName, false, code, message);
        }
    }
}
#endif
