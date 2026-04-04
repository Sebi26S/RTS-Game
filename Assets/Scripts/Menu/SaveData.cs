using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public string sceneName;

    public List<OwnerSupplySaveData> ownerSupplies = new();

    public SerializableVector3 cameraPosition;
    public SerializableQuaternion cameraRotation;

    public List<UnitSaveData> units = new();
    public List<BuildingSaveData> buildings = new();
    public List<ResourceNodeSaveData> resourceNodes = new();
    public List<string> depletedResourceNodeIds = new();
}

[Serializable]
public class OwnerSupplySaveData
{
    public int owner;
    public int minerals;
    public int wood;
    public int stone;
    public int population;
    public int populationLimit;
}

[Serializable]
public class UnitSaveData
{
    public string unitId;
    public int owner;
    public int currentHealth;

    public SerializableVector3 position;
    public SerializableQuaternion rotation;
}

[Serializable]
public class BuildingSaveData
{
    public string buildingId;
    public int owner;
    public int currentHealth;

    public SerializableVector3 position;
    public SerializableQuaternion rotation;
}

[Serializable]
public class ResourceNodeSaveData
{
    public string nodeId;
    public int amount;
}

[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public SerializableVector3(UnityEngine.Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public UnityEngine.Vector3 ToVector3()
    {
        return new UnityEngine.Vector3(x, y, z);
    }
}

[Serializable]
public struct SerializableQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w;

    public SerializableQuaternion(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public SerializableQuaternion(UnityEngine.Quaternion q)
    {
        x = q.x;
        y = q.y;
        z = q.z;
        w = q.w;
    }

    public UnityEngine.Quaternion ToQuaternion()
    {
        return new UnityEngine.Quaternion(x, y, z, w);
    }
}