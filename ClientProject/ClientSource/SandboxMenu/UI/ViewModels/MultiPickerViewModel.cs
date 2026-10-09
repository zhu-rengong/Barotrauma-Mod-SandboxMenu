using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class MultiPickerViewModel : Notifiable
{
    private LocalizedString _title = LocalizedString.EmptyString;
    private string _query = string.Empty;

    public MultiPickerViewModel(LocalizedString title, IEnumerable<PickerToggle> options)
    {
        Title = title;

        SelectAllCommand = new RelayCommand(() => TickVisible(true));
        DeselectAllCommand = new RelayCommand(() => TickVisible(false));

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

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand DeselectAllCommand { get; }

    public ObservableCollection<ToggleRowViewModel> Options { get; } = [];

    private void FillAll(IEnumerable<PickerToggle> options)
    {
        foreach (PickerToggle option in options) { Options.Add(new ToggleRowViewModel(option)); }
    }

    private void TickVisible(bool ticked)
    {
        for (int i = 0; i < Options.Count; i++)
        {
            ToggleRowViewModel row = Options[i];

            if (row.Visible) { row.Tick(ticked); }
        }
    }

    private void ApplyFilter()
    {
        string filter = _query.Trim();

        for (int i = 0; i < Options.Count; i++)
        {
            ToggleRowViewModel row = Options[i];

            row.Visible = filter.Length == 0 || row.Label.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
