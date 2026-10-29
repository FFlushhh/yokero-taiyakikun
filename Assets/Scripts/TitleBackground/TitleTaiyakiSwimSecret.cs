using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 隠し演出として、たいやきくんを画面外から泳がせる。
/// 正常終了・途中中断の両方で終了通知を行う。
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

    // 演出終了時に呼び出すコールバックを保持する。
    private Action _onCompleted;

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
            !isActiveAndEnabled ||
            _characterVisual == null ||
            _targetCamera == null)
        {
            return false;
        }

        _isPlaying = true;
        _onCompleted = onCompleted;

        _characterVisual.gameObject.SetActive(true);

        StartCoroutine(SwimRoutine());
        return true;
    }

    private IEnumerator SwimRoutine()
    {
        Vector3 start = ViewportToWorld(
            _startViewportPosition);

        Vector3 end = ViewportToWorld(
            _endViewportPosition);

        float elapsed = 0f;

        while (elapsed < _travelSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / _travelSeconds);

            Vector3 position =
                Vector3.Lerp(start, end, progress);

            // 上下方向に2周期の揺れを加える。
            position.y += _verticalAmplitude *
                Mathf.Sin(progress * Mathf.PI * 4f);

            _characterVisual.position = position;

            yield return null;
        }

        // 正常に泳ぎ終えた場合の終了処理。
        CompleteEffect();
    }

    /// <summary>
    /// 正常終了・中断に共通する終了処理。
    /// コールバックは最大1回だけ呼び出す。
    /// </summary>
    private void CompleteEffect()
    {
        if (!_isPlaying)
        {
            return;
        }

        // 先に再生状態を解除し、二重終了を防止。
        _isPlaying = false;

        if (_characterVisual != null)
        {
            _characterVisual.gameObject.SetActive(false);
        }

        // 再呼び出しを防ぐため、通知前に参照を消す。
        Action callback = _onCompleted;
        _onCompleted = null;

        callback?.Invoke();
    }

    private Vector3 ViewportToWorld(Vector2 viewport)
    {
        float distance =
            _worldZ - _targetCamera.transform.position.z;

        return _targetCamera.ViewportToWorldPoint(
            new Vector3(
                viewport.x,
                viewport.y,
                distance));
    }

    private void OnDisable()
    {
        // コンポーネント無効化だけではCoroutineは
        // 自動停止しないため、明示的に停止する。
        StopAllCoroutines();

        if (_isPlaying)
        {
            // 中断時も上位管理クラスへ終了を通知する。
            CompleteEffect();
        }
        else if (_characterVisual != null)
        {
            _characterVisual.gameObject.SetActive(false);
        }
    }
}
