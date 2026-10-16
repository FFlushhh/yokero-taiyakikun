using UnityEngine;

/// <summary>
/// 海藻の根元を中心に、異なる周期でゆっくり揺らす
/// スプライトの Pivot は Bottom Center を想定する
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleSeaweedSway : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _amplitudeDegrees = 3.5f;
    [SerializeField, Min(0.1f)] private float _periodSeconds = 6f;
    [SerializeField] private float _phaseOffsetDegrees = 0f;

    private Quaternion _initialLocalRotation;
    private float _elapsedTime;

    private void Awake()
    {
        // インスペクターで決めた傾きを基準に揺らすため、最初の姿勢を保存する
        _initialLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        _elapsedTime = 0f;
        transform.localRotation = _initialLocalRotation;
    }

    private void Update()
    {
        float period = Mathf.Max(0.1f, _periodSeconds);
        _elapsedTime = Mathf.Repeat(_elapsedTime + Time.unscaledDeltaTime, period);

        float phase = 2f * Mathf.PI * _elapsedTime / period
            + _phaseOffsetDegrees * Mathf.Deg2Rad;
        float angle = _amplitudeDegrees * Mathf.Sin(phase);

        transform.localRotation = _initialLocalRotation
            * Quaternion.Euler(0f, 0f, angle);
    }
}
