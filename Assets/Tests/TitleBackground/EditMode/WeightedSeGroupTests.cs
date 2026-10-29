/*
 * WeightedSeGroupTests.cs
 *
 * 【テスト対象】
 * WeightedSeGroup / WeightedSeEntry
 *
 * 【目的】
 * 泡のクリック・破壊SEなどで使用する、重み付きランダム再生機能の
 * 抽選処理と異常値への対応が正しいことを検証する。
 *
 * 【主なテスト内容】
 * - SE候補が空またはnullの場合、nullを返すこと
 * - 未設定のAudioClipや無効なWeightが抽選対象から除外されること
 * - 大きなWeightを指定しても合計値がオーバーフローしないこと
 * - Weight比1:9の抽選結果が設定確率におおむね一致すること
 * - AudioSourceがnullまたは無効な場合、再生に失敗すること
 * - 有効なAudioClipとAudioSourceがある場合、再生処理が成功すること
 *
 * 【テスト方式】
 * Edit Mode / NUnit
 *
 * ※乱数状態を保存・復元し、他のテストへの影響を防止する。
 * ※実際の音質や聴覚上の再生結果は検証対象外。
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// EditModeで使う最小限のSerialized private field設定ヘルパー。
// Production APIはテストのためだけに公開しない。
internal static class TitleEditTestUtil
{
    public static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Field not found: " + name);
        field.SetValue(target, value);
    }

    public static WeightedSeGroup Group(params (AudioClip clip, float weight)[] values)
    {
        var entries = new WeightedSeEntry[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            entries[i] = new WeightedSeEntry();
            SetField(entries[i], "_clip", values[i].clip);
            SetField(entries[i], "_weight", values[i].weight);
        }

        var group = new WeightedSeGroup();
        SetField(group, "_entries", entries);
        return group;
    }
}

public sealed class WeightedSeGroupTests
{
    private readonly List<Object> _resources = new List<Object>();
    private UnityEngine.Random.State _randomState;

    [SetUp]
    public void SetUp() { _randomState = UnityEngine.Random.state; }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Random.state = _randomState;
        foreach (Object resource in _resources)
            if (resource != null) Object.DestroyImmediate(resource);
        _resources.Clear();
    }

    private AudioClip Clip(string name)
    {
        var clip = AudioClip.Create(name, 4410, 1, 44100, false);
        _resources.Add(clip);
        return clip;
    }

    [Test]
    public void EmptyEntries_ReturnsNull()
    {
        Assert.IsNull(TitleEditTestUtil.Group().SelectClip());
    }

    [Test]
    public void NullEntries_ReturnsNull()
    {
        var group = new WeightedSeGroup();
        TitleEditTestUtil.SetField(group, "_entries", null);
        Assert.IsNull(group.SelectClip());
    }

    [Test]
    public void InvalidWeightsAndNullClips_AreExcluded()
    {
        AudioClip valid = Clip("valid");
        AudioClip skipped = Clip("skipped");
        var group = TitleEditTestUtil.Group(
            (null, 50), (skipped, 0), (skipped, -1),
            (skipped, float.NaN), (skipped, float.PositiveInfinity),
            (skipped, float.NegativeInfinity), (valid, 2));
        for (int i = 0; i < 50; i++)
            Assert.AreSame(valid, group.SelectClip());
    }


    [Test]
    public void LargeFiniteWeights_DoNotOverflow()
    {
        AudioClip a = Clip("a");
        AudioClip b = Clip("b");

        var group = TitleEditTestUtil.Group(
            (a, float.MaxValue),
            (b, float.MaxValue)
        );

        for (int i = 0; i < 20; i++)
        {
            AudioClip selected = group.SelectClip();

            Assert.IsTrue(
                ReferenceEquals(a, selected) ||
                ReferenceEquals(b, selected),
                "大きなWeightでも、有効なAudioClipが選択される必要があります。"
            );
        }
    }

    [Test]
    public void WeightOneToNine_ApproximatelyMatchesConfiguredProbability()
    {
        AudioClip rare = Clip("rare"), common = Clip("common");
        var group = TitleEditTestUtil.Group((rare, 1), (common, 9));
        UnityEngine.Random.InitState(271828);
        int rareCount = 0;
        for (int i = 0; i < 2000; i++)
            if (ReferenceEquals(group.SelectClip(), rare)) rareCount++;
        // 乱数による変動を認めつつ、配分の大幅な破損を検出する。
        Assert.That(rareCount, Is.InRange(140, 260));
    }

    [Test]
    public void TryPlay_NullOrDisabledAudioSource_ReturnsFalse()
    {
        var group = TitleEditTestUtil.Group((Clip("sound"), 1));
        Assert.IsFalse(group.TryPlay(null));

        var go = new GameObject("DisabledAudioSource");
        _resources.Add(go);
        AudioSource source = go.AddComponent<AudioSource>();
        source.enabled = false;
        Assert.IsFalse(group.TryPlay(source));
    }

    [Test]
    public void TryPlay_NoValidClip_ReturnsFalse()
    {
        var go = new GameObject("AudioSource");
        _resources.Add(go);
        AudioSource source = go.AddComponent<AudioSource>();
        Assert.IsFalse(TitleEditTestUtil.Group((null, 1)).TryPlay(source));
    }

    [Test]
    public void TryPlay_ValidClipAndSource_ReturnsTrue()
    {
        var go = new GameObject("AudioSource");
        _resources.Add(go);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        var group = TitleEditTestUtil.Group((Clip("sound"), 1));
        Assert.IsTrue(group.TryPlay(source));
        source.Stop();
    }
}
