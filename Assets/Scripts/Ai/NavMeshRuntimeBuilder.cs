using UnityEngine;
using Unity.AI.Navigation;

public class NavMeshRuntimeBuilder : MonoBehaviour
{
    [SerializeField] private NavMeshSurface surface;

    private void Start()
    {
        surface.BuildNavMesh();
    }
}