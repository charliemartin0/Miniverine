using System.Reflection;
using System.Runtime.ExceptionServices;
using MiniVerine.Application.Discovery;
using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Envelope.ValueObjects;
using MiniVerine.Domain.Errors.ValueObjects;

namespace MiniVerine.Application.Execution;

/// <summary>
/// Wrap one handler call: Attempts, typed error policy, missing handler. Fan-out is one wrap per handler.
/// </summary>
public sealed class Executor
{
    private readonly ErrorPolicyCatalog _policies;
    private readonly IErrorQueue? _errorQueue;
    private readonly IMissingHandler? _missingHandler;

    public Executor(
        ErrorPolicyCatalog policies,
        IErrorQueue? errorQueue = null,
        IMissingHandler? missingHandler = null)
    {
        ArgumentNullException.ThrowIfNull(policies);
        _policies = policies;
        _errorQueue = errorQueue;
        _missingHandler = missingHandler;
    }

    public async Task<object?> InvokeAsync(
        Envelope envelope,
        DiscoveredHandler handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(handler);

        Envelope current = envelope;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await InvokeOnce(current, handler, cancellationToken);
            }
            catch (Exception exception)
            {
                Exception fault = Unwrap(exception);
                if (fault is OperationCanceledException)
                {
                    ExceptionDispatchInfo.Capture(fault).Throw();
                    throw;
                }

                current = await NextAttempt(current, handler, fault, cancellationToken);
            }
        }
    }

    public Task HandleMissingAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (_missingHandler is null)
        {
            throw new HandlerNotFound(envelope.Message.Value.GetType());
        }

        return _missingHandler.HandleAsync(envelope, cancellationToken);
    }

    private async Task<Envelope> NextAttempt(
        Envelope current,
        DiscoveredHandler handler,
        Exception fault,
        CancellationToken cancellationToken)
    {
        ErrorPolicyLookup lookup = _policies.For(fault.GetType(), handler.MessageClrType);
        if (lookup is not FoundErrorPolicy found)
        {
            throw new HandlerFault(fault);
        }

        int index = current.Attempts.Value - 1;
        if (index < 0 || index >= found.Actions.Count)
        {
            throw new HandlerFault(fault);
        }

        switch (found.Actions[index])
        {
            case Retry:
                return WithNextAttempt(current);
            case RetryWithCooldown cooldown:
                if (cooldown.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(cooldown.Delay, cancellationToken);
                }

                return WithNextAttempt(current);
            case MoveToErrorQueue:
                _errorQueue?.Move(current);
                throw new HandlerFault(fault);
            default:
                throw new HandlerFault(fault);
        }
    }

    private static Envelope WithNextAttempt(Envelope current) =>
        current with { Attempts = new Attempts(current.Attempts.Value + 1) };

    private static async Task<object?> InvokeOnce(
        Envelope envelope,
        DiscoveredHandler handler,
        CancellationToken cancellationToken)
    {
        object? target = handler.IsStatic ? null : Activator.CreateInstance(handler.HandlerType);
        object? result;
        try
        {
            result = handler.Method.Invoke(target, BindArguments(envelope, handler, cancellationToken));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        if (result is Task task)
        {
            await task;
        }

        return result;
    }

    private static object?[] BindArguments(
        Envelope envelope,
        DiscoveredHandler handler,
        CancellationToken cancellationToken)
    {
        object?[] arguments = new object?[1 + handler.InjectionSlots.Count];
        arguments[0] = envelope.Message.Value;
        for (int i = 0; i < handler.InjectionSlots.Count; i++)
        {
            Type parameterType = handler.InjectionSlots[i].ParameterType;
            if (parameterType == typeof(Envelope))
            {
                arguments[i + 1] = envelope;
            }
            else if (parameterType == typeof(CancellationToken))
            {
                arguments[i + 1] = cancellationToken;
            }
        }

        return arguments;
    }

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
}
