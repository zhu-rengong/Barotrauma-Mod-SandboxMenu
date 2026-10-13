using System.Windows.Input;
using UiFramework.Styling;

namespace UiFramework;

internal static class ViewBuilder
{
    private sealed class TriggerSet
    {
        internal List<TriggerBase> Triggers { get; private set; } = [];

        internal List<EventTrigger> Events { get; private set; } = [];

        internal void Replace(List<TriggerBase> triggers, List<EventTrigger> events)
        {
            if (triggers.Count > 0) { Triggers = triggers; }
            if (events.Count > 0) { Events = events; }
        }
    }

    internal static ViewElement BuildElement(ViewContext view, MarkupNode node, RectTransform? parent, object? dataContext = null, ViewElement? parentElement = null)
    {
        if (ElementRegistry.Find(node.Name) is not { } metadata)
        {
            view.Diagnostics.Report($"unknown element <{node.Name}>", node);
            return Owned(view, new PlaceholderElement(node, parent ?? GUI.Canvas));
        }

        ViewElement element;

        try
        {
            element = metadata.Create(new ElementContext(node, parent ?? GUI.Canvas, view));
        }
        catch (Exception e)
        {
            view.Diagnostics.Report($"<{node.Name}> could not be built ({e.Message})", node);
            return Owned(view, new PlaceholderElement(node, parent ?? GUI.Canvas));
        }

        if (node.ElementName is { } name) { view.Names.Add(name, element, view.Diagnostics, node); }

        element.Node = node;
        element.ElementName = node.Name;
        element.DataContext = dataContext ?? view.DataContext;
        element.Context = view;
        element.Parent = parentElement;
        element.Scope = view.CurrentScope;
        view.Own(element);

        TriggerSet inline = new();
        Style? style = ResolveStyle(view, node, element);

        ApplySetters(view, node, element, style);
        ApplyAttributes(view, node, element, metadata);
        ApplyContent(view, node, element, metadata, inline);
        AttachTriggers(view, node, element, style, inline);

        return element;
    }

    private static ViewElement Owned(ViewContext view, ViewElement element)
    {
        view.Own(element);
        return element;
    }

    private static Style? ResolveStyle(ViewContext view, MarkupNode node, ViewElement element)
    {
        Style? style = null;

        if (node.Text("Style") is { Length: > 0 } key)
        {
            style = view.FindResource(key, element) as Style;

            if (style is null) { view.Diagnostics.Report($"no style named '{key}'", node); }
        }

        style ??= view.FindResource(node.Name, element) as Style;

        return style is null ? null : FoldIn(style, view, node, element);
    }

    private static Style FoldIn(Style style, ViewContext view, MarkupNode node, ViewElement element)
    {
        if (style.BasedOn is not { } baseKey)
        {
            return style;
        }

        if (view.FindResource(baseKey, element) is not Style baseStyle)
        {
            view.Diagnostics.Report($"the style '{style.Key}' is based on '{baseKey}', which is not there", node);
            return style;
        }

        Style folded = FoldIn(baseStyle, view, node, element);
        return new Style
        {
            Key = style.Key,
            TargetType = style.TargetType,
            Setters = [.. folded.Setters, .. style.Setters],
            Triggers = [.. folded.Triggers, .. style.Triggers],
            Events = [.. folded.Events, .. style.Events]
        };
    }

    private static void ApplySetters(ViewContext view, MarkupNode node, ViewElement element, Style? style)
    {
        foreach (Setter setter in style?.Setters ?? [])
        {
            TryApply(view, node, element, setter);
        }
    }

    private static void AttachTriggers(ViewContext view, MarkupNode node, ViewElement element, Style? style, TriggerSet inline)
    {
        List<TriggerBase> triggers = [];
        List<EventTrigger> events = [];

        if (style is not null)
        {
            triggers.AddRange(style.Triggers);
            events.AddRange(style.Events);
        }

        triggers.AddRange(inline.Triggers);
        events.AddRange(inline.Events);

        if (triggers.Count == 0 && events.Count == 0) { return; }

        TriggerContext context = new(view, view.Animations, element, node, element.Scope);

        foreach (TriggerBase trigger in triggers)
        {
            TriggerBase current = trigger;
            UiGuard.Run(() => current.Attach(context));
        }

        bool loaded = false;

        foreach (EventTrigger raised in events)
        {
            EventTrigger current = raised;
            UiGuard.Run(() => current.Attach(context));
            loaded |= string.Equals(current.Event, "Loaded", StringComparison.Ordinal);
        }

        if (loaded) { view.Once(() => element.RaiseEvent("Loaded")); }
    }

