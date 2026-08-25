namespace HomeAssistantX.Models;

/// <summary>A state transition received live or discovered while reconciling after reconnect.</summary>
public sealed class HomeAssistantStateChange
{
    public HomeAssistantStateChange(
        string entityId,
        HomeAssistantState? previousState,
        HomeAssistantState? currentState,
        bool isReconciliation = false)
    {
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        PreviousState = previousState;
        CurrentState = currentState;
        IsReconciliation = isReconciliation;
    }

    public string EntityId { get; }

    public HomeAssistantState? PreviousState { get; }

    public HomeAssistantState? CurrentState { get; }

    public bool IsRemoval => CurrentState is null;

    public bool IsReconciliation { get; }
}
