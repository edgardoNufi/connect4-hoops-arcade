using Microsoft.AspNetCore.Components;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Components;

/// <summary>
/// Base for components that must re-render whenever <see cref="GameSession"/> state changes.
/// Blazor only re-renders the component whose event fired (plus EventCallback receivers), so
/// sibling components that read the GameSession singleton directly must subscribe themselves.
/// </summary>
public abstract class SessionComponentBase : ComponentBase, IDisposable
{
    [Inject] protected GameSession Session { get; set; } = default!;

    protected override void OnInitialized() => Session.StateChanged += OnSessionChanged;

    private void OnSessionChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => Session.StateChanged -= OnSessionChanged;
}
