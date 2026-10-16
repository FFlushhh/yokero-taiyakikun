using UnityEngine;

/// <summary>
/// 敵（通常 / 大型）の生成管理
/// - スポーン間隔、難易度に応じた速度設定、大型敵発生タイミングなどを管理
/// - 元の生成ロジックを踏襲しつつ、GameManager の取得を FindObjectOfType で安定させています
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs & Player")]
    [SerializeField] private GameObject enemyPrefab;      
    [SerializeField] private GameObject largeEnemyPrefab; 
    [SerializeField] private Transform player;            

    [Header("Spawn Timing")]
    [SerializeField] private float maxSpawnInterval = 1f;
    [SerializeField] private float minSpawnInterval = 0.2f; 
    [SerializeField] private float largeSpawnInterval = 5f; 
    [SerializeField] private float largeStartTime = 50f;    
    [SerializeField] private float largeBaseSpeed = 9f;     

    [Header("Enemy Speed Settings")]
    [SerializeField] private float minBaseSpeed = 1.5f;     
    [SerializeField] private float maxBaseSpeed = 3.5f;     
    [SerializeField] private float speedGrowth = 0.15f;     

    private float _spawnTimer = 0f;        
    private float _timer = 0f;             
    private float _largeEnemyTimer = 0f;   
    private GameManager _gameManager;

    private Camera _mainCamera;
    [SerializeField] private AudioSource _LargeEnemySpawnSE;

    private void Start()
    {
        // 安定的に GameManager を取得（推奨: FindFirstObjectByType に変更）
        _gameManager = FindFirstObjectByType<GameManager>();

        if (_gameManager != null)
        {
            _timer = _gameManager.debugStartTime;
        }

        // Camera.main のキャッシュ（頻繁に呼ぶため）
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            Debug.LogWarning("EnemySpawner: Camera.main が見つかりません。");
    }

    private void Update()
    {
        // 変更：ゲーム開始前は生成しないようにする
        if (_gameManager != null && (!_gameManager.GameStarted || _gameManager.GameOver))
        {
            return;
        }

        float delta = Time.deltaTime;
        _timer += delta;
        _largeEnemyTimer += delta;
        _spawnTimer += delta;

        // ...（以下は既存の実装のまま）...
        // 難易度係数（0..1）
        float difficultyFactor;
        if (_timer <= 30f)
        {
            difficultyFactor = 0.3f * (_timer / 30f);
        }
        else if (_timer <= 60f)
        {
            difficultyFactor = 0.3f + 0.7f * ((_timer - 30f) / 30f);
        }
        else
        {
            difficultyFactor = 1.0f;
        }

        // 通常敵生成：非線形な補間を使用（元ロジック維持）
        float spawnInterval = Mathf.Lerp(maxSpawnInterval, minSpawnInterval,
                                        Mathf.Sin(difficultyFactor * Mathf.PI / 2f));

        if (_spawnTimer >= spawnInterval)
        {
            _spawnTimer = 0f;

            int spawnCount = Random.Range(1, 3); 
            for (int i = 0; i < spawnCount; i++)
            {
                SpawnEnemy(difficultyFactor);
            }
        }

        // 大型敵生成
        if (_timer >= largeStartTime && _largeEnemyTimer >= largeSpawnInterval)
        {
            _largeEnemyTimer = 0f;
            int largeCount = Random.Range(1, 3); 
            for (int i = 0; i < largeCount; i++)
            {
                SpawnLargeEnemy();
            }
        }
    }


    // 通常敵生成処理（元ロジックを踏襲）
    private void SpawnEnemy(float difficultyFactor)
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("SpawnEnemy: Camera が見つかりません。");
            return;
        }

        Vector3 min = _mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 max = _mainCamera.ViewportToWorldPoint(new Vector3(1, 1, 0));
        float margin = 0.5f;

        float y = Random.Range(min.y + margin, max.y - margin);
        float spawnX = max.x + margin;

        Vector3 spawnPos = new Vector3(spawnX, y, 0f);
        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        // （任意）Hierarchy 整理のために spawner を親にする場合：
        // enemy.transform.SetParent(transform);

        EnemyController ec = enemy.GetComponent<EnemyController>();
        if (ec != null)
        {
            float baseSpeedFromDifficulty = Mathf.Lerp(minBaseSpeed, maxBaseSpeed, difficultyFactor);
            float randomizedSpeed = baseSpeedFromDifficulty * Random.Range(0.9f, 1.1f);

            ec.InitializeSettings(
                randomizedSpeed,
                speedGrowth,
                0.3f + 0.2f * difficultyFactor,
                0.05f + 0.1f * difficultyFactor,
                player,
                null
            );
        }
    }

    // 大型敵生成処理（元ロジックを踏襲）
    private void SpawnLargeEnemy()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("SpawnLargeEnemy: Camera が見つかりません。");
            return;
        }

        Vector3 min = _mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 max = _mainCamera.ViewportToWorldPoint(new Vector3(1, 1, 0));
        float margin = 0.5f;

        float y = Random.Range(min.y + margin, max.y - margin);
        float spawnX = max.x + margin;

        Vector3 spawnPos = new Vector3(spawnX, y, 0f);
        GameObject enemy = Instantiate(largeEnemyPrefab, spawnPos, Quaternion.identity);

        // enemy.transform.SetParent(transform); // 必要なら親を set

        EnemyController ec = enemy.GetComponent<EnemyController>();
        if (ec != null)
        {
            // プレイヤーへ向かう固定方向（大型敵の直進）
            Vector3 toPlayer = (player.position - spawnPos).normalized;

            ec.InitializeSettings(
                largeBaseSpeed,
                0f,
                0f,
                0f,
                player,
                toPlayer
            );
        }
    }
}
