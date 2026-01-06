using System.Collections.Generic;
using UnityEngine;

public sealed class PoolService
{
    private readonly Dictionary<int, Queue<GameObject>> _pool = new();

    public T Spawn<T>(T prefab, Vector3 pos, Quaternion rot) where T : Component
    {
        int key = prefab.gameObject.GetInstanceID();

        if (_pool.TryGetValue(key, out var q) && q.Count > 0)
        {
            var go = q.Dequeue();
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
            return go.GetComponent<T>();
        }

        return Object.Instantiate(prefab, pos, rot);
    }

    public void Despawn(GameObject go, GameObject prefab)
    {
        if (go == null || prefab == null) return;

        int key = prefab.GetInstanceID();
        if (!_pool.TryGetValue(key, out var q))
        {
            q = new Queue<GameObject>();
            _pool.Add(key, q);
        }

        go.SetActive(false);
        q.Enqueue(go);
    }
}
