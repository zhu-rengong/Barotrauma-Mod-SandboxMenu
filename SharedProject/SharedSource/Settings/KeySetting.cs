#if CLIENT
using System.Xml.Linq;
using Barotrauma.Networking;
using Microsoft.Xna.Framework.Input;

namespace SandboxMenu;

public sealed class KeySetting : BaseSetting<KeyBind>
{
    private static readonly MouseButton[] AllMouseButtons = Enum.GetValues<MouseButton>();

    private static readonly Color LabelColor = UiMetrics.Text;

    private GUIButton? _button;
    private GUICustomComponent? _capture;
    private bool _capturing;
    private int _captureCooldown;

    public LocalizedString Label { get; }
    public LocalizedString? ToolTip { get; set; }

    public KeySetting(Identifier identifier, KeyBind defaultValue, LocalizedString label)
        : base(identifier, defaultValue)
    {
        Label = label;
        SyncMode = SettingSyncMode.NoSync;
    }

    public override void Load(XElement element)
        => Value = KeyBind.FromName(element.GetAttributeString("value", DefaultValue.ToString()), DefaultValue);

    public override void Save(XElement element)
    {
        element.SetAttributeValue("value", Value.ToString());
    }

    public override void WriteMessage(IWriteMessage writeMessage)
    {
        writeMessage.WriteString(Value.ToString());
    }

    public override void ReadMessage(IReadMessage readMessage)
        => PendingValue = KeyBind.FromName(readMessage.ReadString(), DefaultValue);

    public override void CreateUI()
    {
        MainUI = new GUIFrame(new RectTransform(new Vector2(1f, 0.12f), null!), style: null!);

        var layout = new GUILayoutGroup(new RectTransform(Vector2.One, MainUI.RectTransform), isHorizontal: true)
        {
            ChildAnchor = Anchor.CenterLeft
        };

        new GUITextBlock(
            new RectTransform(new Vector2(0.6f, 1f), layout.RectTransform),
            Label,
            textColor: LabelColor,
            textAlignment: Alignment.CenterLeft);

        _button = new GUIButton(
            new RectTransform(new Vector2(0.4f, 0.8f), layout.RectTransform),
            PendingValue.ToString())
        {
            ToolTip = ToolTip ?? LocalizedString.EmptyString,
            OnClicked = (_, _) =>
            {
                _capturing = true;
                _captureCooldown = 1;
                RefreshButtonText();
                return true;
            }
        };
        _button.OnAddedToGUIUpdateList = _ => RefreshButtonText();

        // Polls for the next key/mouse press while capturing. Kept in a field, so its handler can be dropped
        // again when the plugin is unloaded (see Detach).
        _capture = new GUICustomComponent(new RectTransform(Vector2.One, MainUI.RectTransform), onUpdate: UpdateCapture)
        {
            CanBeFocused = false
        };
    }

    internal void Detach()
    {
        if (_button is { } button)
        {
            button.OnClicked = null;
            button.OnAddedToGUIUpdateList = null;
            _button = null;
        }

        if (_capture is { } capture)
        {
            capture.OnUpdate = null;
            _capture = null;
        }

        if (MainUI is { } row)
        {
            row.RemoveFromGUIUpdateList();
            row.RectTransform.Parent = null;
            MainUI = null;
        }
    }

    private void UpdateCapture(float deltaTime, GUICustomComponent component)
    {
        if (!_capturing) { return; }

        // Ignore the frame the capture was started on (avoids binding the click itself).
        if (_captureCooldown > 0)
        {
            _captureCooldown--;
            return;
        }

        if (PlayerInput.KeyHit(Keys.Escape))
        {
            _capturing = false;
            RefreshButtonText();
            return;
        }

        foreach (Keys key in Keyboard.GetState().GetPressedKeys())
        {
            if (key is Keys.None or Keys.Escape) { continue; }
            if (!PlayerInput.KeyHit(key)) { continue; }

            Commit(new KeyBind(key));
            return;
        }

        foreach (MouseButton mouse in AllMouseButtons)
        {
            if (mouse == MouseButton.None) { continue; }
            if (!new KeyBind(mouse).IsHit()) { continue; }

            Commit(new KeyBind(mouse));
            return;
        }
    }

    private void Commit(KeyBind bind)
    {
        PendingValue = bind;
        _capturing = false;
        RefreshButtonText();
    }

    private void RefreshButtonText()
    {
        if (_button is null) { return; }

        _button.Text = _capturing
            ? TextManager.Get("sandboxmenu.togglekey.press")
            : PendingValue.ToString();
    }
}
#endif
