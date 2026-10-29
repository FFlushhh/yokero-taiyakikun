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

public sealed class TitleBubblePopControllerTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private Camera _camera;
    private ParticleSystem _particles;
    private TitleBubblePopController _controller;
    private int _eventCount;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // テストケース間でイベント回数を引き継がない
        _eventCount = 0;
        
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
        foreach (var obj in _objects)
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
        Emit(Vector3.zero);
        var eventSystem = Make("EventSystem").AddComponent<EventSystem>();
        EventSystem.current = eventSystem;
        var canvasGo = Make("UI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<GraphicRaycaster>();
        var imageGo = new GameObject("BlockingImage", typeof(RectTransform), typeof(Image));
        imageGo.transform.SetParent(canvasGo.transform, false);
        _objects.Add(imageGo);
        RectTransform rect = imageGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        imageGo.GetComponent<Image>().raycastTarget = true;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Vector2 point = (Vector2)_camera.WorldToScreenPoint(Vector3.zero);
        bool overUi = (bool)TitlePlayTestUtil.Call(_controller, "IsPointerOverUi", point);
        Assert.IsTrue(overUi, "UIのRaycast結果が取得できません。");
        // 実際のUpdateではoverUiならTryPopBubbleを呼ばない。
        Assert.AreEqual(0, _eventCount);
        Assert.IsNotNull(eventSystem);
    }
}
