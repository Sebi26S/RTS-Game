using System.Linq;
using RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTS.UI.Components
{
    public class StatIcon : MonoBehaviour, IUIElement<AbstractCommandable>, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI upgradeLabel;
        [SerializeField] private TextMeshProUGUI amountLabel;
        [SerializeField] private Tooltip tooltip;
        [SerializeField] private Image icon;

        [Header("Text")]
        [SerializeField] private string statName = "Damage";
        [SerializeField] private string upgradedPrefix = "Upgraded";

        public void EnableFor(AbstractCommandable item)
        {
            if (item == null || item.UnitSO == null || item.UnitSO is not UnitSO unitSO || unitSO.AttackConfig == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            int amount = unitSO.AttackConfig.Damage;

            // ✅ Szép kiírás
            amountLabel.SetText($"{statName}: {amount}");

            // Tooltip is lehet részletesebb
            tooltip.SetText($"{statName}: {amount}");

            icon.sprite = unitSO.AttackConfig.Icon;

            int upgradeCount = unitSO.Upgrades.Count((upgradeSO) =>
                unitSO.TechTree.IsResearched(item.Owner, upgradeSO)
                && upgradeSO.PropertyPath.Contains("AttackConfig/Damage")
            );

            // ✅ Szép kiírás
            upgradeLabel.SetText($"{upgradedPrefix}: {upgradeCount}");
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelInvoke();
            tooltip.Hide();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Invoke(nameof(ShowTooltip), tooltip.HoverDelay);
        }

        private void ShowTooltip()
        {
            tooltip.Show();
        }
    }
}