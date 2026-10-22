using UnityEngine;

/// <summary>
/// 横に並んだ2枚のQuadを無限スクロールさせる。
///
/// ループ時にマテリアルを入れ替えることで、
/// 異なる2枚の画像でも表示の連続性を維持する。
///
/// 前提条件:
/// - 2枚のQuadは同じ幅
/// - 2枚目は1枚目の右隣に配置
/// - 画面のワールド幅がタイル幅以下
/// </summary>
public sealed class SeamlessBackgroundScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    [SerializeField, Min(0f)]
    private float _scrollSpeed = 0.5f;

    [SerializeField, Min(0.01f)]
    private float _tileWidth = 18f;

    [Header("Tile Renderers")]
    [SerializeField] private MeshRenderer _firstTile;

    [SerializeField] private MeshRenderer _secondTile;

    private Vector3 _initialPosition;
    private float _startTime;

    private Material _materialA;
    private Material _materialB;

    private int _previousLoopIndex = -1;

    private void Start()
    {
        if (_firstTile == null ||
            _secondTile == null ||
            _firstTile == _secondTile)
        {
            Debug.LogError(
                "SeamlessBackgroundScroller: " +
                "2つの異なるMeshRendererを設定してください。",
                this);

            enabled = false;
            return;
        }

        // 元のマテリアルを保持する。
        _materialA = _firstTile.sharedMaterial;
        _materialB = _secondTile.sharedMaterial;

        if (_materialA == null || _materialB == null)
        {
            Debug.LogError(
                "SeamlessBackgroundScroller: " +
                "背景マテリアルが設定されていません。",
                this);

            enabled = false;
            return;
        }

        // Quadの間隔が設定値と一致しているか確認する。
        float actualDistance =
            _secondTile.bounds.center.x -
            _firstTile.bounds.center.x;

        if (Mathf.Abs(actualDistance - _tileWidth) > 0.01f)
        {
            Debug.LogWarning(
                "SeamlessBackgroundScroller: " +
                $"Quad間隔({actualDistance})と" +
                $"Tile Width({_tileWidth})が一致していません。",
                this);
        }

        // 画面幅がタイル幅を超えると、
        // マテリアルの切り替えが見える可能性がある。
        Camera camera = Camera.main;

        if (camera != null && camera.orthographic)
        {
            float cameraWidth =
                2f * camera.orthographicSize * camera.aspect;

            if (cameraWidth > _tileWidth)
            {
                Debug.LogWarning(
                    "SeamlessBackgroundScroller: " +
                    $"カメラ幅({cameraWidth})が" +
                    $"タイル幅({_tileWidth})を超えています。",
                    this);
            }
        }

        _initialPosition = transform.position;
        _startTime = Time.time;
    }

    private void Update()
    {
        if (_scrollSpeed <= 0f)
        {
            return;
        }

        float elapsed = Time.time - _startTime;

        // スクロールした累計距離。
        float totalDistance = elapsed * _scrollSpeed;

        // 何回ループしたかを計算する。
        int loopIndex = Mathf.FloorToInt(
            totalDistance / _tileWidth);

        // 1タイル分の範囲内で移動する。
        float offset = Mathf.Repeat(
            totalDistance,
            _tileWidth);

        transform.position =
            _initialPosition + Vector3.left * offset;

        // ループが切り替わったときだけマテリアルを更新。
        if (loopIndex != _previousLoopIndex)
        {
            bool swapped = (loopIndex % 2) != 0;

            _firstTile.sharedMaterial =
                swapped ? _materialB : _materialA;

            _secondTile.sharedMaterial =
                swapped ? _materialA : _materialB;

            _previousLoopIndex = loopIndex;
        }
    }
}