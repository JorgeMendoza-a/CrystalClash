using UnityEngine;

/// <summary>
/// Ajusta este RectTransform al área segura de la pantalla (deja libre el
/// notch, la cámara y los bordes redondeados de los celulares).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private Rect lastSafeArea;
    private Vector2Int lastScreen;

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
        {
            Apply();
        }
    }

    private void Apply()
    {
        lastSafeArea = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = lastSafeArea.position;
        Vector2 max = lastSafeArea.position + lastSafeArea.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        var rect = (RectTransform)transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
