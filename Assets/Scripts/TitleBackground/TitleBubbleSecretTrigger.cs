
using System;
using UnityEngine;

/// <summary>
/// 隠し演出が実装する共通インターフェース。
/// 演出終了時にonCompletedを呼び出す。
/// </summary>
public interface ITitleBubbleSecretEffect
{
    bool TryPlay(Action onCompleted);
}

/// <summary>
/// 通常時に指定数の泡が割れたら、
/// 重みに応じて隠し演出を1つ抽選する。
/// </summary>
public sealed class TitleBubbleSecretTrigger : MonoBehaviour
{
    [Serializable]
    private sealed class SecretEffectEntry
    {
        [SerializeField]
        private MonoBehaviour _effectComponent;

        [SerializeField, Min(0f)]
        private float _weight = 1f;

        public MonoBehaviour EffectComponent => _effectComponent;
        public float Weight => _weight;
    }

    [Header("References")]
    [SerializeField]
    private TitleBubblePopController _popController;

    [Header("Trigger Settings")]
    [SerializeField, Min(1)]
    private int _requiredPopCount = 10;

    [Header("Random Secret Effects")]
    [SerializeField]
    private SecretEffectEntry[] _secretEffects =
        Array.Empty<SecretEffectEntry>();

    [Header("Secret Activation Sound")]
    [SerializeField]
    private AudioSource _activationAudioSource;

    [SerializeField]
    private WeightedSeGroup _activationSoundGroup =
        new WeightedSeGroup();

    private int _popCount;
    private bool _isEffectRunning;

    // 演出へ渡す終了通知を使い回す。
    private Action _onEffectCompleted;

    public int PopCount => _popCount;
    public bool IsEffectRunning => _isEffectRunning;

    private void Awake()
    {
        if (_popController == null)
        {
            _popController =
                GetComponent<TitleBubblePopController>();
        }

        if (_popController == null)
        {
            Debug.LogError(
                "TitleBubbleSecretTrigger: " +
                "Pop Controllerが未設定です。",
                this);

            enabled = false;
            return;
        }

        _popCount = 0;
        _isEffectRunning = false;
        _onEffectCompleted = OnEffectCompleted;
    }

    private void OnEnable()
    {
        if (_popController != null)
        {
            _popController.BubblePopped += HandleBubblePopped;
        }
    }

    private void OnDisable()
    {
        if (_popController != null)
        {
            _popController.BubblePopped -= HandleBubblePopped;
        }
    }

    private void HandleBubblePopped(Vector3 worldPosition)
    {
        // 隠し演出中は泡を割れてもカウントしない。
        if (_isEffectRunning)
        {
            return;
        }

        int requiredCount = Mathf.Max(1, _requiredPopCount);

        _popCount = Mathf.Min(_popCount + 1, requiredCount);

        if (_popCount >= requiredCount)
        {
            TryStartRandomEffect();
        }
    }

    /// <summary>
    /// 有効な演出だけを対象に、重み付き抽選を行う。
    /// </summary>
    private void TryStartRandomEffect()
    {
        if (_isEffectRunning || _secretEffects == null)
        {
            return;
        }

        double totalWeight = 0d;

        // 登録済みの有効な演出の重みを合計する。
        foreach (SecretEffectEntry entry in _secretEffects)
        {
            if (TryGetEffect(entry, out _))
            {
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning(
                "TitleBubbleSecretTrigger: " +
                "抽選可能な隠し演出がありません。",
                this);
            return;
        }

        double randomValue = (double)UnityEngine.Random.value * totalWeight;
        double accumulatedWeight = 0d;

        ITitleBubbleSecretEffect selectedEffect = null;
        ITitleBubbleSecretEffect lastValidEffect = null;

        foreach (SecretEffectEntry entry in _secretEffects)
        {
            if (!TryGetEffect(entry, out ITitleBubbleSecretEffect effect))
            {
                continue;
            }

            lastValidEffect = effect;
            accumulatedWeight += entry.Weight;

            if (selectedEffect == null &&
                randomValue < accumulatedWeight)
            {
                selectedEffect = effect;
            }
        }

        // 浮動小数点の端点誤差への対策。
        if (selectedEffect == null)
        {
            selectedEffect = lastValidEffect;
        }

        if (selectedEffect == null)
        {
            return;
        }

        // 演出開始前にロックする。
        // これ以降、BubblePoppedを受けても加算しない。
        _isEffectRunning = true;

        bool started = selectedEffect.TryPlay(_onEffectCompleted);

        if (!started)
        {
            _isEffectRunning = false;

            Debug.LogWarning(
                "TitleBubbleSecretTrigger: " +
                "選ばれた演出を開始できませんでした。",
                this);
            return;
        }

        // 発動条件を満たしたためカウントをリセット。
        _popCount = 0;

        // 隠し演出の開始に成功したときだけ追加SEを再生。
        // 通常泡SEとは同じフレームで重ねて鳴る。
        _activationSoundGroup?.TryPlay(_activationAudioSource);

        Debug.Log(
            "隠し演出を開始しました。",
            this);
    }

    /// <summary>
    /// 登録されている演出が抽選可能か確認する。
    /// </summary>
    private static bool TryGetEffect(
        SecretEffectEntry entry,
        out ITitleBubbleSecretEffect effect)
    {
        effect = null;

        if (entry == null)
        {
            return false;
        }

        float weight = entry.Weight;

        // 0以下、NaN、InfinityのWeightは抽選対象外。
        if (!(weight > 0f) ||
            float.IsNaN(weight) ||
            float.IsInfinity(weight))
        {
            return false;
        }

        MonoBehaviour component = entry.EffectComponent;

        if (component == null || !component.isActiveAndEnabled)
        {
            return false;
        }

        effect = component as ITitleBubbleSecretEffect;
        return effect != null;
    }

    /// <summary>
    /// 泳ぎ切る・巨大泡を破壊するなど、
    /// 演出が正常終了したときに呼び出される。
    /// </summary>
    private void OnEffectCompleted()
    {
        if (!_isEffectRunning)
        {
            return;
        }

        _isEffectRunning = false;
        _popCount = 0;

        Debug.Log(
            "隠し演出が終了しました。泡のカウントを再開します。",
            this);
    }
}
