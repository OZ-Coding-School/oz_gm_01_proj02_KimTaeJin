using System.Collections.Generic;
using UnityEngine;

public sealed class PoolService
{
    private readonly Dictionary<int, Queue<GameObject>> _pool = new();
    private readonly Dictionary<int, int> _goToKey = new(); 

    public T Spawn<T>(T prefab, Vector3 pos, Quaternion rot) where T : Component
    {
        int key = prefab.gameObject.GetInstanceID();

        GameObject go;
        if (_pool.TryGetValue(key, out var q) && q.Count > 0)
        {
            go = q.Dequeue();
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
        }
        else
        {
            go = Object.Instantiate(prefab.gameObject, pos, rot);
        }

        _goToKey[go.GetInstanceID()] = key;

        return go.GetComponent<T>();
    }

    public void Despawn(GameObject go)
    {
        if (go == null) return;

        int id = go.GetInstanceID();
        if (!_goToKey.TryGetValue(id, out int key))
        {
            go.SetActive(false);
            return;
        }

        if (!_pool.TryGetValue(key, out var q))
        {
            q = new Queue<GameObject>();
            _pool.Add(key, q);
        }

        go.SetActive(false);
        q.Enqueue(go);
    }

    public void Despawn(GameObject go, GameObject prefab)
    {
        if (go == null || prefab == null) return;

        int key = prefab.GetInstanceID();
        _goToKey[go.GetInstanceID()] = key;

        if (!_pool.TryGetValue(key, out var q))
        {
            q = new Queue<GameObject>();
            _pool.Add(key, q);
        }

        go.SetActive(false);
        q.Enqueue(go);
    }
}
