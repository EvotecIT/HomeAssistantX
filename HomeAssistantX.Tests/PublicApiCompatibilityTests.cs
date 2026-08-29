using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
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
    public void ParameterFormatterPreservesByValueMarshallingDirections()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(ByValueDirectionFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(
            "in-flag System.Byte[] input, out-flag System.Byte[] output, in-flag out-flag System.Byte[] inputOutput",
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
        var marshaledLayout = StructLayoutContract(typeof(MarshalAsFieldFixture));
        Assert.Contains("marshal-as(type=LPArray", marshaledLayout, StringComparison.Ordinal);
        Assert.Contains("descriptor=", marshaledLayout, StringComparison.Ordinal);
        Assert.Contains("layout(Sequential", StructLayoutContract(typeof(SequentialClassLayoutFixture)), StringComparison.Ordinal);
    }

#if NET10_0
    [Fact]
    public void TypeFormatterPreservesCollectionBuilderContracts()
    {
        Assert.Equal(
            "collection-builder(HomeAssistantX.Tests.PublicApiCompatibilityTests+CollectionBuilderFixtureBuilder,\"Create\") ",
            CollectionBuilderContract(typeof(CollectionBuilderFixture)));
    }

    [Fact]
    public void TypeFormatterPreservesInlineArrayLength()
    {
        Assert.Equal("inline-array(4) ", InlineArrayContract(typeof(InlineArrayFixture)));
    }

    [Fact]
    public void TypeAndFieldFormattersPreserveNativeBufferAndDelegateContracts()
    {
        var fixedBuffer = typeof(FixedBufferFixture).GetField(nameof(FixedBufferFixture.Data))!;
        Assert.Equal(
            "fixed-buffer(element=System.Byte,length=16) ",
            FixedBufferContract(fixedBuffer));

        var delegateContract = UnmanagedFunctionPointerContract(typeof(UnmanagedDelegateFixture));
        Assert.Contains("calling-convention=Cdecl", delegateContract, StringComparison.Ordinal);
        Assert.Contains("charset=Unicode", delegateContract, StringComparison.Ordinal);
        Assert.Contains("set-last-error=true", delegateContract, StringComparison.Ordinal);
    }

    [Fact]
    public void TypeFormatterPreservesFunctionPointerSignatures()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(FunctionPointerFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var formatted = FormatType(method.GetParameters()[0].ParameterType);

        Assert.Contains("delegate* unmanaged", formatted, StringComparison.Ordinal);
        Assert.Contains("System.Int32", formatted, StringComparison.Ordinal);
        Assert.EndsWith(",System.Void>", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void MethodFormatterPreservesUnmanagedCallableMetadata()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(UnmanagedCallableFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var contract = UnmanagedCallersOnlyContract(method);
        Assert.Contains("entry=\"hax_entry\"", contract, StringComparison.Ordinal);
        Assert.Contains("CallConvCdecl", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void MethodFormatterPreservesUnmanagedCallingConventionMetadata()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(UnmanagedCallConventionFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var contract = UnmanagedCallConvContract(method);
        Assert.Contains("CallConvStdcall", contract, StringComparison.Ordinal);
        Assert.Contains("CallConvSuppressGCTransition", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatterPreservesTrimmingAndDynamicCodeRequirements()
    {
        var method = typeof(RequiresCodeFixture).GetMethod(nameof(RequiresCodeFixture.Invoke))!;
        var constructor = typeof(RequiresCodeFixture).GetConstructors().Single();
        var property = typeof(RequiresCodeFixture).GetProperty(nameof(RequiresCodeFixture.Value))!;

        Assert.Contains("requires-unreferenced-code(message=\"type trim\",url=\"https://example.invalid/type-trim\")", RequiresCodeContract(typeof(RequiresCodeFixture)), StringComparison.Ordinal);
        Assert.Contains("requires-unreferenced-code(message=\"member trim\",url=\"https://example.invalid/member-trim\")", RequiresCodeContract(method), StringComparison.Ordinal);
        Assert.Contains("requires-dynamic-code(message=\"member dynamic\",url=\"https://example.invalid/member-dynamic\")", RequiresCodeContract(method), StringComparison.Ordinal);
        Assert.Contains("requires-dynamic-code(message=\"constructor dynamic\",url=null)", FormatConstructor(constructor), StringComparison.Ordinal);
        Assert.Contains("requires-unreferenced-code(message=\"getter trim\",url=null)", FormatProperty(property), StringComparison.Ordinal);
        Assert.Contains("requires-assembly-files(message=\"member files\",url=null)", RequiresCodeContract(method), StringComparison.Ordinal);

        var annotated = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(DynamicallyAccessedMembersFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Contains("dam(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)", FormatMethod(annotated), StringComparison.Ordinal);
        Assert.Contains("dam(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)", FormatReturnType(annotated), StringComparison.Ordinal);
        Assert.Contains("dam(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)", FormatParameters(annotated.GetParameters()), StringComparison.Ordinal);
    }

    [Fact]
    public void TypeFormatterPreservesComIdentityAndDispatchMetadata()
    {
        var interfaceContract = TypeInteropContract(typeof(ComInterfaceFixture));
        Assert.Contains("guid(\"5E0D079B-34C4-4586-A933-46D1F9987E26\")", interfaceContract, StringComparison.Ordinal);
        Assert.Contains("com-import", interfaceContract, StringComparison.Ordinal);
        Assert.Contains("interface-type(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)", interfaceContract, StringComparison.Ordinal);
        Assert.Contains("class-interface(System.Runtime.InteropServices.ClassInterfaceType.AutoDispatch)", TypeInteropContract(typeof(ComClassFixture)), StringComparison.Ordinal);
    }

    [Fact]
    public void TypeFormatterPreservesNativeIntegerAnnotations()
    {
        var fixture = CreateNativeIntegerFixtureType();
        var method = fixture.GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("nint", FormatReturnType(method));
        Assert.Equal(
            "nuint value, System.Collections.Generic.IReadOnlyList<nint[]> nested",
            FormatParameters(method.GetParameters()));
        Assert.EndsWith("nint Field", FormatField(fixture.GetField("Field")!), StringComparison.Ordinal);
        Assert.Contains("nuint Property", FormatProperty(fixture.GetProperty("Property")!), StringComparison.Ordinal);
    }

    [Fact]
    public void MemberFormatterPreservesExperimentalAndPlatformDiagnostics()
    {
        var experimental = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(ExperimentalFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var constructor = typeof(PlatformConstructorFixture).GetConstructors().Single();
        var guard = typeof(PlatformConstructorFixture).GetProperty(nameof(PlatformConstructorFixture.IsWindows))!;

        Assert.Contains("experimental(id=\"HAX001\"", ExperimentalContract(experimental), StringComparison.Ordinal);
        Assert.Contains("windows10.0", PlatformContract(constructor), StringComparison.Ordinal);
        Assert.Contains("SupportedOSPlatformGuard", PlatformContract(guard), StringComparison.Ordinal);
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

    [Fact]
    public void MemberFormatterPreservesReadonlyStructMembers()
    {
        var method = typeof(ReadonlyMemberFixture).GetMethod(nameof(ReadonlyMemberFixture.Read))!;
        var getter = typeof(ReadonlyMemberFixture).GetProperty(nameof(ReadonlyMemberFixture.Value))!.GetMethod!;
        var overrideMethod = typeof(ReadonlyOverrideFixture).GetMethod(nameof(ToString))!;

        Assert.Equal("instance readonly", MemberScope(method));
        Assert.Equal("instance readonly", MemberScope(getter));
        Assert.Equal("override readonly", MemberScope(overrideMethod));
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
    public void TypeFormatterPreservesAttributeUsageContracts()
    {
        Assert.Equal(
            "attribute-usage(targets=Class|Method,allow-multiple=true,inherited=false) ",
            AttributeUsageContract(typeof(AttributeUsageFixtureAttribute)));
    }

    [Fact]
    public void MethodFormatterPreservesNativeImportContracts()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(NativeImportFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var omitted = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(NativeImportOmittedOptionsFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var disabled = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(NativeImportDisabledOptionsFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var formatted = FormatMethod(method);
        Assert.StartsWith("dll-import(\"native-test\",entry=\"native_entry\",import-flags=", formatted, StringComparison.Ordinal);
        Assert.Contains(",calling=Cdecl,charset=Unicode,exact=true,set-last-error=true", formatted, StringComparison.Ordinal);
        Assert.NotEqual(DllImportContract(omitted), DllImportContract(disabled));
    }

    [Fact]
    public void MethodFormatterPreservesManagedPreserveSigContracts()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(PreserveSigManagedFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.StartsWith("preserve-sig PreserveSigManagedFixture", FormatMethod(method), StringComparison.Ordinal);
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
        var warning = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(WarningObsoleteFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.StartsWith("obsolete ", FormatMethod(warning), StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterFormatterPreservesMarshalAsContracts()
    {
        var method = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(MarshalAsFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var parameters = FormatParameters(method.GetParameters());
        Assert.Contains("marshal-as(type=LPArray", parameters, StringComparison.Ordinal);
        Assert.Contains("array-subtype=I4", parameters, StringComparison.Ordinal);
        Assert.Contains("size-const=4", parameters, StringComparison.Ordinal);
        Assert.Contains("size-param-index=1", parameters, StringComparison.Ordinal);
        Assert.StartsWith("marshal-as(type=Bool", FormatReturnType(method), StringComparison.Ordinal);

        var omittedParameter = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(MarshalAsOmittedSizeParameterFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!.GetParameters()[0];
        var explicitParameter = typeof(PublicApiCompatibilityTests).GetMethod(
            nameof(MarshalAsExplicitZeroSizeParameterFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!.GetParameters()[0];
        Assert.NotEqual(MarshalAsContract(omittedParameter), MarshalAsContract(explicitParameter));

        var fields = typeof(MarshalAsFieldFixture).GetFields(BindingFlags.Public | BindingFlags.Instance);
        Assert.NotEqual(
            MarshalAsContract(fields.Single(value => value.Name == nameof(MarshalAsFieldFixture.OmittedSize))),
            MarshalAsContract(fields.Single(value => value.Name == nameof(MarshalAsFieldFixture.ExplicitZeroSize))));

        var property = typeof(MarshalAsPropertyFixture).GetProperty(nameof(MarshalAsPropertyFixture.Enabled))!;
        Assert.Contains("marshal-as(type=Bool", FormatProperty(property), StringComparison.Ordinal);
    }

    [Fact]
    public void MethodSelectionPreservesUserDefinedOperatorsAndExcludesAccessors()
    {
        var operatorMethod = typeof(OperatorFixture).GetMethod("op_Addition", BindingFlags.Public | BindingFlags.Static)!;
        var propertyGetter = typeof(OperatorFixture).GetProperty(nameof(OperatorFixture.Value))!.GetMethod!;
        var specialMethod = typeof(SpecialNameMemberFixture).GetMethod(nameof(SpecialNameMemberFixture.SpecialHook))!;

        Assert.True(ShouldIncludeMethod(operatorMethod));
        Assert.False(ShouldIncludeMethod(propertyGetter));
        Assert.StartsWith("special-name op_Addition(", FormatMethod(operatorMethod), StringComparison.Ordinal);
        Assert.True(ShouldIncludeMethod(specialMethod));
        Assert.StartsWith("special-name SpecialHook(", FormatMethod(specialMethod), StringComparison.Ordinal);
    }

    [Fact]
    public void MemberFormatterPreservesIndexerAndProtectedConstructorContracts()
    {
        var indexer = typeof(IndexerFixture).GetProperty("Item")!;
        var protectedConstructor = typeof(ProtectedConstructorFixture).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();

        Assert.Equal("[System.String key]", FormatIndexerParameters(indexer));
        Assert.Equal("protected ", ConstructorAccess(protectedConstructor));
        Assert.Equal("default-member(\"Item\") ", DefaultMemberContract(typeof(IndexerFixture)));
    }

    [Fact]
    public void MemberFormatterPreservesSpecialNameAcrossMemberKinds()
    {
        var type = typeof(SpecialNameMemberFixture);
        var constructor = type.GetConstructors().Single();
        var field = type.GetField(nameof(SpecialNameMemberFixture.Field))!;
        var property = type.GetProperty(nameof(SpecialNameMemberFixture.Property))!;
        var eventInfo = type.GetEvent(nameof(SpecialNameMemberFixture.Changed))!;

        Assert.Contains("special-name ", FormatConstructor(constructor), StringComparison.Ordinal);
        Assert.Contains("special-name ", FormatField(field), StringComparison.Ordinal);
        Assert.Contains("special-name ", FormatProperty(property), StringComparison.Ordinal);
        Assert.Equal("special-name ", SpecialNameContract(eventInfo));
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

    [Fact]
    public void AssemblyFormatterPreservesInteropRuntimeContracts()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("HomeAssistantX.AssemblyInteropFixture." + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(DisableRuntimeMarshallingAttribute).GetConstructor(Type.EmptyTypes)!,
            Array.Empty<object>()));
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(DefaultDllImportSearchPathsAttribute).GetConstructor(new[] { typeof(DllImportSearchPath) })!,
            new object[] { DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory }));

        Assert.Equal(
            "A default-dll-import-search-paths(System.Runtime.InteropServices.DllImportSearchPath.AssemblyDirectory, SafeDirectories)\nA disable-runtime-marshalling",
            BuildSurface(assembly));
    }
#endif

    private static string BuildSurface(Assembly assembly)
    {
        var lines = new List<string>(FormatAssemblyContracts(assembly));
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
            lines.Add("T " + TypeAccess(type) + ObsoleteContract(type) + ExperimentalContract(type) + PlatformContract(type) + RequiresCodeContract(type) + ConditionalContract(type) + AttributeUsageContract(type) + DefaultMemberContract(type) + CollectionBuilderContract(type) + InlineArrayContract(type) + UnmanagedFunctionPointerContract(type) + TypeInteropContract(type) + StructLayoutContract(type) + kind + " " + FormatTypeDeclarationName(type) + (contracts.Count == 0 ? string.Empty : " : " + string.Join(", ", contracts)) + typeConstraints);
            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                {
                    var value = Enum.Parse(type, name);
                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)!;
                    lines.Add("  F " + ObsoleteContract(field) + ExperimentalContract(field) + PlatformContract(field) + name + " = " + FormatEnumValue(value, Enum.GetUnderlyingType(type)));
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
                lines.Add("  E " + MemberAccess(accessor) + MemberScope(accessor) + " " + SpecialNameContract(eventInfo) + ObsoleteContract(eventInfo, eventInfo.AddMethod, eventInfo.RemoveMethod) + ExperimentalContract(eventInfo, eventInfo.AddMethod, eventInfo.RemoveMethod) + PlatformContract(eventInfo, eventInfo.AddMethod, eventInfo.RemoveMethod) + RequiresCodeContract(eventInfo, eventInfo.AddMethod, eventInfo.RemoveMethod) + FormatAnnotatedType(eventInfo.EventHandlerType!, eventInfo) + " " + eventInfo.Name);
            }
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(ShouldIncludeMethod).OrderBy(FormatMethod, StringComparer.Ordinal))
                lines.Add("  M " + MemberAccess(method) + MemberScope(method) + " " + FormatReturnType(method) + " " + FormatMethod(method));
        }
        return string.Join("\n", lines);
    }

    private static IEnumerable<string> FormatAssemblyContracts(Assembly assembly)
    {
        var contracts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var attribute in GetCustomAttributes(assembly))
        {
            switch (attribute.AttributeType.FullName)
            {
                case "System.Runtime.CompilerServices.DisableRuntimeMarshallingAttribute":
                    contracts.Add("A disable-runtime-marshalling");
                    break;
                case "System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute"
                    when attribute.ConstructorArguments.Count == 1:
                    contracts.Add(
                        "A default-dll-import-search-paths("
                        + FormatAttributeArgument(attribute.ConstructorArguments[0])
                        + ")");
                    break;
            }
        }
        return contracts;
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

    private static string InlineArrayContract(Type type)
    {
        var attribute = GetCustomAttributes(type).FirstOrDefault(value => string.Equals(
            value.AttributeType.FullName,
            "System.Runtime.CompilerServices.InlineArrayAttribute",
            StringComparison.Ordinal));
        return attribute?.ConstructorArguments.Count == 1
            && attribute.ConstructorArguments[0].Value is int length
                ? "inline-array(" + length.ToString(CultureInfo.InvariantCulture) + ") "
                : string.Empty;
    }

    private static string UnmanagedFunctionPointerContract(Type type)
    {
        var attribute = type.GetCustomAttribute<UnmanagedFunctionPointerAttribute>();
        if (attribute is null)
        {
            return string.Empty;
        }

        return "unmanaged-function-pointer(calling-convention=" + attribute.CallingConvention
            + ",charset=" + attribute.CharSet
            + ",best-fit=" + FormatDefault(attribute.BestFitMapping)
            + ",throw-on-unmappable=" + FormatDefault(attribute.ThrowOnUnmappableChar)
            + ",set-last-error=" + FormatDefault(attribute.SetLastError)
            + ") ";
    }

    private static string FixedBufferContract(FieldInfo field)
    {
        var attribute = field.GetCustomAttribute<FixedBufferAttribute>();
        return attribute is null
            ? string.Empty
            : "fixed-buffer(element=" + FormatType(attribute.ElementType)
                + ",length=" + attribute.Length.ToString(CultureInfo.InvariantCulture)
                + ") ";
    }

    private static string StructLayoutContract(Type type)
    {
        if (type.IsEnum)
        {
            return string.Empty;
        }

        var layout = type.StructLayoutAttribute;
        if (layout is null || (!type.IsValueType && layout.Value == LayoutKind.Auto))
        {
            return string.Empty;
        }
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken)
            .Select(field =>
            {
                var offset = field.GetCustomAttribute<FieldOffsetAttribute>();
                return MarshalAsContract(field) + FixedBufferContract(field) + FormatType(field.FieldType) + " " + field.Name
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
        var nativeIntegerFlags = new NativeIntegerCursor(ReadNativeIntegerFlags(type));
        if (type.BaseType is not null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            contracts.Add(FormatAnnotatedType(type.BaseType, nullability, tupleNames, dynamicFlags, nativeIntegerFlags));
        }

        foreach (var contract in GetDirectInterfaces(type))
        {
            contracts.Add(FormatAnnotatedType(contract, nullability, tupleNames, dynamicFlags, nativeIntegerFlags));
        }

        return contracts.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string FormatMethod(MethodBase method)
    {
        var genericArguments = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;
        var genericList = genericArguments.Length == 0
            ? string.Empty
            : "<" + string.Join(",", genericArguments.Select(argument => DynamicallyAccessedMembersContract(argument) + argument.Name)) + ">";
        var extension = method.IsDefined(typeof(ExtensionAttribute), inherit: false) ? "extension " : string.Empty;
        return SpecialNameContract(method) + ObsoleteContract(method) + ExperimentalContract(method) + PlatformContract(method) + RequiresCodeContract(method) + OverloadResolutionPriorityContract(method) + ConditionalContract(method) + DllImportContract(method) + PreserveSigContract(method) + UnmanagedCallersOnlyContract(method) + UnmanagedCallConvContract(method) + MethodFlowContract(method) + extension + method.Name + genericList + "(" + FormatParameters(method.GetParameters()) + ")" + FormatGenericConstraints(genericArguments);
    }

    private static string SpecialNameContract(MemberInfo member)
        => member switch
        {
            MethodBase method when method.IsSpecialName => "special-name ",
            FieldInfo field when field.IsSpecialName => "special-name ",
            PropertyInfo property when property.IsSpecialName => "special-name ",
            EventInfo eventInfo when eventInfo.IsSpecialName => "special-name ",
            _ => string.Empty
        };

    private static string DefaultMemberContract(Type type)
    {
        var attribute = type.GetCustomAttribute<DefaultMemberAttribute>(inherit: true);
        return attribute is null
            ? string.Empty
            : "default-member(" + FormatDefault(attribute.MemberName) + ") ";
    }

    private static string AttributeUsageContract(Type type)
    {
        if (!typeof(Attribute).IsAssignableFrom(type)) return string.Empty;
        var usage = type.GetCustomAttribute<AttributeUsageAttribute>(inherit: true)
            ?? new AttributeUsageAttribute(AttributeTargets.All);
        var targets = usage.ValidOn == AttributeTargets.All
            ? nameof(AttributeTargets.All)
            : string.Join("|", Enum.GetValues(typeof(AttributeTargets))
                .Cast<AttributeTargets>()
                .Where(value => value != 0
                    && value != AttributeTargets.All
                    && (usage.ValidOn & value) == value)
                .OrderBy(value => (int)value)
                .Select(value => value.ToString()));
        return "attribute-usage(targets=" + targets
            + ",allow-multiple=" + FormatBoolean(usage.AllowMultiple)
            + ",inherited=" + FormatBoolean(usage.Inherited) + ") ";
    }

    private static string DllImportContract(MethodBase method)
    {
        var attribute = method.GetCustomAttribute<DllImportAttribute>();
        if (attribute is null) return string.Empty;
        return "dll-import(" + FormatDefault(attribute.Value)
            + ",entry=" + FormatDefault(attribute.EntryPoint)
            + ",import-flags=" + ((int)ReadMethodImportAttributes(method)).ToString(CultureInfo.InvariantCulture)
            + ",calling=" + attribute.CallingConvention
            + ",charset=" + attribute.CharSet
            + ",exact=" + FormatBoolean(attribute.ExactSpelling)
            + ",set-last-error=" + FormatBoolean(attribute.SetLastError)
            + ",best-fit=" + FormatBoolean(attribute.BestFitMapping)
            + ",throw-unmappable=" + FormatBoolean(attribute.ThrowOnUnmappableChar)
            + ",preserve-sig=" + FormatBoolean(attribute.PreserveSig) + ") ";
    }

    private static string PreserveSigContract(MethodBase method)
        => method.GetCustomAttribute<DllImportAttribute>() is null
            && (method.MethodImplementationFlags & MethodImplAttributes.PreserveSig) != 0
                ? "preserve-sig "
                : string.Empty;

    private static string UnmanagedCallersOnlyContract(MethodBase method)
    {
        var attribute = GetCustomAttributes(method).FirstOrDefault(value => string.Equals(
            value.AttributeType.FullName,
            "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute",
            StringComparison.Ordinal));
        if (attribute is null) return string.Empty;

        var entryPoint = attribute.NamedArguments.FirstOrDefault(value => value.MemberName == "EntryPoint").TypedValue.Value;
        var callingConventions = attribute.NamedArguments.FirstOrDefault(value => value.MemberName == "CallConvs").TypedValue.Value;
        var conventions = callingConventions is IEnumerable<CustomAttributeTypedArgument> values
            ? values.Select(value => value.Value is Type type ? FormatType(type) : FormatDefault(value.Value)).ToArray()
            : Array.Empty<string>();
        return "unmanaged-callers-only(entry=" + FormatDefault(entryPoint)
            + ",call-convs=" + string.Join("|", conventions) + ") ";
    }

    private static string UnmanagedCallConvContract(MethodBase method)
    {
        var attribute = GetCustomAttributes(method).FirstOrDefault(value => string.Equals(
            value.AttributeType.FullName,
            "System.Runtime.InteropServices.UnmanagedCallConvAttribute",
            StringComparison.Ordinal));
        if (attribute is null) return string.Empty;

        var callingConventions = attribute.NamedArguments.FirstOrDefault(value => value.MemberName == "CallConvs").TypedValue.Value;
        var conventions = callingConventions is IEnumerable<CustomAttributeTypedArgument> values
            ? values.Select(value => value.Value is Type type ? FormatType(type) : FormatAttributeArgument(value)).ToArray()
            : Array.Empty<string>();
        return "unmanaged-call-conv(" + string.Join("|", conventions) + ") ";
    }

    private static string TypeInteropContract(Type type)
    {
        var contracts = new List<string>();
        foreach (var attribute in GetCustomAttributes(type))
        {
            switch (attribute.AttributeType.FullName)
            {
                case "System.Runtime.InteropServices.GuidAttribute" when attribute.ConstructorArguments.Count == 1:
                    contracts.Add("guid(" + FormatAttributeArgument(attribute.ConstructorArguments[0]) + ")");
                    break;
                case "System.Runtime.InteropServices.ComImportAttribute":
                    contracts.Add("com-import");
                    break;
                case "System.Runtime.InteropServices.InterfaceTypeAttribute" when attribute.ConstructorArguments.Count == 1:
                    contracts.Add("interface-type(" + FormatAttributeArgument(attribute.ConstructorArguments[0]) + ")");
                    break;
                case "System.Runtime.InteropServices.ClassInterfaceAttribute" when attribute.ConstructorArguments.Count == 1:
                    contracts.Add("class-interface(" + FormatAttributeArgument(attribute.ConstructorArguments[0]) + ")");
                    break;
            }
        }
        return contracts.Count == 0 ? string.Empty : string.Join(" ", contracts.OrderBy(value => value, StringComparer.Ordinal)) + " ";
    }

    private static string RequiresCodeContract(params ICustomAttributeProvider?[] providers)
    {
        var contracts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var provider in providers.Where(value => value is not null))
        {
            foreach (var attribute in GetCustomAttributes(provider!).Where(value =>
                         value.AttributeType.FullName is "System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"
                             or "System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute"
                             or "System.Diagnostics.CodeAnalysis.RequiresAssemblyFilesAttribute"))
            {
                var name = attribute.AttributeType.Name switch
                {
                    "RequiresUnreferencedCodeAttribute" => "requires-unreferenced-code",
                    "RequiresDynamicCodeAttribute" => "requires-dynamic-code",
                    _ => "requires-assembly-files"
                };
                var message = attribute.ConstructorArguments.Count == 1
                    ? attribute.ConstructorArguments[0].Value
                    : null;
                var url = attribute.NamedArguments.FirstOrDefault(value => value.MemberName == "Url").TypedValue.Value;
                contracts.Add(name + "(message=" + FormatDefault(message) + ",url=" + FormatDefault(url) + ")");
            }
        }
        return contracts.Count == 0 ? string.Empty : string.Join(" ", contracts) + " ";
    }

    private static string DynamicallyAccessedMembersContract(ICustomAttributeProvider provider)
    {
        var attribute = GetCustomAttributes(provider).FirstOrDefault(value => string.Equals(
            value.AttributeType.FullName,
            "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute",
            StringComparison.Ordinal));
        return attribute?.ConstructorArguments.Count == 1
            ? "dam(" + FormatAttributeArgument(attribute.ConstructorArguments[0]) + ") "
            : string.Empty;
    }

    private static string FormatAttributeArgument(CustomAttributeTypedArgument argument)
    {
        if (argument.Value is null) return "null";
        if (!argument.ArgumentType.IsEnum) return FormatDefault(argument.Value);
        var value = Enum.ToObject(argument.ArgumentType, argument.Value);
        return FormatDefault(value);
    }

    private static MethodImportAttributes ReadMethodImportAttributes(MethodBase method)
    {
        using var stream = File.OpenRead(method.Module.FullyQualifiedName);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var row = method.MetadataToken & 0x00FFFFFF;
        return reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).GetImport().Attributes;
    }

    private static string FormatConstructor(ConstructorInfo constructor)
        => "C " + ConstructorAccess(constructor) + SpecialNameContract(constructor) + ObsoleteContract(constructor)
            + ExperimentalContract(constructor) + PlatformContract(constructor) + RequiresCodeContract(constructor)
            + OverloadResolutionPriorityContract(constructor) + MethodFlowContract(constructor) + RequiredMemberSatisfaction(constructor)
            + FormatType(constructor.DeclaringType!) + "(" + FormatParameters(constructor.GetParameters()) + ")";

    private static string FormatProperty(PropertyInfo property)
    {
        var accessor = MostAccessible(property.GetMethod, property.SetMethod)!;
        var getter = IsExternallyAccessibleMethod(property.GetMethod) ? property.GetMethod : null;
        var setter = IsExternallyAccessibleMethod(property.SetMethod) ? property.SetMethod : null;
        return "P " + MemberAccess(accessor) + MemberScope(accessor) + " " + SpecialNameContract(property) + ObsoleteContract(
            property,
            getter,
            setter)
            + ExperimentalContract(property, getter, setter) + PlatformContract(property, getter, setter) + RequiresCodeContract(property, getter, setter)
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

    private static void ByValueDirectionFixture(
        [In] byte[] input,
        [Out] byte[] output,
        [In, Out] byte[] inputOutput)
    {
    }

    [PreserveSig]
    private static int PreserveSigManagedFixture() => 0;

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

    [return: MarshalAs(UnmanagedType.Bool)]
    private static bool MarshalAsFixture(
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.I4, SizeConst = 4, SizeParamIndex = 1)] int[] values,
        int count)
        => values.Length == count;

    private static void MarshalAsOmittedSizeParameterFixture(
        [MarshalAs(UnmanagedType.LPArray)] int[] values)
    {
    }

    private static void MarshalAsExplicitZeroSizeParameterFixture(
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] values)
    {
    }

#if NET10_0
    [UnmanagedCallersOnly(EntryPoint = "hax_entry", CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void UnmanagedCallableFixture()
    {
    }

    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall), typeof(CallConvSuppressGCTransition) })]
    private static void UnmanagedCallConventionFixture()
    {
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("type trim", Url = "https://example.invalid/type-trim")]
    private sealed class RequiresCodeFixture
    {
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("constructor dynamic")]
        public RequiresCodeFixture()
        {
        }

        public static int Value
        {
            [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("getter trim")]
            get => 1;
        }

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("member trim", Url = "https://example.invalid/member-trim")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("member dynamic", Url = "https://example.invalid/member-dynamic")]
        [System.Diagnostics.CodeAnalysis.RequiresAssemblyFiles("member files")]
        public static void Invoke()
        {
        }
    }

    [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)]
    private static Type DynamicallyAccessedMembersFixture<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] Type value)
        => value;

    private static unsafe void FunctionPointerFixture(delegate* unmanaged[Cdecl]<int, void> callback)
    {
    }

    private unsafe struct FixedBufferFixture
    {
        public fixed byte Data[16];
    }

    [UnmanagedFunctionPointer(
        CallingConvention.Cdecl,
        CharSet = CharSet.Unicode,
        BestFitMapping = false,
        ThrowOnUnmappableChar = true,
        SetLastError = true)]
    private delegate int UnmanagedDelegateFixture(int value);

    [System.Diagnostics.CodeAnalysis.Experimental("HAX001", UrlFormat = "https://example.invalid/{0}")]
    private static void ExperimentalFixture()
    {
    }

    [InlineArray(4)]
    private struct InlineArrayFixture
    {
        private int _element0;
    }

    private sealed class PlatformConstructorFixture
    {
        [System.Runtime.Versioning.SupportedOSPlatform("windows10.0")]
        public PlatformConstructorFixture()
        {
        }

        [System.Runtime.Versioning.SupportedOSPlatformGuard("windows")]
        public static bool IsWindows => true;
    }
#endif

    [ComImport]
    [Guid("5E0D079B-34C4-4586-A933-46D1F9987E26")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ComInterfaceFixture
    {
    }

    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    private sealed class ComClassFixture
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MarshalAsFieldFixture
    {
        [MarshalAs(UnmanagedType.LPArray)]
        public int[] OmittedSize;

        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
        public int[] ExplicitZeroSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class SequentialClassLayoutFixture
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool Enabled;
    }

    private sealed class MarshalAsPropertyFixture
    {
        public bool Enabled
        {
            [return: MarshalAs(UnmanagedType.Bool)]
            get => true;
        }
    }

    [Obsolete("Warning-only compatibility contract")]
    private static void WarningObsoleteFixture()
    {
    }

    [DllImport(
        "native-test",
        EntryPoint = "native_entry",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true,
        BestFitMapping = false,
        ThrowOnUnmappableChar = true,
        PreserveSig = false)]
    private static extern int NativeImportFixture(string value);

    [DllImport(
        "native-test",
        EntryPoint = "native_entry",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true,
        PreserveSig = false)]
    private static extern int NativeImportOmittedOptionsFixture(string value);

    [DllImport(
        "native-test",
        EntryPoint = "native_entry",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true,
        BestFitMapping = false,
        ThrowOnUnmappableChar = false,
        PreserveSig = false)]
    private static extern int NativeImportDisabledOptionsFixture(string value);

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    private sealed class AttributeUsageFixtureAttribute : Attribute
    {
    }

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

    private sealed class SpecialNameMemberFixture
    {
        public SpecialNameMemberFixture()
        {
        }

        [SpecialName]
        public int Field = 1;

        [SpecialName]
        public int Property { get; set; }

        [SpecialName]
        public event EventHandler? Changed;

        [SpecialName]
        public void SpecialHook()
        {
        }

        public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
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

    private struct ReadonlyMemberFixture
    {
        private int _value;

        public readonly int Value => _value;

        public readonly int Read() => _value;

        public void Write(int value) => _value = value;
    }

    private struct ReadonlyOverrideFixture
    {
        private int _value;

        public readonly override string ToString() => _value.ToString();

        public void Write(int value) => _value = value;
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
        var scope = "instance";
        if (method is MethodInfo methodInfo && methodInfo.IsVirtual)
        {
            var isOverride = methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
            if (methodInfo.IsAbstract) scope = isOverride ? "abstract override" : "abstract";
            else if (isOverride) scope = methodInfo.IsFinal ? "sealed override" : "override";
            else if (!methodInfo.IsFinal) scope = "virtual";
        }
        return HasAttribute(method, "System.Runtime.CompilerServices.IsReadOnlyAttribute")
            ? scope + " readonly"
            : scope;
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
        return "F " + FieldAccess(field) + scope + " " + SpecialNameContract(field) + ObsoleteContract(field) + ExperimentalContract(field) + PlatformContract(field) + RequiredMember(field) + MarshalAsContract(field) + FixedBufferContract(field) + NullableFlowContract(field) + DynamicallyAccessedMembersContract(field) + FormatAnnotatedType(field.FieldType, field) + " " + field.Name + value;
    }

    private static string ExperimentalContract(params ICustomAttributeProvider?[] providers)
    {
        var contracts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var provider in providers.Where(value => value is not null))
        {
            foreach (var attribute in GetCustomAttributes(provider!).Where(value => string.Equals(
                         value.AttributeType.FullName,
                         "System.Diagnostics.CodeAnalysis.ExperimentalAttribute",
                         StringComparison.Ordinal)))
            {
                var diagnosticId = attribute.ConstructorArguments.Count == 1
                    ? attribute.ConstructorArguments[0].Value
                    : null;
                var url = attribute.NamedArguments.FirstOrDefault(value => value.MemberName == "UrlFormat").TypedValue.Value;
                contracts.Add("experimental(id=" + FormatDefault(diagnosticId) + ",url=" + FormatDefault(url) + ")");
            }
        }
        return contracts.Count == 0 ? string.Empty : string.Join(" ", contracts) + " ";
    }

    private static string PlatformContract(params ICustomAttributeProvider?[] providers)
    {
        var contracts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var provider in providers.Where(value => value is not null))
        {
            foreach (var attribute in GetCustomAttributes(provider!).Where(value =>
                         value.AttributeType.FullName is "System.Runtime.Versioning.SupportedOSPlatformAttribute"
                             or "System.Runtime.Versioning.UnsupportedOSPlatformAttribute"
                             or "System.Runtime.Versioning.ObsoletedOSPlatformAttribute"
                             or "System.Runtime.Versioning.SupportedOSPlatformGuardAttribute"
                             or "System.Runtime.Versioning.UnsupportedOSPlatformGuardAttribute"))
            {
                var name = attribute.AttributeType.Name.Replace("Attribute", string.Empty);
                var arguments = attribute.ConstructorArguments.Select(value => FormatDefault(value.Value));
                var named = attribute.NamedArguments
                    .OrderBy(value => value.MemberName, StringComparer.Ordinal)
                    .Select(value => value.MemberName + "=" + FormatDefault(value.TypedValue.Value));
                contracts.Add("platform(" + name + ":" + string.Join(",", arguments.Concat(named)) + ")");
            }
        }
        return contracts.Count == 0 ? string.Empty : string.Join(" ", contracts) + " ";
    }

    private static string ObsoleteContract(params ICustomAttributeProvider?[] providers)
    {
        var found = false;
        foreach (var provider in providers.Where(value => value is not null))
        {
            var attribute = GetCustomAttributes(provider!).FirstOrDefault(value =>
                string.Equals(value.AttributeType.FullName, typeof(ObsoleteAttribute).FullName, StringComparison.Ordinal));
            found |= attribute is not null;
            var isError = attribute is not null
                && attribute.ConstructorArguments.Count > 1
                && attribute.ConstructorArguments[1].Value is bool value
                && value;
            if (isError) return "error obsolete ";
        }
        return found ? "obsolete " : string.Empty;
    }

    private static string MarshalAsContract(ICustomAttributeProvider provider)
    {
        var attribute = provider.GetCustomAttributes(typeof(MarshalAsAttribute), inherit: false)
            .OfType<MarshalAsAttribute>()
            .SingleOrDefault();
        if (attribute is null)
        {
            return string.Empty;
        }

        return "marshal-as(type=" + attribute.Value
            + ",descriptor=" + ReadMarshalDescriptor(provider)
            + ",array-subtype=" + attribute.ArraySubType
            + ",size-const=" + attribute.SizeConst.ToString(CultureInfo.InvariantCulture)
            + ",size-param-index=" + attribute.SizeParamIndex.ToString(CultureInfo.InvariantCulture)
            + ",safe-array-subtype=" + attribute.SafeArraySubType
            + ",iid-param-index=" + attribute.IidParameterIndex.ToString(CultureInfo.InvariantCulture)
            + ",marshal-type=" + FormatDefault(attribute.MarshalType)
            + ",marshal-type-ref=" + (attribute.MarshalTypeRef is null ? "null" : FormatDefault(attribute.MarshalTypeRef.FullName))
            + ",marshal-cookie=" + FormatDefault(attribute.MarshalCookie)
            + ",safe-array-user-type=" + (attribute.SafeArrayUserDefinedSubType is null ? "null" : FormatDefault(attribute.SafeArrayUserDefinedSubType.FullName))
            + ") ";
    }

    private static string ReadMarshalDescriptor(ICustomAttributeProvider provider)
    {
        Module module;
        int metadataToken;
        switch (provider)
        {
            case ParameterInfo parameter:
                module = parameter.Member.Module;
                metadataToken = parameter.MetadataToken;
                break;
            case FieldInfo field:
                module = field.Module;
                metadataToken = field.MetadataToken;
                break;
            default:
                return "none";
        }

        using var stream = File.OpenRead(module.FullyQualifiedName);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var row = metadataToken & 0x00FFFFFF;
        var descriptor = (metadataToken & unchecked((int)0xFF000000)) switch
        {
            0x04000000 => reader.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(row)).GetMarshallingDescriptor(),
            0x08000000 => reader.GetParameter(MetadataTokens.ParameterHandle(row)).GetMarshallingDescriptor(),
            _ => default(BlobHandle)
        };
        if (descriptor.IsNil)
        {
            return "none";
        }

        return string.Concat(reader.GetBlobBytes(descriptor).Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
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
        => IsExternallyAccessibleMethod(method) && !IsPropertyOrEventAccessor(method);

    private static bool IsPropertyOrEventAccessor(MethodInfo method)
    {
        if (!method.IsSpecialName || method.DeclaringType is null) return false;
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        return method.DeclaringType.GetProperties(members)
                .Any(property => property.GetMethod == method || property.SetMethod == method)
            || method.DeclaringType.GetEvents(members)
                .Any(eventInfo => eventInfo.AddMethod == method || eventInfo.RemoveMethod == method
                    || eventInfo.RaiseMethod == method);
    }

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
        return MarshalAsContract(parameter) + ParameterDirectionContract(parameter) + NullableFlowContract(parameter) + DynamicallyAccessedMembersContract(parameter) + FormatParameterType(parameter) + " " + parameter.Name + suffix;
    }));

    private static string ParameterDirectionContract(ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsByRef) return string.Empty;
        return (parameter.IsIn ? "in-flag " : string.Empty)
            + (parameter.IsOut ? "out-flag " : string.Empty);
    }

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
        var safetyPrefix = MarshalAsContract(parameter) + NullableFlowContract(parameter) + DynamicallyAccessedMembersContract(parameter) + RefSafetyPrefix(parameter)
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
        var dynamicallyAccessedMembers = DynamicallyAccessedMembersContract(property);
        var getter = IsExternallyAccessibleMethod(property.GetMethod) ? property.GetMethod : null;
        var setter = IsExternallyAccessibleMethod(property.SetMethod) ? property.SetMethod : null;
        var getterFlow = getter is null
            ? string.Empty
            : NamedFlowContract("get", getter.ReturnParameter);
        var setterValue = setter?.GetParameters().LastOrDefault();
        var setterFlow = setterValue is null ? string.Empty : NamedFlowContract("set", setterValue);
        if (property.PropertyType.IsByRef && property.GetMethod is not null)
        {
            if (getter is not null) return propertyFlow + setterFlow + dynamicallyAccessedMembers + FormatReturnType(getter, property);
            var parameter = property.GetMethod.ReturnParameter;
            var safety = RefSafetyPrefix(parameter)
                + (HasAttribute(property, "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute") ? "unscoped " : string.Empty);
            var readOnly = HasAttribute(parameter, "System.Runtime.CompilerServices.IsReadOnlyAttribute")
                || parameter.GetRequiredCustomModifiers().Any(modifier => string.Equals(
                    modifier.FullName,
                    "System.Runtime.InteropServices.InAttribute",
                    StringComparison.Ordinal));
            return propertyFlow + setterFlow + dynamicallyAccessedMembers + safety + (readOnly ? "ref readonly " : "ref ")
                + FormatAnnotatedType(property.PropertyType.GetElementType()!, parameter);
        }
        var safetyPrefix = HasAttribute(property, "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute") ? "unscoped " : string.Empty;
        var returnMarshalling = getter is null ? string.Empty : MarshalAsContract(getter.ReturnParameter);
        return propertyFlow + getterFlow + setterFlow + dynamicallyAccessedMembers + safetyPrefix + returnMarshalling + FormatAnnotatedType(property.PropertyType, property);
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
            return prefix + DynamicallyAccessedMembersContract(argument) + argument.Name;
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
                new DynamicCursor(Array.Empty<bool>()),
                new NativeIntegerCursor(Array.Empty<bool>()));

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
#if NET10_0
        if (type.IsFunctionPointer) return FormatFunctionPointer(type);
#endif
        if (!type.IsGenericType) return type.FullName ?? type.Name;
        return FormatGenericTypeName(type, type.GetGenericArguments().Select(FormatType).ToArray());
    }

    private static string FormatAnnotatedType(Type type, ICustomAttributeProvider provider)
    {
        var flags = ReadNullableFlags(provider);
        var context = ReadNullableContext(provider);
        var tupleNames = ReadTupleNames(provider);
        var dynamicFlags = ReadDynamicFlags(provider);
        var nativeIntegerFlags = ReadNativeIntegerFlags(provider);
        return FormatAnnotatedType(type, new NullabilityCursor(flags, context), new TupleNameCursor(tupleNames), new DynamicCursor(dynamicFlags), new NativeIntegerCursor(nativeIntegerFlags));
    }

    private static string FormatAnnotatedType(Type type, NullabilityCursor cursor, TupleNameCursor tupleNames, DynamicCursor dynamicFlags, NativeIntegerCursor nativeIntegerFlags)
    {
        var flag = cursor.Next();
        var isDynamic = dynamicFlags.Next();
        var isNativeInteger = nativeIntegerFlags.Next();
#if NET10_0
        if (type.IsFunctionPointer) return FormatFunctionPointer(type, cursor, tupleNames, dynamicFlags, nativeIntegerFlags);
#endif
        if (type.IsArray)
        {
            var array = FormatAnnotatedType(type.GetElementType()!, cursor, tupleNames, dynamicFlags, nativeIntegerFlags) + ArraySuffix(type);
            return flag == 2 ? array + "?" : array;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (IsTupleDefinition(definition) && tupleNames.HasNames)
            {
                return FormatTuple(type, cursor, tupleNames, dynamicFlags, nativeIntegerFlags);
            }
            var arguments = type.GetGenericArguments()
                .Select(argument => FormatAnnotatedType(argument, cursor, tupleNames, dynamicFlags, nativeIntegerFlags))
                .ToArray();
            var formatted = FormatGenericTypeName(type, arguments);
            return !type.IsValueType && flag == 2 ? formatted + "?" : formatted;
        }

        var result = type == typeof(object) && isDynamic
            ? "dynamic"
            : isNativeInteger && type == typeof(IntPtr)
                ? "nint"
                : isNativeInteger && type == typeof(UIntPtr)
                    ? "nuint"
                    : type.FullName ?? type.Name;
        return (!type.IsValueType || type.IsGenericParameter) && flag == 2 ? result + "?" : result;
    }

