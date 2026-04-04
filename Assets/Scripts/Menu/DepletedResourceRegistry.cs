using System.Collections.Generic;

public static class DepletedResourceRegistry
{
    private static readonly HashSet<string> depletedNodeIds = new();

    public static void MarkDepleted(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        depletedNodeIds.Add(nodeId);
    }

    public static bool IsDepleted(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return false;

        return depletedNodeIds.Contains(nodeId);
    }

    public static void Clear()
    {
        depletedNodeIds.Clear();
    }

    public static List<string> GetAll()
    {
        return new List<string>(depletedNodeIds);
    }

    public static void RestoreFrom(List<string> ids)
    {
        depletedNodeIds.Clear();

        if (ids == null)
            return;

        foreach (string id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                depletedNodeIds.Add(id);
            }
        }
    }
}