using UiFramework.Animation;
using UiFramework.Data;

namespace UiFramework.Styling;

internal abstract class TriggerBase
{
    internal int Priority { get; init; }

    internal IReadOnlyList<Setter> Setters { get; init; } = [];

    internal IReadOnlyList<StoryboardAction> EnterActions { get; init; } = [];

    internal IReadOnlyList<StoryboardAction> ExitActions { get; init; } = [];

    internal abstract void Attach(TriggerContext context);
}

internal sealed class PropertyTrigger : TriggerBase
{
    internal required string Property { get; init; }

    internal required MarkupValue Value { get; init; }

    internal override void Attach(TriggerContext context)
    {
        TriggerRuntime runtime = new(context, this);
        context.Watch(Property, runtime.Match(Value.Raw));
    }
}

internal sealed class DataTrigger : TriggerBase
{
    internal required string Path { get; init; }

    internal required MarkupValue Value { get; init; }

    internal override void Attach(TriggerContext context)
    {
        TriggerRuntime runtime = new(context, this);
        context.Bind(Path, runtime.Match(Value.Raw));
    }
}

internal sealed class MultiDataTrigger : TriggerBase
{
    internal required IReadOnlyList<Condition> Conditions { get; init; }

    internal override void Attach(TriggerContext context)
    {
        TriggerRuntime runtime = new(context, this);
        runtime.FollowConditions(context, Conditions);
    }
}

internal sealed class EventTrigger
{
    internal required string Event { get; init; }

    internal IReadOnlyList<StoryboardAction> Actions { get; init; } = [];

    internal void Attach(TriggerContext context)
    {
        if (Actions.Count == 0) { return; }

        StoryboardActionContext actions = context.Actions();

        context.Listen(Event, () =>
        {
            foreach (StoryboardAction action in Actions)
            {
                StoryboardAction current = action;
                UiGuard.Run(() => current.Run(actions));
            }
        });
    }
}

internal sealed record Condition(string? Path, string? Property, MarkupValue Value);

internal sealed class TriggerContext(
    IViewHost view,
    IViewRuntime runtime,
    IViewElement element,
    MarkupNode node,
    IPauseable? scope)
{
    internal IViewHost View { get; } = view;

    internal IViewRuntime Runtime { get; } = runtime;

    internal IViewElement Element { get; } = element;

    internal MarkupNode Node { get; } = node;

    internal IPauseable? Scope { get; } = scope;

    internal StoryboardActionContext Actions() => new(Runtime, View, Element, Scope);

    internal void Bind(string path, Action<object?> match)
    {
        View.Own(Binding.Attach(new BindingDefinition
        {
            Source = Element.DataContext,
            Path = path,
            Mode = BindingMode.OneWay,
            TargetType = typeof(object),
            Apply = value => UiGuard.Run(() => match(value)),
            Report = message => View.Diagnostics.Report(message, Node)
        }));
    }

    internal void Watch(string property, Action<object?> match)
    {
        if (Element.WatchValue(property, match)) { return; }

        View.Diagnostics.Report(
            $"<{Node.Name}> cannot watch '{property}'; a property trigger reads a visual state"
            + " (IsMouseOver, IsPressed, IsSelected) or a property the framework writes",
            Node);
    }

    internal void Listen(string name, Action handler)
    {
        if (Element.Listen(name, handler)) { return; }

        View.Diagnostics.Report(
            $"<{Node.Name}> has no event '{name}'; it raises Loaded, Click, MouseEnter, MouseLeave, Pressed and Released",
            Node);
    }

    internal void Report(string message) => View.Diagnostics.Report(message, Node);
}

internal sealed class TriggerRuntime(TriggerContext context, TriggerBase trigger)
{
    private readonly StoryboardActionContext _actions = context.Actions();

    private readonly List<(Condition Condition, object? Value)> _conditions = [];
    private readonly bool _checked = Check(context, trigger);

    private bool _active;
    private bool _evaluating;

    private static bool Check(TriggerContext context, TriggerBase trigger)
    {
        foreach (Setter setter in trigger.Setters)
        {
            if (context.Element.CanOverride(setter.Property, out string reason)) { continue; }

            context.Report($"a trigger sets '{setter.Property}': {reason}");
        }

        return true;
    }

    internal Action<object?> Match(string expected) => value => Update(expected, value);

    internal void FollowConditions(TriggerContext triggerContext, IReadOnlyList<Condition> conditions)
    {
        foreach (Condition condition in conditions)
        {
            _conditions.Add((condition, null));

            if (condition.Property is { Length: > 0 } property)
            {
                triggerContext.Watch(property, Track(condition));
                continue;
            }

            if (condition.Path is { Length: > 0 } path)
            {
                triggerContext.Bind(path, Track(condition));
                continue;
            }

            triggerContext.Report("a condition needs a Binding or a Property");
        }
    }

    internal void Activate()
    {
        if (_active || _evaluating) { return; }

        _evaluating = true;

        try
        {
            _active = true;

            foreach (Setter setter in trigger.Setters)
            {
                if (context.Element.ApplyOverride(this, trigger.Priority, setter.Property, setter.Value, out string message)) { continue; }

                context.Report($"a trigger sets '{setter.Property}': {message}");
            }

            foreach (StoryboardAction action in trigger.EnterActions)
            {
                StoryboardAction current = action;
                UiGuard.Run(() => current.Run(_actions));
            }
        }
        finally
        {
            _evaluating = false;
        }
    }

