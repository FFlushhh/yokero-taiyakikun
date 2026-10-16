using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class GameOverUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text[] rankTexts; // 5つのランキング表示
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;

    private RankingManager rankingManager;

    void Start()
    {
        fadeGroup.alpha = 0;
        fadeGroup.gameObject.SetActive(false);
        rankingManager = FindObjectOfType<RankingManager>();

        retryButton.onClick.AddListener(OnRetry);
        titleButton.onClick.AddListener(OnTitle);
    }

    public void ShowGameOver(float survivalTime)
    {
        StartCoroutine(ShowSequence(survivalTime));
    }

    private IEnumerator ShowSequence(float time)
    {
        fadeGroup.gameObject.SetActive(true);

        // フェードイン
        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime / 1.0f; // 1秒でフェード
            fadeGroup.alpha = t;
            yield return null;
        }

        // スコア表示
        timeText.text = $"生存時間：{time:F2} 秒";

        // ランキング更新
        var updatedRanking = rankingManager.UpdateRanking(time);

        // 更新後のランキング表示
        for (int i = 0; i < rankTexts.Length; i++)
        {
            rankTexts[i].text = $"{i + 1}位：{updatedRanking[i]:F2} 秒";
        }

        // 入賞時に軽いモーション
        int newRank = rankingManager.GetRank(time);
        if (newRank >= 0)
        {
            TMP_Text target = rankTexts[newRank];
            float scale = 1f;
            for (float s = 0; s < 1; s += Time.unscaledDeltaTime * 4)
            {
                scale = 1f + Mathf.Sin(s * Mathf.PI) * 0.2f;
                target.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            target.transform.localScale = Vector3.one;
        }

        // ボタン表示
        retryButton.gameObject.SetActive(true);
        titleButton.gameObject.SetActive(true);
    }

    private void OnRetry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnTitle()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("TitleScene"); // タイトルシーン名を合わせて変更
    }
}
