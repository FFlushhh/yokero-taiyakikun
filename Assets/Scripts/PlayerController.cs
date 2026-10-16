
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Yokero.Collision;

/// <summary>
/// プレイヤーの位置・回転制御（マウス追従）
///
/// 【主な機能】
/// - マウス座標への追従
/// - 移動方向に応じた回転・左右反転
/// - 画面外への移動制限
/// - GameManagerからの操作有効・無効制御
/// - 高速移動時のすり抜け対策（T01）
///
/// 【T01の変更】
/// 移動先に直接ワープする前に、Collider2D.Castを使用して
/// 移動経路上に敵が存在するかを検査する。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    public Camera mainCamera;

    [Header("Rotation Settings")]
    [SerializeField]
    private float _rotationThreshold = 0.0003f;

    [Header("Collision Settings (T01)")]
    [SerializeField]
    private Collider2D _collisionCollider;

    [SerializeField]
    private PlayerCollision _playerCollision;

    // 前フレームのマウスワールド座標
    private Vector3 _lastMouseWorldPos;

    // プレイヤーのSpriteRenderer
    private SpriteRenderer _spriteRenderer;

    // 左右反転時に元の大きさを維持するための初期スケール
    private Vector3 _initialScale;

    // GameManagerから操作を制御するためのフラグ
    private bool movementEnabled = true;

    // Cast結果を再利用し、毎フレームのメモリ確保を抑える
    private readonly List<RaycastHit2D> _castResults =
        new List<RaycastHit2D>(16);

    /// <summary>
    /// GameManagerからプレイヤー操作の有効・無効を設定する。
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        if (!enabled)
        {
            _lastMouseWorldPos = transform.position;
        }
    }

    private void Start()
    {
        // Main Cameraの取得
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogError(
                "PlayerController: Main Cameraが見つかりません。",
                this
            );
            return;
        }

        // SpriteRendererの取得
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 初期スケールを保持
        _initialScale = transform.localScale;

        // T01：衝突判定用Componentの取得
        if (_collisionCollider == null)
        {
            _collisionCollider = GetComponent<CapsuleCollider2D>();
        }

        if (_playerCollision == null)
        {
            _playerCollision = GetComponent<PlayerCollision>();
        }

        if (_collisionCollider == null || _playerCollision == null)
        {
            Debug.LogError(
                "PlayerController: T01の衝突判定に必要なComponentがありません。",
                this
            );

            // 衝突判定のない状態で操作させない
            movementEnabled = false;
        }

        // 初期のマウス位置を取得
        Vector2 mouseScreenPos = Vector2.zero;

        if (Mouse.current != null)
        {
            mouseScreenPos = Mouse.current.position.ReadValue();
        }

        float camDistance =
            Mathf.Abs(mainCamera.transform.position.z);

        _lastMouseWorldPos = mainCamera.ScreenToWorldPoint(
            new Vector3(
                mouseScreenPos.x,
                mouseScreenPos.y,
                camDistance
            )
        );
    }

    private void Update()
    {
        // 操作が無効な場合は処理しない
        if (!movementEnabled) return;
        if (mainCamera == null) return;
        if (Mouse.current == null) return;

        // ゲームオーバー後は操作させない
        if (_playerCollision != null &&
            _playerCollision.gameManager != null &&
            _playerCollision.gameManager.GameOver)
        {
            return;
        }

        // ----------------------------------------
        // 1. マウス座標をワールド座標に変換
        // ----------------------------------------

        Vector2 mouseScreenPos =
            Mouse.current.position.ReadValue();

        float camDistance =
            Mathf.Abs(mainCamera.transform.position.z);

        Vector3 currentMouseWorldPos =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    mouseScreenPos.x,
                    mouseScreenPos.y,
                    camDistance
                )
            );

        // ----------------------------------------
        // 2. 移動方向に応じた回転・左右反転
        // ----------------------------------------

        Vector3 mouseDelta =
            currentMouseWorldPos - _lastMouseWorldPos;

        if (mouseDelta.sqrMagnitude > _rotationThreshold)
        {
            // マウス移動方向から角度を計算
            float angle =
                Mathf.Atan2(mouseDelta.y, mouseDelta.x)
                * Mathf.Rad2Deg;

            // 左方向に移動するときは左右反転
            bool facingLeft = Mathf.Abs(angle) > 90f;

            Vector3 scale = _initialScale;

            scale.x = facingLeft
                ? -Mathf.Abs(scale.x)
                : Mathf.Abs(scale.x);

            transform.localScale = scale;

            // 左右反転に合わせて角度を補正
            if (facingLeft)
            {
                angle += 180f;
            }

            transform.rotation =
                Quaternion.Euler(0f, 0f, angle);
        }

        _lastMouseWorldPos = currentMouseWorldPos;

        // ----------------------------------------
        // 3. 画面外への移動制限
        // ----------------------------------------

        float halfWidth = 0f;
        float halfHeight = 0f;

        if (_spriteRenderer != null)
        {
            halfWidth = _spriteRenderer.bounds.extents.x;
            halfHeight = _spriteRenderer.bounds.extents.y;
        }

        Vector3 min = mainCamera.ViewportToWorldPoint(
            new Vector3(0f, 0f, camDistance)
        );

        Vector3 max = mainCamera.ViewportToWorldPoint(
            new Vector3(1f, 1f, camDistance)
        );

        // 最終的な移動目標位置
        Vector3 targetPos = currentMouseWorldPos;

        targetPos.x = Mathf.Clamp(
            targetPos.x,
            min.x + halfWidth,
            max.x - halfWidth
        );

        targetPos.y = Mathf.Clamp(
            targetPos.y,
            min.y + halfHeight,
            max.y - halfHeight
        );

        // Z座標は現在の値を維持
        targetPos.z = transform.position.z;

        // ----------------------------------------
        // 4. T01：移動経路上の敵との衝突判定
        // ----------------------------------------

        // 現在位置から移動先までの変位
        Vector2 movement =
            (Vector2)(targetPos - transform.position);

        if (_collisionCollider != null &&
            _playerCollision != null)
        {
            bool hitEnemy =
                PlayerPathCollision2D.HitsEnemy(
                    _collisionCollider,
                    movement,
                    _castResults
                );

            if (hitEnemy)
            {
                // 既存のゲームオーバー処理を呼ぶ
                _playerCollision.ReportEnemyHit();

                // 衝突した場合は移動しない
                return;
            }
        }

        // ----------------------------------------
        // 5. 衝突しなければ移動
        // ----------------------------------------

        transform.position = targetPos;
    }

    /// <summary>
    /// GameManagerがプレイヤーをワープさせた際などに、
    /// 前フレームのマウス座標を補正する。
    /// </summary>
    public void SetLastMouseWorldPos(Vector3 worldPos)
    {
        _lastMouseWorldPos = worldPos;
    }
}
