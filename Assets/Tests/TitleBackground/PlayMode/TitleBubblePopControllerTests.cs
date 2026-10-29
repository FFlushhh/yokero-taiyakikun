/*
 * TitleBubblePopControllerTests.cs
 *
 * 【テスト対象】
 * TitleBubblePopController
 *
 * 【目的】
 * タイトル画面の通常泡に対するクリック判定・Particleの削除・
 * 破壊イベント通知が正しく機能することを検証する。
 *
 * 【主なテスト内容】
 * - 泡をクリックすると対象Particleが削除されること
 * - 泡の破壊成功時にBubblePoppedイベントが1回発火すること
 * - 泡が存在しない位置をクリックしても破壊されないこと
 * - 複数のParticleが重なっていても1クリックで1個だけ破壊すること
 * - UIのGraphicRaycasterによる判定が通常泡より優先されること
 *
 * 【テスト方式】
 * Play Mode / UnityTest
 *
 * ※テスト用Camera・Particle System・UIを生成して検証する。
 * ※Input Systemによる入力イベントを利用してクリック処理を確認する。
 */

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed class TitleBubblePopControllerTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private Camera _camera;
    private ParticleSystem _particles;
    private TitleBubblePopController _controller;
    private int _eventCount;

    // 物理マウスに依存しないテスト用Input System
    private readonly InputTestFixture _inputFixture = new InputTestFixture();

    // テスト中に使用する仮想マウス
    private Mouse _testMouse;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // テストケース間でイベント回数を引き継がない
        _eventCount = 0;

        // Input Systemの状態を保存し、テスト用に初期化する
        _inputFixture.Setup();

        // テストで使用する仮想マウスを作成する
        _testMouse = InputSystem.AddDevice<Mouse>();
        
        GameObject cameraGo = Make("Camera");
        _camera = cameraGo.AddComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = 5f;
        _camera.transform.position = new Vector3(0, 0, -10);

        _particles = Make("BubbleParticles").AddComponent<ParticleSystem>();
        var main = _particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 20;
        main.maxParticles = 32;
        var emission = _particles.emission;
        emission.rateOverTime = 0;
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _particles.Play();

        GameObject root = Make("Interaction");
        root.SetActive(false);
        _controller = root.AddComponent<TitleBubblePopController>();
        TitlePlayTestUtil.SetField(_controller, "_targetCamera", _camera);
        TitlePlayTestUtil.SetField(_controller, "_bubbleSystems", new[] { _particles });
        root.SetActive(true);
        _controller.BubblePopped += _ => _eventCount++;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // テスト用GameObjectを破棄する
        foreach (var obj in _objects)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        _objects.Clear();

        // Destroyによる破棄処理を完了させる
        yield return null;

        // テスト開始前のInput Systemへ復元する
        _inputFixture.TearDown();
    }

    private GameObject Make(string name)
    {
        var go = new GameObject(name);
        _objects.Add(go);
        return go;
    }

    private void Emit(Vector3 world)
    {
        _particles.Emit(new ParticleSystem.EmitParams {
            position = world,
            startLifetime = 20f,
            startSize = 1f
        }, 1);
    }

    private void Click(Vector3 world)
    {
        Vector3 screen = _camera.WorldToScreenPoint(world);
        TitlePlayTestUtil.Call(_controller, "TryPopBubble", (Vector2)screen);
    }

    [UnityTest]
    public IEnumerator ClickingBubble_EmitsExactlyOneEventAndRemovesParticle()
    {
        Emit(Vector3.zero);
        Assert.AreEqual(1, _particles.particleCount);
        Click(Vector3.zero);
        Assert.AreEqual(1, _eventCount);
        _particles.Simulate(0.02f, true, false);
        Assert.AreEqual(0, _particles.particleCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ClickingEmptyArea_DoesNotEmitEvent()
    {
        Emit(Vector3.zero);
        Click(new Vector3(4f, 4f, 0f));
        Assert.AreEqual(0, _eventCount);
        Assert.AreEqual(1, _particles.particleCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OverlappingParticles_OneClickRemovesOnlyOne()
    {
        Emit(Vector3.zero);
        Emit(Vector3.zero);
        Assert.AreEqual(2, _particles.particleCount);
        Click(Vector3.zero);
        Assert.AreEqual(1, _eventCount);
        _particles.Simulate(0.02f, true, false);
        Assert.AreEqual(1, _particles.particleCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator UiGraphicRaycaster_TakesPrecedenceOverBubble()
    {
        // UIの背後に泡を1個生成する。
        Emit(Vector3.zero);

        Assert.AreEqual(1, _particles.particleCount);

        // UIのイベント管理用EventSystemを生成する。
        var eventSystem =
            Make("EventSystem").AddComponent<EventSystem>();

        EventSystem.current = eventSystem;

        // 画面全体を覆うUI Canvasを生成する。
        var canvasGo = Make("UI");

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasGo.AddComponent<GraphicRaycaster>();

        // クリックを遮断するImageを生成する。
        var imageGo = new GameObject(
            "BlockingImage",
            typeof(RectTransform),
            typeof(Image));

        imageGo.transform.SetParent(canvasGo.transform, false);
        _objects.Add(imageGo);

        RectTransform rect =
            imageGo.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 透明な画像でもUI Raycastの対象となる。
        imageGo.GetComponent<Image>().raycastTarget = true;

        Canvas.ForceUpdateCanvases();
        yield return null;

        // 泡が存在する画面中央の座標。
        Vector2 point =
            (Vector2)_camera.WorldToScreenPoint(Vector3.zero);

        // UIがこの座標を遮断することを事前確認。
        bool overUi = (bool)TitlePlayTestUtil.Call(
            _controller,
            "IsPointerOverUi",
            point);

        Assert.IsTrue(
            overUi,
            "UIのRaycast結果が取得できません。");

        // 仮想マウスをUIと泡が重なる位置に移動する。
        _inputFixture.Move(_testMouse.position, point);
        yield return null;

        Assert.AreSame(_testMouse, Mouse.current);

        // UI上で左クリックする。
        _inputFixture.Press(_testMouse.leftButton);
        yield return null;

        // UIが優先されたため、泡の破壊イベントは発生しない。
        Assert.AreEqual(0, _eventCount);

        // 泡そのものも削除されていないこと。
        Assert.AreEqual(1, _particles.particleCount);

        _inputFixture.Release(_testMouse.leftButton);
        yield return null;

        Assert.AreEqual(0, _eventCount);
    }

    [UnityTest]
    public IEnumerator MouseClickOnBubble_PopsOneParticle()
    {
        // 画面中央に泡を1個生成する。
        Emit(Vector3.zero);

        Assert.AreEqual(1, _particles.particleCount);

        // 泡のワールド座標を画面座標に変換する。
        Vector2 screenPoint =
            (Vector2)_camera.WorldToScreenPoint(Vector3.zero);

        // 仮想マウスを泡の位置へ移動する。
        _inputFixture.Move(
            _testMouse.position,
            screenPoint);

        // 入力イベントの処理を待つ。
        yield return null;

        Assert.AreSame(_testMouse, Mouse.current);

        // マウスの左ボタンを押す。
        _inputFixture.Press(_testMouse.leftButton);

        // 次のフレームで入力イベントとUpdateが実行される。
        yield return null;

        // 実際のUpdate経由でイベントが1回発火したこと。
        Assert.AreEqual(1, _eventCount);

        // クリックされたParticleが削除されること。
        _particles.Simulate(0.02f, true, false);
        Assert.AreEqual(0, _particles.particleCount);

        // ボタンを離して入力状態を戻す。
        _inputFixture.Release(_testMouse.leftButton);
        yield return null;

        // 追加の破壊イベントが発生していないこと。
        Assert.AreEqual(1, _eventCount);
    }
}
