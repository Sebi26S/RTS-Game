using System.Collections.Generic;
using System.Linq;
using RTS.Commands;
using RTS.TechTree;
using RTS.Units;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RTS.UI.Components
{
    [RequireComponent(typeof(Button))]
    public class UIActionButton : MonoBehaviour, IUIElement<BaseCommand, IEnumerable<AbstractCommandable>, UnityAction>, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Tooltip tooltip;

        private bool isActive;
        private RectTransform rectTransform;
        private Button button;
        private Key hotkey;
        private bool wasAssignedThisFrame;

        private static readonly string MINERALS_FORMAT = "{0} <color=#00ACFF>Minerals</color>. ";
        private static readonly string WOOD_FORMAT = "{0} <color=#3BEA60>Wood</color>. ";
        private static readonly string STONE_FORMAT = "{0} <color=#A0A0A0>Stone</color>. ";
        private static readonly string HOTKEY_FORMAT = "(<color=#FFFF00>{0}</color>)\n";
        private static readonly string POPULATION_FORMAT = "{0} <color=#eeeeee>Population</color> ";
        private static readonly string DEPENDENCY_FORMAT_NO_COMMA = "<color=#AC0000>{0}</color>.";
        private static readonly string DEPENDENCY_FORMAT_COMMA = "<color=#AC0000>{0}</color>, ";

        private void Awake()
        {
            button = GetComponent<Button>();
            rectTransform = GetComponent<RectTransform>();
            Disable();
        }

        private void Update()
        {
            if (button.interactable && !wasAssignedThisFrame
                && hotkey != Key.None && Keyboard.current[hotkey].wasReleasedThisFrame)
            {
                button.onClick?.Invoke();
            }

            wasAssignedThisFrame = false;
        }

        public void EnableFor(BaseCommand command, IEnumerable<AbstractCommandable> selectedUnits, UnityAction onClick)
        {
            button.onClick.RemoveAllListeners();
            SetIcon(command.Icon);
            hotkey = command.Hotkey;
            wasAssignedThisFrame = true;
            button.interactable = selectedUnits.Any((unit) => !command.IsLocked(new CommandContext(unit, new RaycastHit())));
            button.onClick.AddListener(onClick);
            isActive = true;

            if (tooltip != null)
            {
                tooltip.SetText(GetTooltipText(command));
            }
        }

        public void Disable()
        {
            SetIcon(null);
            button.interactable = false;
            button.onClick.RemoveAllListeners();
            isActive = false;
            CancelInvoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isActive)
            {
                Invoke(nameof(ShowTooltip), tooltip.HoverDelay);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
            CancelInvoke();
        }

        private void ShowTooltip()
        {
            if (tooltip != null)
            {
                tooltip.Show();
                tooltip.RectTransform.position = new Vector2(
                    rectTransform.position.x + rectTransform.rect.width / 2f,
                    rectTransform.position.y + rectTransform.rect.height / 2f
                );
            }
        }

        private void SetIcon(Sprite icon)
        {
            if (icon == null)
            {
                this.icon.enabled = false;
            }
            else
            {
                this.icon.sprite = icon;
                this.icon.enabled = true;
            }
        }

        private string GetTooltipText(BaseCommand command)
        {
            string tooltipText = command.Name;

            if (command.Hotkey != Key.None)
            {
                tooltipText += string.Format(HOTKEY_FORMAT, command.Hotkey);
            }
            else
            {
                tooltipText += "\n";
            }

            SupplyCostSO supplyCost = null;
            PopulationConfigSO populationConfig = null;
            if (command is BuildUnitCommand unitCommand)
            {
                supplyCost = unitCommand.Unit.Cost;
                populationConfig = unitCommand.Unit.PopulationConfig;
            }
            else if (command is BuildBuildingCommand buildingCommand)
            {
                supplyCost = buildingCommand.Building.Cost;
            }
            else if (command is ConvertStoneToMineralsCommand convertCommand)
            {
                supplyCost = convertCommand.Conversion.Cost;
            }

            if (supplyCost != null)
            {
                if (supplyCost.Minerals > 0)
                {
                    tooltipText += string.Format(MINERALS_FORMAT, supplyCost.Minerals);
                }
                if (supplyCost.Wood > 0)
                {
                    tooltipText += string.Format(WOOD_FORMAT, supplyCost.Wood);
                }
                if (supplyCost.Stone > 0)
                {
                    tooltipText += string.Format(STONE_FORMAT, supplyCost.Stone);
                }
            }

            if (populationConfig != null && populationConfig.PopulationCost > 0)
            {
                tooltipText += string.Format(POPULATION_FORMAT, populationConfig.PopulationCost);
            }

            if (command.IsLocked(new CommandContext(Owner.Player1, null, new RaycastHit()))
                && command is IUnlockableCommand unlockableCommand)
            {
                UnlockableSO[] dependencies = unlockableCommand.GetUnmetDependencies(Owner.Player1);

                if (dependencies.Length > 0)
                {
                    tooltipText += "\nRequires: ";
                }

                for (int i = 0; i < dependencies.Length; i++)
                {
                    tooltipText += i == dependencies.Length - 1
                        ? string.Format(DEPENDENCY_FORMAT_NO_COMMA, dependencies[i].Name)
                        : string.Format(DEPENDENCY_FORMAT_COMMA, dependencies[i].Name);
                }
            }

            return tooltipText;
        }
    }
}