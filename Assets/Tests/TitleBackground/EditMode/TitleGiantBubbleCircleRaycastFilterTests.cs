/*
 * TitleGiantBubbleCircleRaycastFilterTests.cs
 *
 * 【テスト対象】
 * TitleGiantBubbleCircleRaycastFilter
 *
 * 【目的】
 * 巨大泡の見た目に合わせた円形クリック判定が正しく機能し、
 * 位置・Scale・縦横比の変化にも対応できることを検証する。
 *
 * 【主なテスト内容】
 * - 円の中心がクリック可能領域として判定されること
 * - 矩形の角など、円の外側がクリック対象にならないこと
 * - 円の内側と外側を正しく区別できること
 * - RectTransformの移動・拡大後も判定領域が追従すること
 * - 縦横比が異なる場合、短辺を基準に半径が計算されること
 *
 * 【テスト方式】
 * Edit Mode / NUnit
 *
 * ※テスト用RectTransformを生成して座標判定を検証する。
 * ※実際のマウスクリックやUIイベントの発火は検証対象外。
 */

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TitleGiantBubbleCircleRaycastFilterTests
{
    private GameObject _go;
    private RectTransform _rect;
    private TitleGiantBubbleCircleRaycastFilter _filter;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("RaycastTarget", typeof(RectTransform),
            typeof(TitleGiantBubbleCircleRaycastFilter));
        _rect = _go.GetComponent<RectTransform>();
        _rect.sizeDelta = new Vector2(200f, 200f);
        _rect.position = new Vector3(500f, 300f, 0f);
        _filter = _go.GetComponent<TitleGiantBubbleCircleRaycastFilter>();
        // EditModeでMonoBehaviour.Awakeが自動呼出されない環境にも対応。
        typeof(TitleGiantBubbleCircleRaycastFilter)
            .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_filter, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    private Vector2 ScreenPoint(Vector2 local)
    {
        return RectTransformUtility.WorldToScreenPoint(
            null, _rect.TransformPoint(local));
    }

    [Test]
    public void Center_IsAccepted()
    {
        Assert.IsTrue(_filter.IsRaycastLocationValid(ScreenPoint(Vector2.zero), null));
    }

    [Test]
    public void CornerOutsideCircle_IsRejected()
    {
        Assert.IsFalse(_filter.IsRaycastLocationValid(
            ScreenPoint(new Vector2(90f, 90f)), null));
    }

    [Test]
    public void InnerPointAndOuterPoint_AreDistinguished()
    {
        Assert.IsTrue(_filter.IsRaycastLocationValid(
            ScreenPoint(new Vector2(80f, 0f)), null));
        Assert.IsFalse(_filter.IsRaycastLocationValid(
            ScreenPoint(new Vector2(100f, 0f)), null));
    }

    [Test]
    public void MovingAndScalingRect_KeepsHitAreaAligned()
    {
        _rect.position = new Vector3(320f, 240f, 0f);
        _rect.localScale = new Vector3(1.5f, 1.5f, 1f);
        Assert.IsTrue(_filter.IsRaycastLocationValid(ScreenPoint(Vector2.zero), null));
        Assert.IsFalse(_filter.IsRaycastLocationValid(
            ScreenPoint(new Vector2(95f, 95f)), null));
    }

    [Test]
    public void NonSquareRect_UsesShorterDimensionAsDiameter()
    {
        _rect.sizeDelta = new Vector2(400f, 100f);
        Assert.IsTrue(_filter.IsRaycastLocationValid(
            ScreenPoint(new Vector2(40f, 0f)), null));
        Assert.IsFalse(_filter.IsRaycastLocationValid(
            ScreenPoint(new Vector2(60f, 0f)), null));
    }
}
