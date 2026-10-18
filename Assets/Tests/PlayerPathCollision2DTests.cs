using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Yokero.Collision;

public class PlayerPathCollision2DTests
{
    private GameObject player;
    private GameObject enemy;
    private GameObject obstacle;

    private CapsuleCollider2D playerCollider;

    private readonly List<RaycastHit2D> results =
        new List<RaycastHit2D>(16);

    [SetUp]
    public void SetUp()
    {
        // プレイヤーを作成
        player = new GameObject("TestPlayer");
        player.transform.position = new Vector3(-5f, 0f, 0f);

        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        playerCollider = player.AddComponent<CapsuleCollider2D>();
        playerCollider.size = new Vector2(0.6f, 0.8f);
        playerCollider.isTrigger = true;

        // 敵を作成
        enemy = new GameObject("TestEnemy");
        enemy.tag = "Enemy";
        enemy.transform.position = Vector3.zero;

        BoxCollider2D enemyCollider =
            enemy.AddComponent<BoxCollider2D>();

        enemyCollider.size = new Vector2(0.1f, 1f);
        enemyCollider.isTrigger = true;

        Physics2D.SyncTransforms();
    }
    
    [TearDown]
    public void TearDown()
    {
        // EditModeではGameObjectを即座に破棄する。
        if (player != null)
        {
            Object.DestroyImmediate(player);
        }

        if (enemy != null)
        {
            Object.DestroyImmediate(enemy);
        }

        if (obstacle != null)
        {
            Object.DestroyImmediate(obstacle);
        }

        results.Clear();
    }

    // UT01：高速移動で細い敵を横切る
    [Test]
    public void FastMovementAcrossEnemy_DetectsCollision()
    {
        bool hit = PlayerPathCollision2D.HitsEnemy(
            playerCollider,
            Vector2.right * 10f,
            results
        );

        Assert.IsTrue(hit,
            "高速移動で敵を横切ったが、衝突を検出できませんでした。");
    }

    // UT02：敵から十分離れた場所を通過
    [Test]
    public void MovementBesideEnemy_DoesNotDetectCollision()
    {
        enemy.transform.position = new Vector3(0f, 3f, 0f);

        bool hit = PlayerPathCollision2D.HitsEnemy(
            playerCollider,
            Vector2.right * 10f,
            results
        );

        Assert.IsFalse(hit,
            "接触していない敵を誤検出しました。");
    }

    // UT03：Enemyタグ以外のCollider
    [Test]
    public void NonEnemyCollider_IsIgnored()
    {
        enemy.tag = "Untagged";

        bool hit = PlayerPathCollision2D.HitsEnemy(
            playerCollider,
            Vector2.right * 10f,
            results
        );

        Assert.IsFalse(hit,
            "Enemyタグ以外のColliderを敵として検出しました。");
    }

    // UT04：Triggerの敵を検出
    [Test]
    public void TriggerEnemy_IsDetected()
    {
        Assert.IsTrue(
            enemy.GetComponent<BoxCollider2D>().isTrigger
        );

        bool hit = PlayerPathCollision2D.HitsEnemy(
            playerCollider,
            Vector2.right * 10f,
            results
        );

        Assert.IsTrue(hit,
            "Triggerの敵を検出できませんでした。");
    }

    // UT05：他のColliderの後方に敵が存在
    [Test]
    public void EnemyBehindOtherCollider_IsDetected()
    {
        obstacle = new GameObject("TestObstacle");
        obstacle.transform.position =
            new Vector3(-2f, 0f, 0f);

        BoxCollider2D obstacleCollider =
            obstacle.AddComponent<BoxCollider2D>();

        obstacleCollider.size = Vector2.one;
        obstacle.tag = "Untagged";

        bool hit = PlayerPathCollision2D.HitsEnemy(
            playerCollider,
            Vector2.right * 10f,
            results
        );

        Assert.IsTrue(hit,
            "別のColliderより後方の敵を見逃しました。");
    }

    // UT06：逆方向への移動
    [Test]
    public void LeftwardMovement_DetectsCollision()
    {
        player.transform.position =
            new Vector3(5f, 0f, 0f);

        bool hit = PlayerPathCollision2D.HitsEnemy(
            playerCollider,
            Vector2.left * 10f,
            results
        );

        Assert.IsTrue(hit,
            "右から左への移動で敵を検出できませんでした。");
    }

    // UT07：移動量ゼロ
    [Test]
    public void ZeroMovement_DoesNotRunPathCollision()
    {
        bool hit = PlayerPathCollision2D.HitsEnemy(
            playerCollider,
            Vector2.zero,
            results
        );

        Assert.IsFalse(hit,
            "移動量ゼロで経路衝突が検出されました。");
    }
}