    internal void Deactivate()
    {
        if (!_active || _evaluating) { return; }

        _evaluating = true;

        try
        {
            _active = false;

            foreach (Setter setter in trigger.Setters)
            {
                context.Element.RemoveOverride(this, setter.Property);
            }

            foreach (StoryboardAction action in trigger.ExitActions)
            {
                StoryboardAction current = action;
                UiGuard.Run(() => current.Run(_actions));
            }
        }
        finally
        {
            _evaluating = false;
        }
    }

    private Action<object?> Track(Condition condition)
        => value =>
        {
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (!ReferenceEquals(_conditions[i].Condition, condition)) { continue; }

                _conditions[i] = (condition, value);
                break;
            }

            if (_evaluating) { return; }

            bool matches = true;

            foreach ((Condition candidate, object? current) in _conditions)
            {
                if (candidate.Value.Raw.Length == 0) { continue; }

                if (string.Equals(current?.ToString(), candidate.Value.Raw, StringComparison.OrdinalIgnoreCase)) { continue; }

                matches = false;
                break;
            }

            if (matches) { Activate(); }
            else { Deactivate(); }
        };

    private void Update(string expected, object? value)
    {
        if (_evaluating) { return; }

        if (string.Equals(value?.ToString(), expected, StringComparison.OrdinalIgnoreCase)) { Activate(); }
        else { Deactivate(); }
    }
}

internal static class TriggerReader
{
    internal static TriggerBase? Read(MarkupNode node, Action<string>? report)
    {
        return node.Name switch
        {
            "Trigger" or "PropertyTrigger" => ReadProperty(node, report),
            "DataTrigger" => ReadData(node, report),
            "MultiDataTrigger" => ReadMulti(node, report),
            _ => Unknown(node, report)
        };
    }

    internal static EventTrigger? ReadEvent(MarkupNode node, Action<string>? report)
    {
        string? name = node.Text("Event");

        if (name is not { Length: > 0 })
        {
            AnimationMarkup.Report(node, report, "an event trigger needs an Event");
            return null;
        }

        MarkupNode source = node.Child("Actions") ?? node;
        List<StoryboardAction> actions = [.. AnimationMarkup.ReadActions(source, report)];

        return actions.Count == 0 ? null : new EventTrigger { Event = name, Actions = actions };
    }

    internal static IReadOnlyList<Setter> ReadSetters(MarkupNode node)
    {
        List<Setter> setters = [];

        foreach (MarkupNode child in node.Content)
        {
            if (!string.Equals(child.Name, "Setter", StringComparison.OrdinalIgnoreCase)) { continue; }

            if (child.Text("Property") is { } property && child.Value("Value") is { } value)
            {
                setters.Add(new Setter(property, value));
            }
        }

        return setters;
    }

    private static TriggerBase? ReadProperty(MarkupNode node, Action<string>? report)
    {
        string? property = node.Text("Property");

        if (property is not { Length: > 0 })
        {
            AnimationMarkup.Report(node, report, "a property trigger needs a Property");
            return null;
        }

        if (node.Value("Value") is not { } value)
        {
            AnimationMarkup.Report(node, report, $"the trigger on '{property}' needs a Value");
            return null;
        }

        return new PropertyTrigger
        {
            Property = property,
            Value = value,
            Priority = ReadPriority(node),
            Setters = ReadSetters(node),
            EnterActions = ReadEnter(node, report),
            ExitActions = ReadExit(node, report)
        };
    }

    private static TriggerBase? ReadData(MarkupNode node, Action<string>? report)
    {
        string? path = node.Text("Path") ?? node.Text("Binding");

        if (path is not { Length: > 0 })
        {
            AnimationMarkup.Report(node, report, "a data trigger needs a Binding");
            return null;
        }

        if (node.Value("Value") is not { } value)
        {
            AnimationMarkup.Report(node, report, $"the trigger on '{path}' needs a Value");
            return null;
        }

        return new DataTrigger
        {
            Path = path,
            Value = value,
            Priority = ReadPriority(node),
            Setters = ReadSetters(node),
            EnterActions = ReadEnter(node, report),
            ExitActions = ReadExit(node, report)
        };
    }

    private static TriggerBase? ReadMulti(MarkupNode node, Action<string>? report)
    {
        List<Condition> conditions = [];

        foreach (MarkupNode child in (node.Child("Conditions") ?? node).Content)
        {
            if (!string.Equals(child.Name, "Condition", StringComparison.OrdinalIgnoreCase)) { continue; }

            if (child.Value("Value") is not { } value)
            {
                AnimationMarkup.Report(child, report, "a condition needs a Value");
                continue;
            }

            conditions.Add(new Condition(child.Text("Binding") ?? child.Text("Path"), child.Text("Property"), value));
        }

        if (conditions.Count == 0)
        {
            AnimationMarkup.Report(node, report, "a multi data trigger needs at least one condition");
            return null;
        }

        return new MultiDataTrigger
        {
            Conditions = conditions,
            Priority = ReadPriority(node),
            Setters = ReadSetters(node),
            EnterActions = ReadEnter(node, report),
            ExitActions = ReadExit(node, report)
        };
    }

    private static int ReadPriority(MarkupNode node)
        => ValueParser.TryInt(node.Text("Priority"), out int priority) ? priority : 0;

    private static IReadOnlyList<StoryboardAction> ReadEnter(MarkupNode node, Action<string>? report)
        => node.Child("EnterActions") is { } enter ? AnimationMarkup.ReadActions(enter, report) : [];

    private static IReadOnlyList<StoryboardAction> ReadExit(MarkupNode node, Action<string>? report)
        => node.Child("ExitActions") is { } exit ? AnimationMarkup.ReadActions(exit, report) : [];

    private static TriggerBase? Unknown(MarkupNode node, Action<string>? report)
    {
        AnimationMarkup.Report(node, report, $"<{node.Name}> is not a trigger");
        return null;
    }
}
