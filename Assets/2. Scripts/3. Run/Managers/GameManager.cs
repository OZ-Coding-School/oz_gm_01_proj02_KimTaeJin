using UnityEngine;

public sealed class GameManager
{
    private readonly RunScope _scope;

    public GameManager(RunScope scope) => _scope = scope;

    public void StartRun()
    {
        Debug.Log("[GameManager] StartRun");

        PlayerEntity player;

        //없을때 더미 관련
        if (GameRoot.Instance != null && GameRoot.Instance.PlayerPrefab != null)
        {
            player = Object.Instantiate(GameRoot.Instance.PlayerPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.position = Vector3.zero;
            player = go.AddComponent<PlayerEntity>();
        }
        if (GameRoot.Instance != null)
        {
            var col = player.GetComponent<Collider>();
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
