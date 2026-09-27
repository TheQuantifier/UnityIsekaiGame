using System;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Places;

namespace UnityIsekaiGame.Quests
{
    public static class QuestObjectiveSignalBus
    {
        public static event Action<string> TalkedTo;
        public static event Action<string> ReachedLocation;
        public static event Action<QuestObjectiveSignal> SignalReported;

        public static void ReportTalk(string targetId)
        {
            if (!string.IsNullOrWhiteSpace(targetId))
            {
                TalkedTo?.Invoke(targetId);
            }
        }

        public static void ReportTalk(string targetId, string actorPersonId, double worldTime)
        {
            ReportTalk(targetId);
            Report(new QuestObjectiveSignal
            {
                actorPersonId = actorPersonId,
                participantPersonId = actorPersonId,
                category = QuestObjectiveCategory.SpeakToPerson,
                target = new InformationSubjectReferenceData
                {
                    subjectType = InformationSubjectType.PersonIdentity,
                    subjectId = targetId
                },
                amount = 1,
                worldTime = worldTime,
                committed = true,
                sourceRuntimeId = "dialogue"
            });
        }

        public static void ReportReachLocation(string locationId)
        {
            if (!string.IsNullOrWhiteSpace(locationId))
            {
                ReachedLocation?.Invoke(locationId);
            }
        }

        public static void ReportReachLocation(string locationId, string actorPersonId, double worldTime)
        {
            ReportReachLocation(locationId);
            Report(new QuestObjectiveSignal
            {
                actorPersonId = actorPersonId,
                participantPersonId = actorPersonId,
                category = QuestObjectiveCategory.ReachLocation,
                target = new InformationSubjectReferenceData
                {
                    subjectType = InformationSubjectType.Location,
                    subjectId = locationId
                },
                amount = 1,
                worldTime = worldTime,
                committed = true,
                sourceRuntimeId = "world-location"
            });
        }

        public static void Report(QuestObjectiveSignal signal)
        {
            if (signal != null)
            {
                SignalReported?.Invoke(signal);
            }
        }

        public static void Report(QuestObjectiveCategory category, string targetId, string actorPersonId, double worldTime, int amount = 1, string sourceEventId = "", InformationSubjectType subjectType = InformationSubjectType.Custom)
        {
            Report(new QuestObjectiveSignal
            {
                actorPersonId = actorPersonId,
                participantPersonId = actorPersonId,
                category = category,
                target = new InformationSubjectReferenceData { subjectType = subjectType, subjectId = targetId },
                amount = Math.Max(1, amount),
                worldTime = worldTime,
                committed = true,
                sourceRuntimeId = "gameplay",
                sourceEventId = sourceEventId
            });
        }

        public static void ReportReachLocation(PlaceDefinition place)
        {
            if (place != null)
            {
                ReportReachLocation(place.Id);
            }
        }
    }
}
