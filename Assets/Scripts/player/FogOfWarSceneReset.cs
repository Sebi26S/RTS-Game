using UnityEngine;

public class FogOfWarSceneReset : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera fogOfWarCamera;
    [SerializeField] private Camera exploredFogOfWarCamera;

    [Header("Source Render Textures")]
    [SerializeField] private RenderTexture fogOfWarSourceTexture;
    [SerializeField] private RenderTexture exploredFogOfWarSourceTexture;

    [Header("Materials")]
    [SerializeField] private Material fogOfWarMaterial;
    [SerializeField] private Material exploredFogOfWarMaterial;

    [Header("Clear Colors")]
    [SerializeField] private Color fogClearColor = Color.black;
    [SerializeField] private Color exploredClearColor = Color.black;

    private RenderTexture fogRuntimeTexture;
    private RenderTexture exploredRuntimeTexture;

    private Material fogRuntimeMaterial;
    private Material exploredRuntimeMaterial;

    private static readonly int FogTexId =
        Shader.PropertyToID("_Fog_of_War_Render_Texture");

    private static readonly int ExploredTexId =
        Shader.PropertyToID("_Explored_Fog_of_War_Render_Texture");

    private static readonly int FrameCopyTexId =
        Shader.PropertyToID("_FrameCopyTex");

    private void Awake()
    {
        CreateRuntimeTextures();
        CreateRuntimeMaterials();
        AssignEverything();
        ClearTextures();
    }

    private void CreateRuntimeTextures()
    {
        if (fogOfWarSourceTexture != null)
        {
            fogRuntimeTexture = new RenderTexture(fogOfWarSourceTexture.descriptor);
            fogRuntimeTexture.name = fogOfWarSourceTexture.name + "_Runtime_" + gameObject.scene.name;
            fogRuntimeTexture.Create();
        }

        if (exploredFogOfWarSourceTexture != null)
        {
            exploredRuntimeTexture = new RenderTexture(exploredFogOfWarSourceTexture.descriptor);
            exploredRuntimeTexture.name = exploredFogOfWarSourceTexture.name + "_Runtime_" + gameObject.scene.name;
            exploredRuntimeTexture.Create();
        }
    }

    private void CreateRuntimeMaterials()
    {
        if (fogOfWarMaterial != null)
            fogRuntimeMaterial = new Material(fogOfWarMaterial);

        if (exploredFogOfWarMaterial != null)
            exploredRuntimeMaterial = new Material(exploredFogOfWarMaterial);
    }

    private void AssignEverything()
    {
        if (fogOfWarCamera != null && fogRuntimeTexture != null)
            fogOfWarCamera.targetTexture = fogRuntimeTexture;

        if (exploredFogOfWarCamera != null && exploredRuntimeTexture != null)
            exploredFogOfWarCamera.targetTexture = exploredRuntimeTexture;

        if (fogRuntimeMaterial != null)
        {
            if (fogRuntimeTexture != null)
                fogRuntimeMaterial.SetTexture(FogTexId, fogRuntimeTexture);

            if (exploredRuntimeTexture != null)
                fogRuntimeMaterial.SetTexture(ExploredTexId, exploredRuntimeTexture);
        }

        if (exploredRuntimeMaterial != null && exploredRuntimeTexture != null)
        {
            exploredRuntimeMaterial.SetTexture(FrameCopyTexId, exploredRuntimeTexture);
        }

        ApplyRuntimeMaterialToSceneObjects();
    }

    private void ApplyRuntimeMaterialToSceneObjects()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        foreach (Renderer r in renderers)
        {
            if (r == null || r.sharedMaterial == null)
                continue;

            if (fogOfWarMaterial != null && r.sharedMaterial == fogOfWarMaterial && fogRuntimeMaterial != null)
            {
                r.material = fogRuntimeMaterial;
            }

            if (exploredFogOfWarMaterial != null && r.sharedMaterial == exploredFogOfWarMaterial && exploredRuntimeMaterial != null)
            {
                r.material = exploredRuntimeMaterial;
            }
        }
    }

    private void ClearTextures()
    {
        ClearRenderTexture(fogRuntimeTexture, fogClearColor);
        ClearRenderTexture(exploredRuntimeTexture, exploredClearColor);
    }

    private void ClearRenderTexture(RenderTexture rt, Color clearColor)
    {
        if (rt == null)
            return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, clearColor);
        RenderTexture.active = previous;
    }

    private void OnDestroy()
    {
        if (fogOfWarCamera != null && fogOfWarCamera.targetTexture == fogRuntimeTexture)
            fogOfWarCamera.targetTexture = null;

        if (exploredFogOfWarCamera != null && exploredFogOfWarCamera.targetTexture == exploredRuntimeTexture)
            exploredFogOfWarCamera.targetTexture = null;

        if (fogRuntimeTexture != null)
        {
            if (fogRuntimeTexture.IsCreated())
                fogRuntimeTexture.Release();
            Destroy(fogRuntimeTexture);
        }

        if (exploredRuntimeTexture != null)
        {
            if (exploredRuntimeTexture.IsCreated())
                exploredRuntimeTexture.Release();
            Destroy(exploredRuntimeTexture);
        }

        if (fogRuntimeMaterial != null)
            Destroy(fogRuntimeMaterial);

        if (exploredRuntimeMaterial != null)
            Destroy(exploredRuntimeMaterial);
    }
}