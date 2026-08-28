using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    public void MethodFormatterPreservesExtensionMethodStatus()
    {
        var method = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(value => value.Name == nameof(Enumerable.Select)
                && value.GetParameters().Length == 2
                && value.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2);

        Assert.StartsWith("extension Select", FormatMethod(method), StringComparison.Ordinal);
    }

    [Fact]
    public void TypeFormatterPreservesEnumStorageAndGenericVariance()
    {
        Assert.Equal("System.UInt64", FormatEnumUnderlyingType(typeof(EnumStorageFixture)));
        Assert.Equal("flags enum", FormatTypeKind(typeof(FlagsFixture)));
        Assert.Equal("ref struct", FormatTypeKind(typeof(RefStructFixture)));
        Assert.Equal("readonly struct", FormatTypeKind(typeof(ReadOnlyStructFixture)));
        Assert.Contains("<out TResult>", FormatTypeDeclarationName(typeof(VariantFixture<>)), StringComparison.Ordinal);
        Assert.EndsWith("+GenericOwner<TOuter>+Nested<TInner>", FormatTypeDeclarationName(typeof(GenericOwner<>.Nested<>)), StringComparison.Ordinal);
        Assert.EndsWith("+CollisionOwner<TOuter>+Nested<TInner>", FormatTypeDeclarationName(typeof(CollisionOwner<>.Nested<>)), StringComparison.Ordinal);
        Assert.EndsWith("+CollisionOwner<TFirst,TSecond>+Nested", FormatTypeDeclarationName(typeof(CollisionOwner<,>.Nested)), StringComparison.Ordinal);
        Assert.NotEqual(
            FormatTypeDeclarationName(typeof(CollisionOwner<>.Nested<>)),
            FormatTypeDeclarationName(typeof(CollisionOwner<,>.Nested)));
    }

    [Fact]
    public void TypeFormatterPreservesStructLayoutContracts()
    {
        Assert.Equal(
            "layout(Explicit,pack=4,size=16,charset=Unicode;fields=System.Int64 Zulu@0|System.Int32 Alpha@8) ",
            StructLayoutContract(typeof(StructLayoutFixture)));
        Assert.Equal(
            "layout(Sequential,pack=2,size=0,charset=Ansi;fields=System.Int32 Zulu|System.Int16 Alpha) ",
            StructLayoutContract(typeof(SequentialStructLayoutFixture)));
    }

#if NET10_0
    [Fact]
    public void TypeFormatterPreservesCollectionBuilderContracts()
    {
        Assert.Equal(
            "collection-builder(HomeAssistantX.Tests.PublicApiCompatibilityTests+CollectionBuilderFixtureBuilder,\"Create\") ",
            CollectionBuilderContract(typeof(CollectionBuilderFixture)));
    }
