using System.Collections.Generic;
using System.Linq;
using RTS.EventBus;
using RTS.Events;
using RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RTS.UI.Components
{
    [RequireComponent(typeof(Button))]
    public class ControlGroup : MonoBehaviour,
        IUIElement<HashSet<AbstractCommandable>, Key, UnityAction<HashSet<AbstractCommandable>>>
    {
        [SerializeField] private Image unitIcon;
        [SerializeField] private TextMeshProUGUI groupText;
        [SerializeField] private TextMeshProUGUI unitCountText;

        private HashSet<AbstractCommandable> unitsInGroup;

        private Button button;
        private Key hotkey;
        private UnityAction<HashSet<AbstractCommandable>> onActivate;

        private void Awake() => button = GetComponent<Button>();

        private void OnEnable()
        {
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += HandleUnitDeath;
        }

        public void EnableFor(HashSet<AbstractCommandable> items, Key hotkey, UnityAction<HashSet<AbstractCommandable>> callback)
        {
            unitsInGroup = items != null ? items.ToHashSet() : new HashSet<AbstractCommandable>();
            this.hotkey = hotkey;
            onActivate = callback;

            gameObject.SetActive(true);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onActivate?.Invoke(unitsInGroup));

            SetIconAndUnitCountText();
        }


        public void Clear()
        {
            if (unitsInGroup != null)
            {
                unitsInGroup.Clear();
            }

            Disable();
        }

        public void Disable()
        {
            button.onClick.RemoveAllListeners();
            gameObject.SetActive(false);

            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= HandleUnitDeath;
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (Keyboard.current != null && Keyboard.current[hotkey].wasReleasedThisFrame)
            {
                onActivate?.Invoke(unitsInGroup);
            }
        }

        private void OnDestroy()
        {
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= HandleUnitDeath;
        }

        private void SetIconAndUnitCountText()
        {
            if (unitsInGroup == null || unitsInGroup.Count == 0)
            {
                unitCountText.SetText("0");
                if (unitIcon != null) unitIcon.sprite = null;
                return;
            }

            unitCountText.SetText(unitsInGroup.Count.ToString());

            var first = unitsInGroup.First();
            if (first != null && first.UnitSO != null && unitIcon != null)
            {
                unitIcon.sprite = first.UnitSO.Icon;
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (unitsInGroup == null) return;

            unitsInGroup.Remove(evt.Unit);

            if (unitsInGroup.Count == 0)
            {
                Disable();
                return;
            }

            SetIconAndUnitCountText();
        }
    }
}