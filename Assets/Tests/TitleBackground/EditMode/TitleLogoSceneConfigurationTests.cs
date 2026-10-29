/*
 * TitleLogoSceneConfigurationTests.cs
 *
 * 【テスト対象】
 * TitleScene.unity
 * TitleLogoInteraction
 * TitleLogoのRectTransform / Image / AudioSource
 * Canvas / EventSystem / StartButton
 *
 * 【目的】
 * タイトルロゴのアニメーションとクリックリアクションに必要な
 * GameObject・コンポーネント・アセット・Inspector設定が、
 * TitleSceneに正しく保存されていることを検証する。
 * また、既存UIの設定やスクリプト参照が意図せず変更・破損
 * していないことを確認する。
 *
 * 【主なテスト内容】
 * - TitleLogoに必要なコンポーネントが存在すること
 * - TitleLogoInteractionが有効でクリックイベントに対応すること
 * - RectTransformのAnchor・Pivot・位置・回転・Scaleが正しいこと
 * - 正しいタイトルロゴ画像がImageに割り当てられていること
 * - ImageのRaycast Targetが有効になっていること
 * - ロゴ画像のRead/Write Enabledが有効になっていること
 * - 透明部分のクリック判定用Alpha閾値が設定されていること
 * - 専用AudioSourceとクリックSEの参照が正しく設定されていること
 * - AudioSourceの自動再生・ループ・2D再生設定が適切であること
 * - SEの音量設定が有効な範囲内にあること
 * - 常時アニメーションの振幅・周期が有効な値であること
 * - クリック時の演出時間・拡大率・角度制限が有効な値であること
 * - CanvasとGraphicRaycasterが存在すること
 * - EventSystemとInputSystemUIInputModuleが存在すること
 * - StartButtonにStartGameイベントが登録されていること
 * - TitleScene内にMissing Scriptが存在しないこと
 *
 * 【テスト方式】
 * Edit Mode / NUnit / UnityEditor
 * EditorSceneManager / SerializedObject / AssetDatabase
 *
 * ※実際のTitleSceneをAdditiveで読み込み、保存済み設定を検証する。
 * ※一部の初期Transformやアセットパスは期待値を固定しているため、
 *   仕様変更時にはテスト側の期待値も確認・更新する。
 * ※アニメーションの実行結果や実際のクリック操作は検証せず、
 *   Play Modeテストで確認する。
 * ※テスト終了時には読み込んだSceneを閉じる。
 */


using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

/// <summary>
/// 実際のTitleSceneに保存されたロゴの参照・設定を検証する。
/// </summary>
public sealed class TitleLogoSceneConfigurationTests
{
    private const string ScenePath =
        "Assets/Scenes/TitleScene.unity";

    private const string LogoImagePath =
        "Assets/yokero_taiyakikun_logo_option2_unity.png";

    private const string LogoSoundPath =
        "Assets/AudioSources/SE/TitleLogoSE/TitleLogoHit_01.mp3";

    private Scene _scene;
    private GameObject _logoObject;
    private MonoBehaviour _controller;
    private Image _image;

    [SetUp]
    public void SetUp()
    {
        SceneAsset asset =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

        Assert.IsNotNull(asset, "TitleSceneが見つかりません。");

        _scene = EditorSceneManager.OpenScene(
            ScenePath, OpenSceneMode.Additive);

        Assert.IsTrue(_scene.IsValid() && _scene.isLoaded);

        _logoObject = FindObject("TitleLogo");
        Assert.IsNotNull(_logoObject);

        _image = _logoObject.GetComponent<Image>();
        Assert.IsNotNull(_image);

        foreach (MonoBehaviour component in
                 _logoObject.GetComponents<MonoBehaviour>())
        {
            if (component != null &&
                component.GetType().Name == "TitleLogoInteraction")
            {
                _controller = component;
                break;
            }
        }

        Assert.IsNotNull(
            _controller,
            "TitleLogoInteractionがTitleLogoにありません。");
    }

    [TearDown]
    public void TearDown()
    {
        if (_scene.IsValid() && _scene.isLoaded)
            EditorSceneManager.CloseScene(_scene, true);
    }

    private GameObject FindObject(string name)
    {
        foreach (GameObject root in _scene.GetRootGameObjects())
        {
            foreach (Transform child in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.name == name)
                    return child.gameObject;
            }
        }

