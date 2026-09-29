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
    private readonly Action<PickerOption> _onPicked;

    private LocalizedString _title = LocalizedString.EmptyString;
    private string _query = string.Empty;

    public OptionsPickerViewModel(LocalizedString title, IEnumerable<PickerOption> options, Action<PickerOption> onPicked)
    {
        Title = title;
        _onPicked = onPicked;

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

    public ObservableCollection<PickerRowViewModel> Options { get; } = [];

    private void FillAll(IEnumerable<PickerOption> options)
    {
        foreach (PickerOption option in options)
        {
            PickerOption picked = option;

            Options.Add(new PickerRowViewModel(picked.Label, () => _onPicked(picked)));
        }
    }

    private void ApplyFilter()
    {
        string filter = _query.Trim();

        for (int i = 0; i < Options.Count; i++)
        {
            PickerRowViewModel row = Options[i];

            row.Visible = filter.Length == 0 || row.Text.Value.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
