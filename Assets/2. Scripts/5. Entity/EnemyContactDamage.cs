using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyContactDamage : MonoBehaviour
{
    [SerializeField] private int damage = 10;
    [SerializeField] private float attacksPerSecond = 1f;
    [SerializeField] private string playerTag = "Player";

    private float _cooldown;

    private void Update()
    {
        if (_cooldown > 0f)
            _cooldown -= Time.deltaTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryDamage(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider other)
    {
        if (other == null) return;
        if (!IsPlayer(other)) return;
        if (_cooldown > 0f) return;

        var hp = other.GetComponentInParent<HealthComponent>();
        if (hp == null) return;

        hp.ApplyDamage(Mathf.Max(0, damage));
        GameAudio.Instance?.PlayPlayerHit();
        _cooldown = attacksPerSecond > 0f ? (1f / attacksPerSecond) : 0.5f;
    }

    private bool IsPlayer(Collider other)
    {
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) return true;
        return other.GetComponentInParent<PlayerEntity>() != null;
    }
}
