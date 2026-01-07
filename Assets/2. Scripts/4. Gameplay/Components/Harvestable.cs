using UnityEngine;

public sealed class Harvestable : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 3;

    [Header("Drop")]
    [SerializeField] private DropItem dropPrefab;
    [SerializeField] private int dropCount = 3;
    [SerializeField] private float dropScatterRadius = 0.6f;

    private int _hp;
    private JellyPunch _jelly;

    private void Awake()
    {
        _hp = maxHp;
        _jelly = GetComponent<JellyPunch>();
    }

    public void TakeHit(int damage, Vector3 from)
    {
        if (_hp <= 0) return;

        _jelly?.Play();

        _hp -= Mathf.Max(1, damage);
        if (_hp <= 0)
            Die();
    }
    private void Die()
    {
        if (dropPrefab != null)
        {
            for (int i = 0; i < dropCount; i++)
            {
                Vector2 r = Random.insideUnitCircle * dropScatterRadius;
                Vector3 pos = transform.position + new Vector3(r.x, 0f, r.y);
                Instantiate(dropPrefab, pos, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }
}
