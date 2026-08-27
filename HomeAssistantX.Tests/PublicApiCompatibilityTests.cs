using System.Globalization;
using System.Reflection;
#if NET10_0
using System.Runtime.Loader;
#endif
using HomeAssistantX;

namespace HomeAssistantX.Tests;

public sealed class PublicApiCompatibilityTests
{
    [Fact]
    public void ParameterFormatterPreservesRefOutAndInDirections()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(ParameterDirectionFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(
            "ref System.Int32 byReference, out System.Int32 output, in System.Int32 input",
            FormatParameters(method.GetParameters()));
    }

    [Fact]
    public void MethodFormatterPreservesParamsAndGenericConstraints()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(GenericConstraintFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(
            "GenericConstraintFixture<TInput,TResult>(params TInput[] values) where TInput : class, System.IDisposable, new() where TResult : struct",
            FormatMethod(method));
    }

    [Fact]
    public void TypeFormatterPreservesEnumStorageAndGenericVariance()
    {
        Assert.Equal("System.UInt64", FormatEnumUnderlyingType(typeof(EnumStorageFixture)));
        Assert.Equal("flags enum", FormatTypeKind(typeof(FlagsFixture)));
        Assert.Contains("<out TResult>", FormatTypeDeclarationName(typeof(VariantFixture<>)), StringComparison.Ordinal);
    }

    [Fact]
    public void TypeAndFieldFormattersPreserveArrayRankAndFieldContracts()
    {
        Assert.Equal("System.String[,]", FormatType(typeof(string[,])));
        Assert.Equal("System.String[,,]?", FormatAnnotatedType(typeof(string[,,]), typeof(FieldFixture).GetField(nameof(FieldFixture.Mutable))!));
        Assert.Equal("F const System.Int32 Constant = 42", FormatField(typeof(FieldFixture).GetField(nameof(FieldFixture.Constant))!));
        Assert.Equal("F static readonly System.String ReadOnly", FormatField(typeof(FieldFixture).GetField(nameof(FieldFixture.ReadOnly))!));
        Assert.Equal("F instance System.String[,,]? Mutable", FormatField(typeof(FieldFixture).GetField(nameof(FieldFixture.Mutable))!));
    }

    [Fact]
    public void MemberFormatterPreservesNullableAndDispatchContracts()
    {
        var baseMethod = typeof(NullableDispatchFixture).GetMethod(nameof(NullableDispatchFixture.Transform))!;
        var overrideMethod = typeof(NullableDispatchOverrideFixture).GetMethod(nameof(NullableDispatchFixture.Transform))!;
        var property = typeof(NullableDispatchFixture).GetProperty(nameof(NullableDispatchFixture.Value))!;

        Assert.Equal("virtual", MemberScope(baseMethod));
        Assert.Equal("override", MemberScope(overrideMethod));
        Assert.Equal("System.String?", FormatAnnotatedType(baseMethod.ReturnType, baseMethod.ReturnParameter));
        Assert.Equal("System.String? value", FormatParameters(baseMethod.GetParameters()));
        Assert.Equal("System.String?", FormatAnnotatedType(property.PropertyType, property));
    }

    [Fact]
    public void MemberFormatterPreservesIndexerAndProtectedConstructorContracts()
    {
        var indexer = typeof(IndexerFixture).GetProperty("Item")!;
        var protectedConstructor = typeof(ProtectedConstructorFixture).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();

        Assert.Equal("[System.String key]", FormatIndexerParameters(indexer));
        Assert.Equal("protected ", ConstructorAccess(protectedConstructor));
    }

