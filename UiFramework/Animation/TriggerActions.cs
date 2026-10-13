namespace UiFramework.Animation;

internal abstract record StoryboardAction
{
    internal abstract void Run(StoryboardActionContext context);
}

internal sealed record BeginStoryboardAction(Storyboard Storyboard) : StoryboardAction
{
    internal override void Run(StoryboardActionContext context) => context.Play(Storyboard, this);
}

internal sealed record StopStoryboardAction : StoryboardAction
{
    internal override void Run(StoryboardActionContext context) => context.Stop(null);
}

internal sealed record SetAction(Setter Setter) : StoryboardAction
{
    internal override void Run(StoryboardActionContext context) => context.Set(Setter);
}

internal sealed class StoryboardActionContext(ViewContext view, ViewElement element, ViewScope? scope)
{
    private readonly Dictionary<StoryboardAction, StoryboardPlayer> _players = [];

    internal ViewContext View { get; } = view;

    internal ViewElement Element { get; } = element;

    internal void Play(Storyboard storyboard, StoryboardAction action)
    {
        if (storyboard.IsEmpty) { return; }

        Stop(action);

        foreach ((StoryboardAction started, StoryboardPlayer player) in _players.ToArray())
        {
            if (!player.SharesProperty(storyboard)) { continue; }

            player.Stop();
            _players.Remove(started);
        }

        _players[action] = View.Play(storyboard, Element, scope);
    }

    internal void Stop(StoryboardAction? action)
    {
        if (action is { } target)
        {
            if (_players.Remove(target, out StoryboardPlayer? player)) { player.Stop(); }

            return;
        }

        foreach (StoryboardPlayer player in _players.Values) { player.Stop(); }

        _players.Clear();
    }

    internal void Set(Setter setter)
    {
        if (!Element.TryApplyMarkup(setter.Property, setter.Value, ValueSource.Trigger, out string message))
        {
            View.Diagnostics.Report($"the action on '{setter.Property}' failed: {message}", Element.Node);
        }
    }
}
