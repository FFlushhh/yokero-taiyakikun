/*
 * TitleBubbleSecretTriggerTests.cs
 *
 * 【テスト対象】
 * TitleBubbleSecretTrigger
 *
 * 【目的】
 * 通常泡の破壊数に応じた隠し演出の発動、Weightによる候補選択、
 * 演出中のロック管理、終了後の再発動が正しいことを検証する。
 *
 * 【主なテスト内容】
 * - 通常泡9個の破壊では隠し演出が発動しないこと
 * - 通常泡10個の破壊で隠し演出が1回開始されること
 * - 隠し演出中に破壊した泡がカウントされないこと
 * - 演出終了後に破壊数のカウントが再開されること
 * - Weightが0の隠し演出が抽選対象から除外されること
 * - 演出開始に失敗しても実行中ロックが残らず、再試行できること
 * - Triggerを無効化すると泡の破壊イベントを受信しないこと
 * - 演出終了通知によって実行中フラグとカウントが初期化されること
 *
 * 【テスト方式】
 * Play Mode / UnityTest
 *
 * ※テスト用の疑似隠し演出を使用し、状態管理を単独で検証する。
 * ※巨大泡・たいやきの実際のアニメーションは検証対象外。
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// PlayModeテストでのみ使用するFake。実際の隠し演出を動かさず、状態遷移を検証する。
public sealed class TitleTestFakeSecretEffect : MonoBehaviour, ITitleBubbleSecretEffect
{
    public int StartCount { get; private set; }
    public bool AllowStart = true;
    private Action _callback;

    public bool TryPlay(Action onCompleted)
    {
        StartCount++;
        if (!AllowStart) return false;
        _callback = onCompleted;
        return true;
    }

    public void Complete()
    {
        Action callback = _callback;
        _callback = null;
        callback?.Invoke();
    }
}

// 同じPlayModeテストアセンブリ内から再利用するテスト専用ヘルパー。
internal static class TitlePlayTestUtil
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, Flags);
        Assert.IsNotNull(field, "Field not found: " + target.GetType().Name + "." + name);
        field.SetValue(target, value);
    }

    public static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Flags);
        Assert.IsNotNull(field, name);
        return field.GetValue(target);
    }

    public static object Call(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, Flags);
        Assert.IsNotNull(method, name);
        return method.Invoke(target, args);
    }

    public static void SendPopped(TitleBubblePopController controller)
    {
        // イベント購読・カウントロジックを、マウス入力と切り離して検証。
        var callback = GetField(controller, "BubblePopped") as Action<Vector3>;
        callback?.Invoke(Vector3.zero);
    }

    public static void SetEffectEntries(
        TitleBubbleSecretTrigger trigger,
        params (MonoBehaviour effect, float weight)[] effects)
    {
        Type entryType = typeof(TitleBubbleSecretTrigger).GetNestedType(
            "SecretEffectEntry", BindingFlags.NonPublic);
        Assert.IsNotNull(entryType);
        Array entries = Array.CreateInstance(entryType, effects.Length);
        for (int i = 0; i < effects.Length; i++)
        {
            object entry = Activator.CreateInstance(entryType, true);
            SetField(entry, "_effectComponent", effects[i].effect);
            SetField(entry, "_weight", effects[i].weight);
            entries.SetValue(entry, i);
        }
        SetField(trigger, "_secretEffects", entries);
    }
}

public sealed class TitleBubbleSecretTriggerTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private GameObject _root;
    private TitleBubblePopController _pop;
    private TitleBubbleSecretTrigger _trigger;
    private TitleTestFakeSecretEffect _fakeA;
    private TitleTestFakeSecretEffect _fakeB;

    [SetUp]
    public void SetUp()
    {
        var cameraGo = Make("TestCamera");
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        var bubbles = Make("ParticleSystem").AddComponent<ParticleSystem>();
        var main = bubbles.main;
        main.playOnAwake = false;
        bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _root = Make("TitleBubbleInteraction");
        _root.SetActive(false);
        _pop = _root.AddComponent<TitleBubblePopController>();
        _trigger = _root.AddComponent<TitleBubbleSecretTrigger>();
        TitlePlayTestUtil.SetField(_pop, "_targetCamera", camera);
        TitlePlayTestUtil.SetField(_pop, "_bubbleSystems", new[] { bubbles });
        TitlePlayTestUtil.SetField(_trigger, "_popController", _pop);
        TitlePlayTestUtil.SetField(_trigger, "_requiredPopCount", 10);

        _fakeA = Make("FakeA").AddComponent<TitleTestFakeSecretEffect>();
        _fakeB = Make("FakeB").AddComponent<TitleTestFakeSecretEffect>();
        TitlePlayTestUtil.SetEffectEntries(_trigger, (_fakeA, 1), (_fakeB, 0));
        _root.SetActive(true);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (GameObject obj in _objects)
            if (obj != null) UnityEngine.Object.Destroy(obj);
        _objects.Clear();
        yield return null;
    }

    private GameObject Make(string name)
    {
        var obj = new GameObject(name);
        _objects.Add(obj);
        return obj;
    }

    private void Pop(int count)
    {
        for (int i = 0; i < count; i++) TitlePlayTestUtil.SendPopped(_pop);
    }

    [UnityTest]
    public IEnumerator NinePops_DoesNotStartEffect_TenthStartsOnce()
    {
        Pop(9);
        Assert.AreEqual(9, _trigger.PopCount);
        Assert.AreEqual(0, _fakeA.StartCount);
        Pop(1);
        Assert.AreEqual(1, _fakeA.StartCount);
        Assert.IsTrue(_trigger.IsEffectRunning);
        Assert.AreEqual(0, _trigger.PopCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DuringEffect_PopsAreNotCounted_AfterCompleteCounterRestarts()
    {
        Pop(10);
        Pop(20);
        Assert.AreEqual(1, _fakeA.StartCount);
        Assert.AreEqual(0, _trigger.PopCount);
        _fakeA.Complete();
        Assert.IsFalse(_trigger.IsEffectRunning);
        Pop(10);
        Assert.AreEqual(2, _fakeA.StartCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ZeroWeightEffect_IsNotSelected()
    {
        Pop(10);
        Assert.AreEqual(1, _fakeA.StartCount);
        Assert.AreEqual(0, _fakeB.StartCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RejectedStart_DoesNotLockAndCanRetry()
    {
        _fakeA.AllowStart = false;
        Pop(10);
        Assert.IsFalse(_trigger.IsEffectRunning);
        Assert.AreEqual(10, _trigger.PopCount); // 現実装は達成カウントを保持。
        _fakeA.AllowStart = true;
        Pop(1);
        Assert.IsTrue(_trigger.IsEffectRunning);
        Assert.AreEqual(2, _fakeA.StartCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisabledTrigger_DoesNotObservePops()
    {
        _trigger.enabled = false;
        Pop(12);
        Assert.AreEqual(0, _trigger.PopCount);
        Assert.AreEqual(0, _fakeA.StartCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OnCompletion_ClearsLockAndCount()
    {
        Pop(10);
        _fakeA.Complete();
        Assert.IsFalse(_trigger.IsEffectRunning);
        Assert.AreEqual(0, _trigger.PopCount);
        yield return null;
    }
}