    private static bool TryApply(ViewContext view, MarkupNode node, ViewElement element, Setter setter)
    {
        if (ElementRegistry.Property(node.Name, setter.Property) is not { } property)
        {
            view.Diagnostics.Report($"a style sets '{setter.Property}', which <{node.Name}> does not have", node);
            return false;
        }

        if (property.Phase == ApplyPhase.Construct)
        {
            view.Diagnostics.Report($"'{setter.Property}' is decided while <{node.Name}> is built, a style cannot set it", node);
            return false;
        }

        if (!property.SettableFromStyle)
        {
            view.Diagnostics.Report($"'{setter.Property}' is not there to be set from a style", node);
            return false;
        }

        return Give(new PropertyAssignment(view, node, element, property), setter.Value);
    }

    private static bool Give(PropertyAssignment assignment, MarkupValue value)
    {
        try
        {
            if (value.IsText) { assignment.SetText(value.Raw); }
            else { value.Extension!.Apply(assignment); }

            return true;
        }
        catch (Exception e)
        {
            assignment.Report(e.Message);
            return false;
        }
    }

    private static void ApplyAttributes(ViewContext view, MarkupNode node, ViewElement element, ElementMetadata metadata)
    {
        foreach (MarkupAttribute attribute in node.Attributes)
        {
            if (IsReserved(attribute.Name)) { continue; }

            if (string.Equals(attribute.Name, "Padding", StringComparison.OrdinalIgnoreCase))
            {
                GivePadding(element, node.Padding);
                continue;
            }

            if (string.Equals(attribute.Name, "Margin", StringComparison.OrdinalIgnoreCase))
            {
                GiveMargin(view, node, element, node.Margin);
                continue;
            }

            if (attribute.Name.Contains('.', StringComparison.Ordinal))
            {
                GiveAttached(view, node, element, attribute);
                continue;
            }

            GiveDeclared(view, node, element, metadata, attribute);
        }
    }

    private static void GiveAttached(ViewContext view, MarkupNode node, ViewElement element, MarkupAttribute attribute)
    {
        if (AttachedProperties.Find(attribute.Name) is not { } attached)
        {
            view.Diagnostics.Report($"<{node.Name}> has no attached property '{attribute.Name}'", node);
            return;
        }

        Give(new PropertyAssignment(view, node, element, attached), attribute.Value);
    }

    private static void GiveDeclared(
        ViewContext view,
        MarkupNode node,
        ViewElement element,
        ElementMetadata metadata,
        MarkupAttribute attribute)
    {
        if (!metadata.Properties.TryGetValue(attribute.Name, out PropertyMetadata? property))
        {
            view.Diagnostics.Report($"<{node.Name}> has no property '{attribute.Name}'", node);
            return;
        }

        if (property.Phase == ApplyPhase.Construct) { return; }

        Give(new PropertyAssignment(view, node, element, property), attribute.Value);
    }

    private static void GivePadding(ViewElement target, Insets padding)
    {
        if (padding.IsEmpty) { return; }

        if (target.Control is GUITextBlock block) { block.Padding = padding.ToVector4(); }
    }

    private static void GiveMargin(ViewContext view, MarkupNode node, ViewElement element, Insets margin)
    {
        if (margin.IsEmpty) { return; }

        RectTransform rect = element.Control.RectTransform;

        if (rect.Parent is not { } parent || parent.Rect.Width <= 0 || parent.Rect.Height <= 0)
        {
            if (element.Control.Parent is not GUILayoutGroup)
            {
                view.Diagnostics.Report($"a margin on <{node.Name}> cannot be taken off here", node);
            }

            return;
        }

        float width = parent.Rect.Width;
        float height = parent.Rect.Height;
        Vector2 size = rect.RelativeSize;

        rect.RelativeSize = new Vector2(
            Math.Max(0f, size.X - margin.Horizontal / width),
            Math.Max(0f, size.Y - margin.Vertical / height));

        rect.AbsoluteOffset += new Point((margin.Left - margin.Right) / 2, (margin.Top - margin.Bottom) / 2);
    }

