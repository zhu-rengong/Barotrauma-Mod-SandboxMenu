using System.Windows.Input;

namespace UiFramework;

// Builds one element of a view from its markup node and hands it what the node says: its style, its attributes,
// its bindings and its content.
internal static class ViewBuilder
{
    internal static ViewElement BuildElement(ViewContext view, MarkupNode node, RectTransform? parent, object? dataContext = null, ViewElement? parentElement = null)
    {
        if (ElementRegistry.Find(node.Name) is not { } metadata)
        {
            view.Diagnostics.Report($"unknown element <{node.Name}>", node);
            return new PlaceholderElement(node, parent ?? GUI.Canvas);
        }

        ViewElement element;

        try
        {
            element = metadata.Create(new ElementContext(node, parent ?? GUI.Canvas, view));
        }
        catch (Exception e)
        {
            view.Diagnostics.Report($"<{node.Name}> could not be built ({e.Message})", node);
            return new PlaceholderElement(node, parent ?? GUI.Canvas);
        }

        if (node.ElementName is { } name) { view.Names.Add(name, element, view.Diagnostics, node); }

        element.Node = node;

        element.DataContext = dataContext ?? view.DataContext;
        element.Context = view;
        element.Parent = parentElement;

        if (element is IDisposable disposable) { view.Own(disposable); }

        Style? style = ResolveStyle(view, node, element);
        ApplySetters(view, node, element, style);
        ApplyAttributes(view, node, element, metadata);
        ApplyTriggers(view, node, element, style);

        ApplyContent(view, node, element, metadata);

        return element;
    }

    private static Style? ResolveStyle(ViewContext view, MarkupNode node, ViewElement element)
    {
        Style? style = node.Text("Style") is { } key ? view.FindResource(key, element) as Style : null;
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
            Triggers = [.. folded.Triggers, .. style.Triggers]
        };
    }

    private static void ApplySetters(ViewContext view, MarkupNode node, ViewElement element, Style? style)
    {
        foreach (Setter setter in style?.Setters ?? [])
        {
            TryApply(view, node, element, setter);
        }
    }

    private static void ApplyTriggers(ViewContext view, MarkupNode node, ViewElement element, Style? style)
    {
        if (style is null) { return; }

        foreach (DataTrigger trigger in style.Triggers)
        {
            Dictionary<string, Setter> active = new(StringComparer.OrdinalIgnoreCase);
            string expected = trigger.Value.Raw;

            view.Own(Binding.Attach(new BindingDefinition
            {
                Source = element.DataContext,
                Path = trigger.Path,
                Mode = BindingMode.OneWay,
                TargetType = typeof(object),
                Apply = value =>
                {
                    try
                    {
                        bool matches = string.Equals(value?.ToString(), expected, StringComparison.OrdinalIgnoreCase);

                        if (matches && active.Count == 0)
                        {
                            foreach (Setter setter in trigger.Setters)
                            {
                                if (TryApply(view, node, element, setter)) { active[setter.Property] = setter; }
                            }
                        }
                        else if (!matches && active.Count > 0)
                        {
                            foreach (Setter setter in active.Values)
                            {
                                Style? own = style;
                                foreach (Setter baseSetter in own.Setters.Where(baseSetter => string.Equals(baseSetter.Property, setter.Property, StringComparison.OrdinalIgnoreCase)))
                                {
                                    TryApply(view, node, element, baseSetter);
                                }
                            }

                            active.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        view.Diagnostics.Report($"a trigger on <{node.Name}> failed ({e.Message})", node);
                    }
                },
                Report = message => view.Diagnostics.Report(message, node)
            }));
        }
    }

    private static bool TryApply(ViewContext view, MarkupNode node, ViewElement element, Setter setter)
    {
        if (ElementRegistry.Find(node.Name)?.Properties.TryGetValue(setter.Property, out PropertyMetadata? property) is not true)
        {
            view.Diagnostics.Report($"a style sets '{setter.Property}', which <{node.Name}> does not have", node);
            return false;
        }

        if (!property.SettableFromStyle)
        {
            view.Diagnostics.Report($"'{setter.Property}' is decided while <{node.Name}> is built, a style cannot set it", node);
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
        // Padding is room kept inside the element, and the element itself is what knows where its content goes: a text
        // block takes it on the spot.
        static void GivePadding(ViewElement target, Insets padding)
        {
            if (padding.IsEmpty) { return; }

            if (target.Control is GUITextBlock block) { block.Padding = padding.ToVector4(); }
        }

        // A margin is room kept outside the element, which has to come off the rect the element was given: that is exact
        // while the room around it is known — the content of a window is sized from its own numbers — and is left to the
        // container that lays the element out otherwise, where a Stack folds it into the share it gives instead.
        static void GiveMargin(ViewContext view, MarkupNode node, ViewElement element, Insets margin)
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

        foreach (MarkupAttribute attribute in node.Attributes)
        {
            if (string.Equals(attribute.Name, "Padding", StringComparison.OrdinalIgnoreCase))
            {
                GivePadding(element, Insets.Parse(node.Text(attribute.Name)));
                continue;
            }

            if (string.Equals(attribute.Name, "Margin", StringComparison.OrdinalIgnoreCase))
            {
                GiveMargin(view, node, element, Insets.Parse(node.Text(attribute.Name)));
                continue;
            }

            if (attribute.Name.Contains('.', StringComparison.Ordinal) || IsStructural(attribute.Name)) { continue; }

            if (!metadata.Properties.TryGetValue(attribute.Name, out PropertyMetadata? property))
            {
                view.Diagnostics.Report($"<{node.Name}> has no property '{attribute.Name}'", node);
                continue;
            }

            Give(new PropertyAssignment(view, node, element, property), attribute.Value);
        }
    }

    private static void ApplyContent(ViewContext view, MarkupNode node, ViewElement element, ElementMetadata metadata)
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
            }
            catch (NotSupportedException)
            {
                view.Diagnostics.Report($"<{node.Name}> takes no content", child);
            }
        }
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

    // The commands of the keys an element answers to are bound off the element's own data context, so a key binding
    // is written exactly like the command of a button.
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
        SettableFromStyle: false,
        KeyText: false,
        (target, value) => ((Input.KeyBinding)target).Command = (ICommand?)value);

    private static void ReadResources(ViewContext view, ResourceDictionary dictionary, MarkupNode propertyNode)
    {
        try
        {
            foreach ((string key, object value) in ResourceDictionary.Read(propertyNode).Entries()) { dictionary.Set(key, value); }
        }
        catch (InvalidDataException e)
        {
            view.Diagnostics.Report(e.Message, propertyNode);
        }
    }

    private static bool IsStructural(string name)
        => name is "Name" or "Skin" or "Align" or "Width" or "Height" or "LabelWidth" or "Orientation" or "ChildAnchor" or "Spacing" or "RelativeSpacing" or "Stretch" or "Draggable" or "BackgroundMenu" or "Virtual" or "Clear" or "Integer" or "ItemTemplate" or "TileTemplate" or "Rows" or "Columns" or "Gap" or "Padding" or "Margin" or "Font";

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
