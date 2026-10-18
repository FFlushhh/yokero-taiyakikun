using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Yokero.Collision;

/// <summary>
/// マウス追従・画面内制限・高速移動の経路衝突判定を維持しつつ、
/// 実際に実行する移動方向へプレイヤーを向ける
/// 既存 PlayerController.cs の置き換え版（同名ファイルを上書きし、既存.metaを維持）
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    [FormerlySerializedAs("mainCamera")]
    [SerializeField] private Camera _mainCamera;

    [Header("Rotation Settings")]
    [SerializeField, Min(0f)] private float _rotationThreshold = 0.0003f;

    [Header("Visual Bounds")]
    [Tooltip("Player/Visual の SpriteRenderer。画面外へのはみ出し防止に使用する。")]
    [SerializeField] private SpriteRenderer _visualSpriteRenderer;

    [Header("Collision Settings (T01)")]
    [SerializeField] private Collider2D _collisionCollider;
    [SerializeField] private PlayerCollision _playerCollision;

    [Header("Screen Edge Settings")]
    [Tooltip("画面端からの余白。0ならプレイヤーの中心が画面端まで移動可能。")]
    [SerializeField, Range(0f, 0.1f)]
    private float _edgeMarginViewport = 0.02f;

    private Vector3 _lastMouseWorldPos;
    private Vector3 _initialScale;
    private bool _movementEnabled = true;

    // Cast結果を使い回し、毎フレームの割り当てを避ける（既存仕様）
    private readonly List<RaycastHit2D> _castResults = new List<RaycastHit2D>(16);

    public void SetMovementEnabled(bool enabled)
    {
        _movementEnabled = enabled;
        if (!enabled)
        {
            _lastMouseWorldPos = transform.position;
        }
    }

    private void Start()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            Debug.LogError("PlayerController: Main Cameraが見つかりません。", this);
            _movementEnabled = false;
            return;
        }

        _initialScale = transform.localScale;

        if (_collisionCollider == null)
        {
            _collisionCollider = GetComponent<CapsuleCollider2D>();
        }

        if (_playerCollision == null)
        {
            _playerCollision = GetComponent<PlayerCollision>();
        }

        if (_visualSpriteRenderer == null)
        {
            // Player本体の空SpriteRendererを拾わないよう、子から探す
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer.gameObject != gameObject && renderer.sprite != null)
                {
                    _visualSpriteRenderer = renderer;
                    break;
                }
            }
        }

        if (_collisionCollider == null || _playerCollision == null)
        {
            Debug.LogError("PlayerController: T01の衝突判定に必要なComponentがありません。", this);
            _movementEnabled = false;
        }

        Vector2 mouseScreenPos = Vector2.zero;
        if (Mouse.current != null)
        {
            mouseScreenPos = Mouse.current.position.ReadValue();
        }

        float camDistance = Mathf.Abs(_mainCamera.transform.position.z);
        _lastMouseWorldPos = _mainCamera.ScreenToWorldPoint(
            new Vector3(mouseScreenPos.x, mouseScreenPos.y, camDistance));
    }

    private void Update()
    {
        if (!_movementEnabled || _mainCamera == null || Mouse.current == null)
        {
            return;
        }

        if (_playerCollision != null &&
            _playerCollision.gameManager != null &&
            _playerCollision.gameManager.GameOver)
        {
            return;
        }

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        float camDistance = Mathf.Abs(_mainCamera.transform.position.z);

        Vector3 currentMouseWorldPos = _mainCamera.ScreenToWorldPoint(
            new Vector3(mouseScreenPos.x, mouseScreenPos.y, camDistance));

        // マウスの前回座標はGameManagerとの互換性のため保持する
        // 向きの判定には使わない（画面端などで実移動と異なるため）
        _lastMouseWorldPos = currentMouseWorldPos;

    // 画面端から一定割合の余白を確保する。
    // 魚の回転角度に左右されない移動範囲を使用する。
    Vector3 min = _mainCamera.ViewportToWorldPoint(
        new Vector3(
            _edgeMarginViewport,
            _edgeMarginViewport,
            camDistance));

    Vector3 max = _mainCamera.ViewportToWorldPoint(
        new Vector3(
            1f - _edgeMarginViewport,
            1f - _edgeMarginViewport,
            camDistance));

    // マウス位置を、固定された移動可能範囲に制限する。
    Vector3 targetPos = currentMouseWorldPos;

    targetPos.x = Mathf.Clamp(targetPos.x, min.x, max.x);
    targetPos.y = Mathf.Clamp(targetPos.y, min.y, max.y);
    targetPos.z = transform.position.z;

        // 「画面内に制限された移動先」への実変位を進行方向に採用する
        Vector2 movement = (Vector2)(targetPos - transform.position);

        // 既存T01: 実際に移動する前に移動経路上の敵を検査
        if (_collisionCollider != null && _playerCollision != null)
        {
            bool hitEnemy = PlayerPathCollision2D.HitsEnemy(
                _collisionCollider, movement, _castResults);
            if (hitEnemy)
            {
                _playerCollision.ReportEnemyHit();
                return;
            }
        }

        // プレイヤー本体を従来どおり回転・反転し、既存のColliderの
        // 姿勢も描画方向と一緒に変える。Animatorは子VisualのSpriteだけを更新する
        if (movement.sqrMagnitude > _rotationThreshold)
        {
            float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            bool facingLeft = Mathf.Abs(angle) > 90f;
            Vector3 scale = _initialScale;
            scale.x = facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;

            if (facingLeft)
            {
                angle += 180f;
            }

            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
        // movement がほぼゼロならTransformを変更せず直前の向きを維持

        transform.position = targetPos;
    }

    /// <summary>
    /// 旧GameManagerの呼び出しとの互換性を維持する
    /// 向きはカーソルの差分ではなく実移動から計算する
    /// </summary>
    public void SetLastMouseWorldPos(Vector3 worldPos)
    {
        _lastMouseWorldPos = worldPos;
    }
}
