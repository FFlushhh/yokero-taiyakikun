using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainSceneのゲームオーバー演出を管理する。
/// ゲーム状態やスコアの管理はGameManagerに任せる。
/// </summary>
public sealed class GameOverTransition : MonoBehaviour
{
    private const string ResultSceneName = "ResultScene";

    [Header("UI")]
    [SerializeField] private Image _fadeImage;

    [Header("Audio")]
    [SerializeField] private AudioSource _gameOverSE;

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float _fadeDuration = 1.0f;

    [SerializeField, Min(0f)]
    private float _transitionDuration = 3.0f;

    private bool _isPlaying;

    // MainSceneからResultSceneへの演出付き遷移かどうかを伝える。
    private static bool _pendingResultEntrance;

    private void Awake()
    {
        // 新しいゲーム開始時は、前回の遷移情報を残さない。
        _pendingResultEntrance = false;

        if (_fadeImage != null)
        {
            _fadeImage.gameObject.SetActive(true);
            SetFadeAlpha(0f);
        }
    }

    /// <summary>
    /// ゲームオーバー演出を開始する。
    /// 重複して呼ばれても1回だけ実行する。
    /// </summary>
    public void Play()
    {
        if (_isPlaying)
        {
            return;
        }

        _isPlaying = true;
        
        if (_gameOverSE != null && _gameOverSE.clip != null)
        {
            _gameOverSE.Play();
        }
        else
        {
            Debug.LogWarning(
                "GameOverTransition: ゲームオーバーSEが未設定です。",
                this);
        }

        StartCoroutine(PlayTransition());
    }

    private IEnumerator PlayTransition()
    {
        float elapsed = 0f;

        // フェード時間が全体時間を超えないようにする。
        float totalDuration = Mathf.Max(
            0f,
            _transitionDuration);

        float fadeDuration = Mathf.Min(
            Mathf.Max(0f, _fadeDuration),
            totalDuration);

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float alpha = fadeDuration > 0f
                ? Mathf.Clamp01(elapsed / fadeDuration)
                : 1f;

            SetFadeAlpha(alpha);

            yield return null;
        }

        // Scene切り替え前に完全に暗転させる。
        SetFadeAlpha(1f);

        // ResultScene側でフェード解除を実行するための通知。
        _pendingResultEntrance = true;

        SceneManager.LoadScene(ResultSceneName);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (_fadeImage == null)
        {
            return;
        }

        Color color = _fadeImage.color;
        color.a = Mathf.Clamp01(alpha);
        _fadeImage.color = color;
    }

    /// <summary>
    /// ResultScene側が遷移情報を取得する。
    /// 読み取ったらフラグを解除する。
    /// </summary>
    public static bool ConsumeResultEntranceRequest()
    {
        bool requested = _pendingResultEntrance;
        _pendingResultEntrance = false;

        return requested;
    }
}