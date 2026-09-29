using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Persistence
{
    public sealed class DelegatePersistenceConsistencyValidator : IPersistenceConsistencyValidator
    {
        private readonly Func<PersistenceConsistencyAuditReport> validation;

        public DelegatePersistenceConsistencyValidator(string validatorKey, bool isRequired, Func<PersistenceConsistencyAuditReport> validation)
        {
            ValidatorKey = string.IsNullOrWhiteSpace(validatorKey) ? throw new ArgumentException("Validator key is required.", nameof(validatorKey)) : validatorKey;
            IsRequired = isRequired;
            this.validation = validation ?? throw new ArgumentNullException(nameof(validation));
        }

        public string ValidatorKey { get; }
        public bool IsRequired { get; }
        public PersistenceConsistencyAuditReport Validate() => validation();
    }

    public abstract class PersistenceRuntimeContext
    {
        private readonly List<string> registrationFailures = new List<string>();

        protected PersistenceRuntimeContext(PersistenceService service)
        {
            Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public PersistenceService Service { get; }
        public IReadOnlyList<string> RegistrationFailures => registrationFailures;

        public bool Register(IPersistenceParticipant participant)
        {
            return Register(participant, out _);
        }

        public bool Register(IPersistenceParticipant participant, out string failureReason)
        {
            if (Service.RegisterParticipant(participant, out failureReason))
            {
                return true;
            }

            registrationFailures.Add(failureReason);
            return false;
        }

        public void Unregister(IPersistenceParticipant participant)
        {
            Service.UnregisterParticipant(participant);
        }

        public PersistenceReadinessReport BuildReadiness(IEnumerable<string> requiredParticipantKeys)
        {
            string[] registered = Service.BuildParticipantManifest().Select(value => value.Key).ToArray();
            HashSet<string> registeredSet = new HashSet<string>(registered, StringComparer.Ordinal);
            string[] required = (requiredParticipantKeys ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            List<string> failures = new List<string>(registrationFailures);
            failures.AddRange(required.Where(value => !registeredSet.Contains(value)).Select(value => $"Required participant '{value}' is not registered."));

            PersistenceDependencyReport dependencies = Service.BuildParticipantDependencyReport();
            if (!dependencies.succeeded)
            {
                failures.Add(dependencies.message);
            }

            return new PersistenceReadinessReport
            {
                succeeded = failures.Count == 0,
                context = Service.ContextKind.ToString(),
                message = failures.Count == 0
                    ? $"{Service.ContextKind} persistence context is ready."
                    : $"{Service.ContextKind} persistence context is not ready: {string.Join(" ", failures)}",
                requiredParticipants = required,
                registeredParticipants = registered,
                failures = failures.ToArray()
            };
        }
    }

    public sealed class PlayerPersistenceContext : PersistenceRuntimeContext
    {
        public PlayerPersistenceContext(PersistenceService service) : base(service)
        {
            if (service.ContextKind != PersistenceContextKind.Player)
            {
                throw new ArgumentException("Player persistence context requires a player-scoped service.", nameof(service));
            }
        }
    }

    public sealed class WorldPersistenceContext : PersistenceRuntimeContext
    {
        public WorldPersistenceContext(PersistenceService service) : base(service)
        {
            if (service.ContextKind != PersistenceContextKind.World)
            {
                throw new ArgumentException("World persistence context requires a world-scoped service.", nameof(service));
            }
        }
    }
}