    [Fact]
    public void MemberFormatterPreservesProtectedInheritanceContracts()
    {
        var type = typeof(ProtectedSurfaceFixture);
        var field = type.GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var property = type.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        var method = type.GetMethod("Transform", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var eventInfo = type.GetEvent("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.True(IsExternallyAccessibleField(field));
        Assert.True(IsExternallyAccessibleProperty(property));
        Assert.True(IsExternallyAccessibleMethod(method));
        Assert.True(IsExternallyAccessibleEvent(eventInfo));
        Assert.StartsWith("F protected ", FormatField(field), StringComparison.Ordinal);
        Assert.Equal("protected ", MemberAccess(method));
        Assert.Equal("get;protected set;", FormatPropertyAccessors(property));
    }

#if NET10_0
    [Fact]
    public void PropertyFormatterPreservesInitOnlyAccessors()
    {
        var mutable = typeof(PropertyAccessorFixture).GetProperty(nameof(PropertyAccessorFixture.Mutable))!;
        var initOnly = typeof(PropertyAccessorFixture).GetProperty(nameof(PropertyAccessorFixture.InitOnly))!;

        Assert.Equal("get;set;", FormatPropertyAccessors(mutable));
        Assert.Equal("get;init;", FormatPropertyAccessors(initOnly));
    }
#endif

    [Fact]
    public void PublicApiMatchesTheReviewedCompatibilityBaseline()
    {
        var current = BuildSurface(typeof(HomeAssistantClient).Assembly);
        Assert.Contains("T sealed class HomeAssistantX.HomeAssistantClient : System.IDisposable", current, StringComparison.Ordinal);
        Assert.Contains("M static HomeAssistantX.HomeAssistantClient Create", current, StringComparison.Ordinal);
        Assert.Contains("T sealed class HomeAssistantX.Exceptions.HomeAssistantConnectionException : HomeAssistantX.Exceptions.HomeAssistantException", current, StringComparison.Ordinal);
        Assert.Contains("T interface HomeAssistantX.Subscriptions.IHomeAssistantSubscription : System.IDisposable", current, StringComparison.Ordinal);
        Assert.Contains("TryGetAttribute<T>(System.String name, out T? value)", current, StringComparison.Ordinal);
        var baselinePath = Path.Combine(AppContext.BaseDirectory, "Contracts", "HomeAssistantX.PublicApi.txt");
        if (string.Equals(Environment.GetEnvironmentVariable("HOMEASSISTANTX_UPDATE_API_BASELINE"), "1", StringComparison.Ordinal))
        {
#if NET10_0
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllText(baselinePath, current + Environment.NewLine);
            var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Contracts", "HomeAssistantX.PublicApi.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, current + Environment.NewLine);
#else
            throw new InvalidOperationException("The public API baseline can be updated only by the net10.0 compatibility test.");
#endif
        }

        Assert.True(File.Exists(baselinePath), "The public API compatibility baseline is missing.");
        var expected = File.ReadAllText(baselinePath).Replace("\r\n", "\n").TrimEnd();
        Assert.Equal(expected, current);
    }

#if NET10_0
    [Fact]
    public void NetStandardPublicApiMatchesTheRuntimeSurface()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var assemblyPath = Path.Combine(projectRoot, "HomeAssistantX", "bin", "Release", "netstandard2.0", "HomeAssistantX.dll");
        Assert.True(File.Exists(assemblyPath), "Build the netstandard2.0 target before running the compatibility suite.");
        var context = new AssemblyLoadContext("HomeAssistantX.PublicApi.netstandard2.0", isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            Assert.Equal(BuildSurface(typeof(HomeAssistantClient).Assembly), BuildSurface(assembly));
        }
        finally
        {
            context.Unload();
        }
    }
#endif

