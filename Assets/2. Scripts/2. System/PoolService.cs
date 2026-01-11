using System.Collections.Generic;
using UnityEngine;

public sealed class PoolService
{
    private readonly Dictionary<int, Queue<GameObject>> _pool = new();
    private readonly Dictionary<int, int> _goToKey = new();
    private readonly Transform _root;

    public PoolService(Transform root)
    {
        _root = root;
    }

    public T Spawn<T>(T prefab, Vector3 pos, Quaternion rot) where T : Component
    {
        int key = prefab.gameObject.GetInstanceID();

        GameObject go = null;

        if (_pool.TryGetValue(key, out var q))
        {
            while (q.Count > 0 && go == null)
                go = q.Dequeue();
        }

        if (go != null)
        {
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
        }
        else
        {
            go = Object.Instantiate(prefab.gameObject, pos, rot, _root);
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
            go.transform.SetParent(_root, false);
            return;
        }

        if (!_pool.TryGetValue(key, out var q))
        {
            q = new Queue<GameObject>();
            _pool.Add(key, q);
        }

        go.SetActive(false);
        go.transform.SetParent(_root, false);
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
        go.transform.SetParent(_root, false);
        q.Enqueue(go);
    }

    public void Clear()
    {
        foreach (var kv in _pool)
        {
            var q = kv.Value;
            while (q.Count > 0)
            {
                var go = q.Dequeue();
                if (go != null) Object.Destroy(go);
            }
        }

        _pool.Clear();
        _goToKey.Clear();
    }
}
