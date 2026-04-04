using System;
using System.Collections.Generic;
using RTS.Environment;
using RTS.EventBus;
using RTS.Events;
using RTS.Units;
using TMPro;
using UnityEngine;

namespace RTS.Player
{
    public class Supplies : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI mineralsText;
        [SerializeField] private TextMeshProUGUI woodText;
        [SerializeField] private TextMeshProUGUI stoneText;
        [SerializeField] private TextMeshProUGUI populationText;

        [SerializeField] private SupplySO mineralsSO;
        [SerializeField] private SupplySO woodSO;
        [SerializeField] private SupplySO stoneSO;

        public static Dictionary<Owner, int> Minerals { get; private set; }
        public static Dictionary<Owner, int> Wood { get; private set; }
        public static Dictionary<Owner, int> Stone { get; private set; }
        public static Dictionary<Owner, int> Population { get; private set; }
        public static Dictionary<Owner, int> PopulationLimit { get; private set; }

        private static readonly string SUPPLY_TEXT_FORMAT = "{0} / {1}";
        private static readonly string POPULATION_TEXT_FORMAT = "{0} / {1}";
        private static readonly string ERROR_POPULATION_TEXT_FORMAT = "<color=#ac0000>{0}</color> / {1}";

        private void Awake()
        {
            Minerals = new Dictionary<Owner, int>();
            Wood = new Dictionary<Owner, int>();
            Stone = new Dictionary<Owner, int>();
            Population = new Dictionary<Owner, int>();
            PopulationLimit = new Dictionary<Owner, int>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                Minerals.Add(owner, 0);
                Wood.Add(owner, 0);
                Stone.Add(owner, 0);
                Population.Add(owner, 0);
                PopulationLimit.Add(owner, 0);
            }

            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            Bus<PopulationEvent>.RegisterForAll(HandlePopulationEvent);

