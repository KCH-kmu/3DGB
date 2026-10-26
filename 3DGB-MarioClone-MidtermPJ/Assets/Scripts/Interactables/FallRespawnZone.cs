using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FallRespawnZone : MonoBehaviour
{
    private const float RespawnTriggerClearance = 1f;
    private const int RespawnGuardFixedUpdates = 3;

    [SerializeField] private Vector3 respawnPosition =
        new Vector3(-7f, -2.5f, 0f);

    private Collider2D fallZoneCollider;
    private PlayerMovement respawningPlayer;
    private int safeFixedUpdatesRemaining;

    private void Awake()
    {
        fallZoneCollider = GetComponent<Collider2D>();
    }

    public void SetRespawnPosition(Vector3 position)
    {
        respawnPosition = position;
    }

    public void RespawnPlayer(PlayerMovement player)
    {
        if (player == null)
        {
            return;
        }

        respawningPlayer = player;
        safeFixedUpdatesRemaining = RespawnGuardFixedUpdates;

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.position = respawnPosition;
            Physics2D.SyncTransforms();
            return;
        }

        player.transform.position = respawnPosition;
        Physics2D.SyncTransforms();
    }

    private void FixedUpdate()
    {
        if (respawningPlayer == null)
        {
            return;
        }

        if (
            fallZoneCollider == null ||
            respawningPlayer.transform.position.y <=
                fallZoneCollider.bounds.max.y + RespawnTriggerClearance
        )
        {
            safeFixedUpdatesRemaining = RespawnGuardFixedUpdates;
            return;
        }

        safeFixedUpdatesRemaining--;

        if (safeFixedUpdatesRemaining <= 0)
        {
            respawningPlayer = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player =
            other.GetComponentInParent<PlayerMovement>();

        if (player == null)
        {
            return;
        }

        if (respawningPlayer != null)
        {
            return;
        }

        if (
            fallZoneCollider != null &&
            player.transform.position.y >
                fallZoneCollider.bounds.max.y + RespawnTriggerClearance
        )
        {
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();

        if (health != null)
        {
            health.TakeFallDamage();
            return;
        }

        RespawnPlayer(player);
    }
}
