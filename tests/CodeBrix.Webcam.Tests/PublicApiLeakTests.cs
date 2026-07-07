using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

/// <summary>
/// THE NO-LEAK RULE, AS A TEST: no consumer of CodeBrix.Webcam should ever have to
/// reference a CodeBrix.Platform.MediaPlayerCore.* type. This test reflects over every
/// publicly reachable signature in the CodeBrix.Webcam assembly — base types, implemented
/// interfaces, method parameters and returns, property/field/event types, generic
/// arguments and constraints, and nested types — and fails if any engine type appears.
/// If this test fails, the public API has leaked the engine dependency; fix the API,
/// never the test.
/// </summary>
public class PublicApiLeakTests
{
    private const string ForbiddenNamespacePrefix = "CodeBrix.Platform.MediaPlayerCore";

    [Fact]
    public void Public_api_surface_never_exposes_a_media_engine_type()
    {
        //Arrange
        var assembly = typeof(WebcamSession).Assembly;
        var leaks = new List<string>();

        //Act
        foreach (var type in assembly.GetExportedTypes())
        {
            InspectType(type, leaks);
        }

        //Assert
        string.Join(Environment.NewLine, leaks).Should().Be(string.Empty);
        leaks.Count.Should().Be(0);
    }

    private static void InspectType(Type type, List<string> leaks)
    {
        CheckType(type.BaseType, $"{type.FullName} base type", leaks);
        foreach (var implementedInterface in type.GetInterfaces())
        {
            CheckType(implementedInterface, $"{type.FullName} implements", leaks);
        }
        foreach (var genericArgument in SafeGenericArguments(type))
        {
            foreach (var constraint in genericArgument.GetGenericParameterConstraints())
            {
                CheckType(constraint, $"{type.FullName} generic constraint", leaks);
            }
        }

        const BindingFlags publicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(publicMembers))
        {
            CheckType(method.ReturnType, $"{type.FullName}.{method.Name} return", leaks);
            foreach (var parameter in method.GetParameters())
            {
                CheckType(parameter.ParameterType,
                    $"{type.FullName}.{method.Name}({parameter.Name})", leaks);
            }
        }
        foreach (var property in type.GetProperties(publicMembers))
        {
            CheckType(property.PropertyType, $"{type.FullName}.{property.Name}", leaks);
        }
        foreach (var field in type.GetFields(publicMembers))
        {
            CheckType(field.FieldType, $"{type.FullName}.{field.Name}", leaks);
        }
        foreach (var eventInfo in type.GetEvents(publicMembers))
        {
            CheckType(eventInfo.EventHandlerType, $"{type.FullName}.{eventInfo.Name}", leaks);
        }
        foreach (var constructor in type.GetConstructors(publicMembers))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                CheckType(parameter.ParameterType,
                    $"{type.FullName}..ctor({parameter.Name})", leaks);
            }
        }
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
        {
            InspectType(nested, leaks);
        }
    }

    private static void CheckType(Type type, string location, List<string> leaks)
    {
        if (type == null)
        {
            return;
        }
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            CheckType(type.GetElementType(), location, leaks);
            return;
        }
        if (type.IsConstructedGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                CheckType(argument, location, leaks);
            }
            CheckType(type.GetGenericTypeDefinition(), location, leaks);
            return;
        }
        var ns = type.Namespace;
        if (ns != null && ns.StartsWith(ForbiddenNamespacePrefix, StringComparison.Ordinal))
        {
            leaks.Add($"{location} exposes {type.FullName}");
        }
    }

    private static IEnumerable<Type> SafeGenericArguments(Type type)
        => type.IsGenericTypeDefinition ? type.GetGenericArguments() : Enumerable.Empty<Type>();
}
