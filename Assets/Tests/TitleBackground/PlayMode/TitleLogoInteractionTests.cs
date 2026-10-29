/*
 * TitleLogoInteractionTests.cs
 *
 * 【テスト対象】
 * TitleLogoInteraction
 * DiveSceneTransition（Scene遷移中のクリック抑止）
 * EventSystem / InputSystemUIInputModule
 *
 * 【目的】
 * タイトルロゴの常時アニメーションとクリック時リアクションが
 * 正しく動作し、連続クリックや無効化などの条件下でも
 * Transformや内部状態が破綻しないことを検証する。
 * また、Input Systemを経由したUIクリック判定が正常に機能し、
 * 周囲のUI操作との競合が発生しないことを確認する。
 *
 * 【主なテスト内容】
 * - 常時アニメーションでY座標とZ軸回転のみが周期的に変化すること
 * - X座標と通常時のScaleが初期値から変化しないこと
 * - 上下移動・回転が設定された振幅の範囲内に収まること
 * - 同じ位相でTransformを繰り返し更新しても値が累積しないこと
 * - 周期に不正な値を設定してもNaNやInfinityが発生しないこと
 * - 左クリックでリアクションが始まり、終了後にScaleが復元すること
 * - 右クリックやnullの入力ではリアクションが発生しないこと
 * - 短時間の連続クリックが最小受付間隔によって制限されること
 * - 演出中の再クリックで現在の回転・Scale差分を引き継ぐこと
 * - 高頻度の連続クリックでも回転角度・Scaleが上限を超えないこと
 * - SE未設定やAudioSource無効時にもアニメーションが動作すること
 * - コンポーネント無効化時に初期Transformが復元されること
 * - Time.timeScaleが0でも常時アニメーションが継続すること
 * - Scene遷移中は新たなクリックリアクションを開始しないこと
 * - 仮想マウスからのクリックが実際のUIイベントを経由して届くこと
 * - ロゴ外のクリックではリアクションが発生しないこと
 * - 前面のUIがロゴへのクリックを遮断すること
 * - 透明Pixelをクリック対象から除外し、不透明Pixelには反応すること
 *
 * 【テスト方式】
 * Play Mode / NUnit / UnityTest
 * Input SystemのInputTestFixtureによる仮想マウス操作
 *
 * ※テスト用GameObject・Canvas・EventSystemを生成して検証する。
 * ※Assembly-CSharp内のクラスやprivateフィールドには
 *   リフレクションを利用してアクセスする。
 * ※UI入力テストでは、実際のInputSystemUIInputModuleを使用する。
 * ※テスト終了時は生成オブジェクトと入力状態を後片付けし、
 *   EventSystem.currentは直接書き換えずUnityの管理に任せる。
 * ※SEの音質・聴感上の音量・実際のScene全体の見た目は検証対象外。
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

/// <summary>
/// Assembly-CSharpにあるTitleLogoInteractionを
/// 既存のPlayModeテストアセンブリから検証するためのヘルパー。
/// </summary>
internal static class TitleLogoTestAccess
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    public static Type LogoType
    {
        get
        {
            Type type = Type.GetType(
                "TitleLogoInteraction, Assembly-CSharp");

            Assert.IsNotNull(type,
                "TitleLogoInteractionがAssembly-CSharpに見つかりません。");

            return type;
        }
    }

    public static MonoBehaviour Add(GameObject gameObject)
    {
        return (MonoBehaviour)gameObject.AddComponent(LogoType);
    }

    public static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, InstanceFlags);

        Assert.IsNotNull(field, "Field not found: " + name);
        field.SetValue(target, value);
    }

    public static T Get<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
            name, InstanceFlags);

        Assert.IsNotNull(field, "Field not found: " + name);
        return (T)field.GetValue(target);
    }

    public static void Call(
        object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName, InstanceFlags);

        Assert.IsNotNull(method, "Method not found: " + methodName);

        // 対象メソッド内で起きた例外をそのままテスト失敗にする。
        try
        {
            method.Invoke(target, args);
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    public static void Click(
        MonoBehaviour controller,
        PointerEventData.InputButton button =
            PointerEventData.InputButton.Left)
    {
        var eventData = new PointerEventData(null)
        {
            button = button
        };

        ((IPointerClickHandler)controller).OnPointerClick(eventData);
    }
}

/// <summary>
/// 常時アニメーション、クリック反応、連打、復元処理を検証。
/// UIイベントの配送そのものは下の統合テストで扱う。
/// </summary>
public sealed class TitleLogoInteractionAnimationTests
{
    private GameObject _gameObject;
    private RectTransform _rect;
    private MonoBehaviour _controller;
    private AudioSource _audioSource;

    private Vector2 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _initialScale;
    private float _previousTimeScale;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;

        _gameObject = new GameObject(
            "TestTitleLogo",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(AudioSource));

        _gameObject.SetActive(false);

        _rect = _gameObject.GetComponent<RectTransform>();
        _audioSource = _gameObject.GetComponent<AudioSource>();

        // 初期値を0や1だけにせず、復元処理を検証する。
        _initialPosition = new Vector2(20f, 230f);
        _initialRotation = Quaternion.Euler(0f, 0f, 13f);
        _initialScale = new Vector3(1.2f, 1.05f, 1f);

        _rect.anchoredPosition = _initialPosition;
        _rect.localRotation = _initialRotation;
        _rect.localScale = _initialScale;

        _audioSource.playOnAwake = false;

        _controller = TitleLogoTestAccess.Add(_gameObject);

        // このテスト群は透明Pixel判定を対象としない。
        TitleLogoTestAccess.Set(
            _controller, "_alphaHitTestMinimumThreshold", 0f);

        _gameObject.SetActive(true);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = _previousTimeScale;

        if (_gameObject != null)
            UObject.Destroy(_gameObject);

        yield return null;
    }

    private void SetIdlePhase(float verticalTime, float tiltTime)
    {
        TitleLogoTestAccess.Set(
            _controller, "_idleVerticalElapsedSeconds", verticalTime);

        TitleLogoTestAccess.Set(
            _controller, "_idleTiltElapsedSeconds", tiltTime);

        TitleLogoTestAccess.Call(_controller, "ApplyPose");
    }

    private void AssertInitialScale(float tolerance = 0.0001f)
    {
        Assert.That(
            Vector3.Distance(_rect.localScale, _initialScale),
            Is.LessThan(tolerance));
    }

    [Test]
    public void IdleQuarterCycle_ChangesOnlyYAndZRotation()
    {
        // 両方のSin波が最大値になる位相。
        SetIdlePhase(3.2f / 4f, 4.8f / 4f);

        Assert.That(
            _rect.anchoredPosition.x,
            Is.EqualTo(_initialPosition.x).Within(0.001f));

        Assert.That(
            _rect.anchoredPosition.y,
            Is.EqualTo(_initialPosition.y + 8f).Within(0.001f));

        Quaternion expected = _initialRotation *
            Quaternion.Euler(0f, 0f, 1.2f);

        Assert.That(
            Quaternion.Angle(expected, _rect.localRotation),
            Is.LessThan(0.01f));

        AssertInitialScale();
    }

    [Test]
    public void IdleHalfCycle_ReturnsToInitialPose()
    {
        SetIdlePhase(3.2f / 2f, 4.8f / 2f);

        Assert.That(
            Vector2.Distance(_rect.anchoredPosition, _initialPosition),
            Is.LessThan(0.001f));

        Assert.That(
            Quaternion.Angle(_rect.localRotation, _initialRotation),
            Is.LessThan(0.01f));

        AssertInitialScale();
    }

    [Test]
    public void IdleAnimation_NeverExceedsConfiguredBounds()
    {
        // 複数周期を独立にサンプリングする。
        for (int i = 0; i < 200; i++)
        {
            float verticalTime = i * 0.053f;
            float tiltTime = i * 0.071f;

            SetIdlePhase(verticalTime, tiltTime);

            Assert.That(
                _rect.anchoredPosition.x,
                Is.EqualTo(_initialPosition.x).Within(0.001f));

            Assert.That(
                Mathf.Abs(
                    _rect.anchoredPosition.y - _initialPosition.y),
                Is.LessThanOrEqualTo(8.001f));

            Assert.That(
                Quaternion.Angle(
                    _initialRotation, _rect.localRotation),
                Is.LessThanOrEqualTo(1.21f));

            AssertInitialScale();
        }
    }

    [Test]
    public void ReapplyingSamePhase_DoesNotAccumulateTransform()
    {
        SetIdlePhase(0.73f, 1.17f);

        Vector2 expectedPosition = _rect.anchoredPosition;
        Quaternion expectedRotation = _rect.localRotation;

        for (int i = 0; i < 100; i++)
            TitleLogoTestAccess.Call(_controller, "ApplyPose");

        Assert.That(
            Vector2.Distance(_rect.anchoredPosition, expectedPosition),
            Is.LessThan(0.0001f));

        Assert.That(
            Quaternion.Angle(
                _rect.localRotation, expectedRotation),
            Is.LessThan(0.01f));

        AssertInitialScale();
    }

    [Test]
    public void InvalidPeriods_DoNotGenerateNaNOrInfinity()
    {
        TitleLogoTestAccess.Set(
            _controller, "_idleVerticalPeriodSeconds", 0f);

        TitleLogoTestAccess.Set(
            _controller, "_idleTiltPeriodSeconds", -5f);

        TitleLogoTestAccess.Call(
            _controller, "UpdateIdleAnimation", 0.016f);

        TitleLogoTestAccess.Call(_controller, "ApplyPose");

        Vector2 position = _rect.anchoredPosition;
        Quaternion rotation = _rect.localRotation;

        Assert.IsTrue(
            !float.IsNaN(position.y) &&
            !float.IsInfinity(position.y));

        Assert.IsTrue(
            !float.IsNaN(rotation.z) &&
            !float.IsInfinity(rotation.z));

        AssertInitialScale();
    }

    [UnityTest]
    public IEnumerator LeftClick_StartsReaction_ThenRestoresScale()
    {
        TitleLogoTestAccess.Click(_controller);

        Assert.IsTrue(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));

        // Updateを経由して反応の途中状態を確認。
        yield return new WaitForSecondsRealtime(0.10f);

        Assert.Greater(
            _rect.localScale.x,
            _initialScale.x);

        yield return new WaitForSecondsRealtime(0.40f);

        Assert.IsFalse(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));

        AssertInitialScale();

        Assert.That(
            Mathf.Abs(TitleLogoTestAccess.Get<float>(
                _controller, "_reactionAngleOffset")),
            Is.LessThan(0.001f));

        // クリック終了後も常時回転の範囲内に収まる。
        Assert.That(
            Quaternion.Angle(
                _initialRotation, _rect.localRotation),
            Is.LessThanOrEqualTo(1.21f));
    }

    [Test]
    public void RightClickAndNullEvent_AreIgnored()
    {
        TitleLogoTestAccess.Click(
            _controller, PointerEventData.InputButton.Right);

        ((IPointerClickHandler)_controller).OnPointerClick(null);

        Assert.IsFalse(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));

        Assert.IsTrue(float.IsNegativeInfinity(
            TitleLogoTestAccess.Get<float>(
                _controller, "_lastAcceptedClickTime")));
    }

    [Test]
    public void ImmediateSecondClick_IsRejectedByCooldown()
    {
        TitleLogoTestAccess.Click(_controller);

        // 反応を途中まで進めてから、即座に再クリック。
        TitleLogoTestAccess.Call(
            _controller, "UpdateClickReaction", 0.07f);

        float elapsed = TitleLogoTestAccess.Get<float>(
            _controller, "_reactionElapsedSeconds");

        TitleLogoTestAccess.Click(_controller);

        Assert.That(
            TitleLogoTestAccess.Get<float>(
                _controller, "_reactionElapsedSeconds"),
            Is.EqualTo(elapsed).Within(0.0001f));
    }

    [Test]
    public void RestartedReaction_PreservesCurrentOffsets()
    {
        TitleLogoTestAccess.Set(
            _controller, "_minimumClickIntervalSeconds", 0f);

        TitleLogoTestAccess.Click(_controller);

        TitleLogoTestAccess.Call(
            _controller, "UpdateClickReaction", 0.06f);

        float angleBefore = TitleLogoTestAccess.Get<float>(
            _controller, "_reactionAngleOffset");

        float scaleBefore = TitleLogoTestAccess.Get<float>(
            _controller, "_reactionScaleOffset");

        TitleLogoTestAccess.Click(_controller);

        Assert.That(
            TitleLogoTestAccess.Get<float>(
                _controller, "_reactionStartAngleOffset"),
            Is.EqualTo(angleBefore).Within(0.0001f));

        Assert.That(
            TitleLogoTestAccess.Get<float>(
                _controller, "_reactionStartScaleOffset"),
            Is.EqualTo(scaleBefore).Within(0.0001f));
    }

    [Test]
    public void RapidClicks_KeepReactionWithinBounds()
    {
        TitleLogoTestAccess.Set(
            _controller, "_minimumClickIntervalSeconds", 0f);

        for (int i = 0; i < 100; i++)
        {
            TitleLogoTestAccess.Click(_controller);

            TitleLogoTestAccess.Call(
                _controller, "UpdateClickReaction", 0.016f);

            float angle = TitleLogoTestAccess.Get<float>(
                _controller, "_reactionAngleOffset");

            float scale = TitleLogoTestAccess.Get<float>(
                _controller, "_reactionScaleOffset");

            Assert.That(angle, Is.InRange(-3.501f, 3.501f));
            Assert.That(scale, Is.InRange(0f, 0.0801f));

            TitleLogoTestAccess.Call(_controller, "ApplyPose");

            Assert.That(
                _rect.localScale.x,
                Is.LessThanOrEqualTo(
                    _initialScale.x * 1.0801f));
        }

        TitleLogoTestAccess.Call(
            _controller, "UpdateClickReaction", 1f);

        TitleLogoTestAccess.Call(_controller, "ApplyPose");

        AssertInitialScale();
    }

    [Test]
    public void MissingOrDisabledAudio_DoesNotPreventReaction()
    {
        // AudioClip未設定でも反応する。
        TitleLogoTestAccess.Set(_controller, "_clickClip", null);
        TitleLogoTestAccess.Click(_controller);

        Assert.IsTrue(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));

        // 次のクリックを受け付ける状態にする。
        TitleLogoTestAccess.Set(
            _controller, "_minimumClickIntervalSeconds", 0f);

        AudioClip clip = AudioClip.Create(
            "TestClick", 4410, 1, 44100, false);

        try
        {
            TitleLogoTestAccess.Set(
                _controller, "_clickClip", clip);

            _audioSource.enabled = false;

            TitleLogoTestAccess.Click(_controller);

            Assert.IsTrue(TitleLogoTestAccess.Get<bool>(
                _controller, "_isReacting"));
        }
        finally
        {
            UObject.DestroyImmediate(clip);
        }
    }

    [Test]
    public void DisablingComponent_RestoresInitialTransform()
    {
        SetIdlePhase(0.8f, 1.2f);

        TitleLogoTestAccess.Click(_controller);

        TitleLogoTestAccess.Call(
            _controller, "UpdateClickReaction", 0.08f);

        TitleLogoTestAccess.Call(_controller, "ApplyPose");

        _controller.enabled = false;

        Assert.That(
            Vector2.Distance(_rect.anchoredPosition, _initialPosition),
            Is.LessThan(0.001f));

        Assert.That(
            Quaternion.Angle(
                _rect.localRotation, _initialRotation),
            Is.LessThan(0.01f));

        AssertInitialScale();

        _controller.enabled = true;

        Assert.IsFalse(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));
    }

    [UnityTest]
    public IEnumerator UnscaledAnimation_ContinuesWhenTimeScaleIsZero()
    {
        SetIdlePhase(0f, 0f);

        Time.timeScale = 0f;

        float before = _rect.anchoredPosition.y;

        yield return new WaitForSecondsRealtime(0.12f);

        Assert.That(
            Mathf.Abs(_rect.anchoredPosition.y - before),
            Is.GreaterThan(0.05f));
    }

    [Test]
    public void SceneTransition_BlocksNewReaction()
    {
        Type transitionType = Type.GetType(
            "DiveSceneTransition, Assembly-CSharp");

        Assert.IsNotNull(transitionType);

        var transitionGo = new GameObject("FakeTransition");
        transitionGo.SetActive(false);

        MonoBehaviour transition =
            (MonoBehaviour)transitionGo.AddComponent(transitionType);

        FieldInfo activeField = transitionType.GetField(
            "_activeTransition",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(activeField);

        object previous = activeField.GetValue(null);

        try
        {
            TitleLogoTestAccess.Set(
                transition, "_isRunning", true);

            activeField.SetValue(null, transition);

            TitleLogoTestAccess.Click(_controller);

            Assert.IsFalse(TitleLogoTestAccess.Get<bool>(
                _controller, "_isReacting"));
        }
        finally
        {
            activeField.SetValue(null, previous);
            UObject.DestroyImmediate(transitionGo);
        }
    }
}

/// <summary>
/// 実際のInputSystemUIInputModuleと仮想マウスを使用し、
/// UIからロゴへクリックが配送されることを検証する。
/// </summary>
public sealed class TitleLogoInteractionInputTests
{
    private readonly InputTestFixture _fixture = new InputTestFixture();
    private readonly List<EventSystem> _previousEventSystems =
        new List<EventSystem>();

    private Mouse _mouse;
    private GameObject _canvasGo;
    private GameObject _eventGo;
    private GameObject _logoGo;
    private RectTransform _rect;
    private Image _image;
    private MonoBehaviour _controller;
    private EventSystem _eventSystem;

    private Texture2D _texture;
    private Sprite _sprite;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _fixture.Setup();

        foreach (EventSystem existing in
                 UObject.FindObjectsByType<EventSystem>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (existing.isActiveAndEnabled)
            {
                existing.enabled = false;
                _previousEventSystems.Add(existing);
            }
        }

        _mouse = InputSystem.AddDevice<Mouse>();

        _canvasGo = new GameObject(
            "TitleLogoTestCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));

        Canvas canvas = _canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        _logoGo = new GameObject(
            "TitleLogoTest",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        _logoGo.SetActive(false);
        _logoGo.transform.SetParent(_canvasGo.transform, false);

        _rect = _logoGo.GetComponent<RectTransform>();
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = new Vector2(160f, 80f);
        _rect.anchoredPosition = Vector2.zero;

        _image = _logoGo.GetComponent<Image>();
        _image.raycastTarget = true;

        _controller = TitleLogoTestAccess.Add(_logoGo);

        // 不透明な矩形による基本クリックテスト。
        TitleLogoTestAccess.Set(
            _controller, "_alphaHitTestMinimumThreshold", 0f);

        _eventGo = new GameObject("TitleLogoTestEventSystem");
        _eventGo.AddComponent<EventSystem>();
        _eventGo.AddComponent<InputSystemUIInputModule>();

        _eventSystem = _eventGo.GetComponent<EventSystem>();
        
        // テスト用EventSystemが自動的にcurrentになることを確認。
        Assert.AreSame(
            _eventSystem,
            EventSystem.current,
            "テスト用EventSystemがcurrentになっていません。");

        _logoGo.SetActive(true);

        yield return null;
        Canvas.ForceUpdateCanvases();
    }


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // テスト中に生成したGameObjectとアセットを破棄する。
        if (_logoGo != null)
            UObject.Destroy(_logoGo);

        if (_canvasGo != null)
            UObject.Destroy(_canvasGo);

        if (_eventGo != null)
            UObject.Destroy(_eventGo);

        if (_sprite != null)
            UObject.Destroy(_sprite);

        if (_texture != null)
            UObject.Destroy(_texture);

        // Destroyを完了させ、テスト用EventSystemの
        // 登録解除を確実に行う。
        yield return null;

        // テスト開始前のInput Systemへ復元する。
        _fixture.TearDown();

        // 一時的に無効化した既存EventSystemを再度有効化する。
        // OnEnable()によってUnityが自動的に登録する。
        foreach (EventSystem previous in _previousEventSystems)
        {
            if (previous != null)
            {
                previous.enabled = true;
            }
        }

        _previousEventSystems.Clear();

        // EventSystem.currentを手動で設定する必要はない。
    }

    private Vector2 ScreenPoint(Vector2 localPoint)
    {
        return RectTransformUtility.WorldToScreenPoint(
            null,
            _rect.TransformPoint(localPoint));
    }

    private bool IsTopHit(GameObject target, Vector2 point)
    {
        var data = new PointerEventData(_eventSystem)
        {
            position = point
        };

        var results = new List<RaycastResult>();
        _eventSystem.RaycastAll(data, results);

        return results.Count > 0 &&
               results[0].gameObject == target;
    }

    private IEnumerator RealClick(Vector2 screenPoint)
    {
        _fixture.Move(_mouse.position, screenPoint);
        yield return null;

        _fixture.Press(_mouse.leftButton);
        yield return null;

        _fixture.Release(_mouse.leftButton);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RealMouseClick_ReachesLogoHandler()
    {
        Vector2 point = ScreenPoint(Vector2.zero);

        Assert.IsTrue(
            IsTopHit(_logoGo, point),
            "ロゴがUI Raycastの最前面で検出されません。");

        yield return RealClick(point);

        Assert.IsTrue(
            TitleLogoTestAccess.Get<bool>(
                _controller, "_isReacting"),
            "Input System経由のクリックがロゴへ届きません。");
    }

    [UnityTest]
    public IEnumerator OutsideClick_DoesNotStartReaction()
    {
        Vector2 point = Vector2.zero;

        Assert.IsFalse(
            IsTopHit(_logoGo, point),
            "テスト用のロゴ外座標がロゴに重なっています。");

        yield return RealClick(point);

        Assert.IsFalse(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));
    }

    [UnityTest]
    public IEnumerator FrontUI_PreventsLogoClick()
    {
        GameObject blocker = new GameObject(
            "BlockingUI",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        blocker.transform.SetParent(_canvasGo.transform, false);

        RectTransform blockerRect =
            blocker.GetComponent<RectTransform>();

        blockerRect.sizeDelta = new Vector2(200f, 120f);

        blocker.GetComponent<Image>().raycastTarget = true;

        Canvas.ForceUpdateCanvases();
        yield return null;

        Vector2 point = ScreenPoint(Vector2.zero);

        Assert.IsTrue(
            IsTopHit(blocker, point),
            "前面のUIがRaycastの最優先対象ではありません。");

        yield return RealClick(point);

        Assert.IsFalse(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));
    }

    [UnityTest]
    public IEnumerator TransparentPixels_RejectClick_OpaquePixelsAccept()
    {
        // 透明領域と不透明領域を持つSpriteを生成。
        _texture = new Texture2D(
            8, 8, TextureFormat.RGBA32, false);

        Color[] pixels = new Color[64];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        for (int y = 2; y < 6; y++)
        for (int x = 2; x < 6; x++)
            pixels[y * 8 + x] = Color.white;

        _texture.SetPixels(pixels);
        _texture.Apply();

        _sprite = Sprite.Create(
            _texture,
            new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f));

        _image.sprite = _sprite;

        TitleLogoTestAccess.Set(
            _controller, "_alphaHitTestMinimumThreshold", 0.15f);

        TitleLogoTestAccess.Call(_controller, "ConfigureRaycast");

        Assert.That(
            _image.alphaHitTestMinimumThreshold,
            Is.EqualTo(0.15f).Within(0.001f));

        Canvas.ForceUpdateCanvases();
        yield return null;

        // 矩形内の透明な左下付近。
        Vector2 transparentPoint =
            ScreenPoint(new Vector2(-60f, -30f));

        Assert.IsFalse(
            IsTopHit(_logoGo, transparentPoint),
            "透明なPixelがクリック対象になっています。");

        yield return RealClick(transparentPoint);

        Assert.IsFalse(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));

        // 不透明な中心部分はクリックできる。
        Vector2 opaquePoint = ScreenPoint(Vector2.zero);

        Assert.IsTrue(
            IsTopHit(_logoGo, opaquePoint),
            "不透明なPixelがクリック対象になっていません。");

        yield return RealClick(opaquePoint);

        Assert.IsTrue(TitleLogoTestAccess.Get<bool>(
            _controller, "_isReacting"));
    }
}
