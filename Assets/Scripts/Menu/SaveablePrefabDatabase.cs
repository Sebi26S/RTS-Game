using System.Collections.Generic;
using RTS.Environment;
using RTS.Units;
using UnityEngine;

public class SaveablePrefabDatabase : MonoBehaviour
{
    [Header("Units")]
    [SerializeField] private AbstractUnitSO[] unitSOs;

    [Header("Buildings")]
    [SerializeField] private BuildingSO[] buildingSOs;

    private Dictionary<string, AbstractUnitSO> unitLookup;
    private Dictionary<string, BuildingSO> buildingLookup;

    private void Awake()
    {
        unitLookup = new Dictionary<string, AbstractUnitSO>();
        buildingLookup = new Dictionary<string, BuildingSO>();

        foreach (AbstractUnitSO unitSO in unitSOs)
        {
            if (unitSO == null)
                continue;

            string id = unitSO.name;
            if (!unitLookup.ContainsKey(id))
            {
                unitLookup.Add(id, unitSO);
            }
            else
            {
                Debug.LogWarning($"Duplicate unit save id found: {id}");
            }
        }

        foreach (BuildingSO buildingSO in buildingSOs)
        {
            if (buildingSO == null)
                continue;

            string id = buildingSO.name;
            if (!buildingLookup.ContainsKey(id))
            {
                buildingLookup.Add(id, buildingSO);
            }
            else
            {
                Debug.LogWarning($"Duplicate building save id found: {id}");
            }
        }
    }

    public AbstractUnitSO GetUnitSO(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        unitLookup.TryGetValue(id, out AbstractUnitSO result);
        return result;
    }

    public BuildingSO GetBuildingSO(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        buildingLookup.TryGetValue(id, out BuildingSO result);
        return result;
    }
}