#if NET10_0
    private static string FormatFunctionPointer(Type type)
    {
        var conventions = type.GetFunctionPointerCallingConventions()
            .Select(FormatType)
            .ToArray();
        var signature = type.GetFunctionPointerParameterTypes()
            .Select(FormatType)
            .Append(FormatType(type.GetFunctionPointerReturnType()));
        return "delegate* "
            + (conventions.Length == 0
                ? (type.IsUnmanagedFunctionPointer ? "unmanaged" : "managed")
                : "unmanaged[" + string.Join(",", conventions) + "]")
            + "<" + string.Join(",", signature) + ">";
    }

    private static string FormatFunctionPointer(Type type, NullabilityCursor cursor, TupleNameCursor tupleNames, DynamicCursor dynamicFlags, NativeIntegerCursor nativeIntegerFlags)
    {
        var conventions = type.GetFunctionPointerCallingConventions()
            .Select(FormatType)
            .ToArray();
        var signature = type.GetFunctionPointerParameterTypes()
            .Select(value => FormatAnnotatedType(value, cursor, tupleNames, dynamicFlags, nativeIntegerFlags))
            .Append(FormatAnnotatedType(type.GetFunctionPointerReturnType(), cursor, tupleNames, dynamicFlags, nativeIntegerFlags));
        return "delegate* "
            + (conventions.Length == 0
                ? (type.IsUnmanagedFunctionPointer ? "unmanaged" : "managed")
                : "unmanaged[" + string.Join(",", conventions) + "]")
            + "<" + string.Join(",", signature) + ">";
    }
