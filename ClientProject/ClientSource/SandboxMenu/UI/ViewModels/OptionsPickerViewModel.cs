using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class OptionsPickerViewModel : Notifiable
{
    private readonly List<(PickerOption Option, OptionRowViewModel Row)> _rows = [];

    private LocalizedString _title = LocalizedString.EmptyString;
    private string _query = string.Empty;
    private bool _onlyEditable;
    private bool _onlySaveable;

    public OptionsPickerViewModel(LocalizedString title, IEnumerable<PickerOption> options, Action<PickerOption> onPicked, bool filterable = false)
    {
        Title = title;
        ShowFilters = filterable;

        _onlySaveable = filterable;
        _onlyEditable = filterable && ClientSpawnDispatcher.IsMultiplayerClient;

        foreach (PickerOption option in options)
        {
            PickerOption picked = option;

            _rows.Add((picked, new OptionRowViewModel(picked.Label, () => onPicked(picked))));
        }

        ApplyFilter();
    }

    public LocalizedString Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

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

    public ObservableCollection<OptionRowViewModel> Options { get; } = [];

    private void ApplyFilter()
    {
        string filter = _query.Trim();
        Options.Clear();

        foreach ((PickerOption option, OptionRowViewModel row) in _rows)
        {
            if (_onlyEditable && !option.Editable) { continue; }
            if (_onlySaveable && !option.Saveable) { continue; }
            if (filter.Length > 0 && !row.Text.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)) { continue; }

            Options.Add(row);
        }
    }
}