    private static string BuildSurface(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(FormatType, StringComparer.Ordinal))
        {
            var kind = FormatTypeKind(type);
            var contracts = new List<string>();
            if (type.IsEnum)
            {
                contracts.Add(FormatEnumUnderlyingType(type));
            }
            else if (type.BaseType is not null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
            {
                contracts.Add(FormatType(type.BaseType));
            }
            if (!type.IsEnum)
            {
                contracts.AddRange(GetDirectInterfaces(type).Select(FormatType).OrderBy(value => value, StringComparer.Ordinal));
            }
            var typeConstraints = FormatGenericConstraints(type.GetGenericArguments());
            lines.Add("T " + kind + " " + FormatTypeDeclarationName(type) + (contracts.Count == 0 ? string.Empty : " : " + string.Join(", ", contracts)) + typeConstraints);
            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                {
                    var value = Enum.Parse(type, name);
                    lines.Add("  F " + name + " = " + FormatEnumValue(value, Enum.GetUnderlyingType(type)));
                }
                continue;
            }

            foreach (var constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .Where(IsExternallyAccessibleConstructor)
                         .OrderBy(FormatMethod, StringComparer.Ordinal))
                lines.Add("  C " + ConstructorAccess(constructor) + FormatType(type) + "(" + FormatParameters(constructor.GetParameters()) + ")");
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(IsExternallyAccessibleField)
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
                lines.Add("  " + FormatField(field));
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(IsExternallyAccessibleProperty)
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                var accessor = MostAccessible(property.GetMethod, property.SetMethod)!;
                lines.Add("  P " + MemberAccess(accessor) + MemberScope(accessor) + " " + FormatAnnotatedType(property.PropertyType, property) + " " + property.Name + FormatIndexerParameters(property) + " {" + FormatPropertyAccessors(property) + "}");
            }
            foreach (var eventInfo in type.GetEvents(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(IsExternallyAccessibleEvent)
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                var accessor = MostAccessible(eventInfo.AddMethod, eventInfo.RemoveMethod)!;
                lines.Add("  E " + MemberAccess(accessor) + MemberScope(accessor) + " " + FormatAnnotatedType(eventInfo.EventHandlerType!, eventInfo) + " " + eventInfo.Name);
            }
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(value => !value.IsSpecialName && IsExternallyAccessibleMethod(value)).OrderBy(FormatMethod, StringComparer.Ordinal))
                lines.Add("  M " + MemberAccess(method) + MemberScope(method) + " " + FormatAnnotatedType(method.ReturnType, method.ReturnParameter) + " " + FormatMethod(method));
        }
        return string.Join("\n", lines);
    }

    private static IEnumerable<Type> GetDirectInterfaces(Type type)
    {
        var inherited = new HashSet<Type>();
        if (type.BaseType is not null)
        {
            inherited.UnionWith(type.BaseType.GetInterfaces());
        }

        foreach (var contract in type.GetInterfaces())
        {
            inherited.UnionWith(contract.GetInterfaces());
        }

        return type.GetInterfaces().Where(contract => !inherited.Contains(contract));
    }

    private static string FormatMethod(MethodBase method)
    {
        var genericArguments = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;
        var genericList = genericArguments.Length == 0
            ? string.Empty
            : "<" + string.Join(",", genericArguments.Select(argument => argument.Name)) + ">";
        return method.Name + genericList + "(" + FormatParameters(method.GetParameters()) + ")" + FormatGenericConstraints(genericArguments);
    }

    private static void ParameterDirectionFixture(ref int byReference, out int output, in int input)
    {
        output = byReference + input;
    }

    private static TResult GenericConstraintFixture<TInput, TResult>(params TInput[] values)
        where TInput : class, IDisposable, new()
        where TResult : struct
        => default;

    private enum EnumStorageFixture : ulong
    {
        Value = ulong.MaxValue
    }

    [Flags]
    private enum FlagsFixture
    {
        None = 0,
        One = 1
    }

    private class NullableDispatchFixture
    {
        public string? Value { get; set; }

        public virtual string? Transform(string? value) => value;
    }

    private sealed class NullableDispatchOverrideFixture : NullableDispatchFixture
    {
        public override string? Transform(string? value) => value;
    }

    private sealed class FieldFixture
    {
        public const int Constant = 42;
        public static readonly string ReadOnly = string.Empty;
        public string[,,]? Mutable = new string[1, 1, 1];
    }

    private sealed class IndexerFixture
    {
        public string this[string key] => key;
    }

    private class ProtectedConstructorFixture
    {
        protected ProtectedConstructorFixture(int value)
        {
        }
    }

    private class ProtectedSurfaceFixture
    {
        protected int Value = 1;

        public string Name { get; protected set; } = string.Empty;

#pragma warning disable CS0067
        protected event EventHandler? Changed;
#pragma warning restore CS0067

        protected virtual string Transform(string value) => value;
    }

    private interface VariantFixture<out TResult>
    {
    }

