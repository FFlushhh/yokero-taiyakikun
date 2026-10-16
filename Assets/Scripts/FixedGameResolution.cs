using UnityEngine;

/// <summary>
/// デスクトップ版のゲーム描画解像度を1920×1080に設定する。
/// </summary>
public sealed class FixedGameResolution : MonoBehaviour
{
    private const int ScreenWidth = 1920;
    private const int ScreenHeight = 1080;

    private void Awake()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        Screen.SetResolution(
            ScreenWidth,
            ScreenHeight,
            FullScreenMode.FullScreenWindow
        );
#endif
    }
}