#endif

    [Fact]
    public void TypeAndFieldFormattersPreserveArrayRankAndFieldContracts()
    {
        Assert.Equal("System.String[,]", FormatType(typeof(string[,])));
        Assert.Equal("System.String[,,]?", FormatAnnotatedType(typeof(string[,,]), typeof(FieldFixture).GetField(nameof(FieldFixture.Mutable))!));
        Assert.Equal("F const System.Int32 Constant = 42", FormatField(typeof(FieldFixture).GetField(nameof(FieldFixture.Constant))!));
        Assert.Equal("F const System.Decimal DecimalConstant = 1.25", FormatField(typeof(FieldFixture).GetField(nameof(FieldFixture.DecimalConstant))!));
        Assert.Equal("F static readonly System.String ReadOnly", FormatField(typeof(FieldFixture).GetField(nameof(FieldFixture.ReadOnly))!));
        Assert.Equal("F instance System.String[,,]? Mutable", FormatField(typeof(FieldFixture).GetField(nameof(FieldFixture.Mutable))!));
        Assert.Equal("F instance volatile System.Int32 Volatile", FormatField(typeof(FieldFixture).GetField(nameof(FieldFixture.Volatile))!));
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

#if NET10_0
    [Fact]
    public void MemberFormatterPreservesNullableFlowContracts()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(NullableFlowFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("not-null-if-not-null(\"value\") System.String?", FormatReturnType(method));
        Assert.Equal(
            "System.Boolean result, not-null-when(true) System.String? value, does-not-return-if(false) System.Boolean assertion",
            FormatParameters(method.GetParameters()));

        var variants = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(NullableFlowVariantsFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(
            "allow-null System.String allowNull, disallow-null System.String? disallowNull, maybe-null System.String maybeNull, not-null System.String? notNull, maybe-null-when(false) System.String? maybeNullWhen",
            FormatParameters(variants.GetParameters()));
        Assert.StartsWith("does-not-return ", FormatMethod(typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(DoesNotReturnFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!), StringComparison.Ordinal);

        var memberState = typeof(NullableFlowMemberFixture).GetMethod(nameof(NullableFlowMemberFixture.Ensure))!;
        var memberStateWhen = typeof(NullableFlowMemberFixture).GetMethod(nameof(NullableFlowMemberFixture.TryEnsure))!;
        var property = typeof(NullableFlowMemberFixture).GetProperty(nameof(NullableFlowMemberFixture.Value))!;
        Assert.StartsWith(
            "member-not-null(\"_first\") member-not-null(\"_first\",\"_second\") ",
            FormatMethod(memberState),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "member-not-null-when(false,\"_first\",\"_second\") ",
            FormatMethod(memberStateWhen),
            StringComparison.Ordinal);
        Assert.Contains("member-not-null(\"_first\")", FormatProperty(property), StringComparison.Ordinal);
        Assert.Contains("get-flow(not-null)", FormatProperty(property), StringComparison.Ordinal);
        Assert.Contains("set-flow(disallow-null)", FormatProperty(property), StringComparison.Ordinal);
        Assert.Contains("get-member-flow(member-not-null(\"_first\"))", FormatProperty(
            typeof(NullableFlowMemberFixture).GetProperty(nameof(NullableFlowMemberFixture.AccessorState))!), StringComparison.Ordinal);
        Assert.DoesNotContain("set-flow", FormatProperty(
            typeof(NullableFlowMemberFixture).GetProperty(nameof(NullableFlowMemberFixture.PrivateSetter))!), StringComparison.Ordinal);
    }
#endif

    [Fact]
    public void ParameterFormatterPreservesMetadataOnlyOptionalParameters()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(MetadataOnlyOptionalFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("System.Int32 value [optional]", FormatParameters(method.GetParameters()));
    }

#if NET10_0
    [Fact]
    public void ParameterFormatterPreservesCallerInformationContracts()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(CallerInformationFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(
            "System.Boolean condition, caller-member-name System.String? member = null, caller-file-path System.String? file = null, caller-line-number System.Int32 line = 0, caller-argument-expression(\"condition\") System.String? expression = null",
            FormatParameters(method.GetParameters()));
    }
#endif

    [Fact]
    public void MethodFormatterPreservesConditionalCallSymbols()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(ConditionalCallFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.StartsWith("conditional(\"DEBUG\",\"TRACE\") ConditionalCallFixture", FormatMethod(method), StringComparison.Ordinal);
        Assert.Equal("conditional(\"DEBUG\",\"TRACE\") ", ConditionalContract(typeof(ConditionalAttributeFixture)));
    }

    [Fact]
    public void TypeFormatterPreservesDynamicMetadataAcrossNestedContracts()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            "DynamicMetadataFixture",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("dynamic", FormatReturnType(method));
        Assert.Equal(
            "dynamic value, System.Collections.Generic.IReadOnlyDictionary<System.String,dynamic[]> nested",
            FormatParameters(method.GetParameters()));
    }

    [Fact]
    public void MethodFormatterPreservesCompileBlockingObsoleteContracts()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            "CompileBlockingObsoleteFixture",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.StartsWith("error obsolete ", FormatMethod(method), StringComparison.Ordinal);
        Assert.Equal(
            "error obsolete ",
            ObsoleteContract(typeof(CompileBlockingObsoleteEnumFixture).GetField("Legacy")!));
        var property = typeof(CompileBlockingObsoleteAccessorFixture).GetProperty(nameof(CompileBlockingObsoleteAccessorFixture.Value))!;
        Assert.Equal("error obsolete ", ObsoleteContract(property, property.GetMethod, property.SetMethod));
        var implementationOnlySetter = typeof(CompileBlockingObsoleteAccessorFixture).GetProperty("ImplementationOnlySetter")!;
        Assert.Equal(
            string.Empty,
            ObsoleteContract(
                implementationOnlySetter,
                IsExternallyAccessibleMethod(implementationOnlySetter.GetMethod) ? implementationOnlySetter.GetMethod : null,
                IsExternallyAccessibleMethod(implementationOnlySetter.SetMethod) ? implementationOnlySetter.SetMethod : null));
        var eventInfo = typeof(CompileBlockingObsoleteAccessorFixture).GetEvent("Changed")!;
        Assert.Equal("error obsolete ", ObsoleteContract(eventInfo, eventInfo.AddMethod, eventInfo.RemoveMethod));
    }

    [Fact]
    public void MethodSelectionPreservesUserDefinedOperatorsAndExcludesAccessors()
    {
        var operatorMethod = typeof(OperatorFixture).GetMethod("op_Addition", BindingFlags.Public | BindingFlags.Static)!;
        var propertyGetter = typeof(OperatorFixture).GetProperty(nameof(OperatorFixture.Value))!.GetMethod!;

        Assert.True(ShouldIncludeMethod(operatorMethod));
        Assert.False(ShouldIncludeMethod(propertyGetter));
        Assert.StartsWith("op_Addition(", FormatMethod(operatorMethod), StringComparison.Ordinal);
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

    [Fact]
    public void TypeAndMemberFormattersPreserveProtectedNestingAndAbstractDispatch()
    {
        Assert.True(IsExternallyAccessibleType(PublicApiProtectedNestedFixture.NestedType));
        Assert.Equal("protected ", TypeAccess(PublicApiProtectedNestedFixture.NestedType));
        Assert.Equal("public ", TypeAccess(typeof(PublicApiProtectedNestedFixture.PublicNested)));
        Assert.Equal("protected internal ", TypeAccess(typeof(PublicApiProtectedNestedFixture).GetNestedType("ProtectedInternalNested", BindingFlags.NonPublic)!));
        Assert.Equal("abstract", MemberScope(typeof(AbstractSurfaceFixture).GetMethod("Transform", BindingFlags.Instance | BindingFlags.NonPublic)!));
        Assert.Equal("abstract override", MemberScope(typeof(AbstractOverrideFixture).GetMethod(nameof(NullableDispatchFixture.Transform))!));
        Assert.Equal("sealed override", MemberScope(typeof(SealedOverrideFixture).GetMethod(nameof(NullableDispatchFixture.Transform))!));
    }

    [Fact]
    public void TypeFormatterPreservesTupleElementNames()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(NamedTupleFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(
            "(System.String Host, System.Int32 Port)",
            FormatAnnotatedType(method.ReturnType, method.ReturnParameter));
        Assert.Equal(
            "(System.String Name, (System.Int32 Width, System.Int32 Height) Size) value",
            FormatParameters(method.GetParameters()));

        var wrapped = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(WrappedNamedTupleFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(
            "System.Threading.Tasks.Task<(System.String Host, System.Int32 Port)>",
            FormatAnnotatedType(wrapped.ReturnType, wrapped.ReturnParameter));

        var property = typeof(NamedTuplePropertyFixture).GetProperty(nameof(NamedTuplePropertyFixture.Endpoint))!;
        Assert.Equal(
            "(System.String Host, System.Int32 Port)",
            FormatAnnotatedType(property.PropertyType, property));
    }

    [Fact]
    public void TypeFormatterPreservesTupleElementNamesInBaseContracts()
    {
        var contracts = FormatInheritanceContracts(typeof(NamedTupleInheritanceFixture));

        Assert.True(
            contracts.Contains("HomeAssistantX.Tests.PublicApiCompatibilityTests+NamedTupleBase<(System.String Host, System.Int32 Port)>", StringComparer.Ordinal),
            string.Join(" | ", contracts));
    }

#if NET10_0
    [Fact]
    public void MemberFormatterPreservesStaticInterfaceDispatch()
    {
        var abstractMethod = typeof(StaticDispatchFixture).GetMethod(nameof(StaticDispatchFixture.Abstract))!;
        var virtualMethod = typeof(StaticDispatchFixture).GetMethod(nameof(StaticDispatchFixture.Virtual))!;

        Assert.Equal("static abstract", MemberScope(abstractMethod));
        Assert.Equal("static virtual", MemberScope(virtualMethod));
    }

    [Fact]
    public void MethodFormatterPreservesModernCallBindingMetadata()
    {
        var generalizedParams = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(GeneralizedParamsFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var prioritized = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(PrioritizedOverloadFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var prioritizedConstructor = typeof(PrioritizedMemberFixture).GetConstructors().Single();
        var prioritizedProperty = typeof(PrioritizedMemberFixture).GetProperty("Item")!;
        var handler = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(InterpolatedHandlerFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("params scoped System.ReadOnlySpan<System.Int32> values", FormatParameters(generalizedParams.GetParameters()));
        Assert.StartsWith("overload-priority(2) PrioritizedOverloadFixture", FormatMethod(prioritized), StringComparison.Ordinal);
        Assert.Contains("overload-priority(3) ", FormatConstructor(prioritizedConstructor), StringComparison.Ordinal);
        Assert.Contains("overload-priority(4) ", FormatProperty(prioritizedProperty), StringComparison.Ordinal);
        Assert.Contains("handler(\"context\") ref ", FormatParameters(handler.GetParameters()), StringComparison.Ordinal);
    }

    [Fact]
    public void GenericConstraintFormatterPreservesNullableContracts()
    {
        Assert.Equal(
            " where TRequired : class where TOptional : class? where TNotNull : notnull",
            FormatGenericConstraints(typeof(NullableConstraintFixture<,,,>).GetGenericArguments()));
        Assert.Equal(
            " where TBase : HomeAssistantX.Tests.PublicApiCompatibilityTests+NullableConstraintBase? where TContract : HomeAssistantX.Tests.PublicApiCompatibilityTests+NullableConstraintContract? where TMixed : HomeAssistantX.Tests.PublicApiCompatibilityTests+NullableConstraintBase?, HomeAssistantX.Tests.PublicApiCompatibilityTests+NullableConstraintContract?",
            FormatGenericConstraints(typeof(NullableTypeConstraintFixture<,,>).GetGenericArguments()));
    }

    [Fact]
    public void PropertyFormatterPreservesInitOnlyAccessors()
    {
        var mutable = typeof(PropertyAccessorFixture).GetProperty(nameof(PropertyAccessorFixture.Mutable))!;
        var initOnly = typeof(PropertyAccessorFixture).GetProperty(nameof(PropertyAccessorFixture.InitOnly))!;

        Assert.Equal("get;set;", FormatPropertyAccessors(mutable));
        Assert.Equal("get;init;", FormatPropertyAccessors(initOnly));
    }

    [Fact]
    public void PropertyFormatterPreservesRequiredMembers()
    {
        var required = typeof(PropertyAccessorFixture).GetProperty(nameof(PropertyAccessorFixture.Required))!;
        var mutable = typeof(PropertyAccessorFixture).GetProperty(nameof(PropertyAccessorFixture.Mutable))!;

        var requiredField = typeof(PropertyAccessorFixture).GetField(nameof(PropertyAccessorFixture.RequiredField))!;

        Assert.Equal("required ", RequiredMember(required));
        Assert.Equal("required ", RequiredMember(requiredField));
        Assert.Equal(string.Empty, RequiredMember(mutable));
    }

    [Fact]
    public void MemberFormatterPreservesScopedParametersAndReadonlyRefReturns()
    {
        var scoped = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(ScopedParameterFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var readOnlyReturn = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(RefReadonlyReturnFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var refReadonlyParameter = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(RefReadonlyParameterFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var writableProperty = typeof(PublicApiCompatibilityTests).GetProperty(
            nameof(WritableRefProperty),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var readOnlyProperty = typeof(PublicApiCompatibilityTests).GetProperty(
            nameof(ReadOnlyRefProperty),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("scoped ref System.Int32 value", FormatParameters(scoped.GetParameters()));
        Assert.Equal("ref readonly System.Int32 value", FormatParameters(refReadonlyParameter.GetParameters()));
        Assert.Equal("ref readonly System.Int32", FormatReturnType(readOnlyReturn));
        Assert.Equal("ref System.Int32", FormatPropertyType(writableProperty));
        Assert.Equal("ref readonly System.Int32", FormatPropertyType(readOnlyProperty));
        Assert.Equal(
            " where T : allows ref struct",
            FormatGenericConstraints(typeof(AllowsRefStructFixture<>).GetGenericArguments()));
    }

    [Fact]
    public void ConstructorFormatterPreservesRequiredMemberSatisfaction()
    {
        var constructors = typeof(RequiredConstructorFixture).GetConstructors();
        var satisfying = constructors.Single(constructor => constructor.GetParameters().Length == 0);
        var ordinary = constructors.Single(constructor => constructor.GetParameters().Length == 1);

        Assert.Equal("sets required ", RequiredMemberSatisfaction(satisfying));
        Assert.Equal(string.Empty, RequiredMemberSatisfaction(ordinary));
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
        foreach (var type in assembly.GetTypes().Where(IsExternallyAccessibleType).OrderBy(FormatType, StringComparer.Ordinal))
        {
            var kind = FormatTypeKind(type);
            var contracts = new List<string>();
            if (type.IsEnum)
            {
                contracts.Add(FormatEnumUnderlyingType(type));
            }
            else
            {
                contracts.AddRange(FormatInheritanceContracts(type));
            }
            var typeConstraints = FormatGenericConstraints(type.GetGenericArguments());
            lines.Add("T " + TypeAccess(type) + ObsoleteContract(type) + ConditionalContract(type) + CollectionBuilderContract(type) + StructLayoutContract(type) + kind + " " + FormatTypeDeclarationName(type) + (contracts.Count == 0 ? string.Empty : " : " + string.Join(", ", contracts)) + typeConstraints);
            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                {
                    var value = Enum.Parse(type, name);
                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)!;
                    lines.Add("  F " + ObsoleteContract(field) + name + " = " + FormatEnumValue(value, Enum.GetUnderlyingType(type)));
                }
                continue;
            }

            foreach (var constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .Where(IsExternallyAccessibleConstructor)
                         .OrderBy(FormatMethod, StringComparer.Ordinal))
                lines.Add("  " + FormatConstructor(constructor));
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(IsExternallyAccessibleField)
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
                lines.Add("  " + FormatField(field));
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(IsExternallyAccessibleProperty)
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                lines.Add("  " + FormatProperty(property));
            }
            foreach (var eventInfo in type.GetEvents(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(IsExternallyAccessibleEvent)
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                var accessor = MostAccessible(eventInfo.AddMethod, eventInfo.RemoveMethod)!;
                lines.Add("  E " + MemberAccess(accessor) + MemberScope(accessor) + " " + ObsoleteContract(eventInfo, eventInfo.AddMethod, eventInfo.RemoveMethod) + FormatAnnotatedType(eventInfo.EventHandlerType!, eventInfo) + " " + eventInfo.Name);
            }
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(ShouldIncludeMethod).OrderBy(FormatMethod, StringComparer.Ordinal))
                lines.Add("  M " + MemberAccess(method) + MemberScope(method) + " " + FormatReturnType(method) + " " + FormatMethod(method));
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

    private static string CollectionBuilderContract(Type type)
    {
        var attribute = GetCustomAttributes(type).FirstOrDefault(value => string.Equals(
            value.AttributeType.FullName,
            "System.Runtime.CompilerServices.CollectionBuilderAttribute",
            StringComparison.Ordinal));
        if (attribute is null
            || attribute.ConstructorArguments.Count != 2
            || attribute.ConstructorArguments[0].Value is not Type builderType
            || attribute.ConstructorArguments[1].Value is not string methodName)
        {
            return string.Empty;
        }

        return "collection-builder(" + FormatType(builderType) + "," + FormatDefault(methodName) + ") ";
    }

    private static string StructLayoutContract(Type type)
    {
        if (!type.IsValueType || type.IsEnum)
        {
            return string.Empty;
        }

        var layout = type.StructLayoutAttribute!;
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken)
            .Select(field =>
            {
                var offset = field.GetCustomAttribute<FieldOffsetAttribute>();
                return FormatType(field.FieldType) + " " + field.Name
                    + (offset is null ? string.Empty : "@" + offset.Value.ToString(CultureInfo.InvariantCulture));
            })
            .ToArray();
        return "layout(" + layout.Value
            + ",pack=" + layout.Pack.ToString(CultureInfo.InvariantCulture)
            + ",size=" + layout.Size.ToString(CultureInfo.InvariantCulture)
            + ",charset=" + layout.CharSet
            + ";fields=" + string.Join("|", fields) + ") ";
    }

    private static IReadOnlyList<string> FormatInheritanceContracts(Type type)
    {
        var contracts = new List<string>();
        var nullability = new NullabilityCursor(ReadNullableFlags(type), ReadNullableContext(type));
        var tupleNames = new TupleNameCursor(ReadTupleNames(type));
        var dynamicFlags = new DynamicCursor(ReadDynamicFlags(type));
        if (type.BaseType is not null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            contracts.Add(FormatAnnotatedType(type.BaseType, nullability, tupleNames, dynamicFlags));
        }

        foreach (var contract in GetDirectInterfaces(type))
        {
            contracts.Add(FormatAnnotatedType(contract, nullability, tupleNames, dynamicFlags));
        }

        return contracts.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string FormatMethod(MethodBase method)
    {
        var genericArguments = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;
        var genericList = genericArguments.Length == 0
            ? string.Empty
            : "<" + string.Join(",", genericArguments.Select(argument => argument.Name)) + ">";
        var extension = method.IsDefined(typeof(ExtensionAttribute), inherit: false) ? "extension " : string.Empty;
        return ObsoleteContract(method) + OverloadResolutionPriorityContract(method) + ConditionalContract(method) + MethodFlowContract(method) + extension + method.Name + genericList + "(" + FormatParameters(method.GetParameters()) + ")" + FormatGenericConstraints(genericArguments);
    }

    private static string FormatConstructor(ConstructorInfo constructor)
        => "C " + ConstructorAccess(constructor) + ObsoleteContract(constructor)
            + OverloadResolutionPriorityContract(constructor) + MethodFlowContract(constructor) + RequiredMemberSatisfaction(constructor)
            + FormatType(constructor.DeclaringType!) + "(" + FormatParameters(constructor.GetParameters()) + ")";

    private static string FormatProperty(PropertyInfo property)
    {
        var accessor = MostAccessible(property.GetMethod, property.SetMethod)!;
        var getter = IsExternallyAccessibleMethod(property.GetMethod) ? property.GetMethod : null;
        var setter = IsExternallyAccessibleMethod(property.SetMethod) ? property.SetMethod : null;
        return "P " + MemberAccess(accessor) + MemberScope(accessor) + " " + ObsoleteContract(
            property,
            getter,
            setter)
            + OverloadResolutionPriorityContract(property) + MethodFlowContract(property)
            + NamedMethodFlowContract("get", getter) + NamedMethodFlowContract("set", setter)
            + RequiredMember(property)
            + FormatPropertyType(property) + " " + property.Name + FormatIndexerParameters(property)
            + " {" + FormatPropertyAccessors(property) + "}";
    }

    private static void ParameterDirectionFixture(ref int byReference, out int output, in int input)
    {
        output = byReference + input;
    }

    private static TResult GenericConstraintFixture<TInput, TResult>(params TInput[] values)
        where TInput : class, IDisposable, new()
        where TResult : struct
        => default;

    private static (string Host, int Port) NamedTupleFixture(
        (string Name, (int Width, int Height) Size) value)
        => (value.Name, value.Size.Width);

    private static Task<(string Host, int Port)> WrappedNamedTupleFixture()
        => Task.FromResult(("localhost", 8123));

    private static dynamic DynamicMetadataFixture(
        dynamic value,
        IReadOnlyDictionary<string, dynamic[]> nested)
        => value;

#if NET10_0
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(value))]
    private static string? NullableFlowFixture(
        bool result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? value,
        [System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool assertion)
        => result ? value : null;

    private static void NullableFlowVariantsFixture(
        [System.Diagnostics.CodeAnalysis.AllowNull] string allowNull,
        [System.Diagnostics.CodeAnalysis.DisallowNull] string? disallowNull,
        [System.Diagnostics.CodeAnalysis.MaybeNull] string maybeNull,
        [System.Diagnostics.CodeAnalysis.NotNull] string? notNull,
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] string? maybeNullWhen)
    {
        notNull ??= string.Empty;
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void DoesNotReturnFixture() => throw new InvalidOperationException();

    private sealed class NullableFlowMemberFixture
    {
        private string? _first;
        private string? _second;

        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_first))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_second), nameof(_first))]
        public void Ensure()
        {
            _first = string.Empty;
            _second = string.Empty;
        }

        [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(false, nameof(_second), nameof(_first))]
        public bool TryEnsure() => true;

        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_first))]
        public string? Value
        {
            [return: System.Diagnostics.CodeAnalysis.NotNull]
            get => _first ??= string.Empty;
            [param: System.Diagnostics.CodeAnalysis.DisallowNull]
            set => _first = value ?? string.Empty;
        }


        public string AccessorState
        {
            [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_first))]
            get => _first ??= string.Empty;
        }

        public string? PrivateSetter
        {
            get => _second;
            [param: System.Diagnostics.CodeAnalysis.DisallowNull]
            private set => _second = value;
        }
    }
