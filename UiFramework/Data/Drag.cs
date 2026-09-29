using Microsoft.Xna.Framework;

namespace UiFramework.Data;

internal enum DropMode
{
    Before,
    After,
    Nest
}

internal interface IItemDropTarget
{
    bool CanDrag(object item);

    bool CanNest(object target);

    void Drop(object dragged, object? onto, DropMode mode);
}

internal interface IListBackground
{
    void ShowBackgroundMenu(Vector2 position);
}
