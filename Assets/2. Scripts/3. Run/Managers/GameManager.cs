using UnityEngine;

public sealed class GameManager
{
    private readonly RunScope _scope;

    public GameManager(RunScope scope) => _scope = scope;

    public void StartRun()
    {
        Debug.Log("[GameManager] StartRun");

        PlayerEntity player;

        Vector3 spawnPos = Vector3.zero; 

        if (GameRoot.Instance != null && GameRoot.Instance.PlayerPrefab != null)
        {
            player = Object.Instantiate(GameRoot.Instance.PlayerPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.position = spawnPos;
            player = go.AddComponent<PlayerEntity>();
        }

        if (GameRoot.Instance != null)
        {
            var col = player.GetComponentInChildren<Collider>(true);

            GroundSnap.TrySnapToGround(
                player.transform,
                col,
                GameRoot.Instance.GroundMask,
                GameRoot.Instance.GroundRayHeight,
                GameRoot.Instance.GroundExtraOffset
            );
        }

        player.Construct(_scope);
        _scope.Entities.RegisterPlayer(player);

        _scope.Spawner.Begin();
    }


}
