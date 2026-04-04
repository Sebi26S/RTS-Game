using System.Collections.Generic;
using System.Linq;
using RTS.UI.Components;
using RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTS.UI.Containers
{
    public class MultipleUnitsSelectedUI : MonoBehaviour, IUIElement<HashSet<AbstractCommandable>>
    {
        [SerializeField] private UIUnitSelectedButton[] selectedUnitButtons;

        private HashSet<AbstractCommandable> selectedUnits;

        public void EnableFor(HashSet<AbstractCommandable> items)
        {
            gameObject.SetActive(true);

            if (items == null || items.Count == 0)
            {
                for (int j = 0; j < selectedUnitButtons.Length; j++)
                {
                    selectedUnitButtons[j].Disable();
                }

                selectedUnits = new HashSet<AbstractCommandable>();
                return;
            }

            List<AbstractCommandable> visibleItems = items.ToList();

            if (visibleItems.Count > selectedUnitButtons.Length)
            {
                Debug.LogWarning(
                    $"Too many units were passed to MultipleUnitsSelectedUI! " +
                    $"Only the first {selectedUnitButtons.Length} will be shown."
                );

                visibleItems = visibleItems.Take(selectedUnitButtons.Length).ToList();
            }

            int i = 0;
            foreach (AbstractCommandable commandable in visibleItems)
            {
                selectedUnitButtons[i].EnableFor(commandable, () => HandleClick(commandable));
                i++;
            }

            for (; i < selectedUnitButtons.Length; i++)
            {
                selectedUnitButtons[i].Disable();
            }

            selectedUnits = new HashSet<AbstractCommandable>(items);
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }

        private void HandleClick(AbstractCommandable clickedCommandable)
        {
            if (Keyboard.current.shiftKey.isPressed)
            {
                clickedCommandable.Deselect();
            }
            else
            {
                selectedUnits.Remove(clickedCommandable);

                AbstractCommandable[] commandables = new AbstractCommandable[selectedUnits.Count];
                selectedUnits.CopyTo(commandables);

                foreach (AbstractCommandable commandable in commandables)
                {
                    commandable.Deselect();
                }

                clickedCommandable.Select();
            }
        }
    }
}