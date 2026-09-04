using System.Reflection;

namespace MiniVerine.Application.Discovery;

/// <summary>
/// Whether a method is a handler by convention. Handle / HandleAsync / Consume / ConsumeAsync / Start / StartAsync.
/// First parameter is the message; extra parameters are injection slots.
/// </summary>
public static class HandlerConvention
{
    private static readonly HashSet<string> HandlerNames =
    [
        "Handle",
        "HandleAsync",
        "Consume",
        "ConsumeAsync",
        "Start",
        "StartAsync"
    ];

    public static DiscoveredHandler? For(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (method.IsPublic is false)
        {
            return null;
        }

        if (!HandlerNames.Contains(method.Name))
        {
            return null;
        }

        if (method.IsGenericMethodDefinition is true)
        {
            return null;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return null;
        }

        Type? handlerType = method.DeclaringType;
        if (handlerType is null)
        {
            return null;
        }

        return new DiscoveredHandler(
            method,
            handlerType,
            parameters[0].ParameterType,
            method.IsStatic,
            parameters[1..]);
    }
}
