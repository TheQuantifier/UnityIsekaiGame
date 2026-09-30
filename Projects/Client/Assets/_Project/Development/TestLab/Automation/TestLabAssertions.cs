#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Development.Automation
{
    public static class TestLabAssertions
    {
        public static TestLabAutomationStepResult Pass(string stepId, string displayName, string diagnostics = "")
        {
            return new TestLabAutomationStepResult(stepId, displayName, TestLabAutomationStatus.Passed, "Pass", string.Empty, string.Empty, string.Empty, string.Empty, diagnostics);
        }

        public static TestLabAutomationStepResult Skip(string stepId, string displayName, string diagnostics)
        {
            return new TestLabAutomationStepResult(stepId, displayName, TestLabAutomationStatus.Skipped, "Skip", string.Empty, string.Empty, string.Empty, string.Empty, diagnostics);
        }

        public static TestLabAutomationStepResult Cancelled(string stepId, string displayName, string diagnostics)
        {
            return new TestLabAutomationStepResult(stepId, displayName, TestLabAutomationStatus.Cancelled, "Cancelled", string.Empty, string.Empty, string.Empty, string.Empty, diagnostics);
        }

        public static TestLabAutomationStepResult Error(string stepId, string displayName, Exception exception)
        {
            return new TestLabAutomationStepResult(stepId, displayName, TestLabAutomationStatus.Error, "Exception", "No exception", exception == null ? string.Empty : exception.GetType().Name, string.Empty, string.Empty, exception == null ? string.Empty : exception.Message, exception);
        }

        public static TestLabAutomationStepResult Fail(string stepId, string displayName, string assertionType, object expected, object actual, string diagnostics = "", string actorId = "", string transactionId = "")
        {
            return new TestLabAutomationStepResult(stepId, displayName, TestLabAutomationStatus.Failed, assertionType, Format(expected), Format(actual), actorId, transactionId, diagnostics);
        }

        public static TestLabAutomationStepResult True(string stepId, string displayName, bool actual, string diagnostics = "", string actorId = "", string transactionId = "")
        {
            return actual ? Pass(stepId, displayName, diagnostics) : Fail(stepId, displayName, "True", true, actual, diagnostics, actorId, transactionId);
        }

        public static TestLabAutomationStepResult False(string stepId, string displayName, bool actual, string diagnostics = "", string actorId = "", string transactionId = "")
        {
            return !actual ? Pass(stepId, displayName, diagnostics) : Fail(stepId, displayName, "False", false, actual, diagnostics, actorId, transactionId);
        }

        public static TestLabAutomationStepResult Null(string stepId, string displayName, object actual, string diagnostics = "")
        {
            return actual == null ? Pass(stepId, displayName, diagnostics) : Fail(stepId, displayName, "Null", null, actual, diagnostics);
        }

        public static TestLabAutomationStepResult NotNull(string stepId, string displayName, object actual, string diagnostics = "")
        {
            return actual != null ? Pass(stepId, displayName, diagnostics) : Fail(stepId, displayName, "NotNull", "not null", null, diagnostics);
        }

        public static TestLabAutomationStepResult Equal<T>(string stepId, string displayName, T expected, T actual, string diagnostics = "", string actorId = "", string transactionId = "")
        {
            return EqualityComparer<T>.Default.Equals(expected, actual)
                ? Pass(stepId, displayName, diagnostics)
                : Fail(stepId, displayName, "Equal", expected, actual, diagnostics, actorId, transactionId);
        }

        public static TestLabAutomationStepResult NotEqual<T>(string stepId, string displayName, T notExpected, T actual, string diagnostics = "")
        {
            return !EqualityComparer<T>.Default.Equals(notExpected, actual)
                ? Pass(stepId, displayName, diagnostics)
                : Fail(stepId, displayName, "NotEqual", $"not {notExpected}", actual, diagnostics);
        }

        public static TestLabAutomationStepResult Approximately(string stepId, string displayName, float expected, float actual, float tolerance, string diagnostics = "")
        {
            return Math.Abs(expected - actual) <= Math.Abs(tolerance)
                ? Pass(stepId, displayName, diagnostics)
                : Fail(stepId, displayName, "Approximately", $"{expected:0.###} +/- {Math.Abs(tolerance):0.###}", actual.ToString("0.###"), diagnostics);
        }

        public static TestLabAutomationStepResult Contains(string stepId, string displayName, string expectedSubstring, string actual, string diagnostics = "")
        {
            bool contains = (actual ?? string.Empty).IndexOf(expectedSubstring ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
            return contains ? Pass(stepId, displayName, diagnostics) : Fail(stepId, displayName, "Contains", expectedSubstring, actual, diagnostics);
        }

        public static TestLabAutomationStepResult DoesNotContain(string stepId, string displayName, string unexpectedSubstring, string actual, string diagnostics = "")
        {
            bool contains = (actual ?? string.Empty).IndexOf(unexpectedSubstring ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
            return !contains ? Pass(stepId, displayName, diagnostics) : Fail(stepId, displayName, "DoesNotContain", $"not {unexpectedSubstring}", actual, diagnostics);
        }

        public static TestLabAutomationStepResult Count(string stepId, string displayName, int expected, IEnumerable values, string diagnostics = "")
        {
            int actual = values == null ? 0 : values.Cast<object>().Count();
            return Equal(stepId, displayName, expected, actual, diagnostics);
        }

        public static TestLabAutomationStepResult SequenceEqual<T>(string stepId, string displayName, IEnumerable<T> expected, IEnumerable<T> actual, string diagnostics = "")
        {
            T[] expectedArray = (expected ?? Array.Empty<T>()).ToArray();
            T[] actualArray = (actual ?? Array.Empty<T>()).ToArray();
            return expectedArray.SequenceEqual(actualArray)
                ? Pass(stepId, displayName, diagnostics)
                : Fail(stepId, displayName, "SequenceEqual", string.Join(",", expectedArray), string.Join(",", actualArray), diagnostics);
        }

        public static TestLabAutomationStepResult OperationSucceeded(string stepId, string displayName, PrototypeTestLabOperation operation, string transactionId = "")
        {
            return operation.Succeeded
                ? Pass(stepId, displayName, $"{operation.Code}: {operation.Message}")
                : Fail(stepId, displayName, "OperationSucceeded", "Succeeded", operation.Code, operation.Message, string.Empty, transactionId);
        }

        public static TestLabAutomationStepResult OperationFailed(string stepId, string displayName, PrototypeTestLabOperation operation, string expectedCode = "", string transactionId = "")
        {
            if (operation.Succeeded)
            {
                return Fail(stepId, displayName, "OperationFailed", string.IsNullOrWhiteSpace(expectedCode) ? "Failure" : expectedCode, operation.Code, operation.Message, string.Empty, transactionId);
            }

            return string.IsNullOrWhiteSpace(expectedCode) || string.Equals(operation.Code, expectedCode, StringComparison.Ordinal)
                ? Pass(stepId, displayName, $"{operation.Code}: {operation.Message}")
                : Fail(stepId, displayName, "OperationFailed", expectedCode, operation.Code, operation.Message, string.Empty, transactionId);
        }

        public static TestLabAutomationStepResult RevisionChanged(string stepId, string displayName, int before, int after, string diagnostics = "")
        {
            return after != before ? Pass(stepId, displayName, diagnostics) : Fail(stepId, displayName, "RevisionChanged", $"not {before}", after, diagnostics);
        }

        public static TestLabAutomationStepResult RevisionUnchanged(string stepId, string displayName, int before, int after, string diagnostics = "")
        {
            return after == before ? Pass(stepId, displayName, diagnostics) : Fail(stepId, displayName, "RevisionUnchanged", before, after, diagnostics);
        }

        public static TestLabAutomationStepResult ValidationSucceeded(string stepId, string displayName, TestLabAutomationValidationResult validation)
        {
            return validation != null && validation.Succeeded
                ? Pass(stepId, displayName, validation == null ? string.Empty : validation.ToSummary())
                : Fail(stepId, displayName, "ValidationSucceeded", "No errors", validation == null ? "Missing validation" : validation.ToSummary());
        }

        public static TestLabAutomationStepResult ValidationFailed(string stepId, string displayName, TestLabAutomationValidationResult validation)
        {
            return validation != null && !validation.Succeeded
                ? Pass(stepId, displayName, validation.ToSummary())
                : Fail(stepId, displayName, "ValidationFailed", "Errors", validation == null ? "Missing validation" : validation.ToSummary());
        }

        private static string Format(object value)
        {
            return value == null ? "null" : value.ToString();
        }
    }
}
#endif