    private static void ApplyContent(ViewContext view, MarkupNode node, ViewElement element, ElementMetadata metadata, TriggerSet inline)
    {
        foreach (MarkupNode property in node.Children.Where(child => child.IsProperty))
        {
            if (string.Equals(property.Name, "Resources", StringComparison.OrdinalIgnoreCase))
            {
                element.Resources ??= new ResourceDictionary();
                ReadResources(view, element.Resources, property);
                continue;
            }

            if (string.Equals(property.Name, "InputBindings", StringComparison.OrdinalIgnoreCase))
            {
                ReadInputBindings(view, element, property);
                continue;
            }

            if (string.Equals(property.Name, "Triggers", StringComparison.OrdinalIgnoreCase))
            {
                ReadTriggers(view, property, inline);
                continue;
            }

            if (metadata.Properties.TryGetValue(property.Name, out PropertyMetadata? declared))
            {
                ApplyPropertyElement(view, node, element, declared, property);
                continue;
            }

            view.Diagnostics.Report($"<{node.Name}> has no property '{property.Name}'", property);
        }

        foreach (MarkupNode child in node.Content)
        {
            ViewElement built = BuildElement(view, child, element.ContentParent, element.DataContext, element);

            try
            {
                element.AddContent(built);
                element.RememberContent(built);
            }
            catch (NotSupportedException)
            {
                view.Diagnostics.Report($"<{node.Name}> takes no content", child);
            }
        }
    }

    private static void ReadTriggers(ViewContext view, MarkupNode propertyNode, TriggerSet inline)
    {
        List<TriggerBase> triggers = [];
        List<EventTrigger> events = [];

        Style.ReadTriggers(propertyNode, message => view.Diagnostics.Report(message, propertyNode), triggers, events);

        inline.Replace(triggers, events);
    }

    private static void ApplyPropertyElement(
        ViewContext view,
        MarkupNode node,
        ViewElement element,
        PropertyMetadata property,
        MarkupNode propertyNode)
    {
        if (string.Equals(property.Name, "Resources", StringComparison.OrdinalIgnoreCase))
        {
            element.Resources ??= new ResourceDictionary();
            ReadResources(view, element.Resources, propertyNode);
            return;
        }

        if (propertyNode.Content.FirstOrDefault() is not { } value)
        {
            view.Diagnostics.Report($"<{node.Name}.{property.Name}> is empty", propertyNode);
            return;
        }

        if (MarkupExtension.ForElement(value) is not { } extension)
        {
            view.Diagnostics.Report($"<{value.Name}> cannot fill a property", value);
            return;
        }

        extension.Apply(new PropertyAssignment(view, propertyNode, element, property));
    }

    private static void ReadInputBindings(ViewContext view, ViewElement element, MarkupNode propertyNode)
    {
        foreach (MarkupNode child in propertyNode.Content)
        {
            if (!string.Equals(child.Name, "KeyBinding", StringComparison.OrdinalIgnoreCase)) { continue; }

            if (Input.KeyBinding.Read(child, element) is not { } binding)
            {
                view.Diagnostics.Report("a key binding needs a Key", child);
                continue;
            }

            (element.Bindings ??= []).Add(binding);
            view.RegisterInput(binding);

            if (child.Value("Command") is { } command)
            {
                Give(new PropertyAssignment(view, child, element, KeyCommandProperty, binding), command);
            }
        }
    }

    private static readonly PropertyMetadata KeyCommandProperty = new(
        "Command",
        typeof(ICommand),
        ApplyPhase.Live,
        BindingMode.OneWay,
        SettableFromStyle: true,
        KeyText: false,
        (target, value) => ((Input.KeyBinding)target).Command = (ICommand?)value,
        target => ((Input.KeyBinding)target).Command);

    private static void ReadResources(ViewContext view, ResourceDictionary dictionary, MarkupNode propertyNode)
    {
        try
        {
            foreach ((string key, object value) in ResourceReader.Read(propertyNode).Entries()) { dictionary.Set(key, value); }
        }
        catch (InvalidDataException e)
        {
            view.Diagnostics.Report(e.Message, propertyNode);
        }
    }

    private static bool IsReserved(string name)
        => name is "Name" or "Style";

    private sealed class PlaceholderElement : ViewElement
    {
        internal PlaceholderElement(MarkupNode node, RectTransform parent)
            : base(new GUITextBlock(
                MarkupPlacement.Of(node, 1f, UiTokens.Percent("control", 0.84f)).ToRectTransform(parent),
                RichString.Rich($"<{node.Name}>"),
                textColor: UiMetrics.Danger,
                textAlignment: Alignment.CenterLeft)
            {
                CanBeFocused = false
            })
        {
        }

        public override void AddContent(ViewElement child)
        {
        }
    }
}