        return null;
    }

    private SerializedProperty Property(string name)
    {
        SerializedProperty property =
            new SerializedObject(_controller).FindProperty(name);

        Assert.IsNotNull(property, "Missing field: " + name);
        return property;
    }

    [Test]
    public void TitleLogo_HasRequiredComponents()
    {
        Assert.IsTrue(_logoObject.activeInHierarchy);
        Assert.IsTrue(_controller.enabled);

        Assert.IsNotNull(_logoObject.GetComponent<RectTransform>());
        Assert.IsNotNull(_logoObject.GetComponent<CanvasRenderer>());
        Assert.IsNotNull(_logoObject.GetComponent<Image>());
        Assert.IsNotNull(_logoObject.GetComponent<AudioSource>());

        Assert.IsInstanceOf<IPointerClickHandler>(_controller);
    }

    [Test]
    public void TitleLogo_HasExpectedInitialTransform()
    {
        RectTransform rect =
            _logoObject.GetComponent<RectTransform>();

        Assert.That(
            Vector2.Distance(rect.anchorMin, new Vector2(0.5f, 0.5f)),
            Is.LessThan(0.001f));

        Assert.That(
            Vector2.Distance(rect.anchorMax, new Vector2(0.5f, 0.5f)),
            Is.LessThan(0.001f));

        Assert.That(
            Vector2.Distance(rect.pivot, new Vector2(0.5f, 0.5f)),
            Is.LessThan(0.001f));

        Assert.That(
            Vector2.Distance(
                rect.anchoredPosition, new Vector2(0f, 230f)),
            Is.LessThan(0.001f));

        Assert.That(
            Vector3.Distance(
                rect.localScale, new Vector3(1.2f, 1.2f, 1f)),
            Is.LessThan(0.001f));

        Assert.That(
            Quaternion.Angle(rect.localRotation, Quaternion.identity),
            Is.LessThan(0.01f));
    }

    [Test]
    public void TitleLogo_SpriteAndAlphaRaycastAreConfigured()
    {
        Assert.IsTrue(_image.raycastTarget);

        Assert.IsNotNull(_image.sprite);

        Assert.AreEqual(
            LogoImagePath,
            AssetDatabase.GetAssetPath(_image.sprite));

        TextureImporter importer =
            AssetImporter.GetAtPath(LogoImagePath) as TextureImporter;

        Assert.IsNotNull(importer);
        Assert.IsTrue(
            importer.isReadable,
            "ロゴ画像のRead/Write EnabledがOFFです。");

        Assert.That(
            Property("_alphaHitTestMinimumThreshold").floatValue,
            Is.InRange(0.01f, 1f));
    }

    [Test]
    public void TitleLogo_HasDedicatedClickSound()
    {
        AudioSource source =
            _logoObject.GetComponent<AudioSource>();

        Assert.IsFalse(source.playOnAwake);
        Assert.IsFalse(source.loop);
        Assert.That(source.spatialBlend, Is.EqualTo(0f).Within(0.001f));

        Assert.AreSame(
            source,
            Property("_clickAudioSource").objectReferenceValue);

        UObject clip =
            Property("_clickClip").objectReferenceValue;

        Assert.IsNotNull(clip);

        Assert.AreEqual(
            LogoSoundPath,
            AssetDatabase.GetAssetPath(clip));

        Assert.That(
            Property("_clickVolume").floatValue,
            Is.InRange(0f, 1f));
    }

    [Test]
    public void TitleLogo_AnimationParametersAreValid()
    {
        Assert.GreaterOrEqual(
            Property("_idleVerticalAmplitude").floatValue, 0f);

        Assert.Greater(
            Property("_idleVerticalPeriodSeconds").floatValue, 0f);

        Assert.That(
            Property("_idleTiltAmplitudeDegrees").floatValue,
            Is.InRange(0f, 5f));

        Assert.Greater(
            Property("_idleTiltPeriodSeconds").floatValue, 0f);

        Assert.Greater(
            Property("_clickDurationSeconds").floatValue, 0f);

        Assert.GreaterOrEqual(
            Property("_clickScaleIncrease").floatValue, 0f);

        Assert.GreaterOrEqual(
            Property("_maxReactionAngleDegrees").floatValue, 0f);

        Assert.GreaterOrEqual(
            Property("_maxReactionScaleIncrease").floatValue, 0f);

        Assert.GreaterOrEqual(
            Property("_minimumClickIntervalSeconds").floatValue, 0f);
    }

    [Test]
    public void TitleScene_HasRequiredUiAndNoMissingScripts()
    {
        GameObject canvasObject = FindObject("Canvas");
        GameObject eventObject = FindObject("EventSystem");
        GameObject startObject = FindObject("StartButton");

        Assert.IsNotNull(canvasObject);
        Assert.IsNotNull(eventObject);
        Assert.IsNotNull(startObject);

        Canvas canvas = canvasObject.GetComponent<Canvas>();

        Assert.IsNotNull(canvas);
        Assert.AreEqual(
            RenderMode.ScreenSpaceOverlay, canvas.renderMode);

        Assert.IsNotNull(
            canvasObject.GetComponent<GraphicRaycaster>());

        Assert.IsNotNull(
            eventObject.GetComponent<EventSystem>());

        bool hasInputModule = false;

        foreach (MonoBehaviour component in
                 eventObject.GetComponents<MonoBehaviour>())
        {
            if (component != null &&
                component.GetType().FullName ==
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule")
            {
                hasInputModule = true;
            }
        }

        Assert.IsTrue(
            hasInputModule,
            "InputSystemUIInputModuleがありません。");

        Button startButton = startObject.GetComponent<Button>();
        Assert.IsNotNull(startButton);
        Assert.IsTrue(startButton.interactable);

        bool hasStartGame = false;

        for (int i = 0; i <
             startButton.onClick.GetPersistentEventCount(); i++)
        {
            if (startButton.onClick.GetPersistentMethodName(i) ==
                "StartGame")
            {
                hasStartGame = true;
            }
        }

        Assert.IsTrue(
            hasStartGame,
            "スタートボタンのStartGameイベントが未設定です。");

        foreach (GameObject root in _scene.GetRootGameObjects())
        {
            foreach (Transform child in
                     root.GetComponentsInChildren<Transform>(true))
            {
                int missing =
                    GameObjectUtility
                        .GetMonoBehavioursWithMissingScriptCount(
                            child.gameObject);

                Assert.AreEqual(
                    0, missing,
                    "Missing Script: " + child.gameObject.name);
            }
        }
    }
}
