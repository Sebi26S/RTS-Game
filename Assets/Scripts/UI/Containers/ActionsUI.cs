using System;
using System.Collections.Generic;
using System.Linq;
using RTS.Commands;
using RTS.EventBus;
using RTS.Events;
using RTS.TechTree;
using RTS.UI.Components;
using RTS.Units;
using UnityEngine;
using UnityEngine.Events;

namespace RTS.UI.Containers
{
    public class ActionsUI : MonoBehaviour, IUIElement<HashSet<AbstractCommandable>>
    {
        [SerializeField] private UIActionButton[] actionButtons;

        private HashSet<BaseBuilding> selectedBuildings = new();

        public void EnableFor(HashSet<AbstractCommandable> selectedUnits)
        {
            if (selectedUnits == null || selectedUnits.Count == 0)
            {
                Disable();
                return;
            }

            gameObject.SetActive(true);

            foreach (BaseBuilding building in selectedBuildings)
                building.OnQueueUpdated -= OnBuildingQueueUpdated;

            selectedBuildings = selectedUnits
                .OfType<BaseBuilding>()
                .ToHashSet();

            foreach (BaseBuilding building in selectedBuildings)
                building.OnQueueUpdated += OnBuildingQueueUpdated;

            RefreshButtons(selectedUnits);
        }

        public void Disable()
        {
            foreach (BaseBuilding building in selectedBuildings)
                building.OnQueueUpdated -= OnBuildingQueueUpdated;

            selectedBuildings.Clear();

            foreach (UIActionButton button in actionButtons)
                button.Disable();

            gameObject.SetActive(false);
        }

        private void OnBuildingQueueUpdated(UnlockableSO[] unitsInQueue)
        {
            if (selectedBuildings == null || selectedBuildings.Count == 0) return;

            RefreshButtons(selectedBuildings.Cast<AbstractCommandable>().ToHashSet());
        }

        private void RefreshButtons(HashSet<AbstractCommandable> selectedUnits)
        {
            if (selectedUnits == null || selectedUnits.Count == 0)
            {
                foreach (var button in actionButtons) button.Disable();
                return;
            }

            AbstractCommandable first = selectedUnits.FirstOrDefault();
            IEnumerable<BaseCommand> availableCommands = first?.AvailableCommands ?? Array.Empty<BaseCommand>();

            if (availableCommands != null)
            {
                availableCommands = availableCommands.Where(action => action.IsAvailable(
                    new CommandContext(
                        Owner.Player1,
                        first,
                        new RaycastHit()
                    )
                ));
            }
            else
            {
                availableCommands = Array.Empty<BaseCommand>();
            }

            foreach (AbstractCommandable commandable in selectedUnits)
            {
                if (commandable == null) continue;

                if (commandable.AvailableCommands != null)
                    availableCommands = availableCommands.Intersect(commandable.AvailableCommands);
                else
                    availableCommands = Array.Empty<BaseCommand>();
            }

            for (int i = 0; i < actionButtons.Length; i++)
            {
                BaseCommand actionForSlot = availableCommands.FirstOrDefault(a => a.Slot == i);

                if (actionForSlot != null)
                    actionButtons[i].EnableFor(actionForSlot, selectedUnits, HandleClick(actionForSlot));
                else
                    actionButtons[i].Disable();
            }
        }

        private UnityAction HandleClick(BaseCommand action)
        {
            return () => Bus<CommandSelectedEvent>.Raise(Owner.Player1, new CommandSelectedEvent(action));
        }
    }
}