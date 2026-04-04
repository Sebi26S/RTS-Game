using RTS.Units;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTS.UI.Components
{
    [RequireComponent(typeof(Button))]
    public class UIUnitSelectedButton : MonoBehaviour, IUIElement<AbstractCommandable, UnityAction>,
                                         IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Tooltip tooltip;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            Disable();
        }

        public void EnableFor(AbstractCommandable item, UnityAction callback)
        {
            button.onClick.RemoveAllListeners();
            gameObject.SetActive(true);

            icon.sprite = item.UnitSO.Icon;
            button.onClick.AddListener(callback);

            tooltip.SetText($"{item.UnitSO.Name}\n{item.CurrentHealth} / {item.MaxHealth}");
            tooltip.Hide();
        }

        public void Disable()
        {
            button.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Invoke(nameof(ShowTooltip), tooltip.HoverDelay);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelInvoke();
            tooltip.Hide();
        }

        private void ShowTooltip()
        {
            tooltip.Show();
        }
    }
}
