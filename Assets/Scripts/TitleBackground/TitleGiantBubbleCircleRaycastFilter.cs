using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UIの巨大泡を円形のクリック領域で判定する。
/// RectTransformの移動・Scale変更にも追従する。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class TitleGiantBubbleCircleRaycastFilter :
    MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField, Range(0.5f, 1.1f)]
    private float _radiusMultiplier = 0.95f;

    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public bool IsRaycastLocationValid(
        Vector2 screenPoint,
        Camera eventCamera)
    {
        if (_rectTransform == null)
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = _rectTransform.rect;

        Vector2 offset = localPoint - rect.center;

        float radius =
            Mathf.Min(rect.width, rect.height) *
            0.5f * _radiusMultiplier;

        return offset.sqrMagnitude <= radius * radius;
    }
}