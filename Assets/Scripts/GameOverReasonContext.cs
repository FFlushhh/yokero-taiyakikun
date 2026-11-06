using UnityEngine;

/// <summary>
/// ゲームオーバーの原因を表す。
/// </summary>
public enum GameOverReason
{
    None = 0,
    EnemyCollision = 1,
    TimeLimit = 2
}

/// <summary>
/// MainSceneからResultSceneへゲームオーバー理由を渡す。
/// 理由は一時的に保持し、取得時にリセットする。
/// </summary>
public static class GameOverReasonContext
{
    private static GameOverReason _pendingReason =
        GameOverReason.None;

    /// <summary>
    /// ゲームオーバー理由を記録する。
    /// </summary>
    public static void SetReason(GameOverReason reason)
    {
        _pendingReason = reason;
    }

    /// <summary>
    /// 記録されている理由を取得し、
    /// 次回へ持ち越さないようにリセットする。
    /// </summary>
    public static GameOverReason ConsumeReason()
    {
        GameOverReason reason = _pendingReason;
        _pendingReason = GameOverReason.None;

        return reason;
    }

    /// <summary>
    /// 記録されている理由を破棄する。
    /// </summary>
    public static void Clear()
    {
        _pendingReason = GameOverReason.None;
    }

    /// <summary>
    /// Play Mode開始時やアプリケーション起動時に
    /// staticフィールドを初期化する。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        Clear();
    }
}