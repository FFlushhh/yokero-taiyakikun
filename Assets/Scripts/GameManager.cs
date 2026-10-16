using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Mouse.current を使う
using System.Collections;

/// <summary>
/// MainScene の基礎管理クラス（変更版）
/// - プレゲームシーケンス（プレイヤー登場 → マウスワープ → READY/START 表示）を追加
/// - ゲーム開始は GameStarted = true を起点にする
/// - 背景スクロールは開始時に StartScroll() を呼ぶ
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource _bgm;
    [SerializeField] private AudioSource _startSE;
    public Transform player;
    public TMP_Text survivalTimeText;

    [Header("Pre-Start UI")]
    public TMP_Text readyText; // Inspector にセット（空文字にしておく）

    [Header("Pre-Start Settings")]
    public float entranceSpeed = 7f; // プレイヤー登場の速さ（ワールド単位）
    public float entranceOffscreenMargin = 0.5f; // 画面左からどれだけ外に出た位置から出現させるか（ワールド単位）
    public float readyDuration = 1.0f; // READY 表示時間
    public float startFlashDuration = 0.5f; // START 表示時間

    [Header("Game Settings")]
    public float forcedEndTime = 180f; // 3分で強制終了
    public float debugStartTime = 0f;   // デバッグ用：経過時間の初値

    private float _startTime;
    private bool _gameOver = false;
    public bool GameOver => _gameOver;

    private bool _gameStarted = false;
    public bool GameStarted => _gameStarted;

    private Camera _mainCamera;
    private PlayerController _playerController;

    void Start()
    {
        if (_bgm != null)
        {
            _bgm.loop = true;
            _bgm.Play();
        }

        _mainCamera = Camera.main;

        // プレイヤーの PlayerController を取得（Inspector の player をセットしておくこと）
        if (player != null)
        {
            _playerController = player.GetComponent<PlayerController>();
            if (_playerController == null)
                Debug.LogWarning("GameManager: player に PlayerController がアタッチされていません。");
        }
        else
        {
            Debug.LogWarning("GameManager: player が未設定です。");
        }

        // 初期状態: ゲーム未開始にしてプレイヤー操作を無効にする
        if (_playerController != null)
            _playerController.SetMovementEnabled(false);

        // readyText は最初空・非表示にしておく
        if (readyText != null)
            readyText.text = "";

        // プレゲームシーケンス開始
        StartCoroutine(PreStartSequence());
    }

    void Update()
    {
        if (!_gameOver && _gameStarted)
        {
            float elapsed = Time.time - _startTime;
            if (survivalTimeText != null)
                survivalTimeText.text = $"Time: {elapsed:F2}s";

            if (elapsed >= forcedEndTime)
                EndGameForced();
        }
        else
        {
            // ゲーム開始前は時間表示を0.00にしておく（任意）
            if (survivalTimeText != null && !_gameStarted)
                survivalTimeText.text = $"Time: {0:F2}s";
        }
    }

    /// <summary>
    /// プレゲーム処理：プレイヤー登場 → マウスワープ → READY/START 表示 → ゲーム開始
    /// </summary>
    private IEnumerator PreStartSequence()
    {
        // safety
        if (_mainCamera == null)
            _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("GameManager: Camera.main が見つかりません。PreStartSequence を中断します。");
            yield break;
        }

        if (player == null)
        {
            Debug.LogWarning("GameManager: player が未設定です。PreStartSequence を中断します。");
            yield break;
        }

        // --- 1) プレイヤーを画面左端中央（画面外）に配置 ---
        // camera の viewport (0, 0.5) の y を基準に中央にする
        float camDistance = Mathf.Abs(_mainCamera.transform.position.z);
        Vector3 leftCenterWorld = _mainCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, camDistance));
        Vector3 centerWorld = _mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, camDistance));

        // プレイヤーの幅を考慮してもう少し左にする（margin）
        float startX = leftCenterWorld.x - entranceOffscreenMargin;
        Vector3 startPos = new Vector3(startX, leftCenterWorld.y, player.position.z);
        Vector3 targetPos = new Vector3(centerWorld.x, centerWorld.y, player.position.z);

        // 即座に startPos に置く（登場アニメはここから開始）
        player.position = startPos;

        // 移動：左端から中央へ一定速度で移動
        while (Vector3.Distance(player.position, targetPos) > 0.01f)
        {
            player.position = Vector3.MoveTowards(player.position, targetPos, entranceSpeed * Time.deltaTime);
            yield return null;
        }
        player.position = targetPos; // 確実に中央に

        // --- 2) マウスポインタをプレイヤーの中央に配置 ---
        // world -> screen
        Vector3 playerScreenPos = _mainCamera.WorldToScreenPoint(player.position);
        if (Mouse.current != null)
        {
            // WarpCursorPosition expects Vector2 screen coordinates
            Mouse.current.WarpCursorPosition(playerScreenPos);
            // 一瞬待ってから InputSystem の状態を更新（必要に応じ）
            yield return null;
        }
        else
        {
            // InputSystem のマウスが無い場合は何もしない
        }

        // --- 3) PlayerController の内部前フレーム座標を合わせる ---
        if (_playerController != null)
        {
            _playerController.SetLastMouseWorldPos(player.position);
        }

        // // --- 4) READY -> START 表示 --- (点滅)
        // if (readyText != null)
        // {
        //     readyText.gameObject.SetActive(true);

        //     // READY を点滅（ReadyDurationの間に2回点滅 = ON→OFF→ON→OFF）
        //     int blinkCount = 2;
        //     float singleBlinkDuration = readyDuration / (blinkCount * 2f); // ON/OFFで1サイクル
        //     for (int i = 0; i < blinkCount; i++)
        //     {
        //         readyText.text = "READY...";
        //         yield return new WaitForSeconds(singleBlinkDuration);
        //         readyText.text = "";
        //         yield return new WaitForSeconds(singleBlinkDuration);
        //     }

        //     // START を点滅せずに点灯
        //     readyText.text = "START!";
        //     yield return new WaitForSeconds(startFlashDuration);

        //     // 非表示にする
        //     readyText.text = "";
        //     readyText.gameObject.SetActive(false);
        // }
        // else
        // {
        //     // readyText が無ければ少しだけ待機してから開始
        //     yield return new WaitForSeconds(0.2f);
        // }

        // --- 4) READY -> START 表示 --- (フェードイン・アウト)
        if (readyText != null)
        {
            readyText.gameObject.SetActive(true);

            // READY フェード点滅（ReadyDuration の間に2回フェードイン・アウト）
            int blinkCount = 2;
            float singleCycle = readyDuration / blinkCount; // 1周期の長さ
            float halfCycle = singleCycle / 2f;             // IN/OUT 各半分

            for (int i = 0; i < blinkCount; i++)
            {
                // フェードイン
                readyText.text = "READY ";
                float t = 0f;
                while (t < halfCycle)
                {
                    t += Time.deltaTime;
                    float alpha = Mathf.Lerp(0f, 1f, t / halfCycle);
                    SetTextAlpha(readyText, alpha);
                    yield return null;
                }

                // フェードアウト
                t = 0f;
                while (t < halfCycle)
                {
                    t += Time.deltaTime;
                    float alpha = Mathf.Lerp(1f, 0f, t / halfCycle);
                    SetTextAlpha(readyText, alpha);
                    yield return null;
                }
            }

            // START を点滅せずに点灯
            readyText.text = "START";
            SetTextAlpha(readyText, 1f);
            yield return new WaitForSeconds(startFlashDuration);

            // 非表示にする
            readyText.text = "";
            readyText.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
        }

        // --- 5) ゲーム開始処理 ---
        _gameStarted = true;
        _startTime = Time.time - debugStartTime;

        // 開始SE 再生
        if (_startSE != null)
            _startSE.Play();

        // プレイヤーの操作を有効化
        if (_playerController != null)
            _playerController.SetMovementEnabled(true);

        // // 背景スクロール開始（シーン内の Done_BGScroller を全て StartScroll）
        // Done_BGScroller[] scrollers = FindObjectsOfType<Done_BGScroller>();
        // foreach (var s in scrollers)
        //     s.StartScroll();

        // （必要あれば）敵スポナーも GameStarted を見て生成を開始するのでここでは特に操作しない

        yield break;
    }
    
    /// <summary>
    /// TMP_Text のアルファ値（透明度）を変更する
    /// </summary>
    private void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null) return;
        Color c = text.color;
        c.a = alpha;
        text.color = c;
    }

    /// <summary>
    /// プレイヤーが敵と衝突したときに呼ぶ（PlayerCollision から呼ばれる）
    /// </summary>
    public void PlayerHit()
    {
        if (_gameOver) return;

        _gameOver = true;
        float survivalTime = (_gameStarted) ? Time.time - _startTime : 0f;

        // データ保存
        PlayerPrefs.SetFloat("LastSurvivalTime", survivalTime);
        PlayerPrefs.Save();

        // BGM停止（音が残らないように）
        if (_bgm != null) _bgm.Stop();

        // リザルトシーンへ遷移
        SceneManager.LoadScene("ResultScene");
    }

    /// <summary>
    /// 強制終了（タイムアップ）時の処理
    /// </summary>
    void EndGameForced()
    {
        if (_gameOver) return;

        _gameOver = true;
        PlayerPrefs.SetFloat("LastSurvivalTime", forcedEndTime);
        PlayerPrefs.Save();

        if (_bgm != null) _bgm.Stop();

        SceneManager.LoadScene("ResultScene");
    }

}
