using UnityEngine;

[DisallowMultipleComponent]
public class SaveableEntityId : MonoBehaviour
{
    [SerializeField] private string uniqueId;

    public string UniqueId => uniqueId;

#if UNITY_EDITOR
    [SerializeField, HideInInspector] private string lastKnownScenePath;
    [SerializeField, HideInInspector] private string lastKnownObjectName;

    private void OnValidate()
    {
        bool needsNewId = string.IsNullOrWhiteSpace(uniqueId);

        string currentScenePath = gameObject.scene.path;
        string currentObjectName = gameObject.name;

        if (lastKnownScenePath != currentScenePath || lastKnownObjectName != currentObjectName)
        {
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                needsNewId = true;
            }
        }

        if (needsNewId)
        {
            uniqueId = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        lastKnownScenePath = currentScenePath;
        lastKnownObjectName = currentObjectName;
    }
#endif
}