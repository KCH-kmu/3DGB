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
            GameObject soundObject = new GameObject("CoinPickupSound");
            AudioSource source = soundObject.AddComponent<AudioSource>();
            source.clip = pickupSound;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.75f;
            source.Play();
            Destroy(soundObject, pickupSound.length + 0.1f);
        }

        GameHUD hud = FindObjectOfType<GameHUD>();

        if (hud != null)
        {
            hud.AddCoin();
        }

        Destroy(gameObject);
    }
}
