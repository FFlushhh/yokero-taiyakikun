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
    [Header("Scene Transition")]
    [SerializeField] private DiveSceneTransition _diveTransition;
    
    [Header("Audio Source")]
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

    public void StartGame()
    {
        if (_diveTransition == null)
        {
            Debug.LogError(
                "TitleManager: DiveSceneTransitionが未設定です。",
                this);

            return;
        }

        // 共通トランジションの開始に成功した場合のみSEを再生する。
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
    /// アプリ終了（エディタ上では動作しない）
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
    }
}
