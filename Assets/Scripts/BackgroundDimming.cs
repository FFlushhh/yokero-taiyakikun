using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲームの経過時間に合わせて背景の明るさを変更する
/// 30秒から暗転を開始し、45秒で初期の1/3の明るさにする
///
/// 既存の背景スクロール・プレイヤー・敵・UIには影響しない
/// </summary>
public sealed class BackgroundDimming : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager _gameManager;

    [Tooltip("暗転対象となる背景Prefabのルートオブジェクト")]
    [SerializeField] private Transform[] _backgroundRoots = new Transform[3];

    [Header("Dimming Settings")]
    [SerializeField, Min(0f)]
    private float _dimmingStartTime = 30f;

    [SerializeField, Min(0f)]
    private float _dimmingEndTime = 45f;

    [Tooltip("暗転完了後の明るさ。1=通常、0=真っ黒")]
    [SerializeField, Range(0f, 1f)]
    private float _finalBrightness = 1f / 2f;

    // 背景マテリアルで使用されているColorプロパティ
    private static readonly int ColorId =
        Shader.PropertyToID("_BaseColor");

    // 暗転対象となるRendererと、その元の色を保持する
    private readonly List<MeshRenderer> _renderers =
        new List<MeshRenderer>();

    private readonly List<Color> _originalColors =
        new List<Color>();

    // Awakeで生成し、毎フレーム再利用する。
    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        // UnityのネイティブオブジェクトはAwake内で生成する。
        _propertyBlock = new MaterialPropertyBlock();

        // GameManagerと同じGameObjectに配置することを想定
        if (_gameManager == null)
        {
            _gameManager = GetComponent<GameManager>();
        }

        // GameManagerと同じGameObjectに配置することを想定
        if (_gameManager == null)
        {
            _gameManager = GetComponent<GameManager>();
        }

        if (_gameManager == null)
        {
            Debug.LogError(
                "BackgroundDimming: GameManagerが設定されていません。",
                this);

            enabled = false;
            return;
        }

        if (_dimmingEndTime <= _dimmingStartTime)
        {
            Debug.LogError(
                "BackgroundDimming: 終了時間は開始時間より後にしてください。",
                this);

            enabled = false;
            return;
        }

        // 各背景Prefabのルートと子オブジェクトを取得する
        foreach (Transform root in _backgroundRoots)
        {
            if (root == null)
            {
                continue;
            }

            MeshRenderer[] meshRenderers =
                root.GetComponentsInChildren<MeshRenderer>(true);

            Debug.Log(
                $"背景: {root.name}, " +
                $"MeshRenderer数: {meshRenderers.Length}",
                root);

            foreach (MeshRenderer renderer in meshRenderers)
            {
                Material material = renderer.sharedMaterial;

                Debug.Log(
                    $"Renderer: {renderer.name}, " +
                    $"Shader: {(material != null ? material.shader.name : "なし")}, " +
                    $"_Color: {(material != null && material.HasProperty("_Color"))}",
                    renderer);
            }

            foreach (MeshRenderer renderer in meshRenderers)
            {
                Material material = renderer.sharedMaterial;

                if (material == null ||
                    !material.HasProperty(ColorId))
                {
                    Debug.LogWarning(
                        $"BackgroundDimming: {renderer.name}の" +
                        "マテリアルに_Colorがありません。",
                        renderer);

                    continue;
                }

                _renderers.Add(renderer);

                // 暗転前の色を保存する
                _originalColors.Add(
                    material.GetColor(ColorId));
            }
        }

        if (_renderers.Count == 0)
        {
            Debug.LogError(
                "BackgroundDimming: 暗転対象のRendererが見つかりません。",
                this);

            enabled = false;
            return;
        }

        // ゲーム開始前は必ず通常の明るさにする
        ApplyBrightness(1f);
    }

    private void Update()
    {
        // ゲーム開始前・ゲームオーバー後は変更しない
        if (!_gameManager.GameStarted ||
            _gameManager.GameOver)
        {
            return;
        }

        float elapsed = _gameManager.ElapsedGameTime;

        // 30秒以前は0、45秒以降は1になる
        float progress = Mathf.InverseLerp(
            _dimmingStartTime,
            _dimmingEndTime,
            elapsed);

        // 明るさを1.0から1/3へ線形補間する
        float brightness = Mathf.Lerp(
            1f,
            _finalBrightness,
            progress);

        ApplyBrightness(brightness);
    }

    /// <summary>
    /// 背景のRGB値に明るさの倍率を適用する。
    /// マテリアルアセット自体は変更しない。
    /// </summary>
    private void ApplyBrightness(float brightness)
    {
        // 初期化が完了していなければ何もしない。
        if (_propertyBlock == null)
        {
            return;
        }

        for (int i = 0; i < _renderers.Count; i++)
        {
            MeshRenderer renderer = _renderers[i];

            if (renderer == null)
            {
                continue;
            }

            // 初期状態の色から明るさを計算する。
            Color color = _originalColors[i];

            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;

            // 透明度は変更しない。
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void OnDisable()
    {
        // コンポーネントを無効化した場合は元の明るさに戻す
        ApplyBrightness(1f);
    }
}