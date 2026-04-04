using System.Collections.Generic;
using System.Linq;
using RTS.UI.Components;
using RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTS.UI.Containers
{
    public class ControlGroupUI : MonoBehaviour, IUIElement<HashSet<AbstractCommandable>>
    {
        [SerializeField] private ControlGroupKeyboardHotkey[] controlGroupHotkeys;

        private HashSet<AbstractCommandable> selectedUnits;

        public void EnableFor(HashSet<AbstractCommandable> items)
        {
            selectedUnits = items;
        }

        public void Disable() { }

        private void Update()
        {
            if (!Keyboard.current.ctrlKey.isPressed) return;

            foreach (ControlGroupKeyboardHotkey groupHotkey in controlGroupHotkeys)
            {
                if (!Keyboard.current[groupHotkey.Key].wasReleasedThisFrame) continue;

                if (Keyboard.current.shiftKey.isPressed)
                {
                    groupHotkey.Group.Clear(); 
                    continue;
                }

                if (selectedUnits != null && selectedUnits.Count > 0)
                {
                    groupHotkey.Group.EnableFor(selectedUnits, groupHotkey.Key, SelectUnits);
                }
            }
        }

        private void SelectUnits(HashSet<AbstractCommandable> units)
        {
            if (selectedUnits == null) return;

            foreach (ISelectable selectable in selectedUnits.ToList())
            {
                selectable.Deselect();
            }

            foreach (ISelectable selectable in units)
            {
                selectable.Select();
            }
        }

        [System.Serializable]
        private struct ControlGroupKeyboardHotkey
        {
            [field: SerializeField] public Key Key { get; private set; }
            [field: SerializeField] public ControlGroup Group { get; private set; }
        }
    }
}