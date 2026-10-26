using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class SpikeHazard : MonoBehaviour
{
    private void Reset()
    {
        Collider2D spikeCollider = GetComponent<Collider2D>();

        if (spikeCollider != null)
        {
            spikeCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        DamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        DamagePlayer(other);
    }

    private static void DamagePlayer(Collider2D other)
    {
        PlayerHealth playerHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage();
        }
    }
}
