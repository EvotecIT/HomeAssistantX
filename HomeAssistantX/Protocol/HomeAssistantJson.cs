using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;
using System.Reflection;
using System.Globalization;
using HomeAssistantX.Exceptions;

namespace HomeAssistantX.Protocol;

internal static class HomeAssistantJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new HomeAssistantDateTimeOffsetConverter() }
    };

    public static JsonSerializerOptions RawSerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonElement Clone(JsonElement value)
    {
        return value.Clone();
    }

    /// <summary>Snapshots an arbitrary dictionary as a JSON object before asynchronous dispatch.</summary>
    internal static IReadOnlyDictionary<string, object?>? FreezeObject(
        IReadOnlyDictionary<string, object?>? value,
        string parameterName,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            return null;
        }

        JsonDocument? document = null;
        var ownershipTransferred = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            document = SerializeSnapshot(value, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The serialized value was not a JSON object.");
            }

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                // JsonElement retains its parent document, transferring the immutable
                // snapshot without an additional uninterruptible deep clone.
                result.Add(property.Name, property.Value);
            }
            cancellationToken.ThrowIfCancellationRequested();
            ownershipTransferred = true;
            return result;
        }
        catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ArgumentException(displayName + " must be a serializable JSON object.", parameterName, ex);
        }
        finally
        {
            if (!ownershipTransferred) document?.Dispose();
        }
    }

    /// <summary>Snapshots an arbitrary provider-specific JSON value before asynchronous dispatch.</summary>
    internal static JsonElement FreezeValue(
        object? value,
        string parameterName,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        JsonDocument? document = null;
        var ownershipTransferred = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            document = SerializeSnapshot(value, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // The returned element owns the document transitively until the element is collected.
            ownershipTransferred = true;
            return document.RootElement;
        }
        catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ArgumentException(displayName + " must be a serializable JSON value.", parameterName, ex);
        }
        finally
        {
            if (!ownershipTransferred) document?.Dispose();
        }
    }

    private static JsonDocument SerializeSnapshot(object? value, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        stream.Position = 0;
        return JsonDocument.ParseAsync(stream, default, cancellationToken)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>Parses a Home Assistant response while preserving the classified protocol-failure contract.</summary>
    public static JsonDocument ParseResponse(string value, string failureMessage)
    {
        try
        {
            return JsonDocument.Parse(value);
        }
        catch (JsonException ex)
        {
            throw new HomeAssistantProtocolException(failureMessage, ex);
        }
    }

    /// <summary>Decodes a built-in Home Assistant response while preserving the classified protocol-failure contract.</summary>
    public static T DeserializeResponse<T>(
        JsonElement value,
        string failureMessage,
        bool allowNullCollectionEntries = false)
    {
        try
        {
            var result = value.Deserialize<T>(SerializerOptions)
                ?? throw new HomeAssistantProtocolException(failureMessage);
            return RequireNoNullCollectionEntries(result, failureMessage, allowNullCollectionEntries);
        }
        catch (JsonException ex)
        {
            throw new HomeAssistantProtocolException(failureMessage, ex);
        }
    }

    /// <summary>Rejects null entries in a built-in response collection, including nested collections.</summary>
    public static T RequireNoNullCollectionEntries<T>(
        T value,
        string failureMessage,
        bool allowNullCollectionEntries = false)
    {
        ValidateValue(value, typeof(T), failureMessage, allowNullCollection: false, allowNullEntries: allowNullCollectionEntries);
        return value;
    }

    private static void ValidateValue(
        object? value,
        Type declaredType,
        string failureMessage,
        bool allowNullCollection,
        bool allowNullEntries)
    {
        if (value is null)
        {
            if (!allowNullCollection && IsCollectionType(declaredType))
                throw new HomeAssistantProtocolException(failureMessage);
            return;
        }

        if (value is IDictionary dictionary)
        {
            var valueType = GetDictionaryValueType(declaredType) ?? typeof(object);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value is null && !allowNullEntries)
                    throw new HomeAssistantProtocolException(failureMessage);
                ValidateValue(entry.Value, valueType, failureMessage, allowNullCollection: allowNullEntries, allowNullEntries: false);
            }

            return;
        }

        if (value is not IEnumerable enumerable || value is string)
        {
            ValidateCollectionProperties(value, failureMessage);
            return;
        }

        var elementType = GetCollectionElementType(declaredType) ?? typeof(object);
        foreach (var item in enumerable)
        {
            if (item is null && !allowNullEntries)
                throw new HomeAssistantProtocolException(failureMessage);
            ValidateValue(item, elementType, failureMessage, allowNullCollection: allowNullEntries, allowNullEntries: false);
        }
    }

    private static void ValidateCollectionProperties(object value, string failureMessage)
    {
        var type = value.GetType();
        if (type.IsValueType || type.Namespace?.StartsWith("HomeAssistantX", StringComparison.Ordinal) != true)
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.SetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var propertyValue = property.GetValue(value);
            if (IsCollectionType(property.PropertyType))
            {
                var nullability = GetCollectionNullability(property);
                ValidateValue(
                    propertyValue,
                    property.PropertyType,
                    failureMessage,
                    nullability.Collection,
                    nullability.Entry);
            }
            else if (propertyValue is not null)
            {
                ValidateCollectionProperties(propertyValue, failureMessage);
            }
        }
    }

    private static (bool Collection, bool Entry) GetCollectionNullability(PropertyInfo property)
    {
        var flags = ReadNullableFlags(property);
        var context = ReadNullableContext(property);
        byte At(int index) => flags.Length == 1 ? flags[0] : index < flags.Length ? flags[index] : context;
        var entryIndex = GetDictionaryValueType(property.PropertyType) is null ? 1 : 2;
        return (At(0) == 2, At(entryIndex) == 2);
    }

    private static byte[] ReadNullableFlags(PropertyInfo property)
    {
        var attribute = CustomAttributeData.GetCustomAttributes(property).FirstOrDefault(value =>
            string.Equals(value.AttributeType.FullName, "System.Runtime.CompilerServices.NullableAttribute", StringComparison.Ordinal));
        if (attribute is null || attribute.ConstructorArguments.Count == 0) return Array.Empty<byte>();
        var argument = attribute.ConstructorArguments[0];
        if (argument.Value is byte single) return new[] { single };
        if (argument.Value is IEnumerable<CustomAttributeTypedArgument> values)
            return values.Select(value => Convert.ToByte(value.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return Array.Empty<byte>();
    }

    private static byte ReadNullableContext(PropertyInfo property)
    {
        for (MemberInfo? current = property; current is not null; current = current.DeclaringType)
        {
            var attribute = CustomAttributeData.GetCustomAttributes(current).FirstOrDefault(value =>
                string.Equals(value.AttributeType.FullName, "System.Runtime.CompilerServices.NullableContextAttribute", StringComparison.Ordinal));
            if (attribute is not null && attribute.ConstructorArguments.Count == 1
                && attribute.ConstructorArguments[0].Value is byte flag)
                return flag;
        }

        return 0;
    }

    private static bool IsCollectionType(Type type)
        => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(value =>
                value.IsGenericType && value.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }

    private static Type? GetDictionaryValueType(Type type)
    {
        var dictionary = type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
            ? type
            : type.GetInterfaces().FirstOrDefault(value => value.IsGenericType
                && (value.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                    || value.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
        return dictionary?.GetGenericArguments()[1];
    }
    private sealed class HomeAssistantDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String
                || !HomeAssistantTimestamp.TryParse(reader.GetString(), out var value))
            {
                throw new JsonException("A Home Assistant timestamp must use Z or an explicit UTC offset.");
            }

            return value;
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }
}
