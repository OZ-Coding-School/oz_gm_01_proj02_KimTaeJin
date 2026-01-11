using UnityEngine;

public sealed class BuildModeController : MonoBehaviour
{
    private RunScope _scope;

    private WorldScroller _worldScroller;
    private HouseDrift _houseDrift;

    private PlayerController _playerController;
    private PlayerMeleeAutoAttack _melee;
    private PlayerHarvestAutoAttack _harvest;
    private EndlessChunks _chunks;
    private Rigidbody _playerRb;


    public void Construct(RunScope scope)
    {
        _scope = scope;

        _worldScroller = FindObjectOfType<WorldScroller>();
        _houseDrift = FindObjectOfType<HouseDrift>();

        _playerController = FindObjectOfType<PlayerController>();
        _melee = FindObjectOfType<PlayerMeleeAutoAttack>();
        _harvest = FindObjectOfType<PlayerHarvestAutoAttack>();
        _chunks = FindObjectOfType<EndlessChunks>();

        if (_playerController != null)
            _playerRb = _playerController.GetComponent<Rigidbody>();

        if (_scope?.Events != null)
            _scope.Events.BuildModeChanged += OnBuildModeChanged;
    }

    private void OnDestroy()
    {
        if (_scope?.Events != null)
            _scope.Events.BuildModeChanged -= OnBuildModeChanged;
    }

    private void OnBuildModeChanged(bool on)
    {

        if (_worldScroller != null) _worldScroller.enabled = !on;
        if (_houseDrift != null) _houseDrift.enabled = !on;

        if (_scope != null && _scope.Spawner != null)
            _scope.Spawner.enabled = !on;

        if (_playerController != null) _playerController.enabled = !on;
        if (_melee != null) _melee.enabled = !on;
        if (_harvest != null) _harvest.enabled = !on;
        if (_chunks != null) _chunks.enabled = !on;

        if (on && _playerRb != null)
        {
            _playerRb.velocity = Vector3.zero;
            _playerRb.angularVelocity = Vector3.zero;
        }

        var enemies = _scope?.Entities?.Enemies;
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null) continue;

                var brain = e.GetComponent<EnemyBrain>();
                if (brain != null) brain.enabled = !on;

                var rb = e.GetComponent<Rigidbody>();
                if (rb != null) rb.velocity = Vector3.zero;
            }
        }
    }
}
