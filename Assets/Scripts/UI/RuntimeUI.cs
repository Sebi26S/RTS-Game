using System.Collections.Generic;
using System.Linq;
using RTS.EventBus;
using RTS.Events;
using RTS.UI.Containers;
using RTS.Units;
using UnityEngine;

namespace RTS.UI
{
    public class RuntimeUI : MonoBehaviour
    {
        [SerializeField] private ActionsUI actionsUI;
        [SerializeField] private BuildingSelectedUI buildingSelectedUI;
        [SerializeField] private UnitIconUI unitIconUI;
        [SerializeField] private SingleUnitSelectedUI singleUnitSelectedUI;
        [SerializeField] private MultipleUnitsSelectedUI multipleUnitsSelectedUI;
        [SerializeField] private ControlGroupUI controlGroupUI;
        [SerializeField] private GameObject border;

        private HashSet<AbstractCommandable> selectedUnits = new(12);

        private void Awake()
        {
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] += HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += HandleUnitDeath;
            Bus<SupplyEvent>.OnEvent[Owner.Player1] += HandleSupplyChange;
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += HandleBuildingSpawn;
            Bus<UpgradeResearchedEvent>.OnEvent[Owner.Player1] += HandleUpgradeResearched;
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] += HandleBuildingDeath;
        }

        private void Start()
        {
            actionsUI.Disable();
            buildingSelectedUI.Disable();
            unitIconUI.Disable();
            singleUnitSelectedUI.Disable();
            multipleUnitsSelectedUI.Disable();

            if (border != null)
                border.SetActive(false);
        }

        private void OnDestroy()
        {
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] -= HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= HandleUnitDeath;
            Bus<SupplyEvent>.OnEvent[Owner.Player1] -= HandleSupplyChange;
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] -= HandleBuildingSpawn;
            Bus<UpgradeResearchedEvent>.OnEvent[Owner.Player1] -= HandleUpgradeResearched;
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] -= HandleBuildingDeath;
        }

        private void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (evt.Unit is AbstractCommandable commandable)
            {
                selectedUnits.Add(commandable);
                RefreshUI();
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            selectedUnits.Remove(evt.Unit);
            RefreshUI();
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            selectedUnits.Remove(evt.Building);
            RefreshUI();
        }

        private void HandleUpgradeResearched(UpgradeResearchedEvent args)
        {
            RefreshUI();
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent args)
        {
            actionsUI.EnableFor(selectedUnits);
        }

        private void HandleUnitDeselected(UnitDeselectedEvent evt)
        {
            if (evt.Unit is AbstractCommandable commandable)
            {
                selectedUnits.Remove(commandable);

                RefreshUI();
            }
        }

        private void RefreshUI()
        {
            bool hasSelection = selectedUnits.Count > 0;

            if (border != null)
                border.SetActive(hasSelection);

            controlGroupUI.EnableFor(selectedUnits);
            if (selectedUnits.Count > 0)
            {
                actionsUI.EnableFor(selectedUnits);

                if (selectedUnits.Count == 1)
                {
                    multipleUnitsSelectedUI.Disable();
                    ResolveSingleUnitSelectedUI();
                }
                else
                {
                    unitIconUI.Disable();
                    singleUnitSelectedUI.Disable();
                    buildingSelectedUI.Disable();
                    multipleUnitsSelectedUI.EnableFor(selectedUnits);
                }
            }
            else
            {
                DisableAllContainers();
            }
        }

        private void DisableAllContainers()
        {
            actionsUI.Disable();
            buildingSelectedUI.Disable();
            unitIconUI.Disable();
            singleUnitSelectedUI.Disable();
            multipleUnitsSelectedUI.Disable();
        }

        private void ResolveSingleUnitSelectedUI()
        {
            AbstractCommandable commandable = selectedUnits.First();
            unitIconUI.EnableFor(commandable);

            if (commandable is BaseBuilding building)
            {
                singleUnitSelectedUI.Disable();
                buildingSelectedUI.EnableFor(building);
            }
            else
            {
                buildingSelectedUI.Disable();
                singleUnitSelectedUI.EnableFor(commandable);
            }
        }

        private void HandleSupplyChange(SupplyEvent evt)
        {
            actionsUI.EnableFor(selectedUnits);
        }
    }
}
