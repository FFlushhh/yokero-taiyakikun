using UnityEngine;

/// <summary>
/// 透明な光線Spriteのアルファだけをゆっくり変え、穏やかな光量変化を作る
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class TitleLightShimmer : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _minimumAlpha = 0.55f;
    [SerializeField, Range(0f, 1f)] private float _maximumAlpha = 0.85f;
    [SerializeField, Min(0.1f)] private float _periodSeconds = 12f;

    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private float _elapsedTime;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalColor = _spriteRenderer.color;
    }

    private void OnEnable()
    {
        _elapsedTime = 0f;
        ApplyAlpha();
    }

    private void Update()
    {
        float period = Mathf.Max(0.1f, _periodSeconds);
        _elapsedTime = Mathf.Repeat(_elapsedTime + Time.unscaledDeltaTime, period);
        ApplyAlpha();
    }

    private void OnDisable()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
    }

    private void ApplyAlpha()
    {
        float period = Mathf.Max(0.1f, _periodSeconds);
        float blend = 0.5f + 0.5f * Mathf.Sin(
            2f * Mathf.PI * _elapsedTime / period);

        Color color = _originalColor;
        color.a *= Mathf.Lerp(
            Mathf.Min(_minimumAlpha, _maximumAlpha),
            Mathf.Max(_minimumAlpha, _maximumAlpha), blend);
        _spriteRenderer.color = color;
    }
}