#endif

    private static void MetadataOnlyOptionalFixture(
        [System.Runtime.InteropServices.Optional] int value)
    {
    }

#if NET10_0
    private static void CallerInformationFixture(
        bool condition,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
    }
#endif

    [System.Diagnostics.Conditional("DEBUG")]
    [System.Diagnostics.Conditional("TRACE")]
    private static void ConditionalCallFixture()
    {
    }

    [System.Diagnostics.Conditional("TRACE")]
    [System.Diagnostics.Conditional("DEBUG")]
    private sealed class ConditionalAttributeFixture : Attribute
    {
    }

    [Obsolete("This fixture must remain a compile-time error.", true)]
    private static void CompileBlockingObsoleteFixture()
    {
    }

    private enum CompileBlockingObsoleteEnumFixture
    {
        Current,
        [Obsolete("This fixture must remain a compile-time error.", true)]
        Legacy
    }

    private sealed class CompileBlockingObsoleteAccessorFixture
    {
        public int Value
        {
            [Obsolete("This getter must remain a compile-time error.", true)]
            get;
            set;
        }

        public int ImplementationOnlySetter
        {
            get;
            [Obsolete("This private setter is not part of the external contract.", true)]
            private set;
        }

        [Obsolete("This event must remain a compile-time error.", true)]
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

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

    private abstract class AbstractSurfaceFixture
    {
        protected abstract string Transform(string value);
    }

    private abstract class AbstractOverrideFixture : NullableDispatchFixture
    {
        public abstract override string? Transform(string? value);
    }

    private sealed class SealedOverrideFixture : NullableDispatchFixture
    {
        public sealed override string? Transform(string? value) => value;
    }

    private sealed class FieldFixture
    {
        public const int Constant = 42;
        public const decimal DecimalConstant = 1.25m;
        public static readonly string ReadOnly = string.Empty;
        public string[,,]? Mutable = new string[1, 1, 1];
        public volatile int Volatile = 1;
    }

    private sealed class IndexerFixture
    {
        public string this[string key] => key;
    }

    private readonly struct OperatorFixture
    {
        public OperatorFixture(int value) => Value = value;

        public int Value { get; }

        public static OperatorFixture operator +(OperatorFixture left, OperatorFixture right)
            => new(left.Value + right.Value);
    }

    private ref struct RefStructFixture
    {
    }

    private readonly struct ReadOnlyStructFixture
    {
    }

    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 16, CharSet = CharSet.Unicode)]
    private struct StructLayoutFixture
    {
        [FieldOffset(0)]
        public long Zulu;

        [FieldOffset(8)]
        public int Alpha;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct SequentialStructLayoutFixture
    {
        public int Zulu;

        public short Alpha;
    }

    private sealed class GenericOwner<TOuter>
    {
        public sealed class Nested<TInner>
        {
        }
    }

    private sealed class CollisionOwner<TOuter>
    {
        public sealed class Nested<TInner>
        {
        }
    }

    private sealed class CollisionOwner<TFirst, TSecond>
    {
        public sealed class Nested
        {
        }
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
    private static void GeneralizedParamsFixture(params ReadOnlySpan<int> values)
    {
    }

    [OverloadResolutionPriority(2)]
    private static void PrioritizedOverloadFixture(int value)
    {
    }

    private sealed class PrioritizedMemberFixture
    {
        [OverloadResolutionPriority(3)]
        public PrioritizedMemberFixture(int value)
        {
        }

        [OverloadResolutionPriority(4)]
        public int this[int index] => index;
    }

    private static void InterpolatedHandlerFixture(
        int context,
        [InterpolatedStringHandlerArgument(nameof(context))] ref ApiBaselineInterpolatedStringHandler handler)
    {
    }

    [InterpolatedStringHandler]
    private ref struct ApiBaselineInterpolatedStringHandler
    {
        public ApiBaselineInterpolatedStringHandler(int literalLength, int formattedCount, int context)
        {
        }

        public void AppendLiteral(string value)
        {
        }

        public void AppendFormatted<T>(T value)
        {
        }
    }

    private interface StaticDispatchFixture
    {
        static abstract int Abstract();

        static virtual int Virtual() => 1;
    }

    private sealed class NullableConstraintFixture<TRequired, TOptional, TNotNull, TUnconstrained>
        where TRequired : class
        where TOptional : class?
        where TNotNull : notnull
    {
    }

    private class NullableConstraintBase
    {
    }

    private interface NullableConstraintContract
    {
    }

    private sealed class NullableTypeConstraintFixture<TBase, TContract, TMixed>
        where TBase : NullableConstraintBase?
        where TContract : NullableConstraintContract?
        where TMixed : NullableConstraintBase?, NullableConstraintContract?
    {
    }

    private sealed class PropertyAccessorFixture
    {
        public string Mutable { get; set; } = string.Empty;

        public string InitOnly { get; init; } = string.Empty;

        public required string Required { get; set; }

        public required string RequiredField = string.Empty;
    }

    private sealed class RequiredConstructorFixture
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public RequiredConstructorFixture()
        {
            Value = string.Empty;
        }

        public RequiredConstructorFixture(string value)
        {
            Value = value;
        }

        public required string Value { get; set; }
    }

    private static int RefReadonlyStorage;

    private static void ScopedParameterFixture(scoped ref int value)
    {
        value++;
    }

    private static ref readonly int RefReadonlyReturnFixture() => ref RefReadonlyStorage;

    private static void RefReadonlyParameterFixture(ref readonly int value)
    {
        _ = value;
    }

    private static ref int WritableRefProperty => ref RefReadonlyStorage;

    private static ref readonly int ReadOnlyRefProperty => ref RefReadonlyStorage;

    private sealed class AllowsRefStructFixture<T> where T : allows ref struct
    {
    }
#endif

    private static string MemberScope(MethodBase method)
    {
        if (method.IsStatic)
        {
            if (method is MethodInfo staticMethod)
            {
                if (staticMethod.IsAbstract) return "static abstract";
                if (staticMethod.IsVirtual) return "static virtual";
            }
            return "static";
        }
        if (method is MethodInfo methodInfo && methodInfo.IsVirtual)
        {
            var isOverride = methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
            if (methodInfo.IsAbstract) return isOverride ? "abstract override" : "abstract";
            if (isOverride) return methodInfo.IsFinal ? "sealed override" : "override";
            if (!methodInfo.IsFinal) return "virtual";
        }
        return "instance";
    }

    private static bool IsExternallyAccessibleType(Type type)
    {
        if (!type.IsNested) return type.IsPublic;
        return (type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem)
            && type.DeclaringType is not null
            && IsExternallyAccessibleType(type.DeclaringType);
    }

    private sealed class NamedTuplePropertyFixture
    {
        public (string Host, int Port) Endpoint { get; set; }
    }

    private class NamedTupleBase<T>
    {
    }

    private sealed class NamedTupleInheritanceFixture :
        NamedTupleBase<(string Host, int Port)>
    {
    }

    private static string TypeAccess(Type type)
    {
        if (!type.IsNested) return string.Empty;
        if (type.IsNestedPublic) return "public ";
        if (type.IsNestedFamORAssem) return "protected internal ";
        if (type.IsNestedFamily) return "protected ";
        throw new InvalidOperationException("The type is not externally accessible.");
    }

    private static string FormatField(FieldInfo field)
    {
        var decimalConstant = field.GetCustomAttribute<DecimalConstantAttribute>();
        var isConstant = field.IsLiteral || decimalConstant is not null;
        var volatileContract = field.GetRequiredCustomModifiers().Any(modifier => string.Equals(
            modifier.FullName,
            "System.Runtime.CompilerServices.IsVolatile",
            StringComparison.Ordinal)) ? " volatile" : string.Empty;
        var scope = isConstant
            ? "const"
            : (field.IsStatic ? "static" : "instance") + volatileContract + (field.IsInitOnly ? " readonly" : string.Empty);
        var constantValue = field.IsLiteral ? field.GetRawConstantValue() : decimalConstant?.Value;
        var value = isConstant ? " = " + FormatDefault(constantValue) : string.Empty;
        return "F " + FieldAccess(field) + scope + " " + ObsoleteContract(field) + RequiredMember(field) + NullableFlowContract(field) + FormatAnnotatedType(field.FieldType, field) + " " + field.Name + value;
    }

    private static string ObsoleteContract(params ICustomAttributeProvider?[] providers)
    {
        foreach (var provider in providers.Where(value => value is not null))
        {
            var attribute = GetCustomAttributes(provider!).FirstOrDefault(value =>
                string.Equals(value.AttributeType.FullName, typeof(ObsoleteAttribute).FullName, StringComparison.Ordinal));
            var isError = attribute is not null
                && attribute.ConstructorArguments.Count > 1
                && attribute.ConstructorArguments[1].Value is bool value
                && value;
            if (isError) return "error obsolete ";
        }
        return string.Empty;
    }

    private static string OverloadResolutionPriorityContract(ICustomAttributeProvider provider)
    {
        var attribute = GetCustomAttributes(provider).FirstOrDefault(value => string.Equals(
            value.AttributeType.FullName,
            "System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute",
            StringComparison.Ordinal));
        return attribute?.ConstructorArguments.Count == 1
            && attribute.ConstructorArguments[0].Value is int priority
                ? "overload-priority(" + priority.ToString(CultureInfo.InvariantCulture) + ") "
                : string.Empty;
    }

    private static string ConditionalContract(ICustomAttributeProvider provider)
    {
        var symbols = GetCustomAttributes(provider)
            .Where(value => string.Equals(
                value.AttributeType.FullName,
                typeof(System.Diagnostics.ConditionalAttribute).FullName,
                StringComparison.Ordinal))
            .Select(value => value.ConstructorArguments.Count == 1 ? value.ConstructorArguments[0].Value as string : null)
            .Where(value => !string.IsNullOrEmpty(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(FormatDefault)
            .ToArray();
        return symbols.Length == 0 ? string.Empty : "conditional(" + string.Join(",", symbols) + ") ";
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

    private static string RequiredMember(MemberInfo member)
        => member.CustomAttributes.Any(attribute => string.Equals(
            attribute.AttributeType.FullName,
            "System.Runtime.CompilerServices.RequiredMemberAttribute",
            StringComparison.Ordinal))
            ? "required "
            : string.Empty;

    private static string RequiredMemberSatisfaction(ConstructorInfo constructor)
        => constructor.CustomAttributes.Any(attribute => string.Equals(
            attribute.AttributeType.FullName,
            "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute",
            StringComparison.Ordinal))
            ? "sets required "
            : string.Empty;

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

    private static bool ShouldIncludeMethod(MethodInfo method)
        => IsExternallyAccessibleMethod(method)
            && (!method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal));

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
        var suffix = parameter.HasDefaultValue
            ? " = " + FormatDefault(parameter.DefaultValue)
            : parameter.IsOptional ? " [optional]" : string.Empty;
        return NullableFlowContract(parameter) + FormatParameterType(parameter) + " " + parameter.Name + suffix;
    }));

    private static string FormatParameterType(ParameterInfo parameter)
    {
        var paramsPrefix = parameter.GetCustomAttribute<ParamArrayAttribute>() is not null
            || HasAttribute(parameter, "System.Runtime.CompilerServices.ParamCollectionAttribute")
                ? "params "
                : string.Empty;
        var callerPrefix = CallerInformationContract(parameter);
        var handlerPrefix = InterpolatedStringHandlerArguments(parameter);
        var safetyPrefix = RefSafetyPrefix(parameter);
        if (!parameter.ParameterType.IsByRef)
        {
            return paramsPrefix + callerPrefix + handlerPrefix + safetyPrefix + FormatAnnotatedType(parameter.ParameterType, parameter);
        }

        var direction = HasAttribute(parameter, "System.Runtime.CompilerServices.RequiresLocationAttribute")
            ? "ref readonly "
            : parameter.IsOut
            ? "out "
            : parameter.IsIn
                ? "in "
                : "ref ";
        return paramsPrefix + callerPrefix + handlerPrefix + safetyPrefix + direction + FormatAnnotatedType(parameter.ParameterType.GetElementType()!, parameter);
    }

    private static string CallerInformationContract(ParameterInfo parameter)
    {
        var contracts = new List<string>();
        foreach (var attribute in GetCustomAttributes(parameter))
        {
            var name = attribute.AttributeType.FullName;
            if (string.Equals(name, "System.Runtime.CompilerServices.CallerMemberNameAttribute", StringComparison.Ordinal)) contracts.Add("caller-member-name");
            else if (string.Equals(name, "System.Runtime.CompilerServices.CallerFilePathAttribute", StringComparison.Ordinal)) contracts.Add("caller-file-path");
            else if (string.Equals(name, "System.Runtime.CompilerServices.CallerLineNumberAttribute", StringComparison.Ordinal)) contracts.Add("caller-line-number");
            if (string.Equals(name, "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute", StringComparison.Ordinal)
                && attribute.ConstructorArguments.Count == 1
                && attribute.ConstructorArguments[0].Value is string referencedParameter)
                contracts.Add("caller-argument-expression(" + FormatDefault(referencedParameter) + ")");
        }
        return contracts.Count == 0
            ? string.Empty
            : string.Join(" ", contracts.OrderBy(value => value, StringComparer.Ordinal)) + " ";
    }

    private static string InterpolatedStringHandlerArguments(ParameterInfo parameter)
    {
        var attribute = GetCustomAttributes(parameter).FirstOrDefault(value => string.Equals(
            value.AttributeType.FullName,
            "System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute",
            StringComparison.Ordinal));
        if (attribute is null || attribute.ConstructorArguments.Count != 1) return string.Empty;

        var argument = attribute.ConstructorArguments[0];
        if (argument.Value is IEnumerable<CustomAttributeTypedArgument> values)
        {
            return "handler(" + string.Join(",", values.Select(value => FormatDefault(value.Value))) + ") ";
        }
        return argument.Value is string value
            ? "handler(" + FormatDefault(value) + ") "
            : string.Empty;
    }

    private static string FormatReturnType(MethodInfo method)
        => FormatReturnType(method, method);

    private static string FormatReturnType(MethodInfo method, ICustomAttributeProvider owner)
    {
        var parameter = method.ReturnParameter;
        var safetyPrefix = NullableFlowContract(parameter) + RefSafetyPrefix(parameter)
            + (HasAttribute(owner, "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute")
                || HasAttribute(method, "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute") ? "unscoped " : string.Empty);
        if (!method.ReturnType.IsByRef)
            return safetyPrefix + FormatAnnotatedType(method.ReturnType, parameter);

        var readOnly = HasAttribute(parameter, "System.Runtime.CompilerServices.IsReadOnlyAttribute")
            || parameter.GetRequiredCustomModifiers().Any(modifier => string.Equals(
                modifier.FullName,
                "System.Runtime.InteropServices.InAttribute",
                StringComparison.Ordinal));
        return safetyPrefix + (readOnly ? "ref readonly " : "ref ")
            + FormatAnnotatedType(method.ReturnType.GetElementType()!, parameter);
    }

    private static string MethodFlowContract(ICustomAttributeProvider provider)
    {
        var contracts = new List<string>();
        foreach (var attribute in GetCustomAttributes(provider))
        {
            var name = attribute.AttributeType.FullName;
            if (string.Equals(name, "System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute", StringComparison.Ordinal))
            {
                contracts.Add("does-not-return");
                continue;
            }

            if (string.Equals(name, "System.Diagnostics.CodeAnalysis.MemberNotNullAttribute", StringComparison.Ordinal)
                && TryGetMemberNames(attribute, 0, out var members))
            {
                contracts.Add("member-not-null(" + string.Join(",", members.Select(FormatDefault)) + ")");
                continue;
            }

            if (string.Equals(name, "System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute", StringComparison.Ordinal)
                && attribute.ConstructorArguments.Count >= 2
                && attribute.ConstructorArguments[0].Value is bool condition
                && TryGetMemberNames(attribute, 1, out members))
            {
                contracts.Add("member-not-null-when(" + FormatBoolean(condition) + "," + string.Join(",", members.Select(FormatDefault)) + ")");
            }
        }

        return contracts.Count == 0
            ? string.Empty
            : string.Join(" ", contracts.OrderBy(value => value, StringComparer.Ordinal)) + " ";
    }

    private static bool TryGetMemberNames(CustomAttributeData attribute, int argumentIndex, out IReadOnlyList<string> members)
    {
        members = Array.Empty<string>();
        if (argumentIndex >= attribute.ConstructorArguments.Count) return false;
        var value = attribute.ConstructorArguments[argumentIndex].Value;
        if (value is string member)
        {
            members = new[] { member };
            return true;
        }
        if (value is not IEnumerable<CustomAttributeTypedArgument> values) return false;
        var collected = values.Select(item => item.Value as string).ToArray();
        if (collected.Any(string.IsNullOrEmpty)) return false;
        members = collected.Cast<string>().OrderBy(item => item, StringComparer.Ordinal).ToArray();
        return true;
    }

    private static string NullableFlowContract(ICustomAttributeProvider provider)
    {
        var contracts = new List<string>();
        foreach (var attribute in GetCustomAttributes(provider))
        {
            var name = attribute.AttributeType.FullName;
            if (string.Equals(name, "System.Diagnostics.CodeAnalysis.AllowNullAttribute", StringComparison.Ordinal)) contracts.Add("allow-null");
            else if (string.Equals(name, "System.Diagnostics.CodeAnalysis.DisallowNullAttribute", StringComparison.Ordinal)) contracts.Add("disallow-null");
            else if (string.Equals(name, "System.Diagnostics.CodeAnalysis.MaybeNullAttribute", StringComparison.Ordinal)) contracts.Add("maybe-null");
            else if (string.Equals(name, "System.Diagnostics.CodeAnalysis.NotNullAttribute", StringComparison.Ordinal)) contracts.Add("not-null");
            else if (string.Equals(name, "System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute", StringComparison.Ordinal)
                && TryGetBooleanArgument(attribute, out var maybeNullWhen)) contracts.Add("maybe-null-when(" + FormatBoolean(maybeNullWhen) + ")");
            else if (string.Equals(name, "System.Diagnostics.CodeAnalysis.NotNullWhenAttribute", StringComparison.Ordinal)
                && TryGetBooleanArgument(attribute, out var notNullWhen)) contracts.Add("not-null-when(" + FormatBoolean(notNullWhen) + ")");
            else if (string.Equals(name, "System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute", StringComparison.Ordinal)
                && attribute.ConstructorArguments.Count == 1
                && attribute.ConstructorArguments[0].Value is string parameterName)
                contracts.Add("not-null-if-not-null(" + FormatDefault(parameterName) + ")");
            else if (string.Equals(name, "System.Diagnostics.CodeAnalysis.DoesNotReturnIfAttribute", StringComparison.Ordinal)
                && TryGetBooleanArgument(attribute, out var doesNotReturnIf)) contracts.Add("does-not-return-if(" + FormatBoolean(doesNotReturnIf) + ")");
        }

        return contracts.Count == 0
            ? string.Empty
            : string.Join(" ", contracts.OrderBy(value => value, StringComparer.Ordinal)) + " ";
    }

    private static bool TryGetBooleanArgument(CustomAttributeData attribute, out bool value)
    {
        value = false;
        if (attribute.ConstructorArguments.Count != 1 || attribute.ConstructorArguments[0].Value is not bool argument) return false;
        value = argument;
        return true;
    }

    private static string FormatBoolean(bool value) => value ? "true" : "false";

    private static string FormatPropertyType(PropertyInfo property)
    {
        var propertyFlow = NullableFlowContract(property);
        var getter = IsExternallyAccessibleMethod(property.GetMethod) ? property.GetMethod : null;
        var setter = IsExternallyAccessibleMethod(property.SetMethod) ? property.SetMethod : null;
        var getterFlow = getter is null
            ? string.Empty
            : NamedFlowContract("get", getter.ReturnParameter);
        var setterValue = setter?.GetParameters().LastOrDefault();
        var setterFlow = setterValue is null ? string.Empty : NamedFlowContract("set", setterValue);
        if (property.PropertyType.IsByRef && property.GetMethod is not null)
        {
            if (getter is not null) return propertyFlow + setterFlow + FormatReturnType(getter, property);
            var parameter = property.GetMethod.ReturnParameter;
            var safety = RefSafetyPrefix(parameter)
                + (HasAttribute(property, "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute") ? "unscoped " : string.Empty);
            var readOnly = HasAttribute(parameter, "System.Runtime.CompilerServices.IsReadOnlyAttribute")
                || parameter.GetRequiredCustomModifiers().Any(modifier => string.Equals(
                    modifier.FullName,
                    "System.Runtime.InteropServices.InAttribute",
                    StringComparison.Ordinal));
            return propertyFlow + setterFlow + safety + (readOnly ? "ref readonly " : "ref ")
                + FormatAnnotatedType(property.PropertyType.GetElementType()!, parameter);
        }
        var safetyPrefix = HasAttribute(property, "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute") ? "unscoped " : string.Empty;
        return propertyFlow + getterFlow + setterFlow + safetyPrefix + FormatAnnotatedType(property.PropertyType, property);
    }

    private static string NamedFlowContract(string name, ICustomAttributeProvider provider)
    {
        var contract = NullableFlowContract(provider).TrimEnd();
        return contract.Length == 0 ? string.Empty : name + "-flow(" + contract + ") ";
    }

    private static string NamedMethodFlowContract(string name, ICustomAttributeProvider? provider)
    {
        if (provider is null) return string.Empty;
        var contract = MethodFlowContract(provider).TrimEnd();
        return contract.Length == 0 ? string.Empty : name + "-member-flow(" + contract + ") ";
    }

    private static string RefSafetyPrefix(ParameterInfo parameter)
    {
        if (HasAttribute(parameter, "System.Runtime.CompilerServices.ScopedRefAttribute")) return "scoped ";
        if (HasAttribute(parameter, "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute")) return "unscoped ";
        return string.Empty;
    }

    private static bool HasAttribute(ICustomAttributeProvider provider, string attributeName)
        => GetCustomAttributes(provider).Any(attribute => string.Equals(attribute.AttributeType.FullName, attributeName, StringComparison.Ordinal));

    private static string FormatTypeKind(Type type)
    {
        var kind = type.IsEnum
            ? (type.IsDefined(typeof(FlagsAttribute), inherit: false) ? "flags enum" : "enum")
            : type.IsInterface ? "interface" : type.IsValueType ? FormatStructKind(type) : type.IsAbstract && type.IsSealed ? "static class" : type.IsAbstract ? "abstract class" : type.IsSealed ? "sealed class" : "class";
        return kind;
    }

    private static string FormatStructKind(Type type)
    {
        var byRefLike = HasCompilerMarker(type, "System.Runtime.CompilerServices.IsByRefLikeAttribute");
        var readOnly = HasCompilerMarker(type, "System.Runtime.CompilerServices.IsReadOnlyAttribute");
        return readOnly && byRefLike ? "readonly ref struct" : byRefLike ? "ref struct" : readOnly ? "readonly struct" : "struct";
    }

    private static bool HasCompilerMarker(MemberInfo member, string attributeName)
        => member.CustomAttributes.Any(attribute => string.Equals(attribute.AttributeType.FullName, attributeName, StringComparison.Ordinal));

    private static string FormatEnumUnderlyingType(Type type) => FormatType(Enum.GetUnderlyingType(type));

    private static string FormatTypeDeclarationName(Type type)
    {
        if (!type.IsGenericTypeDefinition) return FormatType(type);
        var arguments = type.GetGenericArguments().Select(argument =>
        {
            var variance = argument.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;
            var prefix = variance == GenericParameterAttributes.Covariant
                ? "out "
                : variance == GenericParameterAttributes.Contravariant
                    ? "in "
                    : string.Empty;
            return prefix + argument.Name;
        }).ToArray();
        return FormatGenericTypeName(type, arguments);
    }

    private static string FormatGenericConstraints(IEnumerable<Type> genericArguments)
    {
        var clauses = new List<string>();
        foreach (var argument in genericArguments.Where(argument => argument.IsGenericParameter))
        {
            var constraints = new List<string>();
            var genericAttributes = argument.GenericParameterAttributes;
            var attributes = genericAttributes & GenericParameterAttributes.SpecialConstraintMask;
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
                constraints.Add(GenericParameterNullability(argument) == 2 ? "class?" : "class");
            }
            else if (GenericParameterNullability(argument) == 1)
            {
                constraints.Add("notnull");
            }

            var constraintTypes = argument.GetGenericParameterConstraints();
            var constraintNullability = ReadGenericConstraintNullability(argument);
            constraints.AddRange(constraintTypes
                .Select((constraint, index) => new { Constraint = constraint, Index = index })
                .Where(value => value.Constraint != typeof(ValueType))
                .Select(value => FormatGenericConstraint(
                    value.Constraint,
                    value.Index < constraintNullability.Count ? constraintNullability[value.Index] : Array.Empty<byte>()))
                .OrderBy(value => value, StringComparer.Ordinal));
            if (!unmanaged
                && (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0
                && (attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
            {
                constraints.Add("new()");
            }

            const GenericParameterAttributes allowByRefLike = (GenericParameterAttributes)32;
            if ((genericAttributes & allowByRefLike) != 0)
            {
                constraints.Add("allows ref struct");
            }

            if (constraints.Count > 0)
            {
                clauses.Add(" where " + argument.Name + " : " + string.Join(", ", constraints));
            }
        }
        return string.Concat(clauses);
    }

    private static byte GenericParameterNullability(Type argument)
    {
        var flags = ReadNullableFlags(argument);
        return flags.Length == 0 ? ReadNullableContext(argument) : flags[0];
    }

    private static string FormatGenericConstraint(Type constraint, byte[] nullableFlags)
        => nullableFlags.Length == 0
            ? FormatType(constraint)
            : FormatAnnotatedType(
                constraint,
                new NullabilityCursor(nullableFlags, 0),
                new TupleNameCursor(Array.Empty<string?>()),
                new DynamicCursor(Array.Empty<bool>()));

    private static IReadOnlyList<byte[]> ReadGenericConstraintNullability(Type argument)
    {
        if (!argument.IsGenericParameter || string.IsNullOrEmpty(argument.Assembly.Location)) return Array.Empty<byte[]>();
        try
        {
            using var stream = File.OpenRead(argument.Assembly.Location);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var row = argument.MetadataToken & 0x00FFFFFF;
            if (row == 0) return Array.Empty<byte[]>();
            var parameter = reader.GetGenericParameter(MetadataTokens.GenericParameterHandle(row));
            return parameter.GetConstraints()
                .Select(handle => ReadNullableConstraintFlags(reader, reader.GetGenericParameterConstraint(handle)))
                .ToArray();
        }
        catch (BadImageFormatException)
        {
            return Array.Empty<byte[]>();
        }
        catch (IOException)
        {
            return Array.Empty<byte[]>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<byte[]>();
        }
    }

    private static byte[] ReadNullableConstraintFlags(MetadataReader reader, GenericParameterConstraint constraint)
    {
        foreach (var attributeHandle in constraint.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            if (!IsNullableAttribute(reader, attribute.Constructor)) continue;
            var blob = reader.GetBlobBytes(attribute.Value);
            if (blob.Length == 5 && blob[0] == 1 && blob[1] == 0) return new[] { blob[2] };
            if (blob.Length >= 8 && blob[0] == 1 && blob[1] == 0)
            {
                var count = BitConverter.ToInt32(blob, 2);
                if (count >= 0 && count <= blob.Length - 8)
                {
                    var flags = new byte[count];
                    Buffer.BlockCopy(blob, 6, flags, 0, count);
                    return flags;
                }
            }
        }
        return Array.Empty<byte>();
    }

    private static bool IsNullableAttribute(MetadataReader reader, EntityHandle constructor)
    {
        EntityHandle owner = constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default
        };
        return owner.Kind switch
        {
            HandleKind.TypeReference => IsNullableAttribute(reader, reader.GetTypeReference((TypeReferenceHandle)owner)),
            HandleKind.TypeDefinition => IsNullableAttribute(reader, reader.GetTypeDefinition((TypeDefinitionHandle)owner)),
            _ => false
        };
    }

    private static bool IsNullableAttribute(MetadataReader reader, TypeReference type)
        => reader.GetString(type.Namespace) == "System.Runtime.CompilerServices"
            && reader.GetString(type.Name) == "NullableAttribute";

    private static bool IsNullableAttribute(MetadataReader reader, TypeDefinition type)
        => reader.GetString(type.Namespace) == "System.Runtime.CompilerServices"
            && reader.GetString(type.Name) == "NullableAttribute";

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
        return FormatGenericTypeName(type, type.GetGenericArguments().Select(FormatType).ToArray());
    }

    private static string FormatAnnotatedType(Type type, ICustomAttributeProvider provider)
    {
        var flags = ReadNullableFlags(provider);
        var context = ReadNullableContext(provider);
        var tupleNames = ReadTupleNames(provider);
        var dynamicFlags = ReadDynamicFlags(provider);
        return FormatAnnotatedType(type, new NullabilityCursor(flags, context), new TupleNameCursor(tupleNames), new DynamicCursor(dynamicFlags));
    }

    private static string FormatAnnotatedType(Type type, NullabilityCursor cursor, TupleNameCursor tupleNames, DynamicCursor dynamicFlags)
    {
        var flag = cursor.Next();
        var isDynamic = dynamicFlags.Next();
        if (type.IsArray)
        {
            var array = FormatAnnotatedType(type.GetElementType()!, cursor, tupleNames, dynamicFlags) + ArraySuffix(type);
            return flag == 2 ? array + "?" : array;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (IsTupleDefinition(definition) && tupleNames.HasNames)
            {
                return FormatTuple(type, cursor, tupleNames, dynamicFlags);
            }
            var arguments = type.GetGenericArguments()
                .Select(argument => FormatAnnotatedType(argument, cursor, tupleNames, dynamicFlags))
                .ToArray();
            var formatted = FormatGenericTypeName(type, arguments);
            return !type.IsValueType && flag == 2 ? formatted + "?" : formatted;
        }

        var result = type == typeof(object) && isDynamic ? "dynamic" : type.FullName ?? type.Name;
        return (!type.IsValueType || type.IsGenericParameter) && flag == 2 ? result + "?" : result;
    }

    private static string FormatTuple(Type type, NullabilityCursor cursor, TupleNameCursor tupleNames, DynamicCursor dynamicFlags)
    {
        var elements = new List<string>();
        AddTupleElements(type, cursor, tupleNames, dynamicFlags, elements);
        return "(" + string.Join(", ", elements) + ")";
    }

    private static void AddTupleElements(
        Type tupleType,
        NullabilityCursor cursor,
        TupleNameCursor tupleNames,
        DynamicCursor dynamicFlags,
        ICollection<string> elements)
    {
        var arguments = tupleType.GetGenericArguments();
        var logicalCount = arguments.Length == 8 ? 7 : arguments.Length;
        for (var index = 0; index < logicalCount; index++)
        {
            var elementName = tupleNames.Next();
            var formatted = FormatAnnotatedType(arguments[index], cursor, tupleNames, dynamicFlags);
            elements.Add(formatted + (string.IsNullOrEmpty(elementName) ? string.Empty : " " + elementName));
        }

        if (arguments.Length == 8)
        {
            _ = cursor.Next();
            _ = dynamicFlags.Next();
            AddTupleElements(arguments[7], cursor, tupleNames, dynamicFlags, elements);
        }
    }

    private static bool IsTupleDefinition(Type type)
        => type.Namespace == "System"
            && type.Name.StartsWith("ValueTuple`", StringComparison.Ordinal)
            && type.IsGenericTypeDefinition;

    private static string ArraySuffix(Type type) => "[" + new string(',', type.GetArrayRank() - 1) + "]";

    private static string FormatGenericTypeName(Type type, IReadOnlyList<string> formattedArguments)
    {
        var definition = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();
        var segments = (definition.FullName ?? definition.Name).Split('+');
        var formattedSegments = new List<string>(segments.Length);
        var argumentIndex = 0;
        foreach (var segment in segments)
        {
            var marker = segment.LastIndexOf('`');
            if (marker < 0)
            {
                formattedSegments.Add(segment);
                continue;
            }

            if (!int.TryParse(segment.Substring(marker + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var arity)
                || arity < 1
                || argumentIndex + arity > formattedArguments.Count)
                throw new InvalidOperationException("The generic type name contained an invalid arity.");
            formattedSegments.Add(segment.Substring(0, marker) + "<" + string.Join(",", formattedArguments.Skip(argumentIndex).Take(arity)) + ">");
            argumentIndex += arity;
        }

        if (argumentIndex != formattedArguments.Count)
            throw new InvalidOperationException("The generic type name did not own all generic arguments.");
        return string.Join("+", formattedSegments);
    }

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
                Type type when type.IsGenericParameter && type.DeclaringMethod is not null => type.DeclaringMethod,
                MemberInfo member => member.DeclaringType,
                _ => null
            };
        }
        return 0;
    }

    private static string?[] ReadTupleNames(ICustomAttributeProvider provider)
    {
        var attribute = GetCustomAttributes(provider).FirstOrDefault(value =>
            string.Equals(value.AttributeType.FullName, "System.Runtime.CompilerServices.TupleElementNamesAttribute", StringComparison.Ordinal));
        if (attribute is null || attribute.ConstructorArguments.Count != 1) return Array.Empty<string?>();
        if (attribute.ConstructorArguments[0].Value is not IEnumerable<CustomAttributeTypedArgument> values)
            return Array.Empty<string?>();
        return values.Select(value => value.Value as string).ToArray();
    }

    private static bool[] ReadDynamicFlags(ICustomAttributeProvider provider)
    {
        var attribute = GetCustomAttributes(provider).FirstOrDefault(value =>
            string.Equals(value.AttributeType.FullName, "System.Runtime.CompilerServices.DynamicAttribute", StringComparison.Ordinal));
        if (attribute is null) return Array.Empty<bool>();
        if (attribute.ConstructorArguments.Count == 0) return new[] { true };
        var argument = attribute.ConstructorArguments[0];
        if (argument.Value is bool single) return new[] { single };
        if (argument.Value is IEnumerable<CustomAttributeTypedArgument> values)
            return values.Select(value => Convert.ToBoolean(value.Value, CultureInfo.InvariantCulture)).ToArray();
        return Array.Empty<bool>();
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

    private sealed class TupleNameCursor
    {
        private readonly string?[] _names;
        private int _index;

        public TupleNameCursor(string?[] names)
        {
            _names = names;
        }

        public bool HasNames => _names.Length > 0;

        public string? Next() => _index < _names.Length ? _names[_index++] : null;
    }

    private sealed class DynamicCursor
    {
        private readonly bool[] _flags;
        private int _index;

        public DynamicCursor(bool[] flags)
        {
            _flags = flags;
        }

        public bool Next() => _index < _flags.Length && _flags[_index++];
    }

#if NET10_0
    [System.Runtime.CompilerServices.CollectionBuilder(typeof(CollectionBuilderFixtureBuilder), nameof(CollectionBuilderFixtureBuilder.Create))]
    private sealed class CollectionBuilderFixture : List<int>
    {
    }

    private static class CollectionBuilderFixtureBuilder
    {
        public static CollectionBuilderFixture Create(ReadOnlySpan<int> values)
        {
            var result = new CollectionBuilderFixture();
            foreach (var value in values)
            {
                result.Add(value);
            }

            return result;
        }
    }
#endif
}

public class PublicApiProtectedNestedFixture
{
    protected class ProtectedNested
    {
    }

    public class PublicNested
    {
    }

    protected internal class ProtectedInternalNested
    {
    }

    public static Type NestedType => typeof(ProtectedNested);
}
