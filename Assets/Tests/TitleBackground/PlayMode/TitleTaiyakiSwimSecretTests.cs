/*
 * TitleTaiyakiSwimSecretTests.cs
 *
 * 【テスト対象】
 * TitleTaiyakiSwimSecret
 *
 * 【目的】
 * たいやきくんの遊泳演出における表示・移動・上下運動・
 * 終了通知・途中中断時の状態管理が正しいことを検証する。
 *
 * 【主なテスト内容】
 * - 演出開始時にたいやきくんが表示されること
 * - 演出再生中の二重起動が防止されること
 * - 指定時間の経過後に演出が終了すること
 * - 正常終了時に終了通知が1回だけ呼ばれること
 * - 左から右へ移動すること
 * - 上下振幅が0の場合、上下運動が発生しないこと
 * - 上下振幅が0以外の場合、Sin波に従って上下移動すること
 * - 演出途中で無効化しても上位の隠し演出管理がロックされないこと
 *
 * 【テスト方式】
 * Play Mode / UnityTest
 *
 * ※テスト用CameraとVisualを使用してCoroutineの動作を検証する。
 * ※途中中断時の終了通知は、既存実装で不具合が疑われる重要項目。
 */

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class TitleTaiyakiSwimSecretTests
{
    private GameObject _host;
    private GameObject _cameraGo;
    private GameObject _visual;
    private Camera _camera;
    private TitleTaiyakiSwimSecret _swim;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _cameraGo = new GameObject("TestCamera");
        _camera = _cameraGo.AddComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = 5f;
        _camera.transform.position = new Vector3(0, 0, -10);

        _visual = new GameObject("TaiyakiVisual");
        _host = new GameObject("SwimHost");
        _host.SetActive(false);
        _swim = _host.AddComponent<TitleTaiyakiSwimSecret>();
        TitlePlayTestUtil.SetField(_swim, "_targetCamera", _camera);
        TitlePlayTestUtil.SetField(_swim, "_characterVisual", _visual.transform);
        TitlePlayTestUtil.SetField(_swim, "_travelSeconds", .20f);
        TitlePlayTestUtil.SetField(_swim, "_verticalAmplitude", .2f);
        _host.SetActive(true);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_host != null) Object.Destroy(_host);
        if (_visual != null) Object.Destroy(_visual);
        if (_cameraGo != null) Object.Destroy(_cameraGo);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Start_MakesVisualActive_AndRejectsSecondStart()
    {
        Assert.IsTrue(_swim.TryPlay(null));
        Assert.IsTrue(_visual.activeSelf);
        Assert.IsFalse(_swim.TryPlay(null));
        yield return null;
    }

    [UnityTest]
    public IEnumerator Swim_EndsAndCallsCompletionExactlyOnce()
    {
        int completed = 0;
        Assert.IsTrue(_swim.TryPlay(() => completed++));
        yield return new WaitForSecondsRealtime(.35f);
        Assert.AreEqual(1, completed);
        Assert.IsFalse(_visual.activeSelf);
        yield return null;
        Assert.AreEqual(1, completed);
    }

    [UnityTest]
    public IEnumerator HorizontalMotion_ProgressesFromLeftToRight()
    {
        Assert.IsTrue(_swim.TryPlay(null));
        Vector3 start = _visual.transform.position;
        yield return new WaitForSecondsRealtime(.08f);
        Vector3 middle = _visual.transform.position;
        Assert.Greater(middle.x, start.x);
    }

    [UnityTest]
    public IEnumerator ZeroAmplitude_KeepsVerticalBaseline()
    {
        TitlePlayTestUtil.SetField(_swim, "_verticalAmplitude", 0f);
        Assert.IsTrue(_swim.TryPlay(null));
        float initialY = _visual.transform.position.y;
        yield return new WaitForSecondsRealtime(.07f);
        Assert.AreEqual(initialY, _visual.transform.position.y, .001f);
    }

    [UnityTest]
    public IEnumerator NonZeroAmplitude_MakesVerticalPositionChange()
    {
        TitlePlayTestUtil.SetField(_swim, "_travelSeconds", 1f);
        Assert.IsTrue(_swim.TryPlay(null));
        float initialY = _visual.transform.position.y;
        yield return new WaitForSecondsRealtime(.12f);
        Assert.Greater(Mathf.Abs(_visual.transform.position.y - initialY), .005f);
    }

    [UnityTest]
    public IEnumerator InterruptingComponent_NotifiesOwnerAndDoesNotLeaveLock()
    {
        // 現行production実装では失敗が予想される回帰テスト。
        // OnDisable()がcallbackを呼ばない場合、このassertが赤になる。
        int completed = 0;
        Assert.IsTrue(_swim.TryPlay(() => completed++));
        _swim.enabled = false;
        yield return null;
        Assert.AreEqual(1, completed,
            "中断時に終了通知されず、TitleBubbleSecretTriggerがロックされ得ます。");
        Assert.IsFalse(_visual.activeSelf);
    }
}
