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

internal sealed class StoryboardActionContext(
    AnimationHost runner,
    ViewContext host,
    ViewElement element,
    ViewScope? scope)
{
    private readonly Dictionary<StoryboardAction, (Storyboard Storyboard, StoryboardPlayer Player)> _players = [];

    internal AnimationHost Runner { get; } = runner;

    internal ViewContext Host { get; } = host;

    internal ViewElement Element { get; } = element;

    internal void Play(Storyboard storyboard, StoryboardAction action)
    {
        if (storyboard.IsEmpty) { return; }

        Stop(action);

        foreach ((StoryboardAction started, (Storyboard Started, StoryboardPlayer Player) entry) in _players.ToArray())
        {
            if (!entry.Started.SharesProperty(storyboard)) { continue; }

            entry.Player.Stop();
            _players.Remove(started);
        }

        _players[action] = (storyboard, Runner.Play(storyboard, Element, scope, Host));
    }

    internal void Stop(StoryboardAction? action)
    {
        if (action is { } target)
        {
            if (_players.Remove(target, out (Storyboard Storyboard, StoryboardPlayer Player) player)) { player.Player.Stop(); }

            return;
        }

        foreach ((Storyboard _, StoryboardPlayer player) in _players.Values) { player.Stop(); }

        _players.Clear();
    }

    internal void Set(Setter setter)
    {
        if (Element.TryApplyMarkup(setter.Property, setter.Value, ValueSource.Trigger, out string message)) { return; }

        Host.Diagnostics.Report($"the action on '{setter.Property}' failed: {message}", Element.Node);
    }
}
