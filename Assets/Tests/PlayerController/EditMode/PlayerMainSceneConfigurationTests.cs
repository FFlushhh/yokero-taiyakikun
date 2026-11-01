/*
 * PlayerMainSceneConfigurationTests.cs
 *
 * 【テスト対象】
 * Assets/Scenes/MainScene.unity
 * Player、Visual、Rigidbody2D、PolygonCollider2D、
 * PlayerControllerおよび関連コンポーネント
 *
 * 【目的】
 * Playerの移動・描画・当たり判定を分離するために
 * 必要なScene設定と参照関係を保証する。
 *
 * 【主なテスト内容】
 * - PlayerとVisualが正しい親子構造を持つこと
 * - PlayerにKinematic Rigidbody2Dがあること
 * - 使用するPolygonCollider2Dが有効であること
 * - VisualにColliderが付いていないこと
 * - PlayerControllerの主要な参照が正しいこと
 * - VisualのAnimatorが正しく設定されていること
 *
 * 【テスト方式】
 * Edit Mode / NUnit
 *
 * ※MainSceneはAdditiveで開く。
 * ※テストが開いたSceneのみ終了時に閉じる。
 * ※実際の物理的な接触挙動はPlayModeで検証する。
 */

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerMainSceneConfigurationTests
{
    private const string ScenePath =
        "Assets/Scenes/MainScene.unity";

    private const BindingFlags FieldFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private Scene _scene;
    private bool _openedScene;

    private GameObject _player;
    private Transform _visual;

    [SetUp]
    public void SetUp()
    {
        _scene = SceneManager.GetSceneByPath(ScenePath);

        if (!_scene.IsValid() || !_scene.isLoaded)
        {
            _scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);

            _openedScene = true;
        }

        Assert.That(_scene.IsValid(), Is.True);
        Assert.That(_scene.isLoaded, Is.True);

        foreach (GameObject root in _scene.GetRootGameObjects())
        {
            if (root.name == "Player")
            {
                _player = root;
                break;
            }
        }

        Assert.That(_player, Is.Not.Null,
            "MainSceneにPlayerが見つかりません。");

        _visual = _player.transform.Find("Visual");

        Assert.That(_visual, Is.Not.Null,
            "Playerの子にVisualが見つかりません。");
    }

    [TearDown]
    public void TearDown()
    {
        if (_openedScene &&
            _scene.IsValid() &&
            _scene.isLoaded)
        {
            EditorSceneManager.CloseScene(_scene, true);
        }

        _openedScene = false;
        _player = null;
        _visual = null;
    }

    [Test]
    public void Player_HasKinematicRigidbody2D()
    {
        Rigidbody2D rigidbody =
            _player.GetComponent<Rigidbody2D>();

        Assert.That(rigidbody, Is.Not.Null);
        Assert.That(
            rigidbody.bodyType,
            Is.EqualTo(RigidbodyType2D.Kinematic));

        Assert.That(rigidbody.simulated, Is.True);
    }
    
    [Test]
    public void Player_UsesEnabledPolygonTriggerCollider()
    {
        PolygonCollider2D polygon =
            _player.GetComponent<PolygonCollider2D>();

        Assert.That(polygon, Is.Not.Null);
        Assert.That(polygon.enabled, Is.True);
        Assert.That(polygon.isTrigger, Is.True);

        Assert.That(
            polygon.GetTotalPointCount(),
            Is.GreaterThan(2));

        CapsuleCollider2D capsule =
            _player.GetComponent<CapsuleCollider2D>();

        if (capsule != null)
        {
            Assert.That(capsule.enabled, Is.False);
        }
    }

    [Test]
    public void Visual_IsChildWithoutPhysicsComponents()
    {
        Assert.That(
            _visual.parent,
            Is.EqualTo(_player.transform));

        Assert.That(
            _visual.GetComponent<Collider2D>(),
            Is.Null);

        Assert.That(
            _visual.GetComponent<Rigidbody2D>(),
            Is.Null);

        Assert.That(
            _visual.GetComponent<SpriteRenderer>(),
            Is.Not.Null);
    }

    [Test]
    public void Visual_AnimatorDoesNotApplyRootMotion()
    {
        Animator animator = _visual.GetComponent<Animator>();

        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(
            animator.updateMode,
            Is.EqualTo(AnimatorUpdateMode.Normal));
    }

    [Test]
    public void Player_RootTransformStartsWithoutRotation()
    {
        Assert.That(
            Quaternion.Angle(
                _player.transform.localRotation,
                Quaternion.identity),
            Is.LessThan(0.01f));

        Assert.That(
            _player.transform.localScale,
            Is.EqualTo(new Vector3(0.5f, 0.5f, 1f)));
    }

    [Test]
    public void PlayerController_ReferencesExpectedComponents()
    {
        Type controllerType = Type.GetType(
            "PlayerController, Assembly-CSharp");

        Assert.That(controllerType, Is.Not.Null);

        Component controller =
            _player.GetComponent(controllerType);

        Assert.That(controller, Is.Not.Null);

        Camera mainCamera =
            (Camera)GetField(controller, "_mainCamera");

        Collider2D collider =
            (Collider2D)GetField(
                controller, "_collisionCollider");

        SpriteRenderer visualRenderer =
            (SpriteRenderer)GetField(
                controller, "_visualSpriteRenderer");

        Component playerCollision =
            (Component)GetField(
                controller, "_playerCollision");

        Assert.That(mainCamera, Is.Not.Null);
        Assert.That(mainCamera.orthographic, Is.True);

        Assert.That(
            collider,
            Is.SameAs(
                _player.GetComponent<PolygonCollider2D>()));

        Assert.That(
            visualRenderer,
            Is.SameAs(
                _visual.GetComponent<SpriteRenderer>()));

        Assert.That(playerCollision, Is.Not.Null);
        Assert.That(
            playerCollision.gameObject,
            Is.SameAs(_player));
    }

    [Test]
    public void PlayerController_HasValidDirectionParameters()
    {
        Type controllerType = Type.GetType(
            "PlayerController, Assembly-CSharp");

        Assert.That(controllerType, Is.Not.Null);

        Component controller =
            _player.GetComponent(controllerType);

        Assert.That(controller, Is.Not.Null);

        float filterTime = (float)GetField(
            controller, "_directionFilterTime");

        float rotationTime = (float)GetField(
            controller, "_rotationSmoothTime");

        float minimumSpeed = (float)GetField(
            controller, "_minDirectionSpeed");

        float slowDistance = (float)GetField(
            controller, "_slowDirectionDistancePixels");

        float hysteresis = (float)GetField(
            controller, "_facingHysteresis");

        Assert.That(filterTime, Is.GreaterThan(0f));
        Assert.That(rotationTime, Is.GreaterThan(0f));
        Assert.That(minimumSpeed, Is.GreaterThanOrEqualTo(0f));
        Assert.That(slowDistance, Is.GreaterThan(0f));

        Assert.That(hysteresis, Is.InRange(0f, 0.99f));
    }

    private static object GetField(
        Component target, string name)
    {
        FieldInfo field =
            target.GetType().GetField(name, FieldFlags);

        Assert.That(field, Is.Not.Null,
            $"フィールド{name}が見つかりません。");

        return field.GetValue(target);
    }
}
