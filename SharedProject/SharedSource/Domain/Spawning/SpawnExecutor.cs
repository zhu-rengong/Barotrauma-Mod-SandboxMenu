using Barotrauma.Items.Components;

namespace SandboxMenu.Domain.Spawning;

internal sealed class SpawnExecutor(
    SpawnTarget target,
    Func<string, SpawnSet?>? resolveTemplate,
    SpawnResult? result = null,
    IReadOnlyCollection<string>? templateChain = null)
{
    private readonly SpawnTarget _target = target;
    private readonly Func<string, SpawnSet?>? _resolveTemplate = resolveTemplate;
    private readonly SpawnResult _result = result ?? new SpawnResult();
    private readonly IReadOnlyCollection<string> _templateChain = templateChain ?? [];

    private Inventory? _inventory = (target as SpawnTarget.IntoInventory)?.Inventory;
    private bool _atItemInventory;

    internal SpawnResult Result => _result;

    private Character? Character => _target.Owner;

    internal void Run(IReadOnlyList<SpawnEntry> entries)
    {
        if (Entity.Spawner is null) { return; }

        foreach (SpawnEntry entry in entries)
        {
            RunSingle(entry);
        }
    }

    private void RunSingle(SpawnEntry entry)
    {
        switch (entry)
        {
            case RefEntry reference:
                ExpandTemplate(reference);
                break;

            case ItemEntry item:
                SpawnItem(item);
                break;
        }
    }

    private void ExpandTemplate(RefEntry reference)
    {
        if (_templateChain.Contains(reference.TemplateName, StringComparer.OrdinalIgnoreCase))
        {
            _result.Report($"Template '{reference.TemplateName}' is already being expanded (a cycle of templates).");
            return;
        }

        SpawnSet? template = _resolveTemplate?.Invoke(reference.TemplateName);
        if (template is null)
        {
            _result.Report($"Template '{reference.TemplateName}' was not found.");
            return;
        }

        int count = SpawnPlanner.RepeatCount(reference.Amount);
        var expanded = new SpawnExecutor(_target, _resolveTemplate, _result, [.. _templateChain, reference.TemplateName]);
        for (int i = 0; i < count; i++)
        {
            expanded.Run(template.Entries);
        }
    }

    private void SpawnItem(ItemEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Identifier)) { return; }

        ItemPrefab? prefab = ItemPrefab.Find(null, Identifiers.Of(entry.Identifier));
        if (prefab is null)
        {
            _result.Report($"Item prefab '{entry.Identifier}' was not found.");
            return;
        }

        // Stacks are resolved after the first item is spawned, because one stack holds a different
        // number of items depending on where the item ends up (character, bag/wearable, container, world).
        if (entry.Stacks is { } stacksRange)
        {
            float stackCount = MathF.Max(0f, stacksRange.Roll(entry.AmountRound));
            if (stackCount > 0f) { QueueStacks(prefab, entry, stackCount); }
            return;
        }

        float amount = SpawnPlanner.ResolveAmount(entry, prefab, _inventory);
        int whole = (int)MathF.Ceiling(amount);
        if (whole <= 0) { return; }

        for (int i = 1; i <= whole; i++)
        {
            QueueOne(prefab, entry, i == whole ? SpawnPlanner.FractionalCondition(amount, whole, prefab.Health) : null);
        }
    }

    private void QueueOne(ItemPrefab prefab, ItemEntry entry, float? condition, Inventory? destination = null)
    {
        if (destination is not null)
        {
            Entity.Spawner.AddItemToSpawnQueue(prefab, destination, condition, entry.Quality, item => AfterSpawned(item, entry));
            _result.QueuedCount++;
            return;
        }

        if (_target is SpawnTarget.AtWorld world)
        {
            if (entry.Install && TryInstall(prefab, world.WorldPosition, entry, condition))
            {
                _result.QueuedCount++;
                return;
            }

            Entity.Spawner.AddItemToSpawnQueue(prefab, world.WorldPosition, condition, entry.Quality, item => AfterSpawned(item, entry));
            _result.QueuedCount++;
            return;
        }

        if (_inventory is not null)
        {
            Entity.Spawner.AddItemToSpawnQueue(prefab, _inventory, condition, entry.Quality, item => AfterSpawned(item, entry));
            _result.QueuedCount++;
        }
    }

    private void QueueStacks(ItemPrefab prefab, ItemEntry entry, float stackCount)
    {
        if (_inventory is null)
        {
            int worldTotal = SpawnPlanner.StacksToItemCount(stackCount, Math.Max(1, prefab.MaxStackSize));
            for (int i = 0; i < worldTotal; i++) { QueueOne(prefab, entry, null); }
            return;
        }

        Entity.Spawner.AddItemToSpawnQueue(prefab, _inventory, null, entry.Quality, item =>
        {
            AfterSpawned(item, entry);

            Inventory? destination = item.ParentInventory ?? _inventory;
            if (destination is null)
            {
                int worldRemaining = SpawnPlanner.StacksToItemCount(stackCount, Math.Max(1, prefab.MaxStackSize)) - 1;
                for (int i = 0; i < worldRemaining; i++) { QueueOne(prefab, entry, null); }
                return;
            }

            int perStack = SpawnPlanner.StackCapacity(prefab, destination);
            int freeAfterFirst = destination.HowManyCanBePut(prefab);
            int total = Math.Max(1, SpawnPlanner.StacksToItemCount(stackCount, perStack));

            if (total > freeAfterFirst + 1)
            {
                _result.Report(
                    $"'{prefab.Identifier.Value}': {stackCount:0.##} stack(s) = {total} item(s), " +
                    $"but '{destination.Owner}' can only hold {freeAfterFirst + 1}. The rest will be dropped.");
            }

            for (int i = 0; i < total - 1; i++)
            {
                QueueOne(prefab, entry, null, destination);
            }
        });

        _result.QueuedCount++;
    }

    private bool TryInstall(ItemPrefab prefab, Vector2 worldPosition, ItemEntry entry, float? condition)
    {
        foreach (Submarine? sub in Submarine.MainSubs)
        {
            if (sub is null) { continue; }

            Rectangle borders = sub.Borders;
            Vector2 subPosition = sub.WorldPosition;
            var worldRect = new Rectangle(
                (int)(subPosition.X - borders.Width / 2f),
                (int)(subPosition.Y + borders.Height / 2f),
                borders.Width,
                borders.Height);

            if (!Submarine.RectContains(worldRect, worldPosition, true)) { continue; }

            Entity.Spawner.AddItemToSpawnQueue(prefab, worldPosition - sub.Position, sub, condition, entry.Quality, item => AfterSpawned(item, entry));
            return true;
        }

        return false;
    }

    private void AfterSpawned(Item item, ItemEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.Tags))
        {
            item.Tags = entry.Tags;
        }

        PutInInventory(item, entry);

        if (Character is { } character)
        {
            if (entry.InheritChannel)
            {
                InheritChannel(item, character);
            }

            if (entry.Equip)
            {
                Equip(character, item, entry.EquipSlots);
            }
        }

        ApplyProperties(item, entry);
        SpawnNestedItems(item, entry);
    }

    private void ApplyProperties(Item item, ItemEntry entry)
    {
        if (entry.Properties.Count == 0) { return; }

        List<AppliedOverride> applied = [];
        IReadOnlyList<string> problems = PropertyEditService.Apply(item, entry.Properties, applied);

        // Handed to the host's property event so the other clients end up with the same item; whatever that event
        // cannot carry comes back as a problem rather than as a value only this side has.
        SpawnPropertySync.Publish(item, applied, _result);

        if (problems.Count == 0) { return; }

        _result.Record(problems[^1]);
        Log.Warn(problems[^1]);
    }

    private void SpawnNestedItems(Item item, ItemEntry entry)
    {
        if (entry.Inventory.Count == 0) { return; }

        ItemContainer? container = item.GetComponent<ItemContainer>();
        if (container is null)
        {
            _result.Report($"No container found on '{item.Prefab?.Identifier.Value}' for nested items.");
            return;
        }

        var nested = new SpawnExecutor(_target, _resolveTemplate, _result, _templateChain)
        {
            _inventory = container.Inventory,
            _atItemInventory = true
        };

        nested.Run(entry.Inventory);
    }

    private void PutInInventory(Item item, ItemEntry entry)
    {
        if (_inventory is null) { return; }

        if (item.ParentInventory is null)
        {
            if (!_inventory.TryPutItem(item, Character, null))
            {
                _result.Report($"Unable to put '{item.Prefab?.Identifier.Value}' in '{_inventory.Owner}'.");
            }
            return;
        }

        if (_atItemInventory || entry.SlotIndex is not { } slotIndex) { return; }
        if (item.ParentInventory != _inventory) { return; }

        if (!_inventory.CanBePutInSlot(item, slotIndex, false)
            || !_inventory.TryPutItem(item, slotIndex, true, true, Character))
        {
            _result.Report($"Unable to put '{item.Prefab?.Identifier.Value}' in slot {slotIndex}.");
        }
    }

    private static void InheritChannel(Item item, Character character)
    {
        WifiComponent? targetWifi = item.GetComponent<WifiComponent>();
        if (targetWifi is null) { return; }

        Item? headset = character.Inventory.GetItemInLimbSlot(InvSlotType.Headset);
        WifiComponent? sourceWifi = headset?.GetComponent<WifiComponent>();
        if (sourceWifi is null) { return; }

        targetWifi.Channel = sourceWifi.Channel;
        targetWifi.TeamID = sourceWifi.TeamID;
        targetWifi.AllowCrossTeamCommunication = sourceWifi.AllowCrossTeamCommunication;
    }

    private static void Equip(Character character, Item item, InvSlotType[]? allowedSlots)
    {
        var inventory = character.Inventory;
        if (inventory is null) { return; }

        List<InvSlotType> slots = [.. allowedSlots is { Length: > 0 } ? allowedSlots : item.AllowedSlots];
        slots.Remove(InvSlotType.Any);
        if (slots.Count == 0) { slots.Add(InvSlotType.Any); }

        inventory.TryPutItem(item, character, slots);
    }
}
