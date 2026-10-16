using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 【ResultManager】
/// リザルト画面の管理を行うクラス。
/// - 生存時間の表示
/// - ランキングの更新・表示
/// - 新記録の演出
/// - 「リトライ」「タイトルへ戻る」ボタン制御
/// 
/// 対象シーン：ResultScene
/// アタッチ先：ResultManager オブジェクト
/// </summary>
public class ResultManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text survivalTimeText;     // 生存時間表示用テキスト
    public TMP_Text rankingText;          // ランキング表示用テキスト
    public Button retryButton;            // リトライボタン
    public Button backToTitleButton;      // タイトルへ戻るボタン

    [Header("Scene Transition")]
    [SerializeField] private DiveSceneTransition _diveTransition;
    
    [Header("Sequence Timing (seconds)")]
    public float showRankingDelay = 1.5f; // 生存時間表示からランキング表示までの待機時間
    public float showButtonsDelay = 1.0f; // ランキング表示からボタン表示までの待機時間
    public float highlightDuration = 0.8f; // 新記録ハイライト演出時間

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgm;        // リザルトBGM
    [SerializeField] private AudioSource _startSound; // ボタン押下時の効果音

    private float lastTime;       // 今回の生存時間
    private float[] ranks = new float[5]; // 上位5位のランキング
    private int newRankIndex = -1;        // 今回のプレイで更新された順位（-1=なし）
    
    private bool _presentationStarted;

    private void Awake()
    {
        // 前回プレイの生存時間を取得する。
        lastTime = PlayerPrefs.GetFloat(
            "LastSurvivalTime",
            0f);

        survivalTimeText.text =
            $"Survival Time: {lastTime:F2}s";

        // フェード解除が完了するまで結果UIを表示しない。
        survivalTimeText.gameObject.SetActive(false);
        rankingText.gameObject.SetActive(false);
        retryButton.gameObject.SetActive(false);
        backToTitleButton.gameObject.SetActive(false);

        // ランキングの計算・保存処理は既存仕様を維持する。
        LoadRanking();
        UpdateRanking();
    }

    // /// <summary>
    // /// デバッグ用：Ctrl+Rでランキングリセット
    // /// </summary>
    // private void Update()
    // {
    //     if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
    //     {
    //         ClearRanking();
    //     }
    // }

    /// <summary>
    /// Scene遷移演出が完了した後に結果表示を開始する。
    /// </summary>
    public void BeginResultPresentation()
    {
        if (_presentationStarted)
        {
            return;
        }

        _presentationStarted = true;

        // 生存時間を表示する。
        survivalTimeText.gameObject.SetActive(true);

        // 既存のランキング・ボタン表示シーケンスを開始する。
        StartCoroutine(ShowResultSequence());

        // リザルトBGMを開始する。
        if (_bgm != null)
        {
            _bgm.loop = true;
            _bgm.Play();
        }
    }
    
    /// <summary>
    /// PlayerPrefsからランキングを読み込む
    /// </summary>
    private void LoadRanking()
    {
        for (int i = 0; i < 5; i++)
            ranks[i] = PlayerPrefs.GetFloat($"Rank{i + 1}", 0f);
    }

    /// <summary>
    /// 今回の生存時間をランキングに反映
    /// </summary>
    private void UpdateRanking()
    {
        for (int i = 0; i < 5; i++)
        {
            if (lastTime > ranks[i])
            {
                // ランキングを下にずらして挿入
                for (int j = 4; j > i; j--)
                    ranks[j] = ranks[j - 1];
                ranks[i] = lastTime;
                newRankIndex = i;
                break;
            }
        }

        // 保存
        for (int i = 0; i < 5; i++)
            PlayerPrefs.SetFloat($"Rank{i + 1}", ranks[i]);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 結果表示の順序を制御するコルーチン
    /// ① 生存時間 → ② ランキング → ③ 新記録演出 → ④ ボタン表示
    /// </summary>
    private IEnumerator ShowResultSequence()
    {
        // ① 少し待機
        yield return new WaitForSeconds(showRankingDelay);

        // ② ランキング表示
        rankingText.gameObject.SetActive(true);
        UpdateRankingDisplay();

        // ③ 新記録なら点滅演出
        if (newRankIndex != -1)
            yield return StartCoroutine(HighlightNewRecord(newRankIndex));

        // ④ ボタン表示
        yield return new WaitForSeconds(showButtonsDelay);
        retryButton.gameObject.SetActive(true);
        backToTitleButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// ランキングテキスト更新
    /// </summary>
    private void UpdateRankingDisplay()
    {
        rankingText.text = "<size=100%><b>Ranking</b></size>\n\n";
        for (int i = 0; i < 5; i++)
        {
            if (i == newRankIndex)
                rankingText.text += $"<color=#FFD700><b>{i + 1}: {ranks[i]:F2}s NEW!</b></color>\n";
            else
                rankingText.text += $"{i + 1}: {ranks[i]:F2}s\n";
        }
    }

    /// <summary>
    /// 新記録ハイライトの点滅アニメーション
    /// </summary>
    private IEnumerator HighlightNewRecord(int index)
    {
        float elapsed = 0f;
        while (elapsed < highlightDuration)
        {
            float t = Mathf.PingPong(Time.time * 4f, 1f);
            rankingText.alpha = Mathf.Lerp(0.5f, 1f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        rankingText.alpha = 1f;
    }

    /// <summary>
    /// デバッグ用：ランキングリセット
    /// </summary>
    [ContextMenu("Clear Ranking")]
    public void ClearRanking()
    {
        for (int i = 0; i < 5; i++)
            PlayerPrefs.DeleteKey($"Rank{i + 1}");
        PlayerPrefs.Save();
        Debug.Log("ランキングをリセットしました。");
    }

    /// <summary>
    /// リトライボタン押下時の処理
    /// 効果音再生後にMainSceneをロード
    /// </summary>
    public void Retry()
    {
        if (_diveTransition == null)
        {
            Debug.LogError(
                "ResultManager: DiveSceneTransitionが未設定です。",
                this);

            return;
        }

        if (!_diveTransition.TryPlayToMainScene(_bgm))
        {
            return;
        }

        // 既存のボタン操作SEを維持する。
        if (_startSound != null && _startSound.clip != null)
        {
            _startSound.Play();
        }
    }

    /// <summary>
    /// タイトルに戻るボタン押下時の処理
    /// </summary>
    public void BackToTitle()
    {
        SceneManager.LoadScene("TitleScene");
    }
}
