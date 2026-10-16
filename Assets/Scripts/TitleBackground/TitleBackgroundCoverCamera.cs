using UnityEngine;

/// <summary>
/// 背景を正射影カメラの視野全体に収める（端は必要に応じてトリミング）
/// 画面比率が変わっても黒帯を出さないことを優先する
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class TitleBackgroundCoverCamera : MonoBehaviour
{
    [SerializeField] private Camera _targetCamera;

    private SpriteRenderer _spriteRenderer;
    private int _lastPixelWidth = -1;
    private int _lastPixelHeight = -1;
    private float _lastOrthographicSize = -1f;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_targetCamera == null)
        {
            _targetCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (_targetCamera == null || !_targetCamera.orthographic
            || _spriteRenderer.sprite == null)
        {
            return;
        }

        int width = _targetCamera.pixelWidth;
        int height = _targetCamera.pixelHeight;
        float size = _targetCamera.orthographicSize;
        if (width <= 0 || height <= 0 || size <= 0f)
        {
            return;
        }

        if (width == _lastPixelWidth && height == _lastPixelHeight
            && Mathf.Approximately(size, _lastOrthographicSize))
        {
            return;
        }

        // Cover: 余白を作らず、縦横のうち足りない方向に合わせて拡大する
        Vector2 spriteSize = _spriteRenderer.sprite.bounds.size;
        float cameraHeight = 2f * size;
        float cameraWidth = cameraHeight * width / height;
        float scale = Mathf.Max(
            cameraWidth / spriteSize.x, cameraHeight / spriteSize.y);
        transform.localScale = new Vector3(scale, scale, 1f);

        _lastPixelWidth = width;
        _lastPixelHeight = height;
        _lastOrthographicSize = size;
    }
}
