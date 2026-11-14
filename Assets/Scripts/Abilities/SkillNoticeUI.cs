using TMPro;
using UnityEngine;

/// <summary>Aviso de texto en pantalla (se crea solo, sin prefabs). Uso: SkillNoticeUI.Show("texto").</summary>
public class SkillNoticeUI : MonoBehaviour
{
    private static SkillNoticeUI instance;

    private TextMeshProUGUI label;
    private float timer;

    public static void Show(string message, float seconds = 3.5f)
    {
        if (instance == null)
        {
            var go = new GameObject("SkillNoticeUI");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.78f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1200f, 70f);

            instance = go.AddComponent<SkillNoticeUI>();
            instance.label = textGo.AddComponent<TextMeshProUGUI>();
            instance.label.alignment = TextAlignmentOptions.Center;
            instance.label.fontSize = 32;
            instance.label.fontStyle = FontStyles.Bold;
            instance.label.color = new Color(1f, 0.9f, 0.4f);
            instance.label.raycastTarget = false;
        }

        instance.label.text = message;
        instance.label.gameObject.SetActive(true);
        instance.timer = seconds;
    }

    private void Update()
    {
        if (timer <= 0f)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
            label.gameObject.SetActive(false);
    }
}
