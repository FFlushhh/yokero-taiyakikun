using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 巨大泡の表面に、短い弧状の反射と
/// 柔らかい楕円状の反射を表示する。
///
/// 反射用Spriteは起動時に一度だけ生成し、
/// 毎フレームは位置・角度・透明度のみ変更する。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleGiantBubbleShimmer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _bubbleImage;
    [SerializeField] private Image _arcImage;
    [SerializeField] private CanvasGroup _arcGroup;
    [SerializeField] private Image _spotImage;
    [SerializeField] private CanvasGroup _spotGroup;

    [Header("Animation")]
    [SerializeField, Min(0.1f)]
    private float _cycleSeconds = 4.5f;

    [SerializeField, Range(0f, 1f)]
    private float _arcMinimumAlpha = 0.08f;

    [SerializeField, Range(0f, 1f)]
    private float _arcMaximumAlpha = 0.26f;

    [SerializeField, Range(0f, 1f)]
    private float _spotMinimumAlpha = 0.05f;

    [SerializeField, Range(0f, 1f)]
    private float _spotMaximumAlpha = 0.18f;

    [SerializeField, Range(0f, 15f)]
    private float _arcRotationDegrees = 6f;

    [SerializeField, Range(0f, 10f)]
    private float _spotDriftPixels = 3f;

    private Sprite _arcSprite;
    private Sprite _spotSprite;

    private Quaternion _initialArcRotation;
    private Vector2 _initialSpotPosition;

    private float _elapsedSeconds;
    private bool _initialized;

    private void Awake()
    {
        if (_bubbleImage == null ||
            _arcImage == null ||
            _arcGroup == null ||
            _spotImage == null ||
            _spotGroup == null)
        {
            Debug.LogError(
                "TitleGiantBubbleShimmer: 参照が不足しています。",
                this);

            enabled = false;
            return;
        }

        _initialArcRotation =
            _arcImage.rectTransform.localRotation;

        _initialSpotPosition =
            _spotImage.rectTransform.anchoredPosition;

        // TextureはAwakeでのみ生成し、
        // Update中の画像生成を避ける。
        _arcSprite = CreateLightSprite(256, true);
        _spotSprite = CreateLightSprite(64, false);

        _arcImage.sprite = _arcSprite;
        _spotImage.sprite = _spotSprite;

        _arcImage.color = Color.white;
        _spotImage.color =
            new Color(0.88f, 0.98f, 1f, 1f);

        _arcImage.type = Image.Type.Simple;
        _spotImage.type = Image.Type.Simple;

        _arcImage.preserveAspect = false;
        _spotImage.preserveAspect = false;

        _arcImage.raycastTarget = false;
        _spotImage.raycastTarget = false;

        _arcGroup.blocksRaycasts = false;
        _spotGroup.blocksRaycasts = false;

        _arcGroup.interactable = false;
        _spotGroup.interactable = false;

        _initialized = true;
    }

    private void OnEnable()
    {
        if (!_initialized)
        {
            return;
        }

        _elapsedSeconds = 0f;
        UpdateHighlights();
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        _elapsedSeconds += Time.unscaledDeltaTime;
        UpdateHighlights();
    }

    private void UpdateHighlights()
    {
        float phase =
            2f * Mathf.PI * _elapsedSeconds /
            Mathf.Max(0.1f, _cycleSeconds);

        // 緩やかな周期変化。
        float arcPulse =
            0.5f - 0.5f * Mathf.Cos(phase);

        float spotPulse =
            0.5f - 0.5f *
            Mathf.Cos(phase * 0.85f + 1.1f);

        // 泡本体が奥へ移動して薄くなる際は、
        // 反射も薄くする。
        float bubbleAlpha = _bubbleImage.color.a;

        _arcGroup.alpha = Mathf.Lerp(
            _arcMinimumAlpha,
            _arcMaximumAlpha,
            arcPulse) * bubbleAlpha;

        _spotGroup.alpha = Mathf.Lerp(
            _spotMinimumAlpha,
            _spotMaximumAlpha,
            spotPulse) * bubbleAlpha;

        // 弧の角度をわずかに変え、
        // 泡の輪郭に沿うように動かす。
        float rotation =
            _arcRotationDegrees *
            Mathf.Sin(phase * 0.7f);

        _arcImage.rectTransform.localRotation =
            _initialArcRotation *
            Quaternion.Euler(0f, 0f, rotation);

        // 小さな光点は既存の反射付近をわずかに漂う。
        float driftX =
            Mathf.Sin(phase * 0.6f) *
            _spotDriftPixels;

        float driftY =
            (Mathf.Sin(phase * 0.5f + 0.6f)
             - Mathf.Sin(0.6f)) *
            _spotDriftPixels * 0.6f;

        _spotImage.rectTransform.anchoredPosition =
            _initialSpotPosition +
            new Vector2(driftX, driftY);
    }

    /// <summary>
    /// 弧状の光、または柔らかい光点のSpriteを生成する。
    /// 専用Shaderや外部画像は使用しない。
    /// </summary>
    private static Sprite CreateLightSprite(
        int textureSize,
        bool createArc)
    {
        Texture2D texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false);

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels =
            new Color32[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // -1〜1の正規化座標。
                float nx =
                    (x + 0.5f) / textureSize * 2f - 1f;

                float ny =
                    (y + 0.5f) / textureSize * 2f - 1f;

                float alpha;

                if (createArc)
                {
                    float radius =
                        Mathf.Sqrt(nx * nx + ny * ny);

                    float angleDegrees =
                        Mathf.Atan2(ny, nx) *
                        Mathf.Rad2Deg;

                    // 左上の輪郭付近に弧を配置。
                    float angleDifference =
                        Mathf.DeltaAngle(
                            angleDegrees, 145f) *
                        Mathf.Deg2Rad;

                    // 半径方向・角度方向の両方を
                    // 滑らかに減衰させる。
                    float radialDistance =
                        (radius - 0.83f) / 0.028f;

                    float angularDistance =
                        angleDifference / 0.30f;

                    alpha = Mathf.Exp(
                        -0.5f *
                        (radialDistance * radialDistance +
                         angularDistance * angularDistance));
                }
                else
                {
                    // 柔らかい楕円状の光点。
                    // RectTransformの縦横比で楕円化する。
                    float radiusSquared =
                        nx * nx + ny * ny;

                    alpha = Mathf.Pow(
                        Mathf.Clamp01(1f - radiusSquared),
                        3f);
                }

                byte alphaByte = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(alpha) * 255f);

                pixels[y * textureSize + x] =
                    new Color32(
                        235, 250, 255, alphaByte);
            }
        }

        texture.SetPixels32(pixels);

        // GPUへ一度だけ転送し、
        // 以後CPUから読み取らない。
        texture.Apply(false, true);

        return Sprite.Create(
            texture,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private void OnDisable()
    {
        if (!_initialized)
        {
            return;
        }

        _arcGroup.alpha = 0f;
        _spotGroup.alpha = 0f;

        _arcImage.rectTransform.localRotation =
            _initialArcRotation;

        _spotImage.rectTransform.anchoredPosition =
            _initialSpotPosition;
    }

    private void OnDestroy()
    {
        // 実行時に生成したSpriteとTextureを解放。
        if (_arcSprite != null)
        {
            Texture2D texture = _arcSprite.texture;
            Destroy(_arcSprite);
            Destroy(texture);
        }

        if (_spotSprite != null)
        {
            Texture2D texture = _spotSprite.texture;
            Destroy(_spotSprite);
            Destroy(texture);
        }
    }
}
