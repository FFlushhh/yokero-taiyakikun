
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 規定回数クリックすると破壊される巨大泡。
/// 破壊されるまで隠し演出を継続する。
/// </summary>
public sealed class TitleGiantBubbleSecret :
    MonoBehaviour, ITitleBubbleSecretEffect
{
    [Header("References")]
    [SerializeField] private GameObject _bubbleVisual;
    [SerializeField] private Image _bubbleImage;
    [SerializeField] private AudioSource _audioSource;

    [Header("Settings")]
    [SerializeField, Min(1)]
    private int _requiredHits = 4;

    [Header("Optional Sounds")]
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _breakSound;

    private int _hitCount;
    private bool _isPlaying;

    private Color _initialColor;
    private Vector3 _initialScale;

    private Action _onCompleted;

    private void Awake()
    {
        if (_bubbleImage != null)
        {
            _initialColor = _bubbleImage.color;
        }

        if (_bubbleVisual != null)
        {
            _initialScale =
                _bubbleVisual.transform.localScale;

            _bubbleVisual.SetActive(false);
        }
    }

    public bool TryPlay(Action onCompleted)
    {
        if (_isPlaying ||
            _bubbleVisual == null ||
            _bubbleImage == null ||
            !isActiveAndEnabled ||
            _bubbleVisual == gameObject)
        {
            return false;
        }

        _isPlaying = true;
        _hitCount = 0;
        _onCompleted = onCompleted;

        // 再出現時に見た目を初期化する。
        _bubbleImage.color = _initialColor;
        _bubbleVisual.transform.localScale = _initialScale;

        _bubbleVisual.SetActive(true);

        return true;
    }

    /// <summary>
    /// GiantBubbleButtonのOnClickから呼び出す。
    /// </summary>
    public void RegisterHit()
    {
        if (!_isPlaying)
        {
            return;
        }

        _hitCount++;

        if (_hitCount >= _requiredHits)
        {
            PlaySound(_breakSound);
            CompleteEffect();
            return;
        }

        PlaySound(_hitSound);

        // クリックするほど少し膨張させる。
        float progress =
            (float)_hitCount / Mathf.Max(1, _requiredHits);

        _bubbleVisual.transform.localScale =
            _initialScale * (1f + 0.12f * progress);

        // クリックするほど少し透明にする。
        Color color = _initialColor;
        color.a *= Mathf.Lerp(1f, 0.65f, progress);
        _bubbleImage.color = color;
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    private void CompleteEffect()
    {
        if (!_isPlaying)
        {
            return;
        }

        _isPlaying = false;
        _bubbleVisual.SetActive(false);

        Action callback = _onCompleted;
        _onCompleted = null;

        // TitleBubbleSecretTriggerへ終了を通知。
        callback?.Invoke();
    }

    private void OnDisable()
    {
        _isPlaying = false;
        _onCompleted = null;

        if (_bubbleVisual != null)
        {
            _bubbleVisual.SetActive(false);
        }
    }
}
