using System.Globalization;
using System.Reflection;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Domain.Sagas;

/// <summary>
/// Which saga instance a message belongs to. [SagaIdentity], then {SagaType}Id, then Id. No match is empty.
/// </summary>
public static class SagaIdentityNaming
{
    public static SagaId For(object message, Type sagaType)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(sagaType);

        var properties = message.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var attributed = Array.Find(
            properties,
            property => property.GetCustomAttribute<SagaIdentityAttribute>(inherit: true) is not null);
        if (attributed is not null)
        {
            return ToSagaId(attributed.GetValue(message));
        }

        var bySagaTypeName = Array.Find(properties, property => property.Name == sagaType.Name + "Id");
        if (bySagaTypeName is not null)
        {
            return ToSagaId(bySagaTypeName.GetValue(message));
        }

        var byId = Array.Find(properties, property => property.Name == "Id");
        if (byId is not null)
        {
            return ToSagaId(byId.GetValue(message));
        }

        return new SagaId("");
    }

    private static SagaId ToSagaId(object? value)
    {
        if (value is null)
        {
            return new SagaId("");
        }

        return new SagaId(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
    }
}
