using RTS.UI.Components;
using RTS.Units;
using TMPro;
using UnityEngine;

namespace RTS.UI.Containers
{
    public class SingleUnitSelectedUI : MonoBehaviour, IUIElement<AbstractCommandable>
    {
        [SerializeField] private TextMeshProUGUI unitName;
        [SerializeField] private StatIcon damageIcon;
        
        public void EnableFor(AbstractCommandable item)
        {
            gameObject.SetActive(true);
            unitName.SetText(item.UnitSO.Name);
            damageIcon.EnableFor(item);
        }

        public void Disable()
        {
            gameObject.SetActive(false);
            damageIcon.Disable();
        }
    }
}
