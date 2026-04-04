using UnityEngine;
using UnityEngine.UI;

public class HealthTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image borderImage;

    [Header("Colors")]
    [SerializeField] private Color highColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color midColor  = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color lowColor  = new Color(1f, 0.25f, 0.25f, 1f);

    private float fullWidth;

    private void Awake()
    {
        if (fillRect != null)
            fullWidth = fillRect.sizeDelta.x;
    }

    public void UpdateBar(float currentHealth, float maxHealth)
    {
        if (fillRect == null || fillImage == null || maxHealth <= 0f) return;

        float t = Mathf.Clamp01(currentHealth / maxHealth);

        Vector2 min = fillRect.anchorMin;
        Vector2 max = fillRect.anchorMax;
        max.x = t;
        fillRect.anchorMin = min;
        fillRect.anchorMax = max;

        if (t >= 0.6f) fillImage.color = highColor;
        else if (t >= 0.3f) fillImage.color = midColor;
        else fillImage.color = lowColor;
    }

    public void SetOwner(RTS.Units.Owner owner)
    {
        if (borderImage == null) return;

        switch (owner)
        {
            case RTS.Units.Owner.Player1:
                borderImage.color = Color.cyan;
                break;

            case RTS.Units.Owner.Unowned:
                borderImage.color = Color.yellow;
                break;

            default: 
                borderImage.color = Color.red;
                break;
        }
    }
}