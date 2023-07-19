namespace Shane32.AsyncResetEvents;

/// <summary>
/// An internal interface for <see cref="AsyncDelegatePump"/>.
/// </summary>
public interface IDelegateTuple
{
    /// <summary>
    /// Executes the delegate.
    /// </summary>
    Task ExecuteAsync();
}
