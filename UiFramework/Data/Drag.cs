using Microsoft.Xna.Framework;

namespace UiFramework.Data;

public enum DropMode
{
    Before,
    After,
    Nest
}

public interface IItemDropTarget
{
    bool CanDrag(object item);

    bool CanNest(object target);

    void Drop(object dragged, object? onto, DropMode mode);
}

public interface IListBackground
{
    void ShowBackgroundMenu(Vector2 position);
}
