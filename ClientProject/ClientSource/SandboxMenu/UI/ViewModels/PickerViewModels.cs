using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal abstract class PickerRow : Notifiable
{
    private bool _visible = true;

    public bool Visible
    {
        get => _visible;
        set => Set(ref _visible, value);
    }
}

internal sealed class PickerRowViewModel(LocalizedString text, Action onPicked) : PickerRow
{
    public LocalizedString Text { get; } = text;

    public RelayCommand PickCommand { get; } = new(onPicked);
}

internal sealed class ToggleRowViewModel(PickerToggle option) : PickerRow
{
    public LocalizedString Label { get; } = option.Label;

    public bool Selected
    {
        get => option.IsTicked();
        set => option.Toggled(value);
    }
}

internal sealed class MultiPickerViewModel : Notifiable
{
    private LocalizedString _title = LocalizedString.EmptyString;
    private string _query = string.Empty;

    public MultiPickerViewModel(LocalizedString title, IEnumerable<PickerToggle> options)
    {
        Title = title;

        FillAll(options);
    }

    public LocalizedString Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value ?? string.Empty)) { ApplyFilter(); }
        }
    }

    public ObservableCollection<ToggleRowViewModel> Options { get; } = [];

    private void FillAll(IEnumerable<PickerToggle> options)
    {
        foreach (PickerToggle option in options) { Options.Add(new ToggleRowViewModel(option)); }
    }

    private void ApplyFilter()
    {
        string filter = _query.Trim();

        for (int i = 0; i < Options.Count; i++)
        {
            ToggleRowViewModel row = Options[i];

            row.Visible = filter.Length == 0 || row.Label.Value.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}

internal sealed class OptionsPickerViewModel : Notifiable
{
    private readonly List<(PickerOption Option, PickerRowViewModel Row)> _rows = [];

    private LocalizedString _title = LocalizedString.EmptyString;
    private string _query = string.Empty;
    private bool _onlyEditable;
    private bool _onlySaveable;

    public OptionsPickerViewModel(LocalizedString title, IEnumerable<PickerOption> options, Action<PickerOption> onPicked, bool filterable = false)
    {
        Title = title;
        ShowFilters = filterable;

        // The property picker opens narrowed by where the player is: a saveable property is what a spawn carries over
        // on its own, and a multiplayer client also gets the editable ones — what the host would let a client send.
        _onlySaveable = filterable;
        _onlyEditable = filterable && ClientSpawnDispatcher.IsMultiplayerClient;

        foreach (PickerOption option in options)
        {
            PickerOption picked = option;

            _rows.Add((picked, new PickerRowViewModel(picked.Label, () => onPicked(picked))));
        }

        ApplyFilter();
    }

    public LocalizedString Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    // The property picker can be cut down to what the host's own editors would offer or save; other option lists have
    // nothing to go by and leave the filter row out.
    public bool ShowFilters { get; }

    public LocalizedString EditableFilterLabel => TextManager.Get("sandboxmenu.filter.editable");

    public LocalizedString SaveableFilterLabel => TextManager.Get("sandboxmenu.filter.saveable");

    public bool OnlyEditable
    {
        get => _onlyEditable;
        set
        {
            if (Set(ref _onlyEditable, value)) { ApplyFilter(); }
        }
    }

    public bool OnlySaveable
    {
        get => _onlySaveable;
        set
        {
            if (Set(ref _onlySaveable, value)) { ApplyFilter(); }
        }
    }

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value ?? string.Empty)) { ApplyFilter(); }
        }
    }

    public ObservableCollection<PickerRowViewModel> Options { get; } = [];

    // The list is handed the rows that pass rather than hiding the rest: a hidden row keeps its place in the host's
    // list box and would leave a gap behind.
    private void ApplyFilter()
    {
        string filter = _query.Trim();
        Options.Clear();

        foreach ((PickerOption option, PickerRowViewModel row) in _rows)
        {
            if (_onlyEditable && !option.Editable) { continue; }
            if (_onlySaveable && !option.Saveable) { continue; }
            if (filter.Length > 0 && !row.Text.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)) { continue; }

            Options.Add(row);
        }
    }
}
