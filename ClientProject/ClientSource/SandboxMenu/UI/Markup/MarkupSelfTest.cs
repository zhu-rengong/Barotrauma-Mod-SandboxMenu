namespace SandboxMenu.UI;

internal static class MarkupSelfTest
{
    private const string Sample = """
        <Stack Orientation="Vertical" Spacing="4">
          <Stack.Resources>
            <Style Key="Header" TargetType="Text" BasedOn="SectionHeader">
              <Setter Property="Color" Value="Bright" />
              <DataTrigger Binding="Missing" Value="true">
                <Setter Property="Color" Value="Danger" />
              </DataTrigger>
            </Style>
            <DataTemplate Key="SampleRow" DataType="SampleRow">
              <Button Text="{Binding Label}" Command="{Binding Pick}" />
            </DataTemplate>
          </Stack.Resources>
          <Panel Name="Header" Height="0.2" />
          <Text Name="Title" Style="Header" Text="sandboxmenu.title" />
          <Text Literal="{Binding Name}" />
          <Text>
            <Text.Text>
              <MultiBinding Converter="Join" ConverterParameter=" / ">
                <Binding Path="Name" FallbackValue="(none)" />
                <Binding Path="Items[1]" />
              </MultiBinding>
            </Text.Text>
          </Text>
          <Button Name="Save" Text="sandboxmenu.preset.save" Command="{Binding Save}" Width="0.4" />
          <List Items="{Binding Rows}" ItemTemplate="SampleRow" Height="row" />
          <Text Text="{Binding Missing, FallbackValue=fallback}" />
          <Text Mystery="nonsense" />
          <Mystery Name="Nowhere" />
        </Stack>
        """;

    internal static void Run()
    {
        GUIFrame host = new(new RectTransform(MenuTheme.Size(MenuTheme.WindowWidth, MenuTheme.WindowHeight), GUI.Canvas), style: null)
        {
            Visible = false,
            CanBeFocused = false
        };

        try
        {
            using ViewLoadContext view = ViewLoader.Build(MarkupSource.ParseText("selftest", Sample), "selftest", new SampleModel(), parent: host.RectTransform);

            Log.Info($"markup: {Count(view.Root?.Control)} element(s) built");
        }
        finally
        {
            host.RemoveFromGUIUpdateList();
            host.RectTransform.Parent = null;
        }
    }

    private static int Count(GUIComponent? component)
        => component is null ? 0 : 1 + component.RectTransform.Children.Sum(child => Count(child.GUIComponent));

    private sealed class SampleModel : Notifiable
    {
        private string _name = "world";

        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        public IReadOnlyList<string> Items { get; } = ["alpha", "beta"];

        public RelayCommand Save { get; } = new(() => { }, () => false);

        public IReadOnlyList<SampleRow> Rows { get; } = [new("one"), new("two")];

        public sealed record SampleRow(string Label)
        {
            public RelayCommand Pick { get; } = new(() => { });
        }
    }
}
