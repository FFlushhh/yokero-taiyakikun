using System.Collections.Generic;
using UnityEngine;

namespace Yokero.Collision
{
    public static class PlayerPathCollision2D
    {
        /// <summary>
        /// 現在のCollider形状を移動方向へCastし、
        /// 経路上にEnemyタグのColliderがあるか判定する
        ///
        /// movement: 現在位置から移動先までの変位
        /// results: 呼び出し元で再利用する結果リスト
        /// </summary>
        public static bool HitsEnemy(
            Collider2D playerCollider,
            Vector2 movement,
            List<RaycastHit2D> results)
        {
            if (playerCollider == null || results == null)
            {
                Debug.LogError(
                    "PlayerPathCollision2D: 必要な参照がありません。");
                return false;
            }

            if (!playerCollider.enabled ||
                !playerCollider.gameObject.activeInHierarchy)
                return false;

            if (movement.sqrMagnitude <= 0f)
                return false;

            // Transformの変更をPhysics2Dに反映する。
            // 現在の敵はUpdate内でTransformを直接移動させている
            Physics2D.SyncTransforms();

            ContactFilter2D filter = ContactFilter2D.noFilter;
            filter.useTriggers = true;

            results.Clear();

            int count = playerCollider.Cast(
                movement.normalized,
                filter,
                results,
                movement.magnitude
            );

            // 最初のヒットが敵以外でも、
            // その先に敵が存在する可能性があるため全件確認する
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = results[i].collider;

                if (hit != null && hit.CompareTag("Enemy"))
                    return true;
            }

            return false;
        }
    }
}