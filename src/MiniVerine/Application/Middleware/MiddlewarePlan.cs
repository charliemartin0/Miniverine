namespace MiniVerine.Application.Middleware;

/// <summary>
/// Folder: Russian Doll around Execution. Same programming model after codegen later.
///
/// Put here: before/after wrappers, applied by message type or convention (not only
/// globally). Logging, validation, transactional outbox as middleware. Optional
/// FluentValidation-style hook.
///
/// Do not put here: Roslyn, Frame/Variable/MethodCall, or AssemblyLoadContext. Codegen
/// is a later swap behind this contract. Do not change how handlers are written when
/// you add it.
///
/// Prove with: a middleware runs around Handle without Handle knowing it exists.
/// </summary>
public sealed class MiddlewarePlan;
