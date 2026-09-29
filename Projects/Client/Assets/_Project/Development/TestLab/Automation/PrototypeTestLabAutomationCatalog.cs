#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityIsekaiGame.Development.Automation
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PrototypeTestLabAutomationProviderAttribute : Attribute
    {
        public PrototypeTestLabAutomationProviderAttribute(int step, string label, int order)
        {
            Step = step;
            Label = string.IsNullOrWhiteSpace(label) ? $"Step {step}" : label.Trim();
            Order = order;
        }

        public int Step { get; }
        public string Label { get; }
        public int Order { get; }
    }

    public sealed class PrototypeTestLabAutomationProviderDescriptor
    {
        internal PrototypeTestLabAutomationProviderDescriptor(Type providerType, PrototypeTestLabAutomationProviderAttribute attribute)
        {
            ProviderType = providerType;
            Step = attribute.Step;
            Label = attribute.Label;
            Order = attribute.Order;
        }

        public Type ProviderType { get; }
        public int Step { get; }
        public string Label { get; }
        public int Order { get; }
        public string Name => ProviderType?.Name ?? string.Empty;
    }

    public sealed class PrototypeTestLabAutomationSuiteDescriptor
    {
        internal PrototypeTestLabAutomationSuiteDescriptor(string suiteId, int step, string label, string providerName)
        {
            SuiteId = suiteId ?? string.Empty;
            Step = step;
            Label = label ?? string.Empty;
            ProviderName = providerName ?? string.Empty;
        }

        public string SuiteId { get; }
        public int Step { get; }
        public string Label { get; }
        public string ProviderName { get; }
    }

    public static class PrototypeTestLabAutomationCatalog
    {
        private const string RegisterMethodName = "RegisterDefaults";

        public static IReadOnlyList<PrototypeTestLabAutomationProviderDescriptor> Providers => DiscoverProviders();

        public static TestLabAutomationRegistry CreateDefaultRegistry(int stepFilter = 0)
        {
            TestLabAutomationRegistry registry = new TestLabAutomationRegistry();
            RegisterDefaultSuites(registry, stepFilter);
            return registry;
        }

        public static void RegisterDefaultSuites(TestLabAutomationRegistry registry, int stepFilter = 0)
        {
            if (registry == null)
            {
                return;
            }

            foreach (PrototypeTestLabAutomationProviderDescriptor provider in DiscoverProviders())
            {
                if (stepFilter > 0 && provider.Step != stepFilter)
                {
                    continue;
                }

                RegisterProvider(provider, registry);
            }
        }

        public static IReadOnlyList<string> SuiteIds(int stepFilter = 0)
        {
            return CreateDefaultRegistry(stepFilter).Suites.Select(suite => suite.SuiteId).ToArray();
        }

        public static IReadOnlyList<PrototypeTestLabAutomationSuiteDescriptor> DescribeSuites(int stepFilter = 0)
        {
            List<PrototypeTestLabAutomationSuiteDescriptor> suites = new List<PrototypeTestLabAutomationSuiteDescriptor>();
            foreach (PrototypeTestLabAutomationProviderDescriptor provider in DiscoverProviders())
            {
                if (stepFilter > 0 && provider.Step != stepFilter)
                {
                    continue;
                }

                TestLabAutomationRegistry registry = new TestLabAutomationRegistry();
                RegisterProvider(provider, registry);
                suites.AddRange(registry.Suites.Select(suite => new PrototypeTestLabAutomationSuiteDescriptor(suite.SuiteId, provider.Step, provider.Label, provider.Name)));
            }

            return suites
                .OrderBy(suite => suite.Step)
                .ThenBy(suite => suite.SuiteId, StringComparer.Ordinal)
                .ToArray();
        }

        public static PrototypeTestLabAutomationCatalogValidationResult Validate()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            IReadOnlyList<PrototypeTestLabAutomationProviderDescriptor> providers = DiscoverProviders();
            foreach (PrototypeTestLabAutomationProviderDescriptor provider in providers)
            {
                if (provider.Step <= 0)
                {
                    errors.Add($"Provider '{provider.Name}' has invalid Step '{provider.Step}'.");
                }

                if (provider.Order <= 0)
                {
                    errors.Add($"Provider '{provider.Name}' has invalid Order '{provider.Order}'.");
                }

                if (string.IsNullOrWhiteSpace(provider.Label))
                {
                    errors.Add($"Provider '{provider.Name}' has no label.");
                }

                MethodInfo method = provider.ProviderType.GetMethod(
                    RegisterMethodName,
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(TestLabAutomationRegistry) },
                    null);
                if (method == null)
                {
                    errors.Add($"Provider '{provider.Name}' is missing public static {RegisterMethodName}(TestLabAutomationRegistry).");
                }
            }

            foreach (IGrouping<int, PrototypeTestLabAutomationProviderDescriptor> duplicate in providers.GroupBy(provider => provider.Step).Where(group => group.Count() > 1))
            {
                errors.Add($"Duplicate automation Step provider '{duplicate.Key}': {string.Join(", ", duplicate.Select(provider => provider.Name))}.");
            }

            foreach (IGrouping<int, PrototypeTestLabAutomationProviderDescriptor> duplicate in providers.GroupBy(provider => provider.Order).Where(group => group.Count() > 1))
            {
                errors.Add($"Duplicate automation provider order '{duplicate.Key}': {string.Join(", ", duplicate.Select(provider => provider.Name))}.");
            }

            foreach (IGrouping<string, PrototypeTestLabAutomationProviderDescriptor> duplicate in providers.GroupBy(provider => provider.Label, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                errors.Add($"Duplicate automation provider label '{duplicate.Key}': {string.Join(", ", duplicate.Select(provider => provider.Name))}.");
            }

            try
            {
                foreach (IGrouping<string, PrototypeTestLabAutomationSuiteDescriptor> duplicate in DescribeSuites().GroupBy(suite => suite.SuiteId, StringComparer.Ordinal).Where(group => group.Count() > 1))
                {
                    errors.Add($"Duplicate automation suite ownership '{duplicate.Key}': {string.Join(", ", duplicate.Select(suite => suite.ProviderName))}.");
                }

                TestLabAutomationRegistry registry = CreateDefaultRegistry();
                TestLabAutomationValidationResult validation = TestLabAutomationValidation.Validate(registry);
                errors.AddRange(validation.Errors.Select(error => $"Suite validation: {error}"));
                warnings.AddRange(validation.Warnings.Select(warning => $"Suite validation: {warning}"));
            }
            catch (Exception exception)
            {
                errors.Add($"Catalog registration failed: {exception.GetType().Name}: {exception.Message}");
            }

            return new PrototypeTestLabAutomationCatalogValidationResult(errors, warnings);
        }

        private static void RegisterProvider(PrototypeTestLabAutomationProviderDescriptor provider, TestLabAutomationRegistry registry)
        {
            MethodInfo method = provider.ProviderType.GetMethod(
                RegisterMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(TestLabAutomationRegistry) },
                null);
            if (method == null)
            {
                throw new InvalidOperationException($"Prototype Test Lab automation provider '{provider.ProviderType.FullName}' is missing public static {RegisterMethodName}(TestLabAutomationRegistry).");
            }

            method.Invoke(null, new object[] { registry });
        }

        private static IReadOnlyList<PrototypeTestLabAutomationProviderDescriptor> DiscoverProviders()
        {
            return typeof(PrototypeTestLabAutomationCatalog).Assembly
                .GetTypes()
                .Select(type => new
                {
                    Type = type,
                    Attribute = type.GetCustomAttribute<PrototypeTestLabAutomationProviderAttribute>(inherit: false)
                })
                .Where(item => item.Attribute != null)
                .Select(item => new PrototypeTestLabAutomationProviderDescriptor(item.Type, item.Attribute))
                .OrderBy(provider => provider.Order)
                .ThenBy(provider => provider.Step)
                .ThenBy(provider => provider.Name, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public sealed class PrototypeTestLabAutomationCatalogValidationResult
    {
        internal PrototypeTestLabAutomationCatalogValidationResult(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
        public string ToSummary()
        {
            return $"Prototype automation catalog validation: {(Succeeded ? "Succeeded" : "Failed")} with {Errors.Count} error(s), {Warnings.Count} warning(s).";
        }
    }
}
#endif
