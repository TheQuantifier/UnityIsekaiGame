using System;

namespace UnityIsekaiGame.Knowledge
{
    [Serializable]
    public sealed class KnowledgePropositionData
    {
        public string factDefinitionId;
        public string subjectId;
        public KnowledgeSubjectType subjectType;
        public string objectId;
        public KnowledgeSubjectType objectType;
        public KnowledgeValueType valueType;
        public string stableValueId;
        public string qualitativeValue;
        public int numericValue;
        public bool booleanValue;
        public string locationContextId;
        public string bodyContextId;
        public string sourceContextId;
        public bool negated;
        public string qualifier;
        public long sourceRevision;

        public KnowledgePropositionData Clone()
        {
            return (KnowledgePropositionData)MemberwiseClone();
        }
    }

    public sealed class KnowledgeProposition : IEquatable<KnowledgeProposition>
    {
        public KnowledgeProposition(KnowledgePropositionData data)
        {
            Data = data == null ? new KnowledgePropositionData() : data.Clone();
            Identity = BuildIdentity(Data);
        }

        public KnowledgePropositionData Data { get; }
        public string Identity { get; }
        public string FactDefinitionId => Data.factDefinitionId ?? string.Empty;
        public string SubjectId => Data.subjectId ?? string.Empty;
        public string BodyContextId => Data.bodyContextId ?? string.Empty;

        public bool Equals(KnowledgeProposition other)
        {
            return other != null && string.Equals(Identity, other.Identity, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is KnowledgeProposition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Identity);
        }

        public override string ToString()
        {
            return Identity;
        }

        public static string BuildIdentity(KnowledgePropositionData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            return string.Join("|",
                Clean(data.factDefinitionId),
                data.subjectType.ToString(),
                Clean(data.subjectId),
                data.objectType.ToString(),
                Clean(data.objectId),
                data.valueType.ToString(),
                Clean(ValueToken(data)),
                Clean(data.locationContextId),
                Clean(data.bodyContextId),
                Clean(data.sourceContextId),
                data.negated ? "not" : "is",
                Clean(data.qualifier),
                data.sourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static bool Validate(KnowledgePropositionData data, KnowledgeFactDefinition definition, out string failureReason)
        {
            failureReason = string.Empty;
            if (data == null)
            {
                failureReason = "Knowledge proposition is missing.";
                return false;
            }

            if (definition == null)
            {
                failureReason = "Knowledge proposition references a missing Fact definition.";
                return false;
            }

            if (!string.Equals(data.factDefinitionId, definition.Id, StringComparison.Ordinal))
            {
                failureReason = $"Knowledge proposition Fact '{data.factDefinitionId}' does not match definition '{definition.Id}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(data.subjectId))
            {
                failureReason = "Knowledge proposition subject ID is missing.";
                return false;
            }

            if (data.subjectType != definition.SubjectType)
            {
                failureReason = $"Knowledge proposition subject type '{data.subjectType}' does not match Fact subject type '{definition.SubjectType}'.";
                return false;
            }

            if (data.valueType != definition.ValueType)
            {
                failureReason = $"Knowledge proposition value type '{data.valueType}' does not match Fact value type '{definition.ValueType}'.";
                return false;
            }

            if (data.valueType == KnowledgeValueType.StableId && string.IsNullOrWhiteSpace(data.stableValueId))
            {
                failureReason = "Knowledge proposition stable value ID is missing.";
                return false;
            }

            return true;
        }

        private static string ValueToken(KnowledgePropositionData data)
        {
            return data.valueType switch
            {
                KnowledgeValueType.Boolean => data.booleanValue ? "true" : "false",
                KnowledgeValueType.Numeric => data.numericValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                KnowledgeValueType.Qualitative => data.qualitativeValue,
                KnowledgeValueType.Text => data.qualitativeValue,
                KnowledgeValueType.StableId => data.stableValueId,
                _ => string.Empty
            };
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "_" : value.Trim();
        }
    }
}
