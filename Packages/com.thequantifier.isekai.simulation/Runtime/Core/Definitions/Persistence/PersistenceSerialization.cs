using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace UnityIsekaiGame.GameData.Persistence
{
    public interface ISaveSerializer
    {
        string Serialize<T>(T value, bool indented = false);
        T Deserialize<T>(string json);
    }

    /// <summary>
    /// Optional low-allocation serializer contract used by large server checkpoints. Implementations
    /// write directly to the destination instead of first constructing one very large JSON string.
    /// </summary>
    public interface IStreamingSaveSerializer : ISaveSerializer
    {
        void Serialize<T>(TextWriter writer, T value, bool indented = false);
        T Deserialize<T>(TextReader reader);
    }

    public sealed class NewtonsoftSaveSerializer : IStreamingSaveSerializer
    {
        private static readonly JsonSerializerSettings CompactSettings = CreateSettings(Formatting.None);
        private static readonly JsonSerializerSettings IndentedSettings = CreateSettings(Formatting.Indented);

        public string Serialize<T>(T value, bool indented = false)
        {
            return JsonConvert.SerializeObject(value, indented ? IndentedSettings : CompactSettings);
        }

        public void Serialize<T>(TextWriter writer, T value, bool indented = false)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            JsonSerializer serializer = JsonSerializer.Create(indented ? IndentedSettings : CompactSettings);
            using var jsonWriter = new JsonTextWriter(writer)
            {
                CloseOutput = false,
                Formatting = indented ? Formatting.Indented : Formatting.None
            };
            serializer.Serialize(jsonWriter, value);
            jsonWriter.Flush();
        }

        public T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new JsonSerializationException("Cannot deserialize empty save JSON.");
            }

            return JsonConvert.DeserializeObject<T>(json, CompactSettings);
        }

        public T Deserialize<T>(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            JsonSerializer serializer = JsonSerializer.Create(CompactSettings);
            using var jsonReader = new JsonTextReader(reader) { CloseInput = false };
            return serializer.Deserialize<T>(jsonReader);
        }

        private static JsonSerializerSettings CreateSettings(Formatting formatting)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                Formatting = formatting,
                ContractResolver = new PublicFieldContractResolver(),
                DateParseHandling = DateParseHandling.None,
                FloatFormatHandling = FloatFormatHandling.String,
                FloatParseHandling = FloatParseHandling.Double,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
                TypeNameHandling = TypeNameHandling.None
            };
            settings.Converters.Add(new SaveParticipantRecordConverter());
            return settings;
        }

        private sealed class PublicFieldContractResolver : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                return objectType
                    .GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Cast<MemberInfo>()
                    .ToList();
            }
        }

        private sealed class SaveParticipantRecordConverter : JsonConverter
        {
            private static readonly HashSet<string> AllowedProperties = new HashSet<string>(StringComparer.Ordinal)
            {
                "participantKey",
                "participantSchemaVersion",
                "required",
                "persistenceScope",
                "ownerId",
                "loadPhase",
                "loadPriority",
                "payload"
            };

            public override bool CanConvert(Type objectType) => objectType == typeof(SaveParticipantRecord);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                SaveParticipantRecord record = (SaveParticipantRecord)value;
                writer.WriteStartObject();
                Write(writer, "participantKey", record?.participantKey);
                Write(writer, "participantSchemaVersion", record?.participantSchemaVersion ?? 0);
                Write(writer, "required", record?.required ?? false);
                Write(writer, "persistenceScope", record?.persistenceScope ?? 0);
                Write(writer, "ownerId", record?.ownerId);
                Write(writer, "loadPhase", record?.loadPhase ?? 0);
                Write(writer, "loadPriority", record?.loadPriority ?? 0);
                writer.WritePropertyName("payload");
                if (record == null || string.IsNullOrWhiteSpace(record.payloadJson))
                {
                    writer.WriteNull();
                }
                else
                {
                    // Payloads are normalized and validated before envelope serialization. Writing the
                    // raw value avoids building a second JToken tree for multi-megabyte participants.
                    writer.WriteRawValue(record.payloadJson);
                }

                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject json = JObject.Load(reader);
                string unknown = json.Properties().Select(property => property.Name).FirstOrDefault(name => !AllowedProperties.Contains(name));
                if (!string.IsNullOrWhiteSpace(unknown))
                {
                    throw new JsonSerializationException($"Unknown save participant property '{unknown}'.");
                }

                JToken payload = json["payload"] ?? throw new JsonSerializationException("Save participant payload is missing.");
                return new SaveParticipantRecord
                {
                    participantKey = json.Value<string>("participantKey") ?? string.Empty,
                    participantSchemaVersion = json.Value<int?>("participantSchemaVersion") ?? 0,
                    required = json.Value<bool?>("required") ?? false,
                    persistenceScope = json.Value<int?>("persistenceScope") ?? 0,
                    ownerId = json.Value<string>("ownerId") ?? string.Empty,
                    loadPhase = json.Value<int?>("loadPhase") ?? 0,
                    loadPriority = json.Value<int?>("loadPriority") ?? 0,
                    payloadJson = payload.ToString(Formatting.None)
                };
            }

            private static void Write(JsonWriter writer, string name, object value)
            {
                writer.WritePropertyName(name);
                writer.WriteValue(value);
            }
        }
    }

    public static class PersistenceSerialization
    {
        private static ISaveSerializer serializer = new NewtonsoftSaveSerializer();

        public static ISaveSerializer Serializer
        {
            get => serializer;
            set => serializer = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static string Serialize<T>(T value, bool indented = false)
        {
            return serializer.Serialize(value, indented);
        }

        public static T Deserialize<T>(string json)
        {
            return serializer.Deserialize<T>(json);
        }
    }
}
