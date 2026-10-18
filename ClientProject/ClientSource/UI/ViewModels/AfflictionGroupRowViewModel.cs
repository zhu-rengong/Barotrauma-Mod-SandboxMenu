namespace SandboxMenu.UI.ViewModels;

internal sealed class AfflictionGroupRowViewModel : Notifiable
{
    private int _count;

    internal AfflictionGroupRowViewModel(LocalizedString label) => Label = label;

    public LocalizedString Label { get; }

    public RichString Title => Count == 0
        ? RichString.Rich(Label)
        : RichString.Rich(Label + " " + TooltipText.Dim(Count.ToString()));

    public bool HasAfflictions => _count > 0;

    public int Count
    {
        get => _count;
        internal set
        {
            if (!Set(ref _count, value)) { return; }

            Raise(nameof(Title));
            Raise(nameof(HasAfflictions));
        }
    }
}
