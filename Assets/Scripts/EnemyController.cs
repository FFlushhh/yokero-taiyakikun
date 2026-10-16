using UnityEngine;

/// <summary>
/// 敵オブジェクトの挙動（移動 / 加速 / プレイヤー側へのカーブ / 画面外削除）を担当
/// - 固定方向（大型敵）と追尾（通常敵）を切り替え
/// - 初期速度、加速率、カーブ量などは InitializeSettings で外部から設定可能
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float baseSpeed = 2f;        // スポーン時の初期速度
    [SerializeField] private float speedGrowth = 0.2f;    // 経過時間ごとの加速率
    [SerializeField] private float followRadius = 4f;     // プレイヤーを追尾する半径
    [SerializeField] private float maxCurve = 0.3f;       // プレイヤー接近時のカーブ量（最大）
    [SerializeField] private float minCurve = 0.05f;      // プレイヤー遠距離時のカーブ量（最小）

    [Header("References")]
    public Transform Player;                              // プレイヤー参照（Inspector でセット）

    // 大型敵用：固定方向で直線移動（null なら追尾判定を行う）
    [HideInInspector] public Vector3? FixedDirection = null;

    private Camera _mainCamera;
    private float _spawnTime;   // スポーン時刻
    private float _initialY;    // 初期Y座標（上下移動制御に使う場合に利用可）
    private SpriteRenderer _sr; // スプライトレンダラ（画面外判定の精度向上用）

    private void Start()
    {
        _mainCamera = Camera.main;
        _initialY = transform.position.y;
        _spawnTime = Time.time;

        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        Vector3 direction = Vector3.left; // 基本は左方向

        if (FixedDirection.HasValue)
        {
            // 大型敵：固定方向で直進
            direction = FixedDirection.Value.normalized;
        }
        else if (Player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, Player.position);

            if (distanceToPlayer < followRadius)
            {
                // 距離に応じてカーブ量を変化（近ければ maxCurve、遠ければ minCurve）
                float curveAmount = Mathf.Lerp(maxCurve, minCurve, distanceToPlayer / followRadius);
                Vector3 toPlayer = (Player.position - transform.position).normalized;
                direction = Vector3.Lerp(Vector3.left, toPlayer, curveAmount).normalized;
            }
        }

        // 経過時間に応じて加速
        float elapsed = Time.time - _spawnTime;
        float currentSpeed = baseSpeed + elapsed * speedGrowth;
        transform.position += direction * currentSpeed * Time.deltaTime;

        // --- 移動方向に合わせた回転（左向き素材共通） ---
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // --- 画面外に出たら削除 ---
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null)
            return;

        Vector3 min = _mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0));
        float margin = transform.localScale.y / 2f;
        if (_sr != null)
            margin = _sr.bounds.extents.x;

        if (transform.position.x < min.x - margin || transform.position.x > min.x + 100f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 外部から速度・カーブ・参照プレイヤー・固定方向を設定する
    /// </summary>
    public void InitializeSettings(float initialSpeed, float growth, float maxCurveValue, float minCurveValue, Transform player, Vector3? fixedDir = null)
    {
        baseSpeed = initialSpeed;
        speedGrowth = growth;
        maxCurve = maxCurveValue;
        minCurve = minCurveValue;
        Player = player;
        FixedDirection = fixedDir;
    }
}
