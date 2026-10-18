namespace UiFramework.Abstractions;

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
    void ShowContextMenu(Vector2 position);
}
