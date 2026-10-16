using UnityEngine;

/// <summary>
/// 魚影を画面外から画面外へ一定速度で移動させ、見えない位置でループする
/// 開始・終了の両方が完全に画面外となる座標を設定すること
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleFishSwimLoop : MonoBehaviour
{
    [SerializeField] private Vector2 _startLocalPosition = new Vector2(15f, -0.6f);
    [SerializeField] private Vector2 _endLocalPosition = new Vector2(-15f, -0.6f);
    [SerializeField, Min(1f)] private float _travelSeconds = 40f;
    [SerializeField, Min(0f)] private float _verticalAmplitude = 0.06f;
    [SerializeField, Min(0.1f)] private float _bobPeriodSeconds = 9f;
    [SerializeField, Range(0f, 1f)] private float _initialProgress = 0f;

    private float _elapsedTime;

    private void OnEnable()
    {
        // シーン再入場時にも表示位置を予測できるよう、経過時間を初期化する
        _elapsedTime = _initialProgress * Mathf.Max(1f, _travelSeconds);
        ApplyPosition();
    }

    private void Update()
    {
        float travelSeconds = Mathf.Max(1f, _travelSeconds);
        _elapsedTime = Mathf.Repeat(
            _elapsedTime + Time.unscaledDeltaTime, travelSeconds);
        ApplyPosition();
    }

    private void ApplyPosition()
    {
        float travelSeconds = Mathf.Max(1f, _travelSeconds);
        float progress = _elapsedTime / travelSeconds;
        Vector2 position = Vector2.Lerp(
            _startLocalPosition, _endLocalPosition, progress);

        float bobPeriod = Mathf.Max(0.1f, _bobPeriodSeconds);
        position.y += _verticalAmplitude * Mathf.Sin(
            2f * Mathf.PI * _elapsedTime / bobPeriod);

        transform.localPosition = new Vector3(position.x, position.y, 0f);
    }
}
