namespace UiFramework.Controls;

internal sealed class VirtualListWindow(ListBoxElement list, ListRowPool rowPool)
{
    private const int Overscan = 4;

    private readonly GUIListBox _listBox = list.ListBox;

    private int _first = -1;
    private int _height;
    private int _spacing = -1;
    private float _scrolled;
    private GUIFrame? _top;
    private GUIFrame? _bottom;

    internal void Rebuild()
    {
        if (list.Rows.Count > 0) { list.RowHeight = list.Rows[0].Element.Control.Rect.Height; }

        Release();
        list.RefreshIndex();

        list.Probed = false;

        if (list.Index.Count == 0) { return; }

        Apply(force: true);
    }

    internal void Update() => Apply(force: false);

    internal void ReleaseSpacers()
    {
        if (_top is { } top)
        {
            top.RectTransform.Parent = null;
            _top = null;
        }

        if (_bottom is { } bottom)
        {
            bottom.RectTransform.Parent = null;
            _bottom = null;
        }
    }

    private void Apply(bool force)
    {
        int count = list.Index.Count;

        if (count == 0)
        {
            Release();
            return;
        }

        if (list.RowHeight <= 0 && list.Rows.Count == 0 && !Probe()) { return; }

        int height = list.Rows.Count > 0 ? list.Rows[0].Element.Control.Rect.Height : list.RowHeight;
        int viewport = _listBox.Content.Rect.Height;
        int spacing = _listBox.Spacing;

        if (height <= 0 || viewport <= 0) { return; }

        list.RowHeight = height;

        int stride = height + spacing;
        float scrolled = _listBox.BarSize < 1f ? Math.Max(0f, _listBox.TotalSize - viewport) * _listBox.BarScroll : 0f;

        int margin = Math.Abs(scrolled - _scrolled) > Overscan * stride ? 1 : Overscan;

        _scrolled = scrolled;

        int first = Math.Clamp((int)(scrolled / stride), 0, count - 1);
        int last = Math.Clamp((int)((scrolled + viewport) / stride) + 1, first, count - 1);
        int targetFirst = Math.Max(0, first - margin);
        int targetLast = Math.Min(count - 1, last + margin);

        if (!force && height == _height && spacing == _spacing
            && _first >= 0 && _first <= targetFirst && _first + list.Rows.Count - 1 >= targetLast)
        {
            return;
        }

        Build(targetFirst, targetLast, height, spacing);
    }

    private bool Probe()
    {
        if (list.Probed || list.Index.Count == 0) { return false; }

        list.Probed = true;

        if (rowPool.Build(list.Index[0]) is not { } row) { return false; }

        list.Rows.Add(row);
        _first = 0;
        list.RowHeight = row.Element.Control.Rect.Height;

        return true;
    }

    private void Build(int first, int last, int height, int spacing)
    {
        List<BuiltRow?> previous = [.. list.Rows];
        List<BuiltRow> next = new(last - first + 1);
        int failed = 0;

        for (int index = first; index <= last; index++)
        {
            object item = list.Index[index];
            BuiltRow? row = null;
            int position = index - _first;

            if (_first >= 0 && position >= 0 && position < previous.Count
                && previous[position] is { } candidate && ReferenceEquals(candidate.Item, item))
            {
                previous[position] = null;
                row = candidate;
            }

            row ??= rowPool.Take(item);

            if (row is null) { failed++; continue; }

            next.Add(row);
        }

        if (failed > 0)
        {
            rowPool.Release(next);
            rowPool.Release(previous);
            ReleaseSpacers();

            list.Rows.Clear();
            _first = -1;

            if (next.Count == 0) { return; }

            list.AbandonWindow();
            return;
        }

        rowPool.Release(previous);

        list.Rows.Clear();
        list.Rows.AddRange(next);
        _first = first;
        _height = height;
        _spacing = spacing;
        list.RowHeight = height;

        Arrange();
    }

    private void Arrange()
    {
        ReleaseSpacers();

        for (int i = 0; i < list.Rows.Count; i++) { list.Rows[i].Element.Control.RectTransform.Parent = null; }

        int spacing = _listBox.Spacing;
        int stride = list.RowHeight + spacing;
        int width = Math.Max(1, _listBox.Content.Rect.Width);

        if (_first > 0) { _top = CreateSpacer(width, _first * stride - spacing); }

        for (int i = 0; i < list.Rows.Count; i++) { list.Rows[i].Element.Control.RectTransform.Parent = _listBox.Content.RectTransform; }

        int after = list.Index.Count - _first - list.Rows.Count;

        if (after > 0) { _bottom = CreateSpacer(width, after * stride - spacing); }
    }

    private GUIFrame CreateSpacer(int width, int height)
    {
        GUIFrame spacer = new(
            new RectTransform(new Point(width, height), _listBox.Content.RectTransform, Anchor.TopLeft, Pivot.TopLeft, ScaleBasis.Normal, isFixedSize: true),
            style: null)
        {
            CanBeFocused = false
        };

        spacer.RectTransform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);

        return spacer;
    }

    private void Release()
    {
        rowPool.Release(list.Rows);

        list.Rows.Clear();
        _first = -1;

        ReleaseSpacers();
    }
}
