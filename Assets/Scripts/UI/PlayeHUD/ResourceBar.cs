using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResourceBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private string resourceName;

    private float currentFill;
    private float targetFill;

    public void SetInstant(float current, float max)
    {
        currentFill = CalculateFill(current, max);
        targetFill = currentFill;

        ApplyFill(currentFill);

        UpdateText(current, max);
    }

    public void SetTarget(float current, float max)
    {
        targetFill = CalculateFill(current, max);

        UpdateText(current, max);
    }

    private void Update()
    {
        currentFill = Mathf.Lerp(
            currentFill,
            targetFill,
            Time.deltaTime * smoothSpeed);

        if (Mathf.Abs(currentFill - targetFill) < 0.001f)
        {
            currentFill = targetFill;
        }

        ApplyFill(currentFill);
    }

    private float CalculateFill(float current, float max)
    {
        if (max <= 0f)
            return 0f;

        return Mathf.Clamp01(current / max);
    }

    private void ApplyFill(float value)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = value;
        }
    }

    private void UpdateText(float current, float max)
    {
        if (valueText != null)
        {
            valueText.text =
                $"{resourceName} : {Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
        }
    }
}