#endif

    private static string FormatTuple(Type type, NullabilityCursor cursor, TupleNameCursor tupleNames, DynamicCursor dynamicFlags, NativeIntegerCursor nativeIntegerFlags)
    {
        var elements = new List<string>();
        AddTupleElements(type, cursor, tupleNames, dynamicFlags, nativeIntegerFlags, elements);
        return "(" + string.Join(", ", elements) + ")";
    }

    private static void AddTupleElements(
        Type tupleType,
        NullabilityCursor cursor,
        TupleNameCursor tupleNames,
        DynamicCursor dynamicFlags,
        NativeIntegerCursor nativeIntegerFlags,
        ICollection<string> elements)
    {
        var arguments = tupleType.GetGenericArguments();
        var logicalCount = arguments.Length == 8 ? 7 : arguments.Length;
        for (var index = 0; index < logicalCount; index++)
        {
            var elementName = tupleNames.Next();
            var formatted = FormatAnnotatedType(arguments[index], cursor, tupleNames, dynamicFlags, nativeIntegerFlags);
            elements.Add(formatted + (string.IsNullOrEmpty(elementName) ? string.Empty : " " + elementName));
        }

        if (arguments.Length == 8)
        {
            _ = cursor.Next();
            _ = dynamicFlags.Next();
            _ = nativeIntegerFlags.Next();
            AddTupleElements(arguments[7], cursor, tupleNames, dynamicFlags, nativeIntegerFlags, elements);
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

    private static bool[] ReadNativeIntegerFlags(ICustomAttributeProvider provider)
    {
        var attribute = GetCustomAttributes(provider).FirstOrDefault(value =>
            string.Equals(value.AttributeType.FullName, "System.Runtime.CompilerServices.NativeIntegerAttribute", StringComparison.Ordinal));
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

    private sealed class NativeIntegerCursor
    {
        private readonly bool[] _flags;
        private int _index;

        public NativeIntegerCursor(bool[] flags)
        {
            _flags = flags;
        }

        public bool Next() => _index < _flags.Length && _flags[_index++];
    }

#if NET10_0
    private static Type CreateNativeIntegerFixtureType()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("HomeAssistantX.NativeIntegerFixture." + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var attributeBuilder = module.DefineType(
            "System.Runtime.CompilerServices.NativeIntegerAttribute",
            TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic,
            typeof(Attribute));
        var attributeConstructor = attributeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            new[] { typeof(bool[]) });
        var attributeIl = attributeConstructor.GetILGenerator();
        attributeIl.Emit(OpCodes.Ldarg_0);
        attributeIl.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!);
        attributeIl.Emit(OpCodes.Ret);
        var attributeType = attributeBuilder.CreateType()!;
        var nativeConstructor = attributeType.GetConstructor(new[] { typeof(bool[]) })!;

        var fixtureBuilder = module.DefineType(
            "NativeIntegerFixture",
            TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic);
        var nestedType = typeof(IReadOnlyList<>).MakeGenericType(typeof(IntPtr[]));
        var methodBuilder = fixtureBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Private | MethodAttributes.Static,
            typeof(IntPtr),
            new[] { typeof(UIntPtr), nestedType });
        methodBuilder.DefineParameter(0, ParameterAttributes.Retval, null).SetCustomAttribute(
            new CustomAttributeBuilder(nativeConstructor, new object[] { new[] { true } }));
        methodBuilder.DefineParameter(1, ParameterAttributes.None, "value").SetCustomAttribute(
            new CustomAttributeBuilder(nativeConstructor, new object[] { new[] { true } }));
        methodBuilder.DefineParameter(2, ParameterAttributes.None, "nested").SetCustomAttribute(
            new CustomAttributeBuilder(nativeConstructor, new object[] { new[] { false, false, true } }));
        var methodIl = methodBuilder.GetILGenerator();
        methodIl.Emit(OpCodes.Ldsfld, typeof(IntPtr).GetField(nameof(IntPtr.Zero))!);
        methodIl.Emit(OpCodes.Ret);

        var field = fixtureBuilder.DefineField("Field", typeof(IntPtr), FieldAttributes.Public);
        field.SetCustomAttribute(new CustomAttributeBuilder(nativeConstructor, new object[] { new[] { true } }));

        var property = fixtureBuilder.DefineProperty("Property", PropertyAttributes.None, typeof(UIntPtr), Type.EmptyTypes);
        property.SetCustomAttribute(new CustomAttributeBuilder(nativeConstructor, new object[] { new[] { true } }));
        var getter = fixtureBuilder.DefineMethod(
            "get_Property",
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(UIntPtr),
            Type.EmptyTypes);
        var getterIl = getter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldsfld, typeof(UIntPtr).GetField(nameof(UIntPtr.Zero))!);
        getterIl.Emit(OpCodes.Ret);
        property.SetGetMethod(getter);

        return fixtureBuilder.CreateType()!;
    }

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
