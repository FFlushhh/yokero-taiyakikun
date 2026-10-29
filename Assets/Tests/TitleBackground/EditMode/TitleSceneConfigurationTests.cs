/*
 * TitleSceneConfigurationTests.cs
 *
 * 【テスト対象】
 * TitleScene.unity 内の各コンポーネントとInspector設定
 *
 * 【目的】
 * タイトル画面に必要なGameObject・コンポーネント・参照・設定値が
 * 正しく保存されていることを確認し、設定漏れや意図しない変更を検出する。
 *
 * 【主なテスト内容】
 * - 通常泡のカメラ・Particle System・AudioSourceが設定されていること
 * - 通常泡のSEが7種類登録されていること
 * - 隠し演出の発動条件が泡10個の破壊であること
 * - 隠し演出のWeightがたいやき1、巨大泡9であること
 * - 隠し演出発動SEが1種類登録されていること
 * - 巨大泡の必要クリック数が4回であること
 * - 巨大泡のクリックSE・破壊SEがそれぞれ6種類登録されていること
 * - たいやきの移動用参照と上下振幅が正しく設定されていること
 * - 巨大泡ボタンのOnClickにRegisterHitが登録されていること
 * - 巨大泡の反射演出に必要な参照が設定されていること
 *
 * 【テスト方式】
 * Edit Mode / NUnit / UnityEditor
 *
 * ※実際のTitleSceneを読み込み、SerializedObjectで設定を検証する。
 * ※仕様変更によって設定値を変えた場合は、期待値も更新する。
 */

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// TitleSceneの保存済みInspector設定を検証するEditModeテスト。
// 注意: 設定値を意図的に変更した場合は期待値も更新する。
public sealed class TitleSceneConfigurationTests
{
    private const string ScenePath = "Assets/Scenes/TitleScene.unity";
    private Scene _scene;

    [SetUp]
    public void SetUp()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Assert.IsTrue(_scene.IsValid() && _scene.isLoaded);
    }

    [TearDown]
    public void TearDown()
    {
        if (_scene.IsValid() && _scene.isLoaded)
            EditorSceneManager.CloseScene(_scene, true);
    }

    private T Find<T>() where T : Component
    {
        foreach (GameObject root in _scene.GetRootGameObjects())
        {
            T item = root.GetComponentInChildren<T>(true);
            if (item != null) return item;
        }
        Assert.Fail("TitleSceneに" + typeof(T).Name + "が存在しません。");
        return null;
    }

    private static SerializedProperty Property(Object component, string path)
    {
        var p = new SerializedObject(component).FindProperty(path);
        Assert.IsNotNull(p, path);
        return p;
    }

    private static void AssertClipGroup(Object component, string group, int expectedCount)
    {
        var entries = Property(component, group)
            .FindPropertyRelative("_entries");
        Assert.IsNotNull(entries);
        Assert.AreEqual(expectedCount, entries.arraySize, group);
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            Assert.IsNotNull(e.FindPropertyRelative("_clip").objectReferenceValue,
                group + ": Clip[" + i + "] が未設定");
            Assert.Greater(e.FindPropertyRelative("_weight").floatValue, 0f);
        }
    }

    [Test]
    public void NormalBubbleController_HasReferencesAndSevenSounds()
    {
        var controller = Find<TitleBubblePopController>();
        Assert.IsNotNull(Property(controller, "_targetCamera").objectReferenceValue);
        Assert.IsNotNull(Property(controller, "_popAudioSource").objectReferenceValue);
        var systems = Property(controller, "_bubbleSystems");
        Assert.AreEqual(3, systems.arraySize);
        for (int i = 0; i < systems.arraySize; i++)
            Assert.IsNotNull(systems.GetArrayElementAtIndex(i).objectReferenceValue);
        AssertClipGroup(controller, "_popSoundGroup", 7);
    }

    [Test]
    public void SecretTrigger_HasCorrectCountAndOneToNineWeights()
    {
        var trigger = Find<TitleBubbleSecretTrigger>();
        Assert.AreEqual(10, Property(trigger, "_requiredPopCount").intValue);
        Assert.IsNotNull(Property(trigger, "_popController").objectReferenceValue);
        var effects = Property(trigger, "_secretEffects");
        Assert.AreEqual(2, effects.arraySize);
        Assert.AreEqual(1f, effects.GetArrayElementAtIndex(0)
            .FindPropertyRelative("_weight").floatValue);
        Assert.AreEqual(9f, effects.GetArrayElementAtIndex(1)
            .FindPropertyRelative("_weight").floatValue);
        for (int i = 0; i < effects.arraySize; i++)
            Assert.IsNotNull(effects.GetArrayElementAtIndex(i)
                .FindPropertyRelative("_effectComponent").objectReferenceValue);
        Assert.IsInstanceOf<TitleTaiyakiSwimSecret>(effects.GetArrayElementAtIndex(0)
            .FindPropertyRelative("_effectComponent").objectReferenceValue);
        Assert.IsInstanceOf<TitleGiantBubbleSecret>(effects.GetArrayElementAtIndex(1)
            .FindPropertyRelative("_effectComponent").objectReferenceValue);
    }

    [Test]
    public void SecretActivationSound_HasOneValidClip()
    {
        var trigger = Find<TitleBubbleSecretTrigger>();
        Assert.IsNotNull(Property(trigger, "_activationAudioSource").objectReferenceValue);
        AssertClipGroup(trigger, "_activationSoundGroup", 1);
    }

    [Test]
    public void GiantBubble_HasRequiredReferencesAndSixSoundsEach()
    {
        var giant = Find<TitleGiantBubbleSecret>();
        Assert.AreEqual(4, Property(giant, "_requiredHits").intValue);
        foreach (string field in new [] {
            "_bubbleVisual", "_bubbleImage", "_movementArea",
            "_audioSource", "_burstParticles", "_burstCamera"})
            Assert.IsNotNull(Property(giant, field).objectReferenceValue, field);
        AssertClipGroup(giant, "_hitSoundGroup", 6);
        AssertClipGroup(giant, "_breakSoundGroup", 6);
    }

    [Test]
    public void Taiyaki_HasMovementReferencesAndAmplitude()
    {
        var taiyaki = Find<TitleTaiyakiSwimSecret>();
        Assert.IsNotNull(Property(taiyaki, "_targetCamera").objectReferenceValue);
        Assert.IsNotNull(Property(taiyaki, "_characterVisual").objectReferenceValue);
        Assert.AreEqual(6f, Property(taiyaki, "_travelSeconds").floatValue, 0.001f);
        Assert.AreEqual(0.2f, Property(taiyaki, "_verticalAmplitude").floatValue, 0.001f);
    }

    [Test]
    public void GiantBubbleButton_CallsRegisterHit()
    {
        var giant = Find<TitleGiantBubbleSecret>();
        bool registered = false;
        foreach (GameObject root in _scene.GetRootGameObjects())
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == giant &&
                    button.onClick.GetPersistentMethodName(i) == "RegisterHit")
                    registered = true;
            }
        }
        Assert.IsTrue(registered, "巨大泡ButtonにRegisterHitの永続リスナがありません。");
    }

    [Test]
    public void Shimmer_HasAllReferences()
    {
        var shimmer = Find<TitleGiantBubbleShimmer>();
        foreach (string field in new [] {
            "_bubbleImage", "_arcImage", "_arcGroup", "_spotImage", "_spotGroup"})
            Assert.IsNotNull(Property(shimmer, field).objectReferenceValue, field);
    }
}
