using System.Collections.Generic;

namespace UnityIsekaiGame.Beings.Biology
{
    public sealed class BodyOperationResult
    {
        private BodyOperationResult(bool succeeded, BodyOperationResultCode code, string message, bool preview, bool duplicate, BodySnapshot snapshot)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public BodyOperationResultCode Code { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public BodySnapshot Snapshot { get; }
        public List<string> Diagnostics { get; } = new List<string>();

        public static BodyOperationResult Success(string message, BodySnapshot snapshot, bool preview = false, bool duplicate = false)
        {
            return new BodyOperationResult(true, preview ? BodyOperationResultCode.PreviewSucceeded : duplicate ? BodyOperationResultCode.DuplicateAssignment : BodyOperationResultCode.Success, message, preview, duplicate, snapshot);
        }

        public static BodyOperationResult Failure(BodyOperationResultCode code, string message, bool preview = false, BodySnapshot snapshot = null)
        {
            return new BodyOperationResult(false, code, message, preview, false, snapshot);
        }

        public BodyOperationResult WithDiagnostic(string diagnostic)
        {
            if (!string.IsNullOrWhiteSpace(diagnostic))
            {
                Diagnostics.Add(diagnostic);
            }

            return this;
        }
    }
}
