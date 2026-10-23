using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 隠し演出として、たいやきくんを画面外から泳がせる。
/// 既存の泳ぐAnimatorを持つVisualを使用する。
/// </summary>
public sealed class TitleTaiyakiSwimSecret :
    MonoBehaviour, ITitleBubbleSecretEffect
{
    [Header("References")]
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private Transform _characterVisual;

    [Header("Movement")]
    [SerializeField]
    private Vector2 _startViewportPosition =
        new Vector2(-0.15f, 0.36f);

    [SerializeField]
    private Vector2 _endViewportPosition =
        new Vector2(1.15f, 0.36f);

    [SerializeField] private float _worldZ = 0f;

    [SerializeField, Min(0.1f)]
    private float _travelSeconds = 6f;

    [SerializeField, Min(0f)]
    private float _verticalAmplitude = 0.12f;

    private bool _isPlaying;

    private void Awake()
    {
        if (_targetCamera == null)
        {
            _targetCamera = Camera.main;
        }

        if (_characterVisual != null)
        {
            _characterVisual.gameObject.SetActive(false);
        }
    }

    public bool TryPlay(Action onCompleted)
    {
        if (_isPlaying ||
            _characterVisual == null ||
            _targetCamera == null)
        {
            return false;
        }

        _isPlaying = true;
        _characterVisual.gameObject.SetActive(true);

        StartCoroutine(SwimRoutine(onCompleted));
        return true;
    }

    private IEnumerator SwimRoutine(Action onCompleted)
    {
        Vector3 start = ViewportToWorld(_startViewportPosition);
        Vector3 end = ViewportToWorld(_endViewportPosition);

        float elapsed = 0f;

        while (elapsed < _travelSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / _travelSeconds);

            Vector3 position =
                Vector3.Lerp(start, end, progress);

            // 軽い上下運動を追加する。
            position.y += _verticalAmplitude *
                Mathf.Sin(progress * Mathf.PI * 4f);

            _characterVisual.position = position;
            yield return null;
        }

        _characterVisual.gameObject.SetActive(false);
        _isPlaying = false;

        // 条件管理クラスに演出終了を通知。
        onCompleted?.Invoke();
    }

    private Vector3 ViewportToWorld(Vector2 viewport)
    {
        float distance =
            _worldZ - _targetCamera.transform.position.z;

        return _targetCamera.ViewportToWorldPoint(
            new Vector3(viewport.x, viewport.y, distance));
    }

    private void OnDisable()
    {
        _isPlaying = false;

        if (_characterVisual != null)
        {
            _characterVisual.gameObject.SetActive(false);
        }
    }
}
