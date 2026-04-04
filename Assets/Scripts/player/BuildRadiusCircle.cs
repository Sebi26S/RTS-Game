using UnityEngine;
using RTS.Units;

public class BuildRadiusIndicator : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private int segments = 64;
    [SerializeField] private float radius = 12f;
    [SerializeField] private float heightOffset = 0.05f;

    private Worker worker;

    public void Show(Worker targetWorker, float buildRadius)
    {
        worker = targetWorker;
        radius = buildRadius;

        gameObject.SetActive(true);
        DrawCircle();
    }

    public void Hide()
    {
        worker = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (worker == null) return;

        transform.position = worker.transform.position;
    }

    private void DrawCircle()
    {
        lineRenderer.positionCount = segments + 1;

        float angleStep = 360f / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            Vector3 pos = new Vector3(x, heightOffset, z);

            lineRenderer.SetPosition(i, pos);
        }
    }

    private void OnDisable()
    {
        if (lineRenderer == null) return;

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, Vector3.zero);
        lineRenderer.SetPosition(1, Vector3.forward);
    }
}