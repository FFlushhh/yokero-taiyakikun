using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 巨大泡の終了理由。
/// </summary>
public enum GiantBubbleEndReason
{
    None,
    Destroyed,
    Escaped,
    Interrupted
}

/// <summary>
/// タイトル画面の巨大泡演出。
/// 下から上へ漂いながら、ScaleとAlphaで奥行きを表現する。
/// 破壊または画面上端からの完全退出で終了する。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleGiantBubbleSecret :
    MonoBehaviour, ITitleBubbleSecretEffect
{
    [Header("References")]
    [SerializeField] private GameObject _bubbleVisual;
    [SerializeField] private Image _bubbleImage;

    [Tooltip("GiantBubbleButtonの親CanvasのRectTransform")]
    [SerializeField] private RectTransform _movementArea;

    [SerializeField] private AudioSource _audioSource;

    [Header("Hit Settings")]
    [SerializeField, Min(1)]
    private int _requiredHits = 4;

    [SerializeField, Range(0f, 0.5f)]
    private float _hitScaleIncrease = 0.12f;

    [SerializeField, Range(0f, 1f)]
    private float _damagedAlphaMultiplier = 0.65f;

    [Header("Movement")]
    [Tooltip("画面下端より下の出現位置に加えるランダムな距離")]
    [SerializeField, Min(0f)]
    private float _spawnVerticalVariation = 40f;

    [SerializeField, Min(0.1f)]
    private float _riseSpeed = 95f;

    [SerializeField, Min(0f)]
    private float _spawnMargin = 24f;

    [SerializeField, Min(0f)]
    private float _swayAmplitude = 55f;

    [SerializeField, Min(0.1f)]
    private float _swayPeriodSeconds = 5.5f;

    [Header("Depth Illusion")]
    [SerializeField, Min(0.1f)]
    private float _depthCycleSeconds = 8f;

    [SerializeField, Min(0.05f)]
    private float _nearScaleMultiplier = 1.08f;

    [SerializeField, Min(0.05f)]
    private float _farScaleMultiplier = 0.68f;

    [SerializeField, Range(0f, 1f)]
    private float _nearAlphaMultiplier = 1f;

    [SerializeField, Range(0f, 1f)]
    private float _farAlphaMultiplier = 0.75f;

    [Header("Optional Sounds")]
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _breakSound;

    private RectTransform _bubbleRect;

    private Vector2 _initialAnchoredPosition;
    private Vector3 _initialScale;
    private Color _initialColor;

    private float _elapsedSeconds;
    private float _startX;
    private float _startY;

    private int _hitCount;
    private bool _isPlaying;
    private Action _onCompleted;

    // 毎フレーム配列を生成しないため、使い回す。
    private readonly Vector3[] _bubbleCorners = new Vector3[4];
    private readonly Vector3[] _areaCorners = new Vector3[4];

    public bool IsPlaying => _isPlaying;

    public GiantBubbleEndReason LastEndReason { get; private set; }
        = GiantBubbleEndReason.None;

    private void Awake()
    {
        if (_bubbleVisual != null)
        {
            _bubbleRect =
                _bubbleVisual.GetComponent<RectTransform>();
        }

        if (_bubbleRect != null)
        {
            _initialAnchoredPosition =
                _bubbleRect.anchoredPosition;

            _initialScale = _bubbleRect.localScale;

            if (_movementArea == null)
            {
                _movementArea =
                    _bubbleRect.parent as RectTransform;
            }
        }

        if (_bubbleImage != null)
        {
            _initialColor = _bubbleImage.color;
        }

        // 元の実装と同じく、通常時は非表示。
        if (_bubbleVisual != null &&
            _bubbleVisual != gameObject)
        {
            _bubbleVisual.SetActive(false);
        }
    }

    public bool TryPlay(Action onCompleted)
    {
        if (_isPlaying ||
            !isActiveAndEnabled ||
            _bubbleVisual == null ||
            _bubbleRect == null ||
            _bubbleImage == null ||
            _movementArea == null ||
            _bubbleVisual == gameObject)
        {
            return false;
        }

        // この実装はCanvas直下・中央Anchorを前提とする。
        if (_bubbleRect.parent != _movementArea ||
            !HasCenterAnchors())
        {
            Debug.LogError(
                "巨大泡はCanvas直下に配置し、" +
                "AnchorをMiddle Centerにしてください。",
                this);

            return false;
        }

        // 現在の画面サイズに対応するRectを確定。
        Canvas.ForceUpdateCanvases();

        if (_movementArea.rect.width <= 0f ||
            _movementArea.rect.height <= 0f)
        {
            return false;
        }

        _isPlaying = true;
        _hitCount = 0;
        _elapsedSeconds = 0f;
        _onCompleted = onCompleted;
        LastEndReason = GiantBubbleEndReason.None;

        _bubbleVisual.SetActive(true);

        CalculateSpawnPosition();
        ApplyVisual();

        return true;
    }

    private void Update()
    {
        if (!_isPlaying)
        {
            return;
        }

        // 既存のたいやき演出と同じく非スケール時間を使用。
        _elapsedSeconds += Time.unscaledDeltaTime;

        ApplyVisual();

        if (IsFullyAboveScreen())
        {
            CompleteEffect(GiantBubbleEndReason.Escaped);
        }
    }

    private bool HasCenterAnchors()
    {
        return Mathf.Approximately(
                   _bubbleRect.anchorMin.x, 0.5f) &&
               Mathf.Approximately(
                   _bubbleRect.anchorMin.y, 0.5f) &&
               Mathf.Approximately(
                   _bubbleRect.anchorMax.x, 0.5f) &&
               Mathf.Approximately(
                   _bubbleRect.anchorMax.y, 0.5f);
    }


    private void CalculateSpawnPosition()
    {
        Rect areaRect = _movementArea.rect;
        Rect bubbleRect = _bubbleRect.rect;

        // 奥行きによる拡大率の最大値。
        float maxDepthScale = Mathf.Max(
            _nearScaleMultiplier,
            _farScaleMultiplier);

        // ダメージによる膨張も含め、最大サイズを見積もる。
        float maxHitScale = 1f + _hitScaleIncrease;

        float maxHalfWidth =
            bubbleRect.width *
            Mathf.Abs(_initialScale.x) *
            maxDepthScale *
            maxHitScale * 0.5f;

        // Sin関数による左右の揺れ幅を考慮する。
        // 第二周期の補正も含めて余裕を確保。
        float swayAllowance = _swayAmplitude * 1.44f;

        // 泡が左右の画面外へ出にくい抽選範囲。
        float horizontalLimit = Mathf.Max(
            0f,
            areaRect.width * 0.5f -
            maxHalfWidth -
            swayAllowance);

        // 毎回、画面下部のランダムなX座標を選ぶ。
        _startX = UnityEngine.Random.Range(
            -horizontalLimit,
            horizontalLimit);

        // 手前側の大きさを考慮し、
        // 泡の上端が画面下端より下に収まるようにする。
        float bubbleTopExtent =
            bubbleRect.height *
            Mathf.Abs(_initialScale.y) *
            _nearScaleMultiplier *
            (1f - _bubbleRect.pivot.y);

        // Y座標にも少しばらつきを与える。
        float randomVerticalOffset = UnityEngine.Random.Range(
            0f,
            _spawnVerticalVariation);

        _startY =
            -areaRect.height * 0.5f -
            bubbleTopExtent -
            _spawnMargin -
            randomVerticalOffset;
    }

    private void ApplyVisual()
    {
        float swayPeriod = Mathf.Max(
            0.1f, _swayPeriodSeconds);

        float swayPhase =
            2f * Mathf.PI *
            _elapsedSeconds / swayPeriod;

        // 弱い第二周期を加えて単調な横揺れを和らげる。
        float sway =
            _swayAmplitude *
            (
                Mathf.Sin(swayPhase) +
                0.22f *
                (
                    Mathf.Sin(swayPhase / 1.73f + 0.65f) -
                    Mathf.Sin(0.65f)
                )
            );

        // Scale変化後の巨大泡が左右からはみ出しにくくする。
        float maxHalfWidth =
            _bubbleRect.rect.width *
            Mathf.Abs(_initialScale.x) *
            Mathf.Max(_nearScaleMultiplier,
                      _farScaleMultiplier) *
            (1f + _hitScaleIncrease) * 0.5f;

        float xLimit = Mathf.Max(
            0f,
            _movementArea.rect.width * 0.5f -
            maxHalfWidth);

        float x = Mathf.Clamp(
            _startX + sway, -xLimit, xLimit);

        float y =
            _startY + _riseSpeed * _elapsedSeconds;

        _bubbleRect.anchoredPosition = new Vector2(x, y);

        // 0=手前、1=奥。
        // Cosを使うことで折り返し時の変化が滑らかになる。
        float depthPhase =
            2f * Mathf.PI *
            _elapsedSeconds /
            Mathf.Max(0.1f, _depthCycleSeconds);

        float depth = 0.5f -
            0.5f * Mathf.Cos(depthPhase);

        float depthScale = Mathf.Lerp(
            _nearScaleMultiplier,
            _farScaleMultiplier,
            depth);

        float hitProgress =
            (float)_hitCount /
            Mathf.Max(1, _requiredHits);

        // 奥行き変化とクリック時の膨張を合成する。
        float hitScale = 1f +
            _hitScaleIncrease * hitProgress;

        _bubbleRect.localScale =
            _initialScale * depthScale * hitScale;

        // 奥行きとダメージの透明度を掛け合わせる。
        float depthAlpha = Mathf.Lerp(
            _nearAlphaMultiplier,
            _farAlphaMultiplier,
            depth);

        float hitAlpha = Mathf.Lerp(
            1f,
            _damagedAlphaMultiplier,
            hitProgress);

        Color color = _initialColor;
        color.a = _initialColor.a *
            depthAlpha * hitAlpha;

        _bubbleImage.color = color;
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

            CompleteEffect(GiantBubbleEndReason.Destroyed);
            return;
        }

        PlaySound(_hitSound);

        // 移動は止めず、クリックによる見た目だけ更新する。
        ApplyVisual();
    }

    /// <summary>
    /// 泡の矩形全体がCanvasの上端を超えたか調べる。
    /// </summary>
    private bool IsFullyAboveScreen()
    {
        _bubbleRect.GetWorldCorners(_bubbleCorners);
        _movementArea.GetWorldCorners(_areaCorners);

        float bubbleBottom = float.PositiveInfinity;
        float screenTop = float.NegativeInfinity;

        for (int i = 0; i < 4; i++)
        {
            bubbleBottom = Mathf.Min(
                bubbleBottom, _bubbleCorners[i].y);

            screenTop = Mathf.Max(
                screenTop, _areaCorners[i].y);
        }

        return bubbleBottom > screenTop;
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 破壊・画面外退出・中断を共通の終了処理で扱う。
    /// </summary>
    private void CompleteEffect(GiantBubbleEndReason reason)
    {
        // 先にロックを解除し、二重終了を防止する。
        if (!_isPlaying)
        {
            return;
        }

        _isPlaying = false;
        LastEndReason = reason;

        // 演出用GameObjectは破棄せず、次回用に再利用する。
        if (_bubbleVisual != null)
        {
            _bubbleVisual.SetActive(false);
        }

        if (_bubbleRect != null)
        {
            _bubbleRect.anchoredPosition =
                _initialAnchoredPosition;

            _bubbleRect.localScale = _initialScale;
        }

        if (_bubbleImage != null)
        {
            _bubbleImage.color = _initialColor;
        }

        // 終了通知は必ず1回だけ。
        Action callback = _onCompleted;
        _onCompleted = null;

        callback?.Invoke();
    }

    private void OnDisable()
    {
        // 演出コントローラ自体が無効化された場合も
        // 上位のカウント管理がロックされたままにならないようにする。
        if (_isPlaying)
        {
            CompleteEffect(GiantBubbleEndReason.Interrupted);
        }
    }
}
