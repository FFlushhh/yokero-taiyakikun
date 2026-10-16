using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    public GameManager gameManager;

    private void Start()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
    }

    /// <summary>
    /// Trigger判定・移動経路判定で共通して使用する。
    /// </summary>
    public void ReportEnemyHit()
    {
        if (gameManager == null)
        {
            Debug.LogWarning(
                "PlayerCollision: GameManagerが見つかりません。");
            return;
        }

        // 開始演出中やゲームオーバー後は判定しない。
        if (!gameManager.GameStarted || gameManager.GameOver)
            return;

        gameManager.PlayerHit();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            ReportEnemyHit();
        }
    }
}