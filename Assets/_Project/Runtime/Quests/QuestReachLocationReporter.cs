using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Input;

namespace UnityIsekaiGame.Quests
{
    public sealed class QuestReachLocationReporter : MonoBehaviour
    {
        [SerializeField] private PlaceDefinition targetPlace;
        [SerializeField] private string locationId;
        [SerializeField] private bool reportOnce;
        [SerializeField] private bool showPrototypeHudMessage = true;
        [SerializeField] private string prototypeHudMessage;
        [SerializeField, Min(0.1f)] private float prototypeHudMessageCooldown = 1.5f;

        public PlaceDefinition TargetPlace => targetPlace;
        public string ReportedLocationId => targetPlace == null ? locationId : targetPlace.Id;
        public string DisplayName => targetPlace == null ? ReportedLocationId : targetPlace.DisplayName;

        private bool reported;
        private float nextPrototypeHudMessageTime;

        private void OnTriggerEnter(Collider other)
        {
            NotifyPlaceTracker(other, entered: true);
            TryReport(other);
        }

        private void OnTriggerStay(Collider other)
        {
            NotifyPlaceTracker(other, entered: true);
            TryReport(other);
        }

        private void OnTriggerExit(Collider other)
        {
            NotifyPlaceTracker(other, entered: false);
        }

        private void TryReport(Collider other)
        {
            if (reported && reportOnce)
            {
                return;
            }

            if (other == null || other.GetComponentInParent<PlayerInputReader>() == null)
            {
                return;
            }

            if (LocationRestoreGuard.IsRestoringLocation)
            {
                return;
            }

            reported = true;
            if (showPrototypeHudMessage && Time.time >= nextPrototypeHudMessageTime)
            {
                string message = string.IsNullOrWhiteSpace(prototypeHudMessage)
                    ? $"Entered {DisplayName}"
                    : prototypeHudMessage;
                GameHudMessageBus.Show(message);
                nextPrototypeHudMessageTime = Time.time + prototypeHudMessageCooldown;
            }

            PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            QuestObjectiveSignalBus.ReportReachLocation(ReportedLocationId, services?.PlayerPersonId, services?.PlayTime?.CumulativeSeconds ?? Time.unscaledTimeAsDouble);
        }

        private void NotifyPlaceTracker(Collider other, bool entered)
        {
            if (targetPlace == null || other == null)
            {
                return;
            }

            CurrentPlaceTracker tracker = other.GetComponentInParent<CurrentPlaceTracker>();
            if (tracker == null)
            {
                return;
            }

            if (entered)
            {
                tracker.NotifyEntered(targetPlace, LocationRestoreGuard.IsRestoringLocation);
            }
            else
            {
                tracker.NotifyExited(targetPlace, LocationRestoreGuard.IsRestoringLocation);
            }
        }

        public void ResetReporter()
        {
            reported = false;
        }
    }
}
