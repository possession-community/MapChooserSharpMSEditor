using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Services;

public interface IUndoableAction
{
    void Undo();
    void Redo();
}

/// <summary>Sets a property on an ObservableObject back to its previous value.</summary>
internal sealed class PropertyChangeAction : IUndoableAction
{
    public object Target { get; }
    public string Property { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }

    public PropertyChangeAction(object target, string property, object? oldValue, object? newValue)
    {
        Target = target;
        Property = property;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public void Undo() => SetProp(Target, Property, OldValue);
    public void Redo() => SetProp(Target, Property, NewValue);

    private static void SetProp(object target, string name, object? value)
    {
        var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(target, value);
    }
}

internal sealed class CollectionAddAction : IUndoableAction
{
    public IList Collection { get; }
    public int Index { get; }
    public object? Item { get; }

    public CollectionAddAction(IList collection, int index, object? item)
    {
        Collection = collection; Index = index; Item = item;
    }

    public void Undo() { if (Index >= 0 && Index < Collection.Count) Collection.RemoveAt(Index); }
    public void Redo() { Collection.Insert(Math.Min(Index, Collection.Count), Item); }
}

internal sealed class CollectionRemoveAction : IUndoableAction
{
    public IList Collection { get; }
    public int Index { get; }
    public object? Item { get; }

    public CollectionRemoveAction(IList collection, int index, object? item)
    {
        Collection = collection; Index = index; Item = item;
    }

    public void Undo() { Collection.Insert(Math.Min(Index, Collection.Count), Item); }
    public void Redo() { if (Index >= 0 && Index < Collection.Count) Collection.RemoveAt(Index); }
}

/// <summary>Groups multiple actions so a single Ctrl+Z rewinds the entire user operation.</summary>
internal sealed class CompositeAction : IUndoableAction
{
    public List<IUndoableAction> Actions { get; } = new();
    public string Label { get; }
    public CompositeAction(string label) { Label = label; }

    public void Undo() { for (var i = Actions.Count - 1; i >= 0; i--) Actions[i].Undo(); }
    public void Redo() { foreach (var a in Actions) a.Redo(); }
}

/// <summary>
/// Session-wide undo/redo stacks. Actions are pushed by <see cref="UndoHooks"/> whenever
/// a hooked model property or collection changes. While <see cref="IsReplaying"/> is true
/// (during Undo/Redo) hooks skip recording to prevent feedback loops.
/// </summary>
public sealed partial class UndoManager : ObservableObject
{
    private readonly Stack<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();
    private CompositeAction? _manualBatch;
    private CompositeAction? _autoBatch;
    private int _changeDepth;

    public bool IsReplaying { get; private set; }

    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;

    internal void Push(IUndoableAction action)
    {
        if (IsReplaying) return;

        // Manual batch takes priority; auto-batch only collects when no manual batch is open.
        if (_manualBatch is not null) { _manualBatch.Actions.Add(action); return; }
        if (_autoBatch is not null) { _autoBatch.Actions.Add(action); return; }
        _undo.Push(action);
        _redo.Clear();
        UpdateFlags();
    }

    /// <summary>
    /// Records a single action directly (no batching wrapper). The old/new equality check
    /// here means no-op writes don't pollute history.
    /// </summary>
    public void Record(object target, string property, object? oldValue, object? newValue)
    {
        if (IsReplaying || Equals(oldValue, newValue)) return;
        Push(new PropertyChangeAction(target, property, oldValue, newValue));
    }

    public IDisposable BeginBatch(string label = "")
    {
        if (_manualBatch is not null)
        {
            // Nested BeginBatch just joins the outer one.
            return new NoopScope();
        }
        _manualBatch = new CompositeAction(label);
        return new BatchScope(this);
    }

    private void FinishBatch()
    {
        if (_manualBatch is null) return;
        var b = _manualBatch;
        _manualBatch = null;
        PushComposite(b);
    }

    /// <summary>
    /// Called by <see cref="UndoHooks"/> when a PropertyChanging event fires. Opens an
    /// auto-batch so nested setters triggered by partial methods (e.g. value → HasX flip)
    /// collapse into a single undo entry. No-op while a manual batch is already open.
    /// </summary>
    internal void EnterChange()
    {
        if (_changeDepth == 0 && _manualBatch is null && _autoBatch is null)
            _autoBatch = new CompositeAction("Edit");
        _changeDepth++;
    }

    internal void ExitChange()
    {
        if (_changeDepth == 0) return;
        _changeDepth--;
        if (_changeDepth == 0 && _autoBatch is not null && _manualBatch is null)
        {
            var b = _autoBatch;
            _autoBatch = null;
            PushComposite(b);
        }
    }

