using NUnit.Framework;
using UnityIsekaiGame.Editor;

namespace UnityIsekaiGame.Tests
{
    public sealed class VisualGameplayPipelineValidationTests
    {
        [Test]
        public void CompleteVisualGameplayPipelineHasNoReferenceOrSceneFindings()
        {
            VisualGameplayPipelineValidationReport report = VisualGameplayPipelineValidation.Validate();

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
        }
    }
}
