using UnityEngine;
using System.Collections;

public class Done_BGScroller : MonoBehaviour
{
    public float scrollSpeed;
    public float tileWidth;
    public EnemyController enemyController;
    private Vector3 startPosition;
    // private bool _canScroll = false; // スクロール開始フラグ

    void Start ()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // // ここでスクロールを抑止（StartScroll() が呼ばれるまでスクロールしない）
        // if (!_canScroll) return;

        float newPosition = Mathf.Repeat(Time.time * scrollSpeed, tileWidth);
        transform.position = startPosition + Vector3.left * newPosition;
    }

    // public void StartScroll()
    // {
    //     _canScroll = true;
    // }
}