    private void PushComposite(CompositeAction b)
    {
        switch (b.Actions.Count)
        {
            case 0: return;
            case 1:
                // Flatten single-action batches so the history reads clean.
                if (_manualBatch is not null) { _manualBatch.Actions.Add(b.Actions[0]); return; }
                if (_autoBatch is not null) { _autoBatch.Actions.Add(b.Actions[0]); return; }
                _undo.Push(b.Actions[0]);
                _redo.Clear();
                UpdateFlags();
                return;
            default:
                if (_manualBatch is not null) { _manualBatch.Actions.Add(b); return; }
                if (_autoBatch is not null) { _autoBatch.Actions.Add(b); return; }
                _undo.Push(b);
                _redo.Clear();
                UpdateFlags();
                return;
        }
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var a = _undo.Pop();
        IsReplaying = true;
        try { a.Undo(); } finally { IsReplaying = false; }
        _redo.Push(a);
        UpdateFlags();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var a = _redo.Pop();
        IsReplaying = true;
        try { a.Redo(); } finally { IsReplaying = false; }
        _undo.Push(a);
        UpdateFlags();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _manualBatch = null;
        _autoBatch = null;
        _changeDepth = 0;
        UpdateFlags();
    }

    private void UpdateFlags()
    {
        CanUndo = _undo.Count > 0;
        CanRedo = _redo.Count > 0;
    }

    private sealed class BatchScope : IDisposable
    {
        private readonly UndoManager _mgr;
        public BatchScope(UndoManager mgr) { _mgr = mgr; }
        public void Dispose() => _mgr.FinishBatch();
    }

    private sealed class NoopScope : IDisposable { public void Dispose() { } }
}

/// <summary>
/// Hooks every <see cref="INotifyPropertyChanging"/>/<see cref="INotifyPropertyChanged"/> pair
/// and every <see cref="INotifyCollectionChanged"/> to record undo entries.
/// Properties whose name is in <see cref="SkipProperties"/> are ignored (housekeeping fields
/// like IsDirty don't belong in undo history).
/// </summary>
public static class UndoHooks
{
    private static readonly HashSet<string> SkipProperties = new(StringComparer.Ordinal)
    {
        "IsDirty", "DisplayName", "FilePath",
        // auto-set HasX flags ride along with the underlying value change — one undo entry
        // per user action is plenty; the flag flip is replayed by the PropertyChangeAction
        // on the value itself.
    };

    public static void HookObservable(object obj, UndoManager mgr)
    {
        if (obj is not INotifyPropertyChanged npc) return;
        if (obj is not INotifyPropertyChanging nch) return;

        var pending = new Dictionary<string, object?>();

        nch.PropertyChanging += (_, e) =>
        {
            var name = e.PropertyName;
            if (string.IsNullOrEmpty(name) || SkipProperties.Contains(name)) return;
            var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null) return;
            // Opening the auto-batch here pulls any nested setter (e.g. value → HasX) into
            // the same Ctrl+Z step as the outer setter that triggered it.
            mgr.EnterChange();
            pending[name] = prop.GetValue(obj);
        };

        npc.PropertyChanged += (_, e) =>
        {
            var name = e.PropertyName;
            if (string.IsNullOrEmpty(name) || SkipProperties.Contains(name)) return;
            if (!pending.Remove(name, out var oldVal))
            {
                // Shouldn't happen for a well-behaved ObservableObject, but if it does we
                // still need to balance the Enter we never made.
                return;
            }
            try
            {
                var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop is null) return;
                var newVal = prop.GetValue(obj);
                if (Equals(oldVal, newVal)) return;
                mgr.Push(new PropertyChangeAction(obj, name, oldVal, newVal));
            }
            finally
            {
                mgr.ExitChange();
            }
        };
    }

    public static void HookCollection(INotifyCollectionChanged coll, UndoManager mgr)
    {
        if (coll is not IList list) return;
        coll.CollectionChanged += (_, e) =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                {
                    var idx = e.NewStartingIndex;
                    for (var i = 0; i < e.NewItems.Count; i++)
                        mgr.Push(new CollectionAddAction(list, idx + i, e.NewItems[i]));
                    break;
                }
                case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                {
                    var idx = e.OldStartingIndex;
                    for (var i = 0; i < e.OldItems.Count; i++)
                        mgr.Push(new CollectionRemoveAction(list, idx + i, e.OldItems[i]));
                    break;
                }
                // Reset and Replace are not emitted by our mutation paths (we avoid Clear()
                // in favour of iterative RemoveAt inside a batch). If they ever appear,
                // nothing is recorded — undo will silently no-op for that specific event.
            }
        };
    }
}
