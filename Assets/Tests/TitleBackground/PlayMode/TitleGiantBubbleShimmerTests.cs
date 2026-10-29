/*
 * TitleGiantBubbleShimmerTests.cs
 *
 * 【テスト対象】
 * TitleGiantBubbleShimmer
 *
 * 【目的】
 * 巨大泡の表面に表示する反射光の生成・透明度制御・
 * UIクリック判定への影響・無効化時の状態復元を検証する。
 *
 * 【主なテスト内容】
 * - 起動時に弧状の反射光と光点のSpriteが生成されること
 * - 反射光がUIのRaycastを妨げないこと
 * - 反射光のAlphaが指定範囲内に維持されること
 * - 泡本体の透明度に合わせて反射光も透明になること
 * - コンポーネント無効化時に反射光のAlphaが0になること
 * - 無効化時に反射光の位置・回転が初期状態へ戻ること
 *
 * 【テスト方式】
 * Play Mode / UnityTest
 *
 * ※テスト用Image・CanvasGroupを使用して描画関連の状態を検証する。
 * ※反射光の自然さや視覚的な美しさは目視確認が必要。
 */

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TitleGiantBubbleShimmerTests
{
    private GameObject _host;
    private GameObject _arcGo;
    private GameObject _spotGo;
    private Image _bubbleImage;
    private Image _arcImage;
    private Image _spotImage;
    private CanvasGroup _arcGroup;
    private CanvasGroup _spotGroup;
    private TitleGiantBubbleShimmer _shimmer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _host = new GameObject("ShimmerHost", typeof(RectTransform), typeof(Image));
        _host.SetActive(false);
        _bubbleImage = _host.GetComponent<Image>();
        _bubbleImage.color = Color.white;

        _arcGo = new GameObject("Arc", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _arcGo.transform.SetParent(_host.transform, false);
        _arcImage = _arcGo.GetComponent<Image>();
        _arcGroup = _arcGo.GetComponent<CanvasGroup>();

        _spotGo = new GameObject("Spot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _spotGo.transform.SetParent(_host.transform, false);
        _spotImage = _spotGo.GetComponent<Image>();
        _spotGroup = _spotGo.GetComponent<CanvasGroup>();

        _shimmer = _host.AddComponent<TitleGiantBubbleShimmer>();
        TitlePlayTestUtil.SetField(_shimmer, "_bubbleImage", _bubbleImage);
        TitlePlayTestUtil.SetField(_shimmer, "_arcImage", _arcImage);
        TitlePlayTestUtil.SetField(_shimmer, "_arcGroup", _arcGroup);
        TitlePlayTestUtil.SetField(_shimmer, "_spotImage", _spotImage);
        TitlePlayTestUtil.SetField(_shimmer, "_spotGroup", _spotGroup);
        _host.SetActive(true);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_host != null) Object.Destroy(_host);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Awake_CreatesSprites_AndDisablesRaycastBlocking()
    {
        Assert.IsNotNull(_arcImage.sprite);
        Assert.IsNotNull(_spotImage.sprite);
        Assert.IsFalse(_arcImage.raycastTarget);
        Assert.IsFalse(_spotImage.raycastTarget);
        Assert.IsFalse(_arcGroup.blocksRaycasts);
        Assert.IsFalse(_spotGroup.blocksRaycasts);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HighlightAlpha_StaysInConfiguredRange()
    {
        for (int i = 0; i < 8; i++)
        {
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(_arcGroup.alpha, Is.InRange(0f, .26f + .001f));
            Assert.That(_spotGroup.alpha, Is.InRange(0f, .18f + .001f));
        }
    }

    [UnityTest]
    public IEnumerator BubbleBecomesTransparent_HighlightsBecomeTransparent()
    {
        _bubbleImage.color = new Color(1, 1, 1, 0);
        yield return null;
        Assert.AreEqual(0f, _arcGroup.alpha, .001f);
        Assert.AreEqual(0f, _spotGroup.alpha, .001f);
    }

    [UnityTest]
    public IEnumerator DisablingComponent_RestoresTransformsAndClearsAlpha()
    {
        // Awake時に保存された本当の初期状態を取得する。
        Quaternion initialRotation =
            (Quaternion)TitlePlayTestUtil.GetField(
                _shimmer, "_initialArcRotation");

        Vector2 initialPosition =
            (Vector2)TitlePlayTestUtil.GetField(
                _shimmer, "_initialSpotPosition");

        // アニメーションを進める。
        yield return new WaitForSecondsRealtime(0.08f);

        // 反射光の処理を無効化する。
        _shimmer.enabled = false;

        // 透明度が0になることを確認する。
        Assert.AreEqual(0f, _arcGroup.alpha, 0.001f);
        Assert.AreEqual(0f, _spotGroup.alpha, 0.001f);

        // Awake時の回転に戻っていること。
        Assert.Less(
            Quaternion.Angle(
                initialRotation,
                _arcImage.rectTransform.localRotation),
            0.01f);

        // Awake時の位置に戻っていること。
        Assert.Less(
            Vector2.Distance(
                initialPosition,
                _spotImage.rectTransform.anchoredPosition),
            0.01f);
    }
}
