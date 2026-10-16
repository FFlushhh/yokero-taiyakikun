using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ResultSceneへ遷移した際のフェード解除を担当する。
/// 演出が完了したらResultManagerに結果表示を開始させる。
/// </summary>
public sealed class ResultSceneEntrance : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image _transitionOverlay;

    [Header("References")]
    [SerializeField] private ResultManager _resultManager;

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float _fadeOutDuration = 0.8f;

    private void Awake()
    {
        if (_transitionOverlay == null)
        {
            Debug.LogError(
                "ResultSceneEntrance: TransitionOverlayが未設定です。",
                this);

            return;
        }

        // Scene開始時は必ず完全に暗転した状態にする。
        _transitionOverlay.gameObject.SetActive(true);
        SetOverlayAlpha(1f);
    }

    private void Start()
    {
        if (_resultManager == null)
        {
            _resultManager = FindFirstObjectByType<ResultManager>();
        }

        // ゲームオーバー演出経由の遷移かどうかを確認する。
        bool shouldReveal =
            GameOverTransition.ConsumeResultEntranceRequest();

        if (!shouldReveal || _transitionOverlay == null)
        {
            // 直接ResultSceneを開いた場合や、
            // 通常の即時遷移では演出を省略する。
            if (_transitionOverlay != null)
            {
                _transitionOverlay.gameObject.SetActive(false);
            }

            StartResultPresentation();
            return;
        }

        StartCoroutine(RevealResultScene());
    }

    private IEnumerator RevealResultScene()
    {
        float elapsed = 0f;

        float duration = Mathf.Max(
            0f,
            _fadeOutDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration);

            SetOverlayAlpha(1f - progress);

            yield return null;
        }

        // 完全に透明にしてから非表示にする。
        SetOverlayAlpha(0f);

        _transitionOverlay.gameObject.SetActive(false);

        // フェード解除が完了してからResult UIを表示する。
        StartResultPresentation();
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (_transitionOverlay == null)
        {
            return;
        }

        Color color = _transitionOverlay.color;
        color.a = Mathf.Clamp01(alpha);
        _transitionOverlay.color = color;
    }

    private void StartResultPresentation()
    {
        if (_resultManager != null)
        {
            _resultManager.BeginResultPresentation();
        }
        else
        {
            Debug.LogError(
                "ResultSceneEntrance: ResultManagerが見つかりません。",
                this);
        }
    }
}