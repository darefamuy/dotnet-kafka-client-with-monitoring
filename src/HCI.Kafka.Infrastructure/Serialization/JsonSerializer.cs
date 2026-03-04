using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace HCI.Kafka.Infrastructure.Serialization;

/// <summary>
/// Generic JSON serializer/deserializer for Kafka messages.
///
/// </summary>
public sealed class JsonSerializer<T> : ISerializer<T>, IDeserializer<T>
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public byte[] Serialize(T data, SerializationContext context)
    {
        if (data is null) return Array.Empty<byte>();
        return JsonSerializer.SerializeToUtf8Bytes(data, Options);
    }

    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull || data.IsEmpty)
            throw new InvalidOperationException("Cannot deserialize null or empty Kafka message value.");

        return JsonSerializer.Deserialize<T>(data, Options)
               ?? throw new JsonException($"Deserialized value of type {typeof(T).Name} was null.");
    }
}
