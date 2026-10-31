
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Yokero.Collision;

/// <summary>
/// プレイヤーのマウス追従、画面内移動制限、経路衝突判定、
/// 移動方向に応じた回転・左右反転を管理する。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    [FormerlySerializedAs("mainCamera")]
    [SerializeField] private Camera _mainCamera;

    [Header("Collision Settings")]
    [SerializeField] private Collider2D _collisionCollider;
    [SerializeField] private PlayerCollision _playerCollision;

    [Header("Screen Edge Settings")]
    [Tooltip("画面端から確保するViewport座標上の余白")]
    [SerializeField, Range(0f, 0.1f)]
    private float _edgeMarginViewport = 0.02f;

    [Header("Direction Stability")]
    [Tooltip("向きを更新するために必要な累積移動距離")]
    [SerializeField, Min(0.001f)]
    private float _directionUpdateDistance = 0.06f;

    [Tooltip("左右反転の境界付近で向きを維持するための幅")]
    [SerializeField, Range(0f, 0.99f)]
    private float _facingHysteresis = 0.2f;

    private Vector3 _initialScale;
    private Vector2 _directionAnchor;
    private bool _facingLeft;
    private bool _movementEnabled = true;

    // 既存のGameManagerとの互換性維持用
    private Vector3 _lastMouseWorldPos;

    // 毎フレームのメモリ割り当てを抑えるため再利用する
    private readonly List<RaycastHit2D> _castResults =
        new List<RaycastHit2D>(16);

    private void Start()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            Debug.LogError(
                "PlayerController: Main Cameraが見つかりません。",
                this);

            _movementEnabled = false;
            return;
        }

        // 初期姿勢を保存する
        _initialScale = transform.localScale;
        _directionAnchor = transform.position;
        _facingLeft = _initialScale.x < 0f;

        // MainSceneで実際に使用しているPolygonCollider2Dを優先
        if (_collisionCollider == null)
        {
            _collisionCollider = GetComponent<PolygonCollider2D>();
        }

        if (_playerCollision == null)
        {
            _playerCollision = GetComponent<PlayerCollision>();
        }

        if (_collisionCollider == null ||
            !_collisionCollider.enabled ||
            _playerCollision == null)
        {
            Debug.LogError(
                "PlayerController: 有効なCollider2Dまたは" +
                "PlayerCollisionが見つかりません。",
                this);

            _movementEnabled = false;
            return;
        }

        _lastMouseWorldPos = transform.position;
    }

    private void Update()
    {
        if (!_movementEnabled ||
            _mainCamera == null ||
            Mouse.current == null)
        {
            return;
        }

        if (_playerCollision != null &&
            _playerCollision.gameManager != null &&
            _playerCollision.gameManager.GameOver)
        {
            return;
        }

        // 1. マウス座標をワールド座標に変換する
        Vector2 mouseScreenPos =
            Mouse.current.position.ReadValue();

        float cameraDistance = Mathf.Abs(
            _mainCamera.transform.position.z - transform.position.z);

        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(
            new Vector3(
                mouseScreenPos.x,
                mouseScreenPos.y,
                cameraDistance));

        _lastMouseWorldPos = mouseWorldPos;

        // 2. 画面内に収まる移動先を求める
        Vector3 targetPos = GetClampedTargetPosition(
            mouseWorldPos,
            cameraDistance);

        // 3. 実際の移動量を計算する
        Vector2 movement =
            (Vector2)(targetPos - transform.position);

        // 4. 既存の高速移動時の経路衝突判定を維持する
        if (PlayerPathCollision2D.HitsEnemy(
            _collisionCollider,
            movement,
            _castResults))
        {
            _playerCollision.ReportEnemyHit();
            return;
        }

        // 5. 累積移動量に応じて向きを更新する
        UpdateFacingDirection(targetPos);

        // 6. 位置は補間せず、マウスへ直接追従させる
        transform.position = targetPos;
    }

    /// <summary>
    /// マウスのワールド座標を、画面内の移動可能範囲へ制限する。
    /// </summary>
    private Vector3 GetClampedTargetPosition(
        Vector3 mouseWorldPos,
        float cameraDistance)
    {
        Vector3 min = _mainCamera.ViewportToWorldPoint(
            new Vector3(
                _edgeMarginViewport,
                _edgeMarginViewport,
                cameraDistance));

        Vector3 max = _mainCamera.ViewportToWorldPoint(
            new Vector3(
                1f - _edgeMarginViewport,
                1f - _edgeMarginViewport,
                cameraDistance));

        Vector3 targetPos = mouseWorldPos;

        targetPos.x = Mathf.Clamp(targetPos.x, min.x, max.x);
        targetPos.y = Mathf.Clamp(targetPos.y, min.y, max.y);
        targetPos.z = transform.position.z;

        return targetPos;
    }

    /// <summary>
    /// 最後に向きを更新した位置からの累積変位を使用し、
    /// 微小なマウス移動による方向の頻繁な変化を抑制する。
    /// </summary>
    private void UpdateFacingDirection(Vector3 targetPos)
    {
        Vector2 directionMovement =
            (Vector2)targetPos - _directionAnchor;

        float minDistanceSqr =
            _directionUpdateDistance * _directionUpdateDistance;

        if (directionMovement.sqrMagnitude < minDistanceSqr)
        {
            // 移動距離が小さい間は、現在の向きを維持する
            return;
        }

        Vector2 direction = directionMovement.normalized;

        // 左右反転の切り替えにヒステリシスを設ける
        if (direction.x < -_facingHysteresis)
        {
            _facingLeft = true;
        }
        else if (direction.x > _facingHysteresis)
        {
            _facingLeft = false;
        }

        // 実際に移動した方向から回転角度を求める
        float angle =
            Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg;

        // 左右反転時も既存の見た目の向きを維持する
        Vector3 scale = _initialScale;

        scale.x = _facingLeft
            ? -Mathf.Abs(scale.x)
            : Mathf.Abs(scale.x);

        transform.localScale = scale;

        if (_facingLeft)
        {
            angle += 180f;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 次回の方向判定の起点を更新する
        _directionAnchor = targetPos;
    }

    /// <summary>
    /// プレイヤーのマウス操作を有効・無効にする。
    /// 操作再開時には方向判定の起点を現在位置へ戻す。
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        _movementEnabled = enabled;
        _directionAnchor = transform.position;

        if (!enabled)
        {
            _lastMouseWorldPos = transform.position;
        }
    }

    /// <summary>
    /// 既存のGameManagerとの互換性を維持する。
    /// 方向判定には使用しない。
    /// </summary>
    public void SetLastMouseWorldPos(Vector3 worldPos)
    {
        _lastMouseWorldPos = worldPos;
    }
}
