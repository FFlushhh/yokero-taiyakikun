/*
 * GameOverReasonContextTests.cs
 *
 * 【テスト対象】
 * GameOverReasonContext
 * GameOverReason
 *
 * 【目的】
 * ゲームオーバー理由の記録・取得・初期化が
 * 期待どおりに動作することを検証する。
 *
 * 【主なテスト内容】
 * - 初期状態がNoneであること
 * - 敵との接触理由を記録・取得できること
 * - 時間切れ理由を記録・取得できること
 * - 理由を取得するとNoneに戻ること
 * - Clearで理由を初期化できること
 *
 * 【テスト方式】
 * Edit Mode / NUnit / Reflection
 *
 * ※Assembly-CSharp内のクラスをReflectionで参照する。
 * ※実際のScene遷移やGameManagerとの連携は検証対象外。
 */

using System;
using System.Reflection;
using NUnit.Framework;

public sealed class GameOverReasonContextTests
{
    private const BindingFlags StaticFlags =
        BindingFlags.Public | BindingFlags.Static;

    private Type _reasonType;
    private MethodInfo _setReason;
    private MethodInfo _consumeReason;
    private MethodInfo _clear;

    [SetUp]
    public void SetUp()
    {
        Type contextType = Type.GetType(
            "GameOverReasonContext, Assembly-CSharp");

        _reasonType = Type.GetType(
            "GameOverReason, Assembly-CSharp");

        Assert.That(contextType, Is.Not.Null);
        Assert.That(_reasonType, Is.Not.Null);

        _setReason = contextType.GetMethod(
            "SetReason", StaticFlags);

        _consumeReason = contextType.GetMethod(
            "ConsumeReason", StaticFlags);

        _clear = contextType.GetMethod(
            "Clear", StaticFlags);

        Assert.That(_setReason, Is.Not.Null);
        Assert.That(_consumeReason, Is.Not.Null);
        Assert.That(_clear, Is.Not.Null);

        // 各テストが前のテストの状態に依存しないようにする。
        Clear();
    }

    [TearDown]
    public void TearDown()
    {
        // テスト終了後も実行時の状態を残さない。
        if (_clear != null)
        {
            Clear();
        }
    }

    [Test]
    public void InitialReason_IsNone()
    {
        Assert.That(
            ConsumeReason(),
            Is.EqualTo("None"));
    }

    [TestCase("EnemyCollision")]
    [TestCase("TimeLimit")]
    public void SetReason_CanBeConsumed(string expectedReason)
    {
        SetReason(expectedReason);

        Assert.That(
            ConsumeReason(),
            Is.EqualTo(expectedReason));
    }

    [Test]
    public void ConsumeReason_ResetsReasonToNone()
    {
        SetReason("EnemyCollision");

        Assert.That(
            ConsumeReason(),
            Is.EqualTo("EnemyCollision"));

        Assert.That(
            ConsumeReason(),
            Is.EqualTo("None"));
    }

    [Test]
    public void Clear_ResetsReasonToNone()
    {
        SetReason("TimeLimit");
        Clear();

        Assert.That(
            ConsumeReason(),
            Is.EqualTo("None"));
    }

    private void SetReason(string reasonName)
    {
        object reason = Enum.Parse(
            _reasonType, reasonName);

        _setReason.Invoke(
            null, new[] { reason });
    }

    private string ConsumeReason()
    {
        object result = _consumeReason.Invoke(
            null, null);

        return result.ToString();
    }

    private void Clear()
    {
        _clear.Invoke(null, null);
    }
}