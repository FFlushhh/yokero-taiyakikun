using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// タイトルロゴの常時アニメーションとクリックリアクションを制御する。
/// 常時の上下移動・左右回転と、クリック時の回転・拡大縮小を合成する。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public sealed class TitleLogoInteraction :
    MonoBehaviour, IPointerClickHandler
{
    private const float TwoPi = Mathf.PI * 2f;

    [Header("Idle Animation - Vertical Floating")]
    [SerializeField, Min(0f)]
    private float _idleVerticalAmplitude = 8f;

    [SerializeField, Min(0.1f)]
    private float _idleVerticalPeriodSeconds = 3.2f;

    [Header("Idle Animation - Rotation")]
    [SerializeField, Range(0f, 5f)]
    private float _idleTiltAmplitudeDegrees = 1.2f;

    [SerializeField, Min(0.1f)]
    private float _idleTiltPeriodSeconds = 4.8f;

    [Header("Click Reaction")]
    [SerializeField, Min(0.05f)]
    private float _clickDurationSeconds = 0.32f;

    [SerializeField, Range(0f, 5f)]
    private float _clickWobbleDegrees = 2.5f;

    [SerializeField, Range(0f, 0.15f)]
    private float _clickScaleIncrease = 0.06f;

    [SerializeField, Range(0f, 8f)]
    private float _maxReactionAngleDegrees = 3.5f;

    [SerializeField, Range(0f, 0.2f)]
    private float _maxReactionScaleIncrease = 0.08f;

    [SerializeField, Min(0f)]
    private float _minimumClickIntervalSeconds = 0.08f;

    [Header("Click Sound")]
    [SerializeField] private AudioSource _clickAudioSource;
    [SerializeField] private AudioClip _clickClip;

    [SerializeField, Range(0f, 1f)]
    private float _clickVolume = 0.7f;

    [Header("Raycast")]
    [SerializeField, Range(0f, 1f)]
    private float _alphaHitTestMinimumThreshold = 0.15f;

    private RectTransform _rectTransform;
    private Image _logoImage;

    // Sceneに設定された初期Transform。
    private Vector2 _initialAnchoredPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _initialLocalScale;

    // 常時アニメーションの経過時間。
    private float _idleVerticalElapsedSeconds;
    private float _idleTiltElapsedSeconds;

    // クリックリアクションの状態。
    private float _reactionElapsedSeconds;
    private float _reactionAngleOffset;
    private float _reactionScaleOffset;
    private float _reactionStartAngleOffset;
    private float _reactionStartScaleOffset;

    private float _lastAcceptedClickTime = float.NegativeInfinity;
    private bool _isReacting;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _logoImage = GetComponent<Image>();

        _initialAnchoredPosition = _rectTransform.anchoredPosition;
        _initialLocalRotation = _rectTransform.localRotation;
        _initialLocalScale = _rectTransform.localScale;

        if (_clickAudioSource == null)
        {
            _clickAudioSource = GetComponent<AudioSource>();
        }

        ConfigureRaycast();
    }

    private void OnEnable()
    {
        if (_rectTransform == null)
        {
            return;
        }

        ResetAnimationState();
        ApplyPose();
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        UpdateIdleAnimation(deltaTime);
        UpdateClickReaction(deltaTime);
        ApplyPose();
    }

    /// <summary>
    /// 上下移動と左右回転の周期を独立して進める。
    /// 長時間動作しても経過時間が増え続けないようにする。
    /// </summary>
    private void UpdateIdleAnimation(float deltaTime)
    {
        float verticalPeriod = Mathf.Max(
            0.1f, _idleVerticalPeriodSeconds);

        float tiltPeriod = Mathf.Max(
            0.1f, _idleTiltPeriodSeconds);

        _idleVerticalElapsedSeconds = Mathf.Repeat(
            _idleVerticalElapsedSeconds + deltaTime,
            verticalPeriod);

        _idleTiltElapsedSeconds = Mathf.Repeat(
            _idleTiltElapsedSeconds + deltaTime,
            tiltPeriod);
    }

    /// <summary>
    /// ロゴの左クリック時にSEとリアクションを開始する。
    /// 連続クリックでは現在の演出状態を引き継ぐ。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left ||
            DiveSceneTransition.IsTransitioning)
        {
            return;
        }

        float currentTime = Time.unscaledTime;

        if (currentTime - _lastAcceptedClickTime <
            _minimumClickIntervalSeconds)
        {
            return;
        }

        _lastAcceptedClickTime = currentTime;

        PlayClickSound();

        // 現在の角度・Scale差分から次の演出へ移行する。
        _reactionStartAngleOffset = _reactionAngleOffset;
        _reactionStartScaleOffset = _reactionScaleOffset;

        _reactionElapsedSeconds = 0f;
        _isReacting = true;
    }

    /// <summary>
    /// クリック時の揺れと拡大を徐々に減衰させる。
    /// 連続クリックしても角度・Scaleが累積しない。
    /// </summary>
    private void UpdateClickReaction(float deltaTime)
    {
        if (!_isReacting)
        {
            return;
        }

        float duration = Mathf.Max(
            0.05f, _clickDurationSeconds);

        _reactionElapsedSeconds = Mathf.Min(
            _reactionElapsedSeconds + deltaTime,
            duration);

        float progress = _reactionElapsedSeconds / duration;
        float remaining = 1f - progress;
        float decay = remaining * remaining;

        // 左右に揺れながら振幅を減衰させる。
        float wobble = _clickWobbleDegrees *
            Mathf.Sin(4f * Mathf.PI * progress) *
            remaining;

        _reactionAngleOffset = Mathf.Clamp(
            _reactionStartAngleOffset * decay + wobble,
            -_maxReactionAngleDegrees,
            _maxReactionAngleDegrees);

        // 一瞬拡大し、自然に初期Scaleへ戻す。
        float bounce = _clickScaleIncrease *
            Mathf.Sin(Mathf.PI * progress);

        _reactionScaleOffset = Mathf.Clamp(
            _reactionStartScaleOffset * decay + bounce,
            0f,
            _maxReactionScaleIncrease);

        if (progress >= 1f)
        {
            _isReacting = false;
            _reactionAngleOffset = 0f;
            _reactionScaleOffset = 0f;
        }
    }

    /// <summary>
    /// 常時アニメーションとクリックリアクションを合成する。
    /// Transformは必ず初期値を基準に計算し、累積誤差を防ぐ。
    /// </summary>
    private void ApplyPose()
    {
        float verticalPeriod = Mathf.Max(
            0.1f, _idleVerticalPeriodSeconds);

        float tiltPeriod = Mathf.Max(
            0.1f, _idleTiltPeriodSeconds);

        // 常時アニメーション：上下方向の浮遊。
        float verticalOffset = _idleVerticalAmplitude *
            Mathf.Sin(
                TwoPi * _idleVerticalElapsedSeconds /
                verticalPeriod);

        _rectTransform.anchoredPosition = new Vector2(
            _initialAnchoredPosition.x,
            _initialAnchoredPosition.y + verticalOffset);

        // 常時アニメーション：左右への緩やかな傾き。
        float idleTiltAngle = _idleTiltAmplitudeDegrees *
            Mathf.Sin(
                TwoPi * _idleTiltElapsedSeconds /
                tiltPeriod);

        // クリック時の追加回転を合成する。
        float finalAngle = idleTiltAngle + _reactionAngleOffset;

        _rectTransform.localRotation =
            _initialLocalRotation *
            Quaternion.Euler(0f, 0f, finalAngle);

        // Scaleはクリック時だけ変化させる。
        float scaleMultiplier = 1f + _reactionScaleOffset;

        _rectTransform.localScale = new Vector3(
            _initialLocalScale.x * scaleMultiplier,
            _initialLocalScale.y * scaleMultiplier,
            _initialLocalScale.z);
    }

    /// <summary>
    /// 専用SEを再生する。
    /// AudioClip未設定でもアニメーションは継続する。
    /// </summary>
    private void PlayClickSound()
    {
        if (_clickClip == null ||
            _clickAudioSource == null ||
            !_clickAudioSource.isActiveAndEnabled)
        {
            return;
        }

        _clickAudioSource.PlayOneShot(
            _clickClip,
            Mathf.Clamp01(_clickVolume));
    }

    /// <summary>
    /// ロゴ画像のアルファ値に基づくクリック判定を設定する。
    /// 利用できない場合は矩形判定へフォールバックする。
    /// </summary>
    private void ConfigureRaycast()
    {
        _logoImage.alphaHitTestMinimumThreshold = 0f;

        if (_alphaHitTestMinimumThreshold <= 0f)
        {
            return;
        }

        Sprite sprite = _logoImage.sprite;

        if (sprite == null ||
            sprite.texture == null ||
            !sprite.texture.isReadable)
        {
            Debug.LogWarning(
                "TitleLogoInteraction: " +
                "透明部分のクリック判定には、" +
                "ロゴ画像のRead/Write Enabledが必要です。",
                this);

            return;
        }

        _logoImage.alphaHitTestMinimumThreshold =
            _alphaHitTestMinimumThreshold;
    }

    /// <summary>
    /// アニメーションの経過時間とクリック状態を初期化する。
    /// </summary>
    private void ResetAnimationState()
    {
        _idleVerticalElapsedSeconds = 0f;
        _idleTiltElapsedSeconds = 0f;

        _reactionElapsedSeconds = 0f;
        _reactionAngleOffset = 0f;
        _reactionScaleOffset = 0f;

        _reactionStartAngleOffset = 0f;
        _reactionStartScaleOffset = 0f;

        _lastAcceptedClickTime = float.NegativeInfinity;
        _isReacting = false;
    }

    private void OnDisable()
    {
        if (_rectTransform == null)
        {
            return;
        }

        ResetAnimationState();

        // コンポーネント無効化時に初期Transformへ戻す。
        _rectTransform.anchoredPosition = _initialAnchoredPosition;
        _rectTransform.localRotation = _initialLocalRotation;
        _rectTransform.localScale = _initialLocalScale;
    }
}
