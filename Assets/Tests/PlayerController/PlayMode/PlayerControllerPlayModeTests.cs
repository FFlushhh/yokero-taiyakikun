/*
 * PlayerControllerPlayModeTests.cs
 *
 * 【テスト対象】
 * PlayerController、PlayerCollision、
 * PlayerPathCollision2D、Input System
 *
 * 【目的】
 * 実際のマウス入力に相当する入力を与え、
 * 移動・回転・左右反転・経路衝突判定・操作停止が
 * 期待どおり連携していることを検証する。
 *
 * 【主なテスト内容】
 * - マウス座標に直接追従すること
 * - 画面端の移動制限が機能すること
 * - 操作停止・再開が正常に機能すること
 * - Visualだけが回転し、Colliderが回転しないこと
 * - 高速移動時に経路上の敵を検出すること
 * - 敵の近くを通過しても誤検出しないこと
 * - GameOver時に移動が停止すること
 * - Mouseが存在しない場合に安全に停止すること
 *
 * 【テスト方式】
 * Play Mode / NUnit / InputTestFixture
 *
 * ※実際のMainSceneではなく、テスト用オブジェクトを生成する。
 * ※PlayerControllerのUpdateをReflectionで明示的に呼び出す。
 * ※入力状態はInputTestFixtureで分離して再現する。
 * ※実際のフレーム時間や描画遅延は手動検証が必要。
 */

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public sealed class PlayerControllerPlayModeTests : InputTestFixture
{
    private const BindingFlags MemberFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private GameObject _playerObject;
    private GameObject _cameraObject;
    private GameObject _visualObject;
    private GameObject _enemyObject;
    private GameObject _gameManagerObject;

    private Camera _camera;
    private RenderTexture _renderTexture;
    private SpriteRenderer _spriteRenderer;
    private PolygonCollider2D _collider;

    private Component _controller;
    private Component _playerCollision;
    private Mouse _mouse;

    public override void Setup()
    {
        base.Setup();

        Type controllerType = Type.GetType(
            "PlayerController, Assembly-CSharp");

        Type collisionType = Type.GetType(
            "PlayerCollision, Assembly-CSharp");

        Assert.That(controllerType, Is.Not.Null);
        Assert.That(collisionType, Is.Not.Null);

        // テスト用のMouseを作成する。
        _mouse = InputSystem.AddDevice<Mouse>();

        // テスト用Cameraを作成する。
        _cameraObject = new GameObject("TestCamera");
        _camera = _cameraObject.AddComponent<Camera>();

        _camera.orthographic = true;
        _camera.orthographicSize = 5f;
        _camera.transform.position =
            new Vector3(0f, 0f, -10f);

        _renderTexture = new RenderTexture(320, 180, 0);
        _camera.targetTexture = _renderTexture;

        // テスト用Playerを作成する。
        _playerObject = new GameObject("TestPlayer");
        _playerObject.SetActive(false);

        _playerObject.transform.position = Vector3.zero;
        _playerObject.transform.localScale =
            new Vector3(0.5f, 0.5f, 1f);

        Rigidbody2D rigidbody =
            _playerObject.AddComponent<Rigidbody2D>();

        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;

        _collider =
            _playerObject.AddComponent<PolygonCollider2D>();

        _collider.points = new[]
        {
            new Vector2(-0.4f, -0.3f),
            new Vector2(-0.4f,  0.3f),
            new Vector2( 0.4f,  0.3f),
            new Vector2( 0.4f, -0.3f)
        };

        _collider.isTrigger = true;

        // VisualはPlayerの子として作成する。
        _visualObject = new GameObject("Visual");

        _visualObject.transform.SetParent(
            _playerObject.transform, false);

        _visualObject.transform.localScale =
            new Vector3(1.8f, 1.8f, 1f);

        _spriteRenderer =
            _visualObject.AddComponent<SpriteRenderer>();

        _playerCollision =
            _playerObject.AddComponent(collisionType);

        _controller =
            _playerObject.AddComponent(controllerType);

        // 自動Updateを無効化し、入力を明示的に検証する。
        ((Behaviour)_controller).enabled = false;

        SetField(_controller, "_mainCamera", _camera);
        SetField(_controller, "_collisionCollider", _collider);
        SetField(_controller, "_playerCollision", _playerCollision);
        SetField(
            _controller,
            "_visualSpriteRenderer",
            _spriteRenderer);

        // 入力デバイスと初期位置を合わせる。
        Set(_mouse.position, GetScreenCenter());

        _playerObject.SetActive(true);

        // Startは自動実行されないため明示的に呼ぶ。
        InvokeController("Start");
    }

    public override void TearDown()
    {
        if (_gameManagerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(
                _gameManagerObject);
        }

        if (_enemyObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_enemyObject);
        }

        if (_playerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_playerObject);
        }

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

        base.TearDown();
    }

    [Test]
    public void MouseMovement_MovesPlayerToTargetImmediately()
    {
        Vector2 mousePosition = new Vector2(200f, 110f);

        MoveMouseAndUpdate(mousePosition);

        Vector3 expected = ScreenToWorld(mousePosition);

        Assert.That(
            Vector3.Distance(
                _playerObject.transform.position, expected),
            Is.LessThan(0.001f),
            "マウス位置への追従に誤差があります。");
    }

    [TestCase(-1000f, -1000f)]
    [TestCase(10000f, 10000f)]
    public void MouseOutsideScreen_IsClampedToBounds(
        float x, float y)
    {
        Rect rect = _camera.pixelRect;

        float marginX = rect.width * 0.02f;
        float marginY = rect.height * 0.02f;

        Vector2 clamped = new Vector2(
            Mathf.Clamp(
                x,
                rect.xMin + marginX,
                rect.xMax - marginX),

            Mathf.Clamp(
                y,
                rect.yMin + marginY,
                rect.yMax - marginY));

        MoveMouseAndUpdate(new Vector2(x, y));

        Vector3 expected = ScreenToWorld(clamped);

        Assert.That(
            Vector3.Distance(
                _playerObject.transform.position, expected),
            Is.LessThan(0.001f));
    }

    [Test]
    public void MovementDisabled_PlayerDoesNotMove()
    {
        InvokeController("SetMovementEnabled", false);

        Vector3 initialPosition =
            _playerObject.transform.position;

        MoveMouseAndUpdate(new Vector2(230f, 120f));

        Assert.That(
            _playerObject.transform.position,
            Is.EqualTo(initialPosition));
    }

    [Test]
    public void MovementReenabled_PlayerFollowsMouse()
    {
        InvokeController("SetMovementEnabled", false);

        MoveMouseAndUpdate(new Vector2(200f, 100f));

        Assert.That(
            _playerObject.transform.position,
            Is.EqualTo(Vector3.zero));

        InvokeController("SetMovementEnabled", true);

        Vector2 target = new Vector2(230f, 120f);
        MoveMouseAndUpdate(target);

        Assert.That(
            Vector3.Distance(
                _playerObject.transform.position,
                ScreenToWorld(target)),
            Is.LessThan(0.001f));
    }

    [Test]
    public void VisualRotation_DoesNotRotatePlayerOrCollider()
    {
        Quaternion initialPlayerRotation =
            _playerObject.transform.rotation;

        Vector3 initialPlayerScale =
            _playerObject.transform.localScale;

        Physics2D.SyncTransforms();

        Bounds initialColliderBounds = _collider.bounds;

        SetField(_controller, "_currentAngle", 0f);
        SetField(_controller, "_targetAngle", 135f);
        SetField(_controller, "_angularVelocity", 0f);

        // 複数の回転更新を模擬する。
        for (int i = 0; i < 60; i++)
        {
            InvokeController(
                "UpdateVisualRotation",
                1f / 60f);
        }

        Physics2D.SyncTransforms();

        Assert.That(
            Quaternion.Angle(
                _visualObject.transform.localRotation,
                Quaternion.identity),
            Is.GreaterThan(10f));

        Assert.That(
            Quaternion.Angle(
                _playerObject.transform.rotation,
                initialPlayerRotation),
            Is.LessThan(0.001f));

        Assert.That(
            _playerObject.transform.localScale,
            Is.EqualTo(initialPlayerScale));

        Assert.That(
            Vector3.Distance(
                _collider.bounds.center,
                initialColliderBounds.center),
            Is.LessThan(0.001f));

        Assert.That(
            Vector3.Distance(
                _collider.bounds.size,
                initialColliderBounds.size),
            Is.LessThan(0.001f));
    }

    [Test]
    public void HighSpeedMovementAcrossEnemy_StopsBeforeCrossing()
    {
        CreateEnemy(
            new Vector3(2f, 0f, 0f),
            new Vector2(0.2f, 0.5f));

        Vector3 initialPosition =
            _playerObject.transform.position;

        // このテストではGameManagerを作成していないため、
        // PlayerCollisionの警告を期待する。
        LogAssert.Expect(
            LogType.Warning,
            "PlayerCollision: GameManagerが見つかりません。");

        MoveMouseAndUpdate(new Vector2(260f, 90f));

        Assert.That(
            _playerObject.transform.position,
            Is.EqualTo(initialPosition),
            "経路上の敵を検出せずに移動しました。");
    }

    [Test]
    public void MovementBesideEnemy_DoesNotReportCollision()
    {
        CreateEnemy(
            new Vector3(2f, 3f, 0f),
            new Vector2(0.2f, 0.5f));

        Vector2 target = new Vector2(260f, 90f);

        MoveMouseAndUpdate(target);

        Assert.That(
            Vector3.Distance(
                _playerObject.transform.position,
                ScreenToWorld(target)),
            Is.LessThan(0.001f),
            "接触していない敵によって移動が停止しました。");
    }

    [Test]
    public void GameOver_PlayerDoesNotMove()
    {
        Type managerType = Type.GetType(
            "GameManager, Assembly-CSharp");

        Assert.That(managerType, Is.Not.Null);

        _gameManagerObject =
            new GameObject("TestGameManager");

        _gameManagerObject.SetActive(false);

        Component gameManager =
            _gameManagerObject.AddComponent(managerType);

        // GameManagerのGameOver状態だけを設定する。
        SetField(gameManager, "_gameOver", true);

        SetField(
            _playerCollision,
            "gameManager",
            gameManager);

        Vector3 initialPosition =
            _playerObject.transform.position;

        MoveMouseAndUpdate(new Vector2(230f, 120f));

        Assert.That(
            _playerObject.transform.position,
            Is.EqualTo(initialPosition));
    }

    [Test]
    public void MissingMouse_DoesNotMovePlayer()
    {
        Vector3 initialPosition =
            _playerObject.transform.position;

        InputSystem.RemoveDevice(_mouse);

        Assert.That(Mouse.current, Is.Null);

        InvokeController("Update");

        Assert.That(
            _playerObject.transform.position,
            Is.EqualTo(initialPosition));
    }

    private void CreateEnemy(
        Vector3 position, Vector2 size)
    {
        _enemyObject = new GameObject("TestEnemy");
        _enemyObject.tag = "Enemy";
        _enemyObject.transform.position = position;

        BoxCollider2D enemyCollider =
            _enemyObject.AddComponent<BoxCollider2D>();

        enemyCollider.size = size;
        enemyCollider.isTrigger = true;

        Physics2D.SyncTransforms();
    }

    private void MoveMouseAndUpdate(Vector2 screenPosition)
    {
        // 通常のNUnit TestではSetが入力更新も実行する。
        Set(_mouse.position, screenPosition);

        Assert.That(Mouse.current, Is.SameAs(_mouse));

        InvokeController("Update");
    }

    private Vector2 GetScreenCenter()
    {
        Rect rect = _camera.pixelRect;
        return rect.center;
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        float distance = Mathf.Abs(
            _camera.transform.position.z -
            _playerObject.transform.position.z);

        Vector3 world = _camera.ScreenToWorldPoint(
            new Vector3(
                screenPosition.x,
                screenPosition.y,
                distance));

        world.z = _playerObject.transform.position.z;

        return world;
    }

    private static void SetField(
        Component target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, MemberFlags);

        Assert.That(field, Is.Not.Null,
            $"フィールド{name}が見つかりません。");

        field.SetValue(target, value);
    }

    private object InvokeController(
        string name, params object[] arguments)
    {
        MethodInfo method = _controller.GetType().GetMethod(
            name, MemberFlags);

        Assert.That(method, Is.Not.Null,
            $"メソッド{name}が見つかりません。");

        return method.Invoke(_controller, arguments);
    }
}