            RefreshSupplyTexts();
            RefreshPopulationText();
        }

        private void OnDestroy()
        {
            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
            Bus<PopulationEvent>.UnregisterForAll(HandlePopulationEvent);
        }

        public static void ResetAll()
        {
            Minerals = new Dictionary<Owner, int>();
            Wood = new Dictionary<Owner, int>();
            Stone = new Dictionary<Owner, int>();
            Population = new Dictionary<Owner, int>();
            PopulationLimit = new Dictionary<Owner, int>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                Minerals[owner] = 0;
                Wood[owner] = 0;
                Stone[owner] = 0;
                Population[owner] = 0;
                PopulationLimit[owner] = 0;
            }
        }

        public static void EnsureInitialized()
        {
            Minerals ??= new Dictionary<Owner, int>();
            Wood ??= new Dictionary<Owner, int>();
            Stone ??= new Dictionary<Owner, int>();
            Population ??= new Dictionary<Owner, int>();
            PopulationLimit ??= new Dictionary<Owner, int>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                if (!Minerals.ContainsKey(owner))
                    Minerals.Add(owner, 0);

                if (!Wood.ContainsKey(owner))
                    Wood.Add(owner, 0);

                if (!Stone.ContainsKey(owner))
                    Stone.Add(owner, 0);

                if (!Population.ContainsKey(owner))
                    Population.Add(owner, 0);

                if (!PopulationLimit.ContainsKey(owner))
                    PopulationLimit.Add(owner, 0);
            }
        }

        public static void SetOwnerResources(
            Owner owner,
            int minerals,
            int wood,
            int stone,
            int population,
            int populationLimit)
        {
            EnsureInitialized();

            Minerals[owner] = minerals;
            Wood[owner] = wood;
            Stone[owner] = stone;
            Population[owner] = population;
            PopulationLimit[owner] = populationLimit;
        }

        private void HandlePopulationEvent(PopulationEvent evt)
        {
            EnsureInitialized();

            Population[evt.Owner] += evt.PopulationChange;
            PopulationLimit[evt.Owner] += evt.PopulationLimitChange;

            if (Owner.Player1 == evt.Owner)
            {
                RefreshPopulationText();
            }
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            EnsureInitialized();

            if (evt.Supply == null)
            {
                Debug.LogWarning("SupplyEvent received with null SupplySO.");
                return;
            }

            int maxStored = evt.Supply.MaxStoredAmount;

            switch (evt.Supply.Type)
            {
                case SupplyType.Minerals:
                    Minerals[evt.Owner] = Mathf.Clamp(Minerals[evt.Owner] + evt.Amount, 0, maxStored);
                    if (Owner.Player1 == evt.Owner && mineralsText != null)
                    {
                        mineralsText.SetText(string.Format(SUPPLY_TEXT_FORMAT, Minerals[evt.Owner], maxStored));
                    }
                    break;

                case SupplyType.Wood:
                    Wood[evt.Owner] = Mathf.Clamp(Wood[evt.Owner] + evt.Amount, 0, maxStored);
                    if (Owner.Player1 == evt.Owner && woodText != null)
                    {
                        woodText.SetText(string.Format(SUPPLY_TEXT_FORMAT, Wood[evt.Owner], maxStored));
                    }
                    break;

                case SupplyType.Stone:
                    Stone[evt.Owner] = Mathf.Clamp(Stone[evt.Owner] + evt.Amount, 0, maxStored);
                    if (Owner.Player1 == evt.Owner && stoneText != null)
                    {
                        stoneText.SetText(string.Format(SUPPLY_TEXT_FORMAT, Stone[evt.Owner], maxStored));
                    }
                    break;

                default:
                    Debug.LogWarning($"Unhandled supply type: {evt.Supply.Type}");
                    break;
            }
        }

        private void RefreshSupplyTexts()
        {
            EnsureInitialized();

            if (mineralsText != null && mineralsSO != null)
            {
                mineralsText.SetText(string.Format(
                    SUPPLY_TEXT_FORMAT,
                    Minerals[Owner.Player1],
                    mineralsSO.MaxStoredAmount
                ));
            }

            if (woodText != null && woodSO != null)
            {
                woodText.SetText(string.Format(
                    SUPPLY_TEXT_FORMAT,
                    Wood[Owner.Player1],
                    woodSO.MaxStoredAmount
                ));
            }

            if (stoneText != null && stoneSO != null)
            {
                stoneText.SetText(string.Format(
                    SUPPLY_TEXT_FORMAT,
                    Stone[Owner.Player1],
                    stoneSO.MaxStoredAmount
                ));
            }
        }

        private void RefreshPopulationText()
        {
            EnsureInitialized();

            if (populationText == null) return;

            int currentPopulation = Population[Owner.Player1];
            int maxPopulation = PopulationLimit[Owner.Player1];

            if (currentPopulation <= maxPopulation)
            {
                populationText.SetText(string.Format(POPULATION_TEXT_FORMAT, currentPopulation, maxPopulation));
            }
            else
            {
                populationText.SetText(string.Format(ERROR_POPULATION_TEXT_FORMAT, currentPopulation, maxPopulation));
            }
        }

        public static int GetAmount(Owner owner, SupplySO supply)
        {
            EnsureInitialized();

            if (supply == null) return 0;

            return supply.Type switch
            {
                SupplyType.Minerals => Minerals[owner],
                SupplyType.Wood => Wood[owner],
                SupplyType.Stone => Stone[owner],
                _ => 0
            };
        }

        public static bool IsAtMax(Owner owner, SupplySO supply)
        {
            if (supply == null) return true;
            return GetAmount(owner, supply) >= supply.MaxStoredAmount;
        }

        public static int GetRemainingCapacity(Owner owner, SupplySO supply)
        {
            if (supply == null) return 0;
            return Mathf.Max(0, supply.MaxStoredAmount - GetAmount(owner, supply));
        }

        public void RefreshAllUI()
        {
            RefreshSupplyTexts();
            RefreshPopulationText();
        }
    }
}