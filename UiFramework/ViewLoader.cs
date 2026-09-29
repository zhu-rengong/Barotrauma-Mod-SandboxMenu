namespace UiFramework;

internal static class ViewLoader
{
    internal static ViewLoadContext Load(string fileName, object? dataContext, Action<string>? diagnosticSink = null, RectTransform? parent = null)
    {
        MarkupNode root = MarkupSource.Load(fileName);
        return Build(root, fileName, dataContext, diagnosticSink, parent);
    }

    internal static ViewLoadContext Build(MarkupNode root, string view, object? dataContext, Action<string>? diagnosticSink = null, RectTransform? parent = null)
    {
        ViewLoadContext context = new(view) { DataContext = dataContext, DiagnosticSink = diagnosticSink };
        context.Root = BuildElement(context, root, parent);
        context.Resources = context.Root?.Resources;
        context.Diagnostics.Flush(context.DiagnosticSink);
        return context;
    }

    internal static ViewElement BuildElement(ViewLoadContext view, MarkupNode node, RectTransform? parent, object? dataContext = null, ViewElement? parentElement = null)
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
        element.Parent = parentElement;

        if (element is IDisposable disposable) { view.Own(disposable); }

        Style? style = ResolveStyle(view, node, element);
        ApplySetters(view, node, element, style);
        ApplyAttributes(view, node, element, metadata);
        ApplyTriggers(view, node, element, style);

        ApplyContent(view, node, element, metadata);

        return element;
    }

    private static Style? ResolveStyle(ViewLoadContext view, MarkupNode node, ViewElement element)
    {
        Style? style = node.Text("Style") is { } key ? view.FindResource(key, element) as Style : null;
        style ??= view.FindResource(node.Name, element) as Style;

        return style is null ? null : FoldIn(style, view, node, element);
    }

    private static Style FoldIn(Style style, ViewLoadContext view, MarkupNode node, ViewElement element)
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

    private static void ApplySetters(ViewLoadContext view, MarkupNode node, ViewElement element, Style? style)
    {
        foreach (Setter setter in style?.Setters ?? [])
        {
            TryApply(view, node, element, setter);
        }
    }

    private static void ApplyTriggers(ViewLoadContext view, MarkupNode node, ViewElement element, Style? style)
    {
        if (style is null) { return; }

        foreach (DataTrigger trigger in style.Triggers)
        {
            Dictionary<string, Setter> active = new(StringComparer.OrdinalIgnoreCase);
            string expected = trigger.Value.Raw;

            view.Own(Binding.Attach(new BindingRequest
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

    private static bool TryApply(ViewLoadContext view, MarkupNode node, ViewElement element, Setter setter)
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

    private static void ApplyAttributes(ViewLoadContext view, MarkupNode node, ViewElement element, ElementMetadata metadata)
    {
        foreach (MarkupAttribute attribute in node.Attributes)
        {
            if (attribute.Name.Contains('.', StringComparison.Ordinal) || IsStructural(attribute.Name)) { continue; }

            if (!metadata.Properties.TryGetValue(attribute.Name, out PropertyMetadata? property))
            {
                view.Diagnostics.Report($"<{node.Name}> has no property '{attribute.Name}'", node);
                continue;
            }

            Give(new PropertyAssignment(view, node, element, property), attribute.Value);
        }
    }

    private static void ApplyContent(ViewLoadContext view, MarkupNode node, ViewElement element, ElementMetadata metadata)
    {
        foreach (MarkupNode property in node.Children.Where(child => child.IsProperty))
        {
            if (string.Equals(property.Name, "Resources", StringComparison.OrdinalIgnoreCase))
            {
                element.Resources ??= new ResourceDictionary();
                ReadResources(view, element.Resources, property);
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
        ViewLoadContext view,
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

    private static void ReadResources(ViewLoadContext view, ResourceDictionary dictionary, MarkupNode propertyNode)
    {
        foreach (MarkupNode child in propertyNode.Content)
        {
            if (string.Equals(child.Name, "Style", StringComparison.OrdinalIgnoreCase))
            {
                Style style = Style.Read(child);
                dictionary.Set(style.Key ?? style.TargetType ?? throw new InvalidDataException($"a style needs a Key or a TargetType ({child})"), style);
                continue;
            }

            if (string.Equals(child.Name, "DataTemplate", StringComparison.OrdinalIgnoreCase))
            {
                Templating.DataTemplate template = Templating.DataTemplate.Read(child);
                dictionary.Set(template.Key ?? template.DataType ?? throw new InvalidDataException($"a data template needs a Key or a DataType ({child})"), template);
                continue;
            }

            view.Diagnostics.Report($"<{child.Name}> cannot be declared as a resource (yet)", child);
        }
    }

    private static bool IsStructural(string name)
        => name is "Name" or "Skin" or "Align" or "Width" or "Height" or "LabelWidth" or "Orientation" or "ChildAnchor" or "Spacing" or "RelativeSpacing" or "Stretch" or "Draggable" or "BackgroundMenu" or "Clear" or "Integer" or "Focus" or "ItemTemplate" or "Rows" or "Columns" or "Gap" or "Padding" or "Font";

    private sealed class PlaceholderElement : ViewElement
    {
        internal PlaceholderElement(MarkupNode node, RectTransform parent)
            : base(new GUITextBlock(
                MarkupPlacement.Of(node, 1f, UiMetrics.ControlHeight).ToRectTransform(parent),
                RichString.Rich($"<{node.Name}>"),
                textColor: UiMetrics.Danger,
                textAlignment: Alignment.CenterLeft)
            {
                CanBeFocused = false
            })
        {
        }

        internal override void AddContent(ViewElement child)
        {
        }
    }
}
