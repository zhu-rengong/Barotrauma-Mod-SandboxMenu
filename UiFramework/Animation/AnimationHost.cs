namespace UiFramework.Animation;

internal sealed class AnimationHost : IViewRuntime
{
    private readonly List<StoryboardPlayer> _players = [];
    private readonly Dictionary<IViewElement, List<StoryboardPlayer>> _targets = [];

    public IAnimationPlayer Play(IStoryboard storyboard, IViewElement target, IPauseable? scope, IViewHost host)
    {
        StoryboardPlayer player = new((Storyboard)storyboard, target, scope, host, OnStopped);

        _players.Add(player);
        Players(target).Add(player);

        return player;
    }

    public void Tick()
    {
        if (_players.Count == 0) { return; }

        float delta = AnimationClock.Delta;

        for (int i = _players.Count - 1; i >= 0; i--)
        {
            StoryboardPlayer player = _players[i];

            if (!UiGuard.TryRun((Player: player, Delta: delta), static state => state.Player.Tick(state.Delta)))
            {
                player.Stop();
                _players.RemoveAt(i);
            }
        }
    }

    public void Reset(IViewElement target)
    {
        if (!_targets.TryGetValue(target, out List<StoryboardPlayer>? players)) { return; }

        foreach (StoryboardPlayer player in players.ToArray()) { player.Stop(); }
    }

    public void StopAll()
    {
        foreach (StoryboardPlayer player in _players.ToArray()) { UiGuard.Run(player.Stop); }

        _players.Clear();
        _targets.Clear();
    }

    private List<StoryboardPlayer> Players(IViewElement target)
    {
        if (_targets.TryGetValue(target, out List<StoryboardPlayer>? players)) { return players; }

        _targets[target] = players = [];
        return players;
    }

    private void OnStopped(StoryboardPlayer player)
    {
        if (!_targets.TryGetValue(player.Target, out List<StoryboardPlayer>? players)) { return; }

        players.Remove(player);

        if (players.Count == 0) { _targets.Remove(player.Target); }
    }
}
