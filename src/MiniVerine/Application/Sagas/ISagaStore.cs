using MiniVerine.Domain.Sagas;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Application.Sagas;

/// <summary>
/// Load and success-replace saga instances by type and id. Persistence owns durable rows later.
/// </summary>
public interface ISagaStore
{
    Saga? Load(Type sagaType, SagaId id);

    void Save(Type sagaType, SagaId id, Saga instance);
}
