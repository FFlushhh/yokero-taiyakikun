/*
 * TitleGiantBubbleSecretTests.cs
 *
 * 【テスト対象】
 * TitleGiantBubbleSecret
 *
 * 【目的】
 * 巨大泡の出現・クリック・破壊・画面外退出・中断処理が
 * 正しく動作し、演出状態と終了通知が適切に管理されることを検証する。
 *
 * 【主なテスト内容】
 * - TryPlay成功時に巨大泡が表示されること
 * - 演出再生中の二重起動が防止されること
 * - 4回目のクリックで巨大泡が破壊されること
 * - 破壊時に終了通知が1回だけ呼ばれること
 * - クリックに応じてScale・Alphaが変化すること
 * - 演出終了後に表示状態が初期化されること
 * - 演出途中で無効化した場合、Interruptedとして終了すること
 * - 画面外への退出時に破裂VFXが再生されないこと
 * - 必要なVisual参照がない場合、演出開始を拒否すること
 *
 * 【テスト方式】
 * Play Mode / UnityTest
 *
 * ※テスト用Canvas・Image・Particle Systemを生成して検証する。
 * ※巨大泡の表示とライフサイクルの整合性を重点的に確認する。
 */

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TitleGiantBubbleSecretTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private TitleGiantBubbleSecret _giant;
    private GameObject _visual;
    private RectTransform _rect;
    private Image _image;
    private ParticleSystem _burst;
    private RectTransform _area;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        GameObject canvasGo = Make("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _area = canvasGo.GetComponent<RectTransform>();

        _visual = new GameObject("GiantBubbleVisual", typeof(RectTransform), typeof(Image));
        _objects.Add(_visual);
        _visual.transform.SetParent(canvasGo.transform, false);
        _rect = _visual.GetComponent<RectTransform>();
        _rect.anchorMin = new Vector2(.5f, .5f);
        _rect.anchorMax = new Vector2(.5f, .5f);
        _rect.sizeDelta = new Vector2(180f, 180f);
        _image = _visual.GetComponent<Image>();

        var cameraGo = Make("BurstCamera");
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        _burst = Make("BurstParticles").AddComponent<ParticleSystem>();
        _burst.transform.position = Vector3.zero;
        var main = _burst.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 5f;
        main.maxParticles = 100;
        var emission = _burst.emission;
        emission.rateOverTime = 0;
        _burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        GameObject host = Make("GiantHost");
        host.SetActive(false);
        _giant = host.AddComponent<TitleGiantBubbleSecret>();
        TitlePlayTestUtil.SetField(_giant, "_bubbleVisual", _visual);
        TitlePlayTestUtil.SetField(_giant, "_bubbleImage", _image);
        TitlePlayTestUtil.SetField(_giant, "_movementArea", _area);
        TitlePlayTestUtil.SetField(_giant, "_burstParticles", _burst);
        TitlePlayTestUtil.SetField(_giant, "_burstCamera", camera);
        TitlePlayTestUtil.SetField(_giant, "_requiredHits", 4);
        host.SetActive(true);
        Canvas.ForceUpdateCanvases();
        yield return null;
        Assert.Greater(_area.rect.width, 0f, "Canvasのレイアウトが確定していません。");
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
        var go = new GameObject(name);
        _objects.Add(go);
        return go;
    }

    [UnityTest]
    public IEnumerator TryPlay_ShowsBubble_PreventsDoubleStart()
    {
        Assert.IsTrue(_giant.TryPlay(null));
        Assert.IsTrue(_visual.activeSelf);
        Assert.IsTrue(_giant.IsPlaying);
        Assert.IsFalse(_giant.TryPlay(null));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FourthHit_DestroysAndCallsCompletionOnce()
    {
        int completed = 0;
        Assert.IsTrue(_giant.TryPlay(() => completed++));
        for (int i = 0; i < 3; i++)
        {
            _giant.RegisterHit();
            Assert.IsTrue(_giant.IsPlaying);
            Assert.AreEqual(0, completed);
        }
        _giant.RegisterHit();
        Assert.IsFalse(_giant.IsPlaying);
        Assert.AreEqual(GiantBubbleEndReason.Destroyed, _giant.LastEndReason);
        Assert.AreEqual(1, completed);
        Assert.IsFalse(_visual.activeSelf);
        Assert.Greater(_burst.particleCount, 0, "破裂Particleが放出されていません。");
        _giant.RegisterHit();
        Assert.AreEqual(1, completed);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HitChangesScaleAndAlpha_AndAfterCompletionResets()
    {
        Vector3 originalScale = _rect.localScale;
        Color originalColor = _image.color;
        Assert.IsTrue(_giant.TryPlay(null));
        Vector3 beforeHitScale = _rect.localScale;
        float beforeAlpha = _image.color.a;
        _giant.RegisterHit();
        Assert.Greater(_rect.localScale.x, beforeHitScale.x);
        Assert.Less(_image.color.a, beforeAlpha);
        _giant.RegisterHit();
        _giant.RegisterHit();
        _giant.RegisterHit();
        Assert.AreEqual(originalScale.x, _rect.localScale.x, .001f);
        Assert.AreEqual(originalColor.a, _image.color.a, .001f);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisableHost_EndsWithInterrupted_Once()
    {
        int count = 0;
        Assert.IsTrue(_giant.TryPlay(() => count++));
        _giant.enabled = false;
        Assert.AreEqual(GiantBubbleEndReason.Interrupted, _giant.LastEndReason);
        Assert.AreEqual(1, count);
        Assert.IsFalse(_giant.IsPlaying);
        _giant.enabled = true;
        Assert.IsTrue(_giant.TryPlay(() => count++));
        _giant.RegisterHit();
        _giant.RegisterHit();
        _giant.RegisterHit();
        _giant.RegisterHit();
        Assert.AreEqual(2, count);
        yield return null;
    }

    [UnityTest]
    public IEnumerator EscapingScreen_DoesNotEmitBurst()
    {
        // 画面外へ確実に退出できる上昇速度を設定する。
        TitlePlayTestUtil.SetField(
            _giant, "_riseSpeed", 100000f);

        int completed = 0;

        Assert.IsTrue(_giant.TryPlay(() => completed++));

        // フレームの実経過時間に依存しないように、
        // 演出の内部経過時間を明示的に設定する。
        TitlePlayTestUtil.SetField(
            _giant, "_elapsedSeconds", 1f);

        // Updateを1回実行して、位置更新と退出判定を行う。
        TitlePlayTestUtil.Call(_giant, "Update");

        // 画面外への退出として正常終了すること。
        Assert.IsFalse(_giant.IsPlaying);

        Assert.AreEqual(
            GiantBubbleEndReason.Escaped,
            _giant.LastEndReason);

        // 終了通知は1回だけ。
        Assert.AreEqual(1, completed);

        // 画面外退出では破裂VFXを発生させない。
        Assert.AreEqual(0, _burst.particleCount);

        yield return null;
    }

    [UnityTest]
    public IEnumerator MissingVisualReference_RejectsStart()
    {
        _giant.enabled = false;
        TitlePlayTestUtil.SetField(_giant, "_bubbleVisual", null);
        _giant.enabled = true;
        Assert.IsFalse(_giant.TryPlay(null));
        yield return null;
    }
}
