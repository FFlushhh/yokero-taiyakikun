using System;
using UnityEngine;

/// <summary>
/// 1つの効果音候補と抽選用Weight。
/// </summary>
[Serializable]
public sealed class WeightedSeEntry
{
    [SerializeField]
    private AudioClip _clip;

    [SerializeField, Min(0f)]
    private float _weight = 1f;

    public AudioClip Clip => _clip;
    public float Weight => _weight;
}

/// <summary>
/// Inspectorから設定できる、重み付きSEグループ。
/// 有効なAudioClipから1つ選択し、指定AudioSourceで再生する。
/// </summary>
[Serializable]
public sealed class WeightedSeGroup
{
    [SerializeField, Range(0f, 1f)]
    private float _volume = 0.65f;

    [SerializeField]
    private WeightedSeEntry[] _entries =
        Array.Empty<WeightedSeEntry>();

    /// <summary>
    /// 正の有限Weightを持つ候補から、
    /// 重みに応じてAudioClipを1つ選ぶ。
    /// 有効候補がなければnullを返す。
    /// </summary>
    public AudioClip SelectClip()
    {
        if (_entries == null || _entries.Length == 0)
        {
            return null;
        }

        double totalWeight = 0d;
        AudioClip lastValidClip = null;

        // 有効な候補だけを集計する。
        // doubleで加算してWeight合計のオーバーフローを避ける。
        for (int i = 0; i < _entries.Length; i++)
        {
            WeightedSeEntry entry = _entries[i];

            if (!IsValidEntry(entry))
            {
                continue;
            }

            totalWeight += entry.Weight;
            lastValidClip = entry.Clip;
        }

        if (totalWeight <= 0d)
        {
            return null;
        }

        double randomValue =
            UnityEngine.Random.value * totalWeight;

        double accumulatedWeight = 0d;

        for (int i = 0; i < _entries.Length; i++)
        {
            WeightedSeEntry entry = _entries[i];

            if (!IsValidEntry(entry))
            {
                continue;
            }

            accumulatedWeight += entry.Weight;

            if (randomValue < accumulatedWeight)
            {
                return entry.Clip;
            }
        }

        // UnityEngine.Random.valueは1を返す場合がある。
        // その端点や丸め誤差でも有効な候補を返す。
        return lastValidClip;
    }

    /// <summary>
    /// 音源とAudioSourceが有効な場合だけ再生する。
    /// 再生を開始できた場合はtrue。
    /// </summary>
    public bool TryPlay(AudioSource audioSource)
    {
        if (audioSource == null ||
            !audioSource.isActiveAndEnabled)
        {
            return false;
        }

        AudioClip clip = SelectClip();

        if (clip == null)
        {
            return false;
        }

        audioSource.PlayOneShot(
            clip,
            Mathf.Clamp01(_volume));

        return true;
    }

    /// <summary>
    /// 未設定Clip、不正なWeight、Weight=0を抽選から除外。
    /// </summary>
    private static bool IsValidEntry(WeightedSeEntry entry)
    {
        if (entry == null || entry.Clip == null)
        {
            return false;
        }

        float weight = entry.Weight;

        return weight > 0f &&
               !float.IsNaN(weight) &&
               !float.IsInfinity(weight);
    }
}
