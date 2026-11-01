/*
 * PlayerDirectionEditModeTests.cs
 *
 * 【テスト対象】
 * PlayerControllerのUpdateTargetDirection、
 * UpdateVisualRotation、ResetDirectionTracking、
 * GetClampedScreenPosition
 *
 * 【目的】
 * マウス移動方向の平滑化、低速移動への対応、
 * 回転補間、左右反転の安定性を検証する。
 *
 * 【主なテスト内容】
 * - 斜め方向のピクセル単位の揺れを抑制すること
 * - 微小入力で不要な方向変更が発生しないこと
 * - 低速移動でも累積距離から方向を更新できること
 * - 急反転後に新しい進行方向へ追従すること
 * - 停止後に目標角度が不必要に変化しないこと
 * - FPSが異なっても方向推定結果が大きく変わらないこと
 * - SmoothDampAngleで目標角度に収束すること
 * - 左右反転のヒステリシスが機能すること
 * - 操作履歴を正しくリセットできること
 *
 * 【テスト方式】
 * Edit Mode / NUnit
 *
 * ※Assembly-CSharpのクラスをReflectionで呼び出す。
 * ※実機の入力遅延や描画の自然さは検証対象外。
 */

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerDirectionEditModeTests
{
    private const float DeltaTime = 1f / 60f;

    private const BindingFlags InstanceFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private GameObject _playerObject;
    private GameObject _visualObject;
    private GameObject _cameraObject;

    private Component _controller;
    private Camera _camera;
    private SpriteRenderer _spriteRenderer;
    private RenderTexture _renderTexture;

    [SetUp]
    public void SetUp()
    {
        Type controllerType = Type.GetType(
            "PlayerController, Assembly-CSharp");

        Assert.That(controllerType, Is.Not.Null,
            "PlayerControllerがAssembly-CSharpに見つかりません。");

        _playerObject = new GameObject("TestPlayer");
        _controller = _playerObject.AddComponent(controllerType);

        _visualObject = new GameObject("Visual");
        _visualObject.transform.SetParent(
            _playerObject.transform, false);

        _spriteRenderer =
            _visualObject.AddComponent<SpriteRenderer>();

        _cameraObject = new GameObject("TestCamera");
        _camera = _cameraObject.AddComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = 5f;
        _camera.transform.position =
            new Vector3(0f, 0f, -10f);

        _renderTexture = new RenderTexture(320, 180, 0);
        _camera.targetTexture = _renderTexture;

        SetField("_mainCamera", _camera);
        SetField("_visualSpriteRenderer", _spriteRenderer);
        SetField("_visualTransform", _visualObject.transform);

        SetField("_directionFilterTime", 0.065f);
        SetField("_minDirectionSpeed", 18f);
        SetField("_slowDirectionDistancePixels", 3f);
        SetField("_rotationSmoothTime", 0.045f);
        SetField("_facingHysteresis", 0.2f);
        SetField("_keepUpright", true);

        ResetTestState(Vector2.zero, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        if (_camera != null)
        {
            _camera.targetTexture = null;
        }

        if (_renderTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(_renderTexture);
        }

        if (_cameraObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_cameraObject);
        }

        if (_playerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_playerObject);
        }
    }

    [Test]
    public void DiagonalPixelSteps_KeepEstimatedAngleNearDiagonal()
    {
        Vector2 position = Vector2.zero;

        // 水平2px、垂直2pxを交互に移動する。
        // 生の1フレーム差分では0度と90度が交互に出る。
        for (int i = 0; i < 60; i++)
        {
            position += i % 2 == 0
                ? new Vector2(2f, 0f)
                : new Vector2(0f, 2f);

            UpdateDirection(position, DeltaTime);

            // フィルタの立ち上がり後を検証する。
            if (i >= 20)
            {
                float angle = GetField<float>("_targetAngle");

                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(angle, 45f)),
                    Is.LessThan(20f),
                    $"フレーム{i}で方向が過度に変動しました。");
            }
        }
    }

    [Test]
    public void TinyMovements_DoNotChangeDirectionBelowThreshold()
    {
        SetField("_minDirectionSpeed", 100000f);
        SetField("_slowDirectionDistancePixels", 4f);

        ResetTestState(Vector2.zero, 30f);

        Vector2[] positions =
        {
            new Vector2(1f, 0f),
            new Vector2(2f, 0f),
            new Vector2(3f, 0f),
            Vector2.zero
        };

        foreach (Vector2 position in positions)
        {
            UpdateDirection(position, DeltaTime);

            Assert.That(
                GetField<float>("_targetAngle"),
                Is.EqualTo(30f).Within(0.001f));
        }
    }

    [Test]
    public void SlowMovement_UpdatesDirectionAfterAccumulation()
    {
        // 常に低速側の判定を使用させる。
        SetField("_minDirectionSpeed", 100000f);
        SetField("_slowDirectionDistancePixels", 3f);

        ResetTestState(Vector2.zero, 90f);

        UpdateDirection(new Vector2(1f, 0f), DeltaTime);
        UpdateDirection(new Vector2(2f, 0f), DeltaTime);

        Assert.That(
            GetField<float>("_targetAngle"),
            Is.EqualTo(90f).Within(0.001f));

        UpdateDirection(new Vector2(3f, 0f), DeltaTime);

        Assert.That(
            Mathf.Abs(Mathf.DeltaAngle(
                GetField<float>("_targetAngle"), 0f)),
            Is.LessThan(0.001f));
    }

    [Test]
    public void SuddenReverse_EventuallyFacesOppositeDirection()
    {
        Vector2 position = Vector2.zero;

        // 右方向への移動
        for (int i = 0; i < 30; i++)
        {
            position += new Vector2(2f, 0f);
            UpdateDirection(position, DeltaTime);
        }

        // 左方向への急反転
        for (int i = 0; i < 30; i++)
        {
            position += new Vector2(-2f, 0f);
            UpdateDirection(position, DeltaTime);
        }

        float angle = GetField<float>("_targetAngle");

        Assert.That(
            Mathf.Abs(Mathf.DeltaAngle(angle, 180f)),
            Is.LessThan(10f),
            "急反転後に左方向へ追従していません。");
    }

    [Test]
    public void StoppingMouse_DoesNotCreateNewTargetDirection()
    {
        Vector2 position = Vector2.zero;

        for (int i = 0; i < 30; i++)
        {
            position += new Vector2(2f, 0f);
            UpdateDirection(position, DeltaTime);
        }

        float angleBeforeStop =
            GetField<float>("_targetAngle");

        for (int i = 0; i < 60; i++)
        {
            UpdateDirection(position, DeltaTime);
        }

        float angleAfterStop =
            GetField<float>("_targetAngle");

        Assert.That(
            Mathf.Abs(Mathf.DeltaAngle(
                angleBeforeStop, angleAfterStop)),
            Is.LessThan(0.1f));
    }

    [Test]
    public void DifferentFrameRates_ProduceSimilarDirectionAfterTurn()
    {
        float angle30Fps =
            SimulateDirectionAfterTurn(1f / 30f);

        float angle120Fps =
            SimulateDirectionAfterTurn(1f / 120f);

        Assert.That(
            Mathf.Abs(Mathf.DeltaAngle(
                angle30Fps, angle120Fps)),
            Is.LessThan(10f),
            "FPSの違いによって推定角度が大きく変化しました。");
    }

    [Test]
    public void SmoothRotation_ConvergesTowardTarget()
    {
        SetField("_keepUpright", false);
        SetField("_currentAngle", 0f);
        SetField("_targetAngle", 90f);
        SetField("_angularVelocity", 0f);

        float previousError = 90f;

        for (int i = 0; i < 60; i++)
        {
            InvokePrivate("UpdateVisualRotation", DeltaTime);

            float currentAngle =
                GetField<float>("_currentAngle");

            float error = Mathf.Abs(
                Mathf.DeltaAngle(currentAngle, 90f));

            Assert.That(
                error,
                Is.LessThanOrEqualTo(previousError + 0.01f));

            previousError = error;
        }

        Assert.That(previousError, Is.LessThan(1f));
    }

    [Test]
    public void FacingHysteresis_PreventsFlipNearVertical()
    {
        ApplyVisualAngle(75f);
        Assert.That(_spriteRenderer.flipY, Is.False);

        // 90度付近では直前の状態を維持。
        ApplyVisualAngle(90f);
        Assert.That(_spriteRenderer.flipY, Is.False);

        // 十分に左向きになったら反転。
        ApplyVisualAngle(110f);
        Assert.That(_spriteRenderer.flipY, Is.True);

        // 垂直方向へ戻っても直ちに再反転しない。
        ApplyVisualAngle(90f);
        Assert.That(_spriteRenderer.flipY, Is.True);

        ApplyVisualAngle(70f);
        Assert.That(_spriteRenderer.flipY, Is.False);
    }

    [Test]
    public void KeepUprightDisabled_ClearsSpriteFlip()
    {
        _spriteRenderer.flipY = true;

        SetField("_keepUpright", false);
        SetField("_currentAngle", 180f);
        SetField("_targetAngle", 180f);
        SetField("_angularVelocity", 0f);

        InvokePrivate("UpdateVisualRotation", DeltaTime);

        Assert.That(_spriteRenderer.flipY, Is.False);
    }

    [Test]
    public void ResetDirectionTracking_ClearsHistory()
    {
        SetField("_filteredVelocity", new Vector2(30f, 20f));
        SetField("_currentAngle", 35f);
        SetField("_targetAngle", 100f);
        SetField("_angularVelocity", 15f);

        InvokePrivate(
            "ResetDirectionTracking",
            Vector3.zero);

        Assert.That(
            GetField<Vector2>("_filteredVelocity"),
            Is.EqualTo(Vector2.zero));

        Assert.That(
            GetField<float>("_angularVelocity"),
            Is.EqualTo(0f));

        Assert.That(
            GetField<float>("_targetAngle"),
            Is.EqualTo(35f).Within(0.001f));

        Vector2 expectedScreenPosition =
            _camera.WorldToScreenPoint(Vector3.zero);

        Assert.That(
            Vector2.Distance(
                GetField<Vector2>("_previousScreenPosition"),
                expectedScreenPosition),
            Is.LessThan(0.01f));
    }

    [Test]
    public void ScreenClamping_RespectsCameraPixelRect()
    {
        SetField("_edgeMarginViewport", 0.02f);

        Rect rect = _camera.pixelRect;

        Assert.That(rect.width, Is.GreaterThan(0f));
        Assert.That(rect.height, Is.GreaterThan(0f));

        Vector2 lower = (Vector2)InvokePrivate(
            "GetClampedScreenPosition",
            new Vector2(-1000f, -1000f));

        Vector2 upper = (Vector2)InvokePrivate(
            "GetClampedScreenPosition",
            new Vector2(10000f, 10000f));

        Assert.That(
            lower.x,
            Is.EqualTo(rect.xMin + rect.width * 0.02f)
                .Within(0.01f));

        Assert.That(
            lower.y,
            Is.EqualTo(rect.yMin + rect.height * 0.02f)
                .Within(0.01f));

        Assert.That(
            upper.x,
            Is.EqualTo(rect.xMax - rect.width * 0.02f)
                .Within(0.01f));

        Assert.That(
            upper.y,
            Is.EqualTo(rect.yMax - rect.height * 0.02f)
                .Within(0.01f));
    }

    private float SimulateDirectionAfterTurn(float deltaTime)
    {
        ResetTestState(Vector2.zero, 0f);

        Vector2 position = Vector2.zero;

        int firstFrames = Mathf.RoundToInt(0.2f / deltaTime);
        int secondFrames = Mathf.RoundToInt(0.1f / deltaTime);

        for (int i = 0; i < firstFrames; i++)
        {
            position += Vector2.right * 100f * deltaTime;
            UpdateDirection(position, deltaTime);
        }

        for (int i = 0; i < secondFrames; i++)
        {
            position += Vector2.up * 100f * deltaTime;
            UpdateDirection(position, deltaTime);
        }

        return GetField<float>("_targetAngle");
    }

    private void ApplyVisualAngle(float angle)
    {
        SetField("_currentAngle", angle);
        SetField("_targetAngle", angle);
        SetField("_angularVelocity", 0f);

        InvokePrivate("UpdateVisualRotation", DeltaTime);
    }

    private void ResetTestState(
        Vector2 screenPosition, float angle)
    {
        SetField("_previousScreenPosition", screenPosition);
        SetField("_directionAnchorScreen", screenPosition);
        SetField("_filteredVelocity", Vector2.zero);

        SetField("_currentAngle", angle);
        SetField("_targetAngle", angle);
        SetField("_angularVelocity", 0f);

        _spriteRenderer.flipY = false;
    }

    private void UpdateDirection(
        Vector2 position, float deltaTime)
    {
        InvokePrivate(
            "UpdateTargetDirection",
            position,
            deltaTime);
    }

    private void SetField(string name, object value)
    {
        FieldInfo field = _controller.GetType().GetField(
            name, InstanceFlags);

        Assert.That(field, Is.Not.Null,
            $"フィールド{name}が見つかりません。");

        field.SetValue(_controller, value);
    }

    private T GetField<T>(string name)
    {
        FieldInfo field = _controller.GetType().GetField(
            name, InstanceFlags);

        Assert.That(field, Is.Not.Null,
            $"フィールド{name}が見つかりません。");

        return (T)field.GetValue(_controller);
    }

    private object InvokePrivate(
        string name, params object[] arguments)
    {
        MethodInfo method = _controller.GetType().GetMethod(
            name, InstanceFlags);

        Assert.That(method, Is.Not.Null,
            $"メソッド{name}が見つかりません。");

        return method.Invoke(_controller, arguments);
    }
}
