namespace UiFramework.Animation;

internal static class AnimationClock
{
    internal static float Delta { get; private set; } = 1f / 60f;

    internal static void Advance(float delta) => Delta = Math.Clamp(delta, 0f, 0.25f);
}

internal sealed class Storyboard
{
    internal string? Key { get; init; }

    internal IReadOnlyList<AnimationBase> Animations { get; init; } = [];

    internal bool IsEmpty => Animations.Count == 0;
}

internal sealed class StoryboardPlayer
{
    private readonly Storyboard _storyboard;
    private readonly ViewElement _target;
    private readonly ViewScope? _scope;
    private readonly ViewContext? _view;
    private readonly float[] _elapsed;
    private readonly object?[] _start;
    private readonly bool[] _settled;

    private bool _stopped;

    internal StoryboardPlayer(Storyboard storyboard, ViewElement target, ViewScope? scope, ViewContext? view)
    {
        _storyboard = storyboard;
        _target = target;
        _scope = scope;
        _view = view;
        _elapsed = new float[storyboard.Animations.Count];
        _start = new object?[storyboard.Animations.Count];
        _settled = new bool[storyboard.Animations.Count];

        for (int i = 0; i < storyboard.Animations.Count; i++)
        {
            AnimationBase animation = storyboard.Animations[i];

            if (!_target.CanAnimate(animation.Property, out string message))
            {
                _stopped = true;
                _view?.Diagnostics.Report($"the animation on '{animation.Property}' cannot play: {message}", _target.Node);
                continue;
            }

            bool captured = _target.TryReadValue(animation.Property, out _start[i]);

            if (captured && !_target.HasBaseValue(animation.Property))
            {
                _target.RememberRestoreValue(animation.Property, _start[i]);
            }
        }
    }

    internal bool SharesProperty(Storyboard storyboard)
    {
        foreach (AnimationBase mine in _storyboard.Animations)
        {
            foreach (AnimationBase other in storyboard.Animations)
            {
                if (string.Equals(mine.Property, other.Property, StringComparison.Ordinal)) { return true; }
            }
        }

        return false;
    }

    internal bool Tick(float delta)
    {
        if (_stopped) { return false; }

        if (_scope is { IsPaused: true }) { return true; }

        bool running = false;

        for (int i = 0; i < _storyboard.Animations.Count; i++)
        {
            if (_settled[i]) { continue; }

            _elapsed[i] += delta;

            if (Advance(_storyboard.Animations[i], i)) { running = true; }
        }

        return running;
    }

    internal void Stop()
    {
        if (_stopped) { return; }

        _stopped = true;
        _target.UntrackPlayer(this);

        for (int i = 0; i < _storyboard.Animations.Count; i++)
        {
            if (_settled[i]) { continue; }

            _settled[i] = true;

            if (_storyboard.Animations[i].Fill == FillBehavior.Stop)
            {
                _target.RestoreBaseValue(_storyboard.Animations[i].Property);
            }
        }
    }

    private bool Advance(AnimationBase animation, int index)
    {
        float local = _elapsed[index] - animation.BeginTime;

        if (local < 0f) { return true; }

        float duration = animation.Duration;

        if (duration <= 0f)
        {
            Settle(animation, index);
            return false;
        }

        float cycle = animation.TotalSeconds;

        if (cycle <= 0f)
        {
            Settle(animation, index);
            return false;
        }

        int completed = (int)(local / cycle);

        if (animation.Repeat.IsFinished(completed))
        {
            Settle(animation, index);
            return false;
        }

        float within = local - completed * cycle;
        float progress = within / duration;

        if (progress > 1f) { progress = 2f - progress; }

        Apply(animation, animation.ValueAt(progress, _start[index]));

        return true;
    }

    private void Settle(AnimationBase animation, int index)
    {
        _settled[index] = true;

        if (animation.Fill == FillBehavior.HoldEnd)
        {
            Apply(animation, animation.ValueAt(1f, _start[index]));
            return;
        }

        _target.RestoreBaseValue(animation.Property);
    }

    private void Apply(AnimationBase animation, object? value)
    {
        if (!_target.TryApplyValue(animation.Property, value, ValueSource.Animation, out string message))
        {
            _target.RestoreBaseValue(animation.Property);
            _stopped = true;
            _view?.Diagnostics.Report($"the animation on '{animation.Property}' could not be played: {message}", _target.Node);
        }
    }
}
