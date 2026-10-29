/*
 * TitleSceneInteractionTests.cs
 *
 * 【テスト対象】
 * TitleScene.unity
 * TitleBubblePopController
 * TitleBubbleSecretTrigger
 * TitleGiantBubbleSecret
 * TitleTaiyakiSwimSecret
 *
 * 【目的】
 * 実際のTitleSceneを読み込み、通常泡の破壊イベントから
 * 隠し演出の発動・終了・再発動までのコンポーネント間連携を検証する。
 *
 * 【主なテスト内容】
 * - 通常泡の破壊イベント10回で巨大泡が発動すること
 * - 巨大泡の4回クリックで演出が正常終了すること
 * - 巨大泡終了後に再び隠し演出を発動できること
 * - たいやきの遊泳演出が正常に終了すること
 * - たいやき終了後に隠し演出管理の状態がリセットされること
 * - Scene内のGameObject・コンポーネント間の参照が機能すること
 * - 巨大泡ButtonのOnClickとRegisterHitの連携が機能すること
 *
 * 【テスト方式】
 * Play Mode / UnityTest / Scene Integration Test
 *
 * ※Build Settingsに登録された実際のTitleSceneを読み込んで検証する。
 * ※通常泡の破壊イベントはテストコードから送信するため、
 *   実際のマウスクリックやSEの聞こえ方は検証対象外。
 */

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 実際のTitleSceneを読み込む統合テスト。
// TitleSceneは Build Settings に登録されていること。
public sealed class TitleSceneInteractionTests
{
    private TitleBubblePopController _pop;
    private TitleBubbleSecretTrigger _trigger;
    private TitleGiantBubbleSecret _giant;
    private TitleTaiyakiSwimSecret _taiyaki;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("TitleScene", LoadSceneMode.Single);
        Assert.IsNotNull(load, "TitleSceneがBuild Settingsに登録されていません。");
        while (!load.isDone) yield return null;
        yield return null;

        GameObject root = GameObject.Find("TittleBubbleInteraction");
        Assert.IsNotNull(root, "TitleSceneのTittleBubbleInteractionが見つかりません。");
        _pop = root.GetComponent<TitleBubblePopController>();
        _trigger = root.GetComponent<TitleBubbleSecretTrigger>();
        _giant = Object.FindFirstObjectByType<TitleGiantBubbleSecret>();
        _taiyaki = Object.FindFirstObjectByType<TitleTaiyakiSwimSecret>();
        Assert.IsNotNull(_pop);
        Assert.IsNotNull(_trigger);
        Assert.IsNotNull(_giant);
        Assert.IsNotNull(_taiyaki);
        Assert.IsFalse(_trigger.IsEffectRunning);
    }

    private static void HitActualGiantButton(int times)
    {
        // Scene内のButton.onClick永続リスナを通して検証。
        // Assembly Definition移行でUnityEventの参照が壊れた場合も検出する。
        GameObject go = GameObject.Find("GiantBubbleButton");
        Assert.IsNotNull(go, "巨大泡の表示中なのにGiantBubbleButtonが存在しません。");
        Button button = go.GetComponent<Button>();
        Assert.IsNotNull(button);
        for (int i = 0; i < times; i++) button.onClick.Invoke();
    }

    private void Pop(int count)
    {
        // 実Sceneのイベント接続〜演出を通して検証する。
        // Particleのクリック判定自体はTitleBubblePopControllerTests側で扱う。
        for (int i = 0; i < count; i++)
            TitlePlayTestUtil.SendPopped(_pop);
    }

    [UnityTest]
    public IEnumerator TenBubbleEvents_StartActualGiantBubble_ThenFourHitsComplete()
    {
        TitlePlayTestUtil.SetEffectEntries(_trigger, (_giant, 1));
        Pop(9);
        Assert.IsFalse(_trigger.IsEffectRunning);
        Pop(1);
        Assert.IsTrue(_trigger.IsEffectRunning);
        Assert.IsTrue(_giant.IsPlaying);

        HitActualGiantButton(4);
        Assert.AreEqual(GiantBubbleEndReason.Destroyed, _giant.LastEndReason);
        Assert.IsFalse(_trigger.IsEffectRunning);
        Assert.AreEqual(0, _trigger.PopCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CompletedGiantBubble_AllowsNewActivation()
    {
        TitlePlayTestUtil.SetEffectEntries(_trigger, (_giant, 1));
        Pop(10);
        HitActualGiantButton(4);
        Assert.IsFalse(_trigger.IsEffectRunning);
        Pop(10);
        Assert.IsTrue(_trigger.IsEffectRunning);
        Assert.IsTrue(_giant.IsPlaying);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ActualTaiyakiSceneEffect_CompletesAndResetsTrigger()
    {
        TitlePlayTestUtil.SetEffectEntries(_trigger, (_taiyaki, 1));
        // このテスト中だけ時間を短縮する。PlayMode終了時にScene変更は破棄される。
        TitlePlayTestUtil.SetField(_taiyaki, "_travelSeconds", .12f);
        Pop(10);
        Assert.IsTrue(_trigger.IsEffectRunning);
        yield return new WaitForSecondsRealtime(.30f);
        Assert.IsFalse(_trigger.IsEffectRunning);
        Assert.AreEqual(0, _trigger.PopCount);
    }
}
