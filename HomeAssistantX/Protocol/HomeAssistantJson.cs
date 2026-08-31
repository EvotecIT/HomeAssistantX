using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;
using System.Buffers;
using System.Reflection;
using System.Globalization;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;

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

    /// <summary>Parses caller-provided JSON without making cancellation wait for synchronous parser completion.</summary>
    internal static async Task<JsonDocument> ParseDocumentAsync(
        string value,
        CancellationToken cancellationToken,
        Func<string, JsonDocument>? parser = null)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        cancellationToken.ThrowIfCancellationRequested();
        parser ??= text => JsonDocument.Parse(text);
        var parseTask = Task.Run(() => parser(value), CancellationToken.None);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        if (await Task.WhenAny(parseTask, canceled.Task).ConfigureAwait(false) != parseTask)
        {
            _ = parseTask.ContinueWith(
                static task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion) task.Result.Dispose();
                    else _ = task.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var document = await parseTask.ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            document.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        return document;
    }

    /// <summary>Decodes a JSON string without making cancellation wait for synchronous unescaping.</summary>
    internal static async Task<string?> GetStringAsync(
        JsonElement value,
        CancellationToken cancellationToken,
        Func<JsonElement, string?>? decoder = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("A JSON string value is required.");

        decoder ??= static element => element.GetString();
        if (!cancellationToken.CanBeCanceled)
            return decoder(value);

        var decodeTask = Task.Factory.StartNew(
            () => decoder(value),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        if (await Task.WhenAny(decodeTask, canceled.Task).ConfigureAwait(false) != decodeTask)
        {
            _ = decodeTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var result = await decodeTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Decodes a JSON property name without making cancellation wait for synchronous unescaping.</summary>
    internal static async Task<string> GetPropertyNameAsync(
        JsonProperty property,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled) return property.Name;

        var decodeTask = Task.Factory.StartNew(
            () => property.Name,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        if (await Task.WhenAny(decodeTask, canceled.Task).ConfigureAwait(false) != decodeTask)
        {
            _ = decodeTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var result = await decodeTask.ConfigureAwait(false);
        ThrowIfStringTraversalCanceled(result, cancellationToken);
        return result;
    }

    /// <summary>Decodes a JSON string inside an already cancellation-isolated validation operation.</summary>
    internal static string? GetString(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("A JSON string value is required.");

        var result = value.GetString();
        ThrowIfStringTraversalCanceled(result, cancellationToken);
        return result;
    }

    /// <summary>Runs one synchronous response-validation operation on a dedicated worker while keeping caller cancellation prompt.</summary>
    internal static T RunCancellationIsolated<T>(
        Func<T> operation,
        CancellationToken cancellationToken)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));
        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled)
            return operation();

        var operationTask = Task.Factory.StartNew(
            operation,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        var completed = Task.WhenAny(operationTask, canceled.Task).ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ReferenceEquals(completed, operationTask))
        {
            _ = operationTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var result = operationTask.ConfigureAwait(false).GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Snapshots a response DOM with cancellation checks throughout traversal.</summary>
    internal static async Task<JsonElement> SnapshotResponseAsync(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        JsonDocument? document = null;
        var ownershipTransferred = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteJsonElement(writer, value, cancellationToken);
                writer.Flush();
            }

            cancellationToken.ThrowIfCancellationRequested();
            stream.Position = 0;
            document = await JsonDocument.ParseAsync(
                stream,
                default,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ownershipTransferred = true;
            return document.RootElement;
        }
        catch (JsonException ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new HomeAssistantProtocolException(failureMessage, ex);
        }
        finally
        {
            if (!ownershipTransferred) document?.Dispose();
        }
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
                ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
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

    internal static byte[] SerializeToUtf8Bytes(
        object? value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream();
        var options = CreateCancellationAwareOptions(cancellationToken);
        using var cancellationScope = HomeAssistantAttributeDictionaryConverter.UseCancellationToken(cancellationToken);
        JsonSerializer.SerializeAsync(stream, value, options, cancellationToken)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        return stream.ToArray();
    }

    internal static bool HasDuplicateProperties(
        JsonElement value,
        CancellationToken cancellationToken = default)
        => RunCancellationIsolated(
            () => HasDuplicatePropertiesCore(value, cancellationToken),
            cancellationToken);

    /// <summary>Checks a value inside an already isolated response traversal.</summary>
    internal static bool HasDuplicatePropertiesInline(
        JsonElement value,
        CancellationToken cancellationToken = default)
        => HasDuplicatePropertiesCore(value, cancellationToken);

    private static bool HasDuplicatePropertiesCore(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasDuplicatePropertiesCore(item, cancellationToken)) return true;
            }
            return false;
        }

        if (value.ValueKind != JsonValueKind.Object) return false;
        var names = new HashSet<string>(
            new CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
            if (!names.Add(property.Name) || HasDuplicatePropertiesCore(property.Value, cancellationToken)) return true;
        }
        return false;
    }

    internal static bool HasDuplicateObjectProperties(
        JsonElement value,
        CancellationToken cancellationToken = default)
        => RunCancellationIsolated(
            () => HasDuplicateObjectPropertiesCore(value, cancellationToken),
            cancellationToken);

    /// <summary>Checks one object inside an already isolated response traversal.</summary>
    internal static bool HasDuplicateObjectPropertiesInline(
        JsonElement value,
        CancellationToken cancellationToken = default)
        => HasDuplicateObjectPropertiesCore(value, cancellationToken);

    private static bool HasDuplicateObjectPropertiesCore(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.Object) return false;

        var names = new HashSet<string>(
            new CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
            if (!names.Add(property.Name)) return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private static JsonDocument SerializeSnapshot(object? value, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var options = CreateCancellationAwareOptions(cancellationToken);
        using var cancellationScope = HomeAssistantAttributeDictionaryConverter.UseCancellationToken(cancellationToken);
        JsonSerializer.SerializeAsync(stream, value, options, cancellationToken)
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

    internal static JsonSerializerOptions CreateCancellationAwareOptions(CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions(SerializerOptions);
        options.Converters.Insert(0, new CancellationAwareJsonDocumentConverter(cancellationToken));
        options.Converters.Insert(0, new CancellationAwareJsonElementConverter(cancellationToken));
        options.Converters.Insert(0, new HomeAssistantX.Rest.HomeAssistantCalendarBoundaryJsonConverter(cancellationToken));
        return options;
    }

    internal static JsonSerializerOptions CreateCancellationAwareResponseOptions(CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions(SerializerOptions);
        options.Converters.Insert(0, new CancellationAwareJsonElementConverter(cancellationToken));
        options.Converters.Insert(0, new CancellationAwareStringConverter(cancellationToken));
        options.Converters.Insert(0, new HomeAssistantX.Rest.HomeAssistantCalendarEventJsonConverter(cancellationToken));
        options.Converters.Insert(0, new HomeAssistantX.Rest.HomeAssistantCalendarBoundaryJsonConverter(cancellationToken));
        return options;
    }

    internal static void WriteJsonElement(
        Utf8JsonWriter writer,
        JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
                    writer.WritePropertyName(property.Name);
                    WriteJsonElement(writer, property.Value, cancellationToken);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WriteJsonElement(writer, item, cancellationToken);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                var stringValue = value.GetString();
                ThrowIfStringTraversalCanceled(stringValue, cancellationToken);
                writer.WriteStringValue(stringValue);
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException("Undefined JSON values cannot be snapshotted.");
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void ThrowIfStringTraversalCanceled(
        string? value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is not null)
        {
            for (var index = 0; index < value.Length; index += 64)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private sealed class CancellationAwareJsonElementConverter : JsonConverter<JsonElement>
    {
        private readonly CancellationToken _cancellationToken;

        internal CancellationAwareJsonElementConverter(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => HomeAssistantX.Rest.HomeAssistantCancellationJsonValueReader.Read(
                ref reader,
                _cancellationToken);

        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
            => WriteJsonElement(writer, value, _cancellationToken);
    }

    private sealed class CancellationAwareStringConverter : JsonConverter<string>
    {
        private readonly CancellationToken _cancellationToken;

        internal CancellationAwareStringConverter(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public override string? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
            => HomeAssistantX.Rest.HomeAssistantCancellationJsonValueReader.ReadString(
                ref reader,
                _cancellationToken);

        public override void Write(
            Utf8JsonWriter writer,
            string value,
            JsonSerializerOptions options)
        {
            ThrowIfStringTraversalCanceled(value, _cancellationToken);
            writer.WriteStringValue(value);
        }
    }

    private sealed class CancellationAwareJsonDocumentConverter : JsonConverter<JsonDocument>
    {
        private readonly CancellationToken _cancellationToken;

        internal CancellationAwareJsonDocumentConverter(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public override JsonDocument? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, JsonDocument value, JsonSerializerOptions options)
            => WriteJsonElement(writer, value.RootElement, _cancellationToken);
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
        bool allowNullCollectionEntries = false,
        CancellationToken cancellationToken = default)
    {
        return DeserializeResponseAsync<T>(
                value,
                failureMessage,
                cancellationToken,
                allowNullCollectionEntries)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>Decodes a built-in Home Assistant response while honoring cancellation during DOM traversal and typed projection.</summary>
    internal static async Task<T> DeserializeResponseAsync<T>(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken,
        bool allowNullCollectionEntries = false)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteJsonElement(writer, value, cancellationToken);
                writer.Flush();
            }

            cancellationToken.ThrowIfCancellationRequested();
            stream.Position = 0;
            T result;
            using (HomeAssistantAttributeDictionaryConverter.UseCancellationToken(cancellationToken))
            {
                var options = CreateCancellationAwareResponseOptions(cancellationToken);
                result = await JsonSerializer.DeserializeAsync<T>(
                    stream,
                    options,
                    cancellationToken).ConfigureAwait(false)
                    ?? throw new HomeAssistantProtocolException(failureMessage);
            }
            return RequireNoNullCollectionEntries(
                result,
                failureMessage,
                allowNullCollectionEntries,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new HomeAssistantProtocolException(failureMessage, ex);
        }
    }

    /// <summary>Decodes a complete DTO graph on one isolated worker when framework-owned member projection cannot poll cancellation.</summary>
    internal static T DeserializeResponseIsolated<T>(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken,
        bool allowNullCollectionEntries = false)
        => RunCancellationIsolated(
            () => DeserializeResponse<T>(
                value,
                failureMessage,
                allowNullCollectionEntries,
                CancellationToken.None),
            cancellationToken);

    /// <summary>Rejects null entries in a built-in response collection, including nested collections.</summary>
    public static T RequireNoNullCollectionEntries<T>(
        T value,
        string failureMessage,
        bool allowNullCollectionEntries = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateValue(
            value,
            typeof(T),
            failureMessage,
            allowNullCollection: false,
            allowNullEntries: allowNullCollectionEntries,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    private static void ValidateValue(
        object? value,
        Type declaredType,
        string failureMessage,
        bool allowNullCollection,
        bool allowNullEntries,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Value is null && !allowNullEntries)
                    throw new HomeAssistantProtocolException(failureMessage);
                ValidateValue(
                    entry.Value,
                    valueType,
                    failureMessage,
                    allowNullCollection: allowNullEntries,
                    allowNullEntries: false,
                    cancellationToken);
            }

            return;
        }

        if (value is not IEnumerable enumerable || value is string)
        {
            ValidateCollectionProperties(value, failureMessage, cancellationToken);
            return;
        }

        var elementType = GetCollectionElementType(declaredType) ?? typeof(object);
        foreach (var item in enumerable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item is null && !allowNullEntries)
                throw new HomeAssistantProtocolException(failureMessage);
            ValidateValue(
                item,
                elementType,
                failureMessage,
                allowNullCollection: allowNullEntries,
                allowNullEntries: false,
                cancellationToken);
        }
    }

    private static void ValidateCollectionProperties(
        object value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var type = value.GetType();
        if (type.IsValueType || type.Namespace?.StartsWith("HomeAssistantX", StringComparison.Ordinal) != true)
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                    nullability.Entry,
                    cancellationToken);
            }
            else if (propertyValue is not null)
            {
                ValidateCollectionProperties(propertyValue, failureMessage, cancellationToken);
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
