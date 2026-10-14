namespace UiFramework;

public static class ViewAudit
{
    public static int ReportReleases(ViewElement? root, Action<string> message)
    {
        if (root is null) { return 0; }

        List<ViewElement> pending = [root];
        int found = 0;

        for (int i = 0; i < pending.Count; i++)
        {
            ViewElement element = pending[i];

            if (!element.IsReleased)
            {
                found++;
                message($"a <{element.ElementName}> is still alive after the view closed");
            }
            else if (element.SubscriptionCount > 0)
            {
                found++;
                message($"a <{element.ElementName}> keeps {element.SubscriptionCount} subscriptions after the view closed");
            }

            foreach (ViewElement child in element.ChildElements) { pending.Add(child); }
        }

        return found;
    }

    public static int Count(ViewElement? root)
    {
        if (root is null) { return 0; }

        List<ViewElement> pending = [root];

        for (int i = 0; i < pending.Count; i++)
        {
            foreach (ViewElement child in pending[i].ChildElements) { pending.Add(child); }
        }

        return pending.Count;
    }
}
