using System.Collections;
using System.Collections.Generic;
using RTS.Environment;
using RTS.Player;
using RTS.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSaveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Supplies suppliesUI;
    [SerializeField] private SaveablePrefabDatabase prefabDatabase;

    private void Start()
    {
        if (SaveSystem.ShouldLoadOnSceneStart)
        {
            StartCoroutine(LoadAfterSceneStart());
        }
    }

    private IEnumerator LoadAfterSceneStart()
    {
        yield return null;

        SaveData data = SaveSystem.Load();
        if (data == null)
        {
            SaveSystem.ShouldLoadOnSceneStart = false;
            SaveSystem.IsLoadingFromSave = false;
            yield break;
        }

        LoadGameFromData(data);

        yield return null;

        LoadOwnerSupplies(data);

        if (suppliesUI != null)
            suppliesUI.RefreshAllUI();

        SaveSystem.ShouldLoadOnSceneStart = false;
        SaveSystem.IsLoadingFromSave = false;
    }

    public void SaveGame()
    {
        Debug.Log("SaveGame elindult");

        SaveData data = new SaveData();

        data.sceneName = SceneManager.GetActiveScene().name;

        SaveOwnerSupplies(data);

        if (cameraTransform != null)
        {
            data.cameraPosition = new SerializableVector3(cameraTransform.position);
            data.cameraRotation = new SerializableQuaternion(cameraTransform.rotation);
        }

        SaveUnits(data);
        SaveBuildings(data);
        SaveResourceNodes(data);

        data.depletedResourceNodeIds = DepletedResourceRegistry.GetAll();

        Debug.Log($"Mentés előtt units: {data.units.Count}, buildings: {data.buildings.Count}, resources: {data.resourceNodes.Count}");
        SaveSystem.Save(data);
        Debug.Log("SaveGame lefutott");
    }

    public void LoadGame()
    {
        SaveData data = SaveSystem.Load();
        if (data == null)
            return;

        LoadGameFromData(data);
    }

    private void LoadGameFromData(SaveData data)
    {
        DepletedResourceRegistry.RestoreFrom(data.depletedResourceNodeIds);

        ClearCurrentSaveables();

        LoadBuildings(data);
        LoadUnits(data);
        LoadResourceNodes(data);

        LoadOwnerSupplies(data);

        if (cameraTransform != null)
        {
            cameraTransform.position = data.cameraPosition.ToVector3();
            cameraTransform.rotation = data.cameraRotation.ToQuaternion();
        }

        if (suppliesUI != null)
        {
            suppliesUI.RefreshAllUI();
        }

        Debug.Log("Save betöltve.");
    }

    private void SaveOwnerSupplies(SaveData data)
    {
        data.ownerSupplies.Clear();

        if (Supplies.Minerals == null ||
            Supplies.Wood == null ||
            Supplies.Stone == null ||
            Supplies.Population == null ||
            Supplies.PopulationLimit == null)
        {
            return;
        }

        foreach (Owner owner in System.Enum.GetValues(typeof(Owner)))
        {
            OwnerSupplySaveData ownerData = new OwnerSupplySaveData
            {
                owner = (int)owner,
                minerals = GetDictionaryValue(Supplies.Minerals, owner),
                wood = GetDictionaryValue(Supplies.Wood, owner),
                stone = GetDictionaryValue(Supplies.Stone, owner),
                population = GetDictionaryValue(Supplies.Population, owner),
                populationLimit = GetDictionaryValue(Supplies.PopulationLimit, owner)
            };

            data.ownerSupplies.Add(ownerData);
        }
    }

    private void LoadOwnerSupplies(SaveData data)
    {
        if (data.ownerSupplies == null)
            return;

        Supplies.EnsureInitialized();

        foreach (OwnerSupplySaveData ownerData in data.ownerSupplies)
        {
            Owner owner = (Owner)ownerData.owner;

            Supplies.SetOwnerResources(
                owner,
                ownerData.minerals,
                ownerData.wood,
                ownerData.stone,
                ownerData.population,
                ownerData.populationLimit
            );
        }
    }

    private void EnsureSupplyEntryExistsForAllOwners()
    {
        Supplies.EnsureInitialized();
    }

    private int GetDictionaryValue(Dictionary<Owner, int> dictionary, Owner owner)
    {
        if (dictionary == null)
            return 0;

        if (dictionary.TryGetValue(owner, out int value))
            return value;

        return 0;
    }

    private void SaveUnits(SaveData data)
    {
        AbstractUnit[] allUnits = FindObjectsByType<AbstractUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (AbstractUnit unit in allUnits)
        {
            if (unit == null)
                continue;

            if (!unit.gameObject.scene.IsValid())
                continue;

            UnitSaveData unitData = new UnitSaveData
            {
                unitId = GetUnitSaveId(unit),
                owner = (int)unit.Owner,
                currentHealth = unit.CurrentHealth,
                position = new SerializableVector3(unit.transform.position),
                rotation = new SerializableQuaternion(unit.transform.rotation)
            };

            data.units.Add(unitData);
        }
    }

    private void SaveBuildings(SaveData data)
    {
        BaseBuilding[] allBuildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (BaseBuilding building in allBuildings)
        {
            if (building == null)
                continue;

            if (!building.gameObject.scene.IsValid())
                continue;

            BuildingSaveData buildingData = new BuildingSaveData
            {
                buildingId = GetBuildingSaveId(building),
                owner = (int)building.Owner,
                currentHealth = building.CurrentHealth,
                position = new SerializableVector3(building.transform.position),
                rotation = new SerializableQuaternion(building.transform.rotation)
            };

            data.buildings.Add(buildingData);
        }
    }

    private void LoadUnits(SaveData data)
    {
        if (prefabDatabase == null)
        {
            Debug.LogError("GameSaveManager: prefabDatabase reference is missing.");
            return;
        }

        foreach (UnitSaveData unitData in data.units)
        {
            AbstractUnitSO unitSO = prefabDatabase.GetUnitSO(NormalizeSaveId(unitData.unitId));
            if (unitSO == null)
            {
                Debug.LogWarning($"Unit SO not found for save id: {unitData.unitId}");
                continue;
            }

            GameObject unitGO = Instantiate(
                unitSO.Prefab,
                unitData.position.ToVector3(),
                unitData.rotation.ToQuaternion()
            );

            AbstractCommandable commandable = unitGO.GetComponent<AbstractCommandable>();
            if (commandable != null)
            {
                commandable.Owner = (Owner)unitData.owner;
                commandable.SetHealthDirect(unitData.currentHealth);
            }
        }
    }

    private void LoadBuildings(SaveData data)
    {
        if (prefabDatabase == null)
        {
            Debug.LogError("GameSaveManager: prefabDatabase reference is missing.");
            return;
        }

        foreach (BuildingSaveData buildingData in data.buildings)
        {
            BuildingSO buildingSO = prefabDatabase.GetBuildingSO(NormalizeSaveId(buildingData.buildingId));
            if (buildingSO == null)
            {
                Debug.LogWarning($"Building SO not found for save id: {buildingData.buildingId}");
                continue;
            }

            GameObject buildingGO = Instantiate(
                buildingSO.Prefab,
                buildingData.position.ToVector3(),
                buildingData.rotation.ToQuaternion()
            );

            BaseBuilding building = buildingGO.GetComponent<BaseBuilding>();
            if (building != null)
            {
                building.Owner = (Owner)buildingData.owner;
                building.enabled = true;
                building.SetHealthDirect(buildingData.currentHealth);
            }
        }
    }

    private void SaveResourceNodes(SaveData data)
    {
        GatherableSupply[] allNodes = FindObjectsByType<GatherableSupply>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GatherableSupply node in allNodes)
        {
            if (node == null)
                continue;

            SaveableEntityId idComponent = node.GetComponent<SaveableEntityId>();
            if (idComponent == null || string.IsNullOrWhiteSpace(idComponent.UniqueId))
            {
                Debug.LogWarning($"GatherableSupply '{node.name}' is missing SaveableEntityId.");
                continue;
            }

            ResourceNodeSaveData nodeData = new ResourceNodeSaveData
            {
                nodeId = idComponent.UniqueId,
                amount = node.Amount
            };

            data.resourceNodes.Add(nodeData);
        }
    }

    private void LoadResourceNodes(SaveData data)
    {
        Dictionary<string, GatherableSupply> lookup = BuildResourceLookup();

        foreach (KeyValuePair<string, GatherableSupply> pair in lookup)
        {
            string nodeId = pair.Key;
            GatherableSupply node = pair.Value;

            if (node == null)
                continue;

            if (DepletedResourceRegistry.IsDepleted(nodeId))
            {
                Destroy(node.gameObject);
            }
        }

        foreach (ResourceNodeSaveData nodeData in data.resourceNodes)
        {
            if (string.IsNullOrWhiteSpace(nodeData.nodeId))
                continue;

            if (DepletedResourceRegistry.IsDepleted(nodeData.nodeId))
                continue;

            if (!lookup.TryGetValue(nodeData.nodeId, out GatherableSupply node) || node == null)
            {
                Debug.LogWarning($"Saved resource node not found in scene for id: {nodeData.nodeId}");
                continue;
            }

            if (nodeData.amount <= 0)
            {
                Destroy(node.gameObject);
                continue;
            }

            node.RestoreAmountFromSave(nodeData.amount);
        }
    }

    private Dictionary<string, GatherableSupply> BuildResourceLookup()
    {
        Dictionary<string, GatherableSupply> lookup = new Dictionary<string, GatherableSupply>();

        GatherableSupply[] allNodes = FindObjectsByType<GatherableSupply>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GatherableSupply node in allNodes)
        {
            if (node == null)
                continue;

            SaveableEntityId idComponent = node.GetComponent<SaveableEntityId>();
            if (idComponent == null || string.IsNullOrWhiteSpace(idComponent.UniqueId))
            {
                Debug.LogWarning($"GatherableSupply '{node.name}' is missing SaveableEntityId.");
                continue;
            }

            if (!lookup.ContainsKey(idComponent.UniqueId))
            {
                lookup.Add(idComponent.UniqueId, node);
            }
            else
            {
                Debug.LogWarning($"Duplicate resource node id found: {idComponent.UniqueId}");
            }
        }

        return lookup;
    }

    private void ClearCurrentSaveables()
    {
        AbstractUnit[] allUnits = FindObjectsByType<AbstractUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AbstractUnit unit in allUnits)
        {
            if (unit != null)
            {
                Destroy(unit.gameObject);
            }
        }

        BaseBuilding[] allBuildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BaseBuilding building in allBuildings)
        {
            if (building != null)
            {
                Destroy(building.gameObject);
            }
        }
    }

    private string NormalizeSaveId(string rawId)
    {
        if (string.IsNullOrEmpty(rawId))
            return string.Empty;

        return rawId.Replace("(Clone)", "").Trim();
    }

    private string GetUnitSaveId(AbstractUnit unit)
    {
        if (unit == null || unit.UnitSO == null)
            return string.Empty;

        return NormalizeSaveId(unit.UnitSO.name);
    }

    private string GetBuildingSaveId(BaseBuilding building)
    {
        if (building == null || building.BuildingSO == null)
            return string.Empty;

        return NormalizeSaveId(building.BuildingSO.name);
    }
}