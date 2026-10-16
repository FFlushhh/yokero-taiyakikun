using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// TitleScene・ResultSceneからMainSceneへの
/// 水中ダイブ演出付きScene遷移を管理する。
/// </summary>
public sealed class DiveSceneTransition : MonoBehaviour
{
    private const string MainSceneName = "MainScene";
    private const int BubbleCount = 14;
    private const int TextureSize = 32;

    [Header("UI")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private GraphicRaycaster _graphicRaycaster;
    [SerializeField] private Image _inputBlocker;
    [SerializeField] private Image _fadeCover;
    [SerializeField] private RectTransform _effectsRoot;

    [Header("Audio")]
    [SerializeField] private AudioSource _diveSE;

    [Header("Timing")]
    [SerializeField, Min(0.1f)]
    private float _coverDuration = 0.95f;

    [SerializeField, Min(0f)]
    private float _fadeDelay = 0.2f;

    [SerializeField, Min(0f)]
    private float _splashDuration = 0.7f;

    [SerializeField, Min(0.1f)]
    private float _revealDuration = 0.55f;

    [Header("Ripple Settings")]
    [SerializeField, Min(1f)]
    private float _rippleSizeMultiplier = 1.5f;

    [SerializeField, Range(0.1f, 0.95f)]
    private float _rippleExpansionEnd = 0.75f;
    
    private static DiveSceneTransition _activeTransition;

    private readonly List<Image> _bubbles = new List<Image>();

    private Image _ripple;
    private Texture2D _bubbleTexture;
    private Sprite _bubbleSprite;

    private AudioSource _outgoingBgm;
    private float _initialBgmVolume;

    private bool _isRunning;

    /// <summary>
    /// MainScene側から演出中かどうかを確認する。
    /// </summary>
    public static bool IsTransitioning =>
        _activeTransition != null &&
        _activeTransition._isRunning;

    private void Awake()
    {
        // 通常時は描画と入力遮断を無効化する。
        if (_canvas != null)
        {
            _canvas.enabled = false;
        }

        if (_graphicRaycaster != null)
        {
            _graphicRaycaster.enabled = false;
        }

        SetCoverAlpha(0f);
    }

    /// <summary>
    /// MainSceneへの遷移を開始する。
    /// ボタン連打による二重起動を防ぐ。
    /// </summary>
    public bool TryPlayToMainScene(AudioSource outgoingBgm)
    {
        if (_isRunning || _activeTransition != null)
        {
            return false;
        }

        if (_canvas == null ||
            _graphicRaycaster == null ||
            _inputBlocker == null ||
            _fadeCover == null ||
            _effectsRoot == null)
        {
            Debug.LogError(
                "DiveSceneTransition: UI参照が未設定です。",
                this);

            return false;
        }

        _activeTransition = this;
        _isRunning = true;

        _outgoingBgm = outgoingBgm;
        _initialBgmVolume = outgoingBgm != null
            ? outgoingBgm.volume
            : 0f;

        // 透過ImageでもUI入力を遮断する。
        _inputBlocker.raycastTarget = true;
        _inputBlocker.color = Color.clear;

        _canvas.enabled = true;
        _graphicRaycaster.enabled = true;

        _effectsRoot.gameObject.SetActive(true);

        BuildEffects();
        SetCoverAlpha(0f);

        // // Sceneの切り替え後もCanvasとCoroutineを維持する。
        // DontDestroyOnLoad(gameObject);

        if (_diveSE != null && _diveSE.clip != null)
        {
            _diveSE.Play();
        }

        StartCoroutine(RunTransition());

        return true;
    }

    private IEnumerator RunTransition()
    {
        // 非同期でMainSceneの読み込みを開始する。
        AsyncOperation loading = SceneManager.LoadSceneAsync(
            MainSceneName,
            LoadSceneMode.Single);

        if (loading == null)
        {
            Debug.LogError(
                "DiveSceneTransition: MainSceneを読み込めません。",
                this);

            FinishTransition();
            yield break;
        }

        // 完全に画面を覆うまでSceneの有効化を待機する。
        loading.allowSceneActivation = false;

        float elapsed = 0f;

        while (elapsed < _coverDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / _coverDuration);

            UpdateEffects(elapsed);

            float fadeProgress = Mathf.Clamp01(
                (elapsed - _fadeDelay) /
                Mathf.Max(0.01f, _coverDuration - _fadeDelay));

            SetCoverAlpha(fadeProgress);

            // BGMも画面と一緒にフェードアウトする。
            if (_outgoingBgm != null)
            {
                _outgoingBgm.volume = Mathf.Lerp(
                    _initialBgmVolume,
                    0f,
                    progress);
            }

            yield return null;
        }

        SetCoverAlpha(1f);

        // 完全暗転した後は泡を非表示にする。
        // フェードインで再び見えてしまうことを防ぐ。
        _effectsRoot.gameObject.SetActive(false);

        // 読み込み準備が完了するまで暗転を維持する。
        while (loading.progress < 0.9f)
        {
            yield return null;
        }

        // 画面が完全に覆われた状態でSceneを切り替える。
        loading.allowSceneActivation = true;

        yield return loading;

        // MainScene読み込み完了後にフェードインする。
        elapsed = 0f;

        while (elapsed < _revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / _revealDuration);

            SetCoverAlpha(1f - progress);

            yield return null;
        }

        SetCoverAlpha(0f);

        FinishTransition();
    }

    /// <summary>
    /// トランジションを終了する。
    /// 描画と入力遮断を先に解除し、オブジェクトの破棄は遅らせる。
    /// </summary>
    private void FinishTransition()
    {
        // ゲーム開始処理が進められるようにする。
        _isRunning = false;

        if (_activeTransition == this)
        {
            _activeTransition = null;
        }

        // 演出用Canvasを無効化する。
        if (_graphicRaycaster != null)
        {
            _graphicRaycaster.enabled = false;
        }

        if (_canvas != null)
        {
            _canvas.enabled = false;
        }

        // ゲーム開始処理とリソース解放のタイミングを分離する。
        StartCoroutine(DestroyAfterDelay());
    }

    /// <summary>
    /// 演出終了から少し待ってGameObjectを破棄する。
    /// </summary>
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.3f);

        Destroy(gameObject);
    }

    private void SetCoverAlpha(float alpha)
    {
        if (_fadeCover == null)
        {
            return;
        }

        Color color = _fadeCover.color;
        color.a = Mathf.Clamp01(alpha);

        _fadeCover.color = color;
    }

    /// <summary>
    /// 泡と波紋のSpriteを生成する。
    /// 外部画像を必要としない簡易的なドット調エフェクト。
    /// </summary>
    private void BuildEffects()
    {
        if (_bubbleSprite != null)
        {
            return;
        }

        _bubbleSprite = CreateRingSprite();

        _ripple = CreateEffectImage("Ripple");

        for (int i = 0; i < BubbleCount; i++)
        {
            Image bubble = CreateEffectImage(
                $"Bubble_{i:00}");

            _bubbles.Add(bubble);
        }

        UpdateEffects(0f);
    }

    private Image CreateEffectImage(string objectName)
    {
        GameObject effectObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        effectObject.transform.SetParent(
            _effectsRoot,
            false);

        RectTransform rect =
            effectObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = effectObject.GetComponent<Image>();
        image.sprite = _bubbleSprite;
        image.raycastTarget = false;
        image.preserveAspect = true;

        return image;
    }

    /// <summary>
    /// 泡を外側へ飛ばし、波紋を拡大する。
    /// </summary>
    private void UpdateEffects(float elapsed)
    {
        float progress = Mathf.Clamp01(
            elapsed / Mathf.Max(0.01f, _splashDuration));

        float fade = 1f - progress;

        // 中央の波紋を拡大する。
        // 中央の波紋を画面全体に広がるまで拡大する。
        if (_ripple != null)
        {
            RectTransform rippleRect = _ripple.rectTransform;

            // EffectsRootのサイズから画面の対角線を計算する。
            float width = _effectsRoot.rect.width;
            float height = _effectsRoot.rect.height;

            float screenDiagonal = Mathf.Sqrt(
                width * width + height * height);

            // 最大サイズを画面サイズに応じて決定する。
            float maxSize = screenDiagonal * _rippleSizeMultiplier;

            // 拡大と透明化のタイミングを分離する。
            float expansionProgress = Mathf.Clamp01(
                progress / _rippleExpansionEnd);

            // 拡大速度を滑らかに変化させる。
            float easedProgress = Mathf.SmoothStep(
                0f,
                1f,
                expansionProgress);

            float size = Mathf.Lerp(
                120f,
                maxSize,
                easedProgress);

            rippleRect.sizeDelta = new Vector2(size, size);
            rippleRect.anchoredPosition = Vector2.zero;

            // 拡大が完了するまでは透明度を維持し、
            // その後にフェードアウトする。
            float rippleAlpha = 1f - Mathf.SmoothStep(
                _rippleExpansionEnd,
                1f,
                progress);

            _ripple.color = new Color(
                0.56f,
                0.92f,
                1f,
                0.75f * rippleAlpha);
        }

        // 泡を放射状に拡散させる。
        for (int i = 0; i < _bubbles.Count; i++)
        {
            Image bubble = _bubbles[i];

            float angle =
                2f * Mathf.PI * i / BubbleCount;

            Vector2 direction = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle));

            float distance =
                35f + progress * (260f + (i % 3) * 75f);

            Vector2 position =
                direction * distance +
                Vector2.up * (progress * 65f);

            RectTransform rect =
                bubble.rectTransform;

            rect.anchoredPosition = position;

            float size =
                20f + (i % 5) * 12f + progress * 18f;

            rect.sizeDelta = new Vector2(size, size);

            bubble.color = new Color(
                0.72f,
                0.95f,
                1f,
                0.9f * fade);
        }
    }

    /// <summary>
    /// 32×32のリング状Spriteを生成する。
    /// Point Filterで少しドット感のある輪郭にする。
    /// </summary>
    private Sprite CreateRingSprite()
    {
        _bubbleTexture = new Texture2D(
            TextureSize,
            TextureSize,
            TextureFormat.RGBA32,
            false);

        _bubbleTexture.filterMode = FilterMode.Point;
        _bubbleTexture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels =
            new Color32[TextureSize * TextureSize];

        float center = (TextureSize - 1) * 0.5f;

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;

                float distance = Mathf.Sqrt(
                    dx * dx + dy * dy);

                bool isRing =
                    Mathf.Abs(distance - 11f) <= 1.5f;

                pixels[y * TextureSize + x] = isRing
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        _bubbleTexture.SetPixels32(pixels);
        _bubbleTexture.Apply();

        return Sprite.Create(
            _bubbleTexture,
            new Rect(
                0,
                0,
                TextureSize,
                TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
    }

    private void OnDestroy()
    {
        if (_activeTransition == this)
        {
            _activeTransition = null;
        }

        // 実行時生成したTextureとSpriteを解放する。
        if (_bubbleSprite != null)
        {
            Destroy(_bubbleSprite);
        }

        if (_bubbleTexture != null)
        {
            Destroy(_bubbleTexture);
        }
    }
}