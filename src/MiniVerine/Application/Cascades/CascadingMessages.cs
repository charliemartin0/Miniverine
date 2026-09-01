using System.Collections;
using System.Runtime.CompilerServices;
using MiniVerine.Domain.Sagas;

namespace MiniVerine.Application.Cascades;

/// <summary>
/// Unpack a handler return value into outgoing message bodies. Emit only after success.
/// </summary>
public static class CascadingMessages
{
    public static IReadOnlyList<object> From(object? handlerResult)
    {
        var outgoing = new List<object>();
        Append(outgoing, handlerResult);
        return outgoing;
    }

    private static void Append(List<object> outgoing, object? value)
    {
        switch (value)
        {
            case null:
            case Saga:
            case Domain.Envelope.Envelope:
                return;
            case Task task:
                AppendCompletedTask(outgoing, task);
                return;
            case string:
                outgoing.Add(value);
                return;
            case OutgoingMessages messages:
                foreach (object message in messages)
                {
                    Append(outgoing, message);
                }

                return;
            case ITuple tuple:
                for (int i = 0; i < tuple.Length; i++)
                {
                    Append(outgoing, tuple[i]);
                }

                return;
            case IEnumerable enumerable:
                foreach (object? item in enumerable)
                {
                    Append(outgoing, item);
                }

                return;
            default:
                outgoing.Add(value);
                return;
        }
    }

    private static void AppendCompletedTask(List<object> outgoing, Task task)
    {
        if (task is not { IsCompletedSuccessfully: true })
        {
            return;
        }

        Type taskType = task.GetType();
        if (!taskType.IsGenericType)
        {
            return;
        }

        Type resultType = taskType.GetGenericArguments()[0];
        if (!resultType.IsPublic)
        {
            return;
        }

        object? result = taskType.GetProperty(nameof(Task<object>.Result))?.GetValue(task);
        Append(outgoing, result);
    }
}
