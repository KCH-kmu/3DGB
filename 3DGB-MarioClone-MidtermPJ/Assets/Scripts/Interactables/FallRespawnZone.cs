using UnityEngine;

public class FallRespawnZone : MonoBehaviour
{
    [SerializeField] private Vector3 respawnPosition =
        new Vector3(-7f, -2.5f, 0f);

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player =
            other.GetComponentInParent<PlayerMovement>();

        if (player == null)
        {
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();

        if (health != null)
        {
            health.TakeDamage();
        }

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            playerBody.velocity = Vector2.zero;
        }

        player.transform.position = respawnPosition;
    }
}
