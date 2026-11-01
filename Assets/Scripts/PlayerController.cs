using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Yokero.Collision;

/// <summary>
/// マウスへの即時追従、移動範囲の制限、経路衝突判定、
/// 安定した進行方向の推定とVisualの滑らかな回転を管理する。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    [FormerlySerializedAs("mainCamera")]
    [SerializeField] private Camera _mainCamera;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _visualSpriteRenderer;

    [Header("Collision")]
    [SerializeField] private Collider2D _collisionCollider;
    [SerializeField] private PlayerCollision _playerCollision;

    [Header("Screen Edge")]
    [SerializeField, Range(0f, 0.1f)]
    private float _edgeMarginViewport = 0.02f;

    [Header("Direction Filtering")]
    [Tooltip("マウス速度の平滑化時間。大きいほど方向が安定する。")]
    [SerializeField, Min(0.001f)]
    private float _directionFilterTime = 0.065f;

    [Tooltip("速度から向きを判断する最低速度（pixel/s）")]
    [SerializeField, Min(0f)]
    private float _minDirectionSpeed = 18f;

    [Tooltip("低速移動でも向きを更新するための累積距離（pixel）")]
    [SerializeField, Min(0.1f)]
    private float _slowDirectionDistancePixels = 3f;

    [Header("Rotation")]
    [Tooltip("見た目の角度が目標角度へ追従する時間")]
    [SerializeField, Min(0.001f)]
    private float _rotationSmoothTime = 0.045f;

    [Tooltip("真上・真下付近での左右反転を安定させる幅")]
    [SerializeField, Range(0f, 0.99f)]
    private float _facingHysteresis = 0.2f;

    [Tooltip("左右移動時にも魚の上下を保つ。OFFなら完全な360度回転。")]
    [SerializeField]
    private bool _keepUpright = true;

    private Transform _visualTransform;
    private bool _movementEnabled = true;
    private bool _facingLeft;

    private Vector2 _previousScreenPosition;
    private Vector2 _directionAnchorScreen;
    private Vector2 _filteredVelocity;

    private float _currentAngle;
    private float _targetAngle;
    private float _angularVelocity;

    private readonly List<RaycastHit2D> _castResults =
        new List<RaycastHit2D>(16);

    private void Start()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_collisionCollider == null)
        {
            _collisionCollider = GetComponent<PolygonCollider2D>();
        }

        if (_playerCollision == null)
        {
            _playerCollision = GetComponent<PlayerCollision>();
        }

        if (_visualSpriteRenderer == null)
        {
            Transform visual = transform.Find("Visual");

            if (visual != null)
            {
                _visualSpriteRenderer =
                    visual.GetComponent<SpriteRenderer>();
            }
        }

        if (_mainCamera == null ||
            _collisionCollider == null ||
            !_collisionCollider.enabled ||
            _playerCollision == null ||
            _visualSpriteRenderer == null)
        {
            Debug.LogError(
                "PlayerController: 必要な参照が不足しています。",
                this);

            enabled = false;
            return;
        }

        _visualTransform = _visualSpriteRenderer.transform;

        // 以後、回転はVisualのみに適用する。
        _currentAngle = _visualTransform.localEulerAngles.z;
        _targetAngle = _currentAngle;
        _facingLeft = _visualSpriteRenderer.flipY;

        ResetDirectionTracking(transform.position);
    }

    private void Update()
    {
        if (!_movementEnabled ||
            _mainCamera == null ||
            Mouse.current == null)
        {
            return;
        }

        if (_playerCollision.gameManager != null &&
            _playerCollision.gameManager.GameOver)
        {
            return;
        }

        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        Vector2 targetScreenPosition =
            GetClampedScreenPosition(mouseScreenPosition);

        float cameraDistance = Mathf.Abs(
            _mainCamera.transform.position.z -
            transform.position.z);

        Vector3 targetPosition = _mainCamera.ScreenToWorldPoint(
            new Vector3(
                targetScreenPosition.x,
                targetScreenPosition.y,
                cameraDistance));

        targetPosition.z = transform.position.z;

        // 移動前に経路上の敵との衝突を確認する。
        Vector2 movement =
            (Vector2)(targetPosition - transform.position);

        if (PlayerPathCollision2D.HitsEnemy(
            _collisionCollider,
            movement,
            _castResults))
        {
            _playerCollision.ReportEnemyHit();
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime > 0f)
        {
            UpdateTargetDirection(
                targetScreenPosition,
                deltaTime);

            UpdateVisualRotation(deltaTime);
        }

        // 位置には補間を適用しない。
        transform.position = targetPosition;
    }

    /// <summary>
    /// マウス位置をCameraの画面内へ制限する。
    /// </summary>
    private Vector2 GetClampedScreenPosition(Vector2 screenPosition)
    {
        Rect rect = _mainCamera.pixelRect;

        float marginX = rect.width * _edgeMarginViewport;
        float marginY = rect.height * _edgeMarginViewport;

        return new Vector2(
            Mathf.Clamp(
                screenPosition.x,
                rect.xMin + marginX,
                rect.xMax - marginX),

            Mathf.Clamp(
                screenPosition.y,
                rect.yMin + marginY,
                rect.yMax - marginY));
    }

    /// <summary>
    /// マウスの短時間の移動速度を平滑化して、
    /// ピクセル単位の揺れに強い目標角度を推定する。
    /// </summary>
    private void UpdateTargetDirection(
        Vector2 screenPosition,
        float deltaTime)
    {
        Vector2 frameDelta =
            screenPosition - _previousScreenPosition;

        _previousScreenPosition = screenPosition;

        Vector2 instantaneousVelocity =
            frameDelta / deltaTime;

        // フレーム時間に依存しにくい指数平滑化係数。
        float blend = 1f - Mathf.Exp(
            -deltaTime / _directionFilterTime);

        _filteredVelocity = Vector2.LerpUnclamped(
            _filteredVelocity,
            instantaneousVelocity,
            blend);

        float minSpeedSqr =
            _minDirectionSpeed * _minDirectionSpeed;

        Vector2 direction;

        if (_filteredVelocity.sqrMagnitude >= minSpeedSqr)
        {
            // 通常移動：時間的に平滑化した速度方向を使う。
            direction = _filteredVelocity.normalized;

            _directionAnchorScreen = screenPosition;
        }
        else
        {
            // 低速移動：移動距離を蓄積して方向を判断する。
            Vector2 accumulated =
                screenPosition - _directionAnchorScreen;

            float minDistanceSqr =
                _slowDirectionDistancePixels *
                _slowDirectionDistancePixels;

            if (accumulated.sqrMagnitude < minDistanceSqr)
            {
                // 信頼できる方向がないときは前回の目標を維持。
                return;
            }

            direction = accumulated.normalized;
            _directionAnchorScreen = screenPosition;
        }

        _targetAngle =
            Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg;
    }

    /// <summary>
    /// 推定した進行方向へVisualだけを滑らかに回転する。
    /// </summary>
    private void UpdateVisualRotation(float deltaTime)
    {
        _currentAngle = Mathf.SmoothDampAngle(
            _currentAngle,
            _targetAngle,
            ref _angularVelocity,
            _rotationSmoothTime,
            Mathf.Infinity,
            deltaTime);

        // 連続した角度をそのまま使う。
        // 左右反転に合わせて180度加算しない。
        _visualTransform.localRotation =
            Quaternion.Euler(0f, 0f, _currentAngle);

        if (!_keepUpright)
        {
            // 360度回転を優先する場合、反転は使わない。
            _visualSpriteRenderer.flipY = false;
            return;
        }

        // 魚の上下を保つための左右状態を、
        // 実際に表示している滑らかな角度から判断する。
        float directionX =
            Mathf.Cos(_currentAngle * Mathf.Deg2Rad);

        if (directionX < -_facingHysteresis)
        {
            _facingLeft = true;
        }
        else if (directionX > _facingHysteresis)
        {
            _facingLeft = false;
        }

        // 描画のみ反転。Colliderには影響させない。
        _visualSpriteRenderer.flipY = _facingLeft;
    }

    /// <summary>
    /// ゲーム開始時や操作再開時に、
    /// 方向推定の履歴を現在の位置へ同期する。
    /// </summary>
    private void ResetDirectionTracking(Vector3 worldPosition)
    {
        if (_mainCamera == null)
        {
            return;
        }

        Vector2 screenPosition =
            _mainCamera.WorldToScreenPoint(worldPosition);

        _previousScreenPosition = screenPosition;
        _directionAnchorScreen = screenPosition;
        _filteredVelocity = Vector2.zero;

        _targetAngle = _currentAngle;
        _angularVelocity = 0f;
    }

    public void SetMovementEnabled(bool enabled)
    {
        _movementEnabled = enabled;
        ResetDirectionTracking(transform.position);
    }

    /// <summary>
    /// GameManagerの既存呼び出しとの互換性を維持する。
    /// </summary>
    public void SetLastMouseWorldPos(Vector3 worldPos)
    {
        ResetDirectionTracking(worldPos);
    }
}
