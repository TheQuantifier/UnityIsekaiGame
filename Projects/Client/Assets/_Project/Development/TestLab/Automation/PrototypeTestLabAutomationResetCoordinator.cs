#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UnityIsekaiGame.Development.Automation
{
    public sealed class PrototypeTestLabAutomationResetCoordinator : ITestLabAutomationResetCoordinator
    {
        public TestLabAutomationStepResult Reset(TestLabAutomationContext context, string reason)
        {
            PrototypeTestLabService service = context?.GetHost<PrototypeTestLabAutomationHost>()?.Service;
            if (service == null)
            {
                context?.EventCapture?.Clear();
                return TestLabAssertions.Pass("reset", "Reset runtime state", reason ?? "No scene Test Lab service reset required.");
            }

            service.ResetAutomationRuntimeState();
            context.EventCapture?.Clear();
            return TestLabAssertions.Pass("reset", "Reset runtime state", reason ?? string.Empty);
        }
    }
}
#endif
