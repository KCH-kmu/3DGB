using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinPickup : MonoBehaviour
{
    [SerializeField] private AudioClip pickupSound;

    public void Configure(AudioClip sound)
    {
        pickupSound = sound;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(
                pickupSound,
                transform.position,
                0.75f
            );
        }

        GameHUD hud = FindObjectOfType<GameHUD>();

        if (hud != null)
        {
            hud.AddCoin();
        }

        Destroy(gameObject);
    }
}
