using System.Collections.Generic;
using System.Linq;

namespace MO2ExportImport
{
    public class SelectionHistoryManager
    {
        private readonly List<HashSet<string>> _undoStack = new();
        private readonly List<HashSet<string>> _redoStack = new();
        private const int MaxHistorySize = 20;
        private bool _isUndoRedoOperation = false;

        public bool CanUndo => _undoStack.Count >= 2;
        public bool CanRedo => _redoStack.Count > 0;
        public bool IsUndoRedoOperation => _isUndoRedoOperation;

        public void SetUndoRedoOperation(bool value)
        {
            _isUndoRedoOperation = value;
        }

        public void SaveState(IEnumerable<Mod> selectedMods)
        {
            if (_isUndoRedoOperation)
                return;

            var state = new HashSet<string>(selectedMods.Select(m => m.DisplayName));
            
            // Don't save duplicate states
            if (_undoStack.Count > 0 && _undoStack[_undoStack.Count - 1].SetEquals(state))
                return;
            
            _undoStack.Add(state);

            // Limit stack size
            if (_undoStack.Count > MaxHistorySize)
            {
                _undoStack.RemoveAt(0);
            }

            // Clear redo stack when new action is performed
            _redoStack.Clear();
        }

        public HashSet<string> Undo()
        {
            if (!CanUndo)
                return null;

            // Remove current state (last item) and save to redo stack
            var currentState = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(currentState);

            // Get the previous state (now the last item)
            var previousState = _undoStack[_undoStack.Count - 1];

            return previousState;
        }

        public HashSet<string> Redo()
        {
            if (!CanRedo)
                return null;

            // Get and remove last state from redo stack
            var nextState = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            
            // Add it back to undo stack
            _undoStack.Add(nextState);

            return nextState;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}