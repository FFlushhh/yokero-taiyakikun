using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// TitleScene の管理クラス
/// - BGM の再生
/// - スタート時の効果音再生 → メインシーンへ遷移
/// - アプリ終了（ビルド時のみ有効）
/// </summary>
public class TitleManager : MonoBehaviour
{
    [SerializeField] private AudioSource _bgm;
    [SerializeField] private AudioSource _startSound;

    void Start()
    {
        // BGM が割り当てられていればループ再生
        if (_bgm != null)
        {
            _bgm.loop = true;
            _bgm.Play();
        }
    }

    /// <summary>
    /// UI ボタン等から呼び出す：ゲーム開始処理
    /// - スタート音があれば再生してからシーン遷移（元実装通り）
    /// </summary>
    public void StartGame()
    {
        if (_startSound != null && _startSound.clip != null)
        {
            StartCoroutine(PlayStartAndLoadScene());
        }
        else
        {
            LoadMainScene();
        }
    }

    /// <summary>
    /// スタート音を再生してからメインシーンへ遷移するコルーチン
    /// </summary>
    private IEnumerator PlayStartAndLoadScene()
    {
        _startSound.Play();
        float waitTime = Mathf.Max(0f, _startSound.clip.length - 1.5f);
        yield return new WaitForSeconds(waitTime);
        LoadMainScene();
    }

    /// <summary>
    /// MainScene をロードするヘルパー（Invoke 用）
    /// </summary>
    private void LoadMainScene()
    {
        SceneManager.LoadScene("MainScene");
    }

    /// <summary>
    /// アプリ終了（エディタ上では動作しない）
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
    }
}