#if NET10_0
    private sealed class PropertyAccessorFixture
    {
        public string Mutable { get; set; } = string.Empty;

        public string InitOnly { get; init; } = string.Empty;
    }
#endif

    private static string MemberScope(MethodBase method)
    {
        if (method.IsStatic) return "static";
        if (method is MethodInfo methodInfo && methodInfo.IsVirtual)
        {
            if (methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType) return "override";
            if (!methodInfo.IsAbstract && !methodInfo.IsFinal) return "virtual";
        }
        return "instance";
    }

    private static string FormatField(FieldInfo field)
    {
        var scope = field.IsLiteral
            ? "const"
            : (field.IsStatic ? "static" : "instance") + (field.IsInitOnly ? " readonly" : string.Empty);
        var value = field.IsLiteral ? " = " + FormatDefault(field.GetRawConstantValue()) : string.Empty;
        return "F " + FieldAccess(field) + scope + " " + FormatAnnotatedType(field.FieldType, field) + " " + field.Name + value;
    }

    private static string FormatPropertyAccessors(PropertyInfo property)
    {
        var propertyAccess = MemberAccess(MostAccessible(property.GetMethod, property.SetMethod)!);
        var getter = FormatAccessor(property.GetMethod, propertyAccess, "get;");
        if (!IsExternallyAccessibleMethod(property.SetMethod)) return getter;

        var isInitOnly = property.SetMethod!.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => string.Equals(
                modifier.FullName,
                "System.Runtime.CompilerServices.IsExternalInit",
                StringComparison.Ordinal));
        return getter + FormatAccessor(property.SetMethod, propertyAccess, isInitOnly ? "init;" : "set;");
    }

    private static string FormatIndexerParameters(PropertyInfo property)
    {
        var parameters = property.GetIndexParameters();
        return parameters.Length == 0 ? string.Empty : "[" + FormatParameters(parameters) + "]";
    }

    private static bool IsExternallyAccessibleConstructor(ConstructorInfo constructor)
        => constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly;

    private static string ConstructorAccess(ConstructorInfo constructor)
        => constructor.IsPublic ? string.Empty : constructor.IsFamilyOrAssembly ? "protected internal " : "protected ";

    private static bool IsExternallyAccessibleMethod(MethodBase? method)
        => method is not null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);

    private static bool IsExternallyAccessibleField(FieldInfo field)
        => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    private static bool IsExternallyAccessibleProperty(PropertyInfo property)
        => IsExternallyAccessibleMethod(property.GetMethod) || IsExternallyAccessibleMethod(property.SetMethod);

    private static bool IsExternallyAccessibleEvent(EventInfo eventInfo)
        => IsExternallyAccessibleMethod(eventInfo.AddMethod) || IsExternallyAccessibleMethod(eventInfo.RemoveMethod);

    private static MethodInfo? MostAccessible(MethodInfo? first, MethodInfo? second)
    {
        if (first is null) return second;
        if (second is null) return first;
        return AccessRank(first) >= AccessRank(second) ? first : second;
    }

    private static int AccessRank(MethodBase method)
        => method.IsPublic ? 3 : method.IsFamilyOrAssembly ? 2 : method.IsFamily ? 1 : 0;

    private static string MemberAccess(MethodBase method)
        => method.IsPublic ? string.Empty : method.IsFamilyOrAssembly ? "protected internal " : "protected ";

    private static string FieldAccess(FieldInfo field)
        => field.IsPublic ? string.Empty : field.IsFamilyOrAssembly ? "protected internal " : "protected ";

    private static string FormatAccessor(MethodInfo? accessor, string propertyAccess, string text)
    {
        if (!IsExternallyAccessibleMethod(accessor)) return string.Empty;
        var accessorAccess = MemberAccess(accessor!);
        return (string.Equals(accessorAccess, propertyAccess, StringComparison.Ordinal) ? string.Empty : accessorAccess) + text;
    }

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters) => string.Join(", ", parameters.Select(parameter =>
    {
        var suffix = parameter.HasDefaultValue ? " = " + FormatDefault(parameter.DefaultValue) : string.Empty;
        return FormatParameterType(parameter) + " " + parameter.Name + suffix;
    }));

    private static string FormatParameterType(ParameterInfo parameter)
    {
        var paramsPrefix = parameter.GetCustomAttribute<ParamArrayAttribute>() is null ? string.Empty : "params ";
        if (!parameter.ParameterType.IsByRef)
        {
            return paramsPrefix + FormatAnnotatedType(parameter.ParameterType, parameter);
        }

        var direction = parameter.IsOut
            ? "out "
            : parameter.IsIn
                ? "in "
                : "ref ";
        return direction + FormatAnnotatedType(parameter.ParameterType.GetElementType()!, parameter);
    }

    private static string FormatTypeKind(Type type)
    {
        var kind = type.IsEnum
            ? (type.IsDefined(typeof(FlagsAttribute), inherit: false) ? "flags enum" : "enum")
            : type.IsInterface ? "interface" : type.IsValueType ? "struct" : type.IsAbstract && type.IsSealed ? "static class" : type.IsAbstract ? "abstract class" : type.IsSealed ? "sealed class" : "class";
        return kind;
    }

    private static string FormatEnumUnderlyingType(Type type) => FormatType(Enum.GetUnderlyingType(type));

    private static string FormatTypeDeclarationName(Type type)
    {
        if (!type.IsGenericTypeDefinition) return FormatType(type);
        var name = (type.FullName ?? type.Name).Split('`')[0];
        var arguments = type.GetGenericArguments().Select(argument =>
        {
            var variance = argument.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;
            var prefix = variance == GenericParameterAttributes.Covariant
                ? "out "
                : variance == GenericParameterAttributes.Contravariant
                    ? "in "
                    : string.Empty;
            return prefix + argument.Name;
        });
        return name + "<" + string.Join(",", arguments) + ">";
    }

    private static string FormatGenericConstraints(IEnumerable<Type> genericArguments)
    {
        var clauses = new List<string>();
        foreach (var argument in genericArguments.Where(argument => argument.IsGenericParameter))
        {
            var constraints = new List<string>();
            var attributes = argument.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;
            var unmanaged = argument.CustomAttributes.Any(attribute =>
                string.Equals(attribute.AttributeType.FullName, "System.Runtime.CompilerServices.IsUnmanagedAttribute", StringComparison.Ordinal));
            if (unmanaged)
            {
                constraints.Add("unmanaged");
            }
            else if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                constraints.Add("struct");
            }
            else if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                constraints.Add("class");
            }

            constraints.AddRange(argument.GetGenericParameterConstraints()
                .Where(constraint => constraint != typeof(ValueType))
                .Select(FormatType)
                .OrderBy(value => value, StringComparer.Ordinal));
            if (!unmanaged
                && (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0
                && (attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
            {
                clauses.Add(" where " + argument.Name + " : " + string.Join(", ", constraints));
            }
        }
        return string.Concat(clauses);
    }

    private static string FormatEnumValue(object value, Type underlyingType)
    {
        var unsigned = underlyingType == typeof(byte)
            || underlyingType == typeof(ushort)
            || underlyingType == typeof(uint)
            || underlyingType == typeof(ulong);
        return unsigned
            ? Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatDefault(object? value)
    {
        if (value is null) return "null";
        if (value is string text) return "\"" + text.Replace("\"", "\\\"") + "\"";
        if (value is bool boolean) return boolean ? "true" : "false";
        if (value.GetType().IsEnum) return FormatType(value.GetType()) + "." + value;
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
    }

    private static string FormatType(Type type)
    {
        if (type.IsByRef) return "ref " + FormatType(type.GetElementType()!);
        if (type.IsArray) return FormatType(type.GetElementType()!) + ArraySuffix(type);
        if (!type.IsGenericType) return type.FullName ?? type.Name;
        var definition = type.GetGenericTypeDefinition();
        var name = (definition.FullName ?? definition.Name).Split('`')[0];
        return name + "<" + string.Join(",", type.GetGenericArguments().Select(FormatType)) + ">";
    }

    private static string FormatAnnotatedType(Type type, ICustomAttributeProvider provider)
    {
        var flags = ReadNullableFlags(provider);
        var context = ReadNullableContext(provider);
        return FormatAnnotatedType(type, new NullabilityCursor(flags, context));
    }

    private static string FormatAnnotatedType(Type type, NullabilityCursor cursor)
    {
        var flag = cursor.Next();
        if (type.IsArray)
        {
            var array = FormatAnnotatedType(type.GetElementType()!, cursor) + ArraySuffix(type);
            return flag == 2 ? array + "?" : array;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var name = (definition.FullName ?? definition.Name).Split('`')[0];
            var formatted = name + "<" + string.Join(",", type.GetGenericArguments().Select(argument => FormatAnnotatedType(argument, cursor))) + ">";
            return !type.IsValueType && flag == 2 ? formatted + "?" : formatted;
        }

        var result = type.FullName ?? type.Name;
        return (!type.IsValueType || type.IsGenericParameter) && flag == 2 ? result + "?" : result;
    }

    private static string ArraySuffix(Type type) => "[" + new string(',', type.GetArrayRank() - 1) + "]";

    private static byte[] ReadNullableFlags(ICustomAttributeProvider provider)
    {
        var attribute = GetCustomAttributes(provider).FirstOrDefault(value =>
            string.Equals(value.AttributeType.FullName, "System.Runtime.CompilerServices.NullableAttribute", StringComparison.Ordinal));
        if (attribute is null || attribute.ConstructorArguments.Count == 0) return Array.Empty<byte>();
        var argument = attribute.ConstructorArguments[0];
        if (argument.Value is byte single) return new[] { single };
        if (argument.Value is IEnumerable<CustomAttributeTypedArgument> values)
            return values.Select(value => Convert.ToByte(value.Value, CultureInfo.InvariantCulture)).ToArray();
        return Array.Empty<byte>();
    }

    private static byte ReadNullableContext(ICustomAttributeProvider provider)
    {
        object? current = provider;
        while (current is not null)
        {
            var attribute = GetCustomAttributes((ICustomAttributeProvider)current).FirstOrDefault(value =>
                string.Equals(value.AttributeType.FullName, "System.Runtime.CompilerServices.NullableContextAttribute", StringComparison.Ordinal));
            if (attribute is not null && attribute.ConstructorArguments.Count == 1 && attribute.ConstructorArguments[0].Value is byte flag)
                return flag;

            current = current switch
            {
                ParameterInfo parameter => parameter.Member,
                MemberInfo member => member.DeclaringType,
                _ => null
            };
        }
        return 0;
    }

    private static IList<CustomAttributeData> GetCustomAttributes(ICustomAttributeProvider provider)
        => provider switch
        {
            ParameterInfo parameter => CustomAttributeData.GetCustomAttributes(parameter),
            MemberInfo member => CustomAttributeData.GetCustomAttributes(member),
            Assembly assembly => CustomAttributeData.GetCustomAttributes(assembly),
            Module module => CustomAttributeData.GetCustomAttributes(module),
            _ => Array.Empty<CustomAttributeData>()
        };

    private sealed class NullabilityCursor
    {
        private readonly byte[] _flags;
        private readonly byte _context;
        private int _index;

        public NullabilityCursor(byte[] flags, byte context)
        {
            _flags = flags;
            _context = context;
        }

        public byte Next()
        {
            if (_flags.Length == 1) return _flags[0];
            return _index < _flags.Length ? _flags[_index++] : _context;
        }
    }
}
