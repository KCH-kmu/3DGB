using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class FireFlowerPowerUp : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.85f;
    [SerializeField, Min(0f)] private float bobHeight = 0.08f;
    [SerializeField, Min(0f)] private float bobSpeed = 2.5f;

    private Vector3 startPosition;
    private Collider2D pickupCollider;
    private bool collected;

    private void Awake()
    {
        startPosition = transform.position;
        pickupCollider = GetComponent<Collider2D>();
        pickupCollider.isTrigger = true;
    }

    private void Update()
    {
        if (collected || bobHeight <= 0f)
        {
            return;
        }

        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPosition + Vector3.up * offset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
        {
            return;
        }

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();

        if (player == null)
        {
            return;
        }

        PlayerPowerUp powerUp = player.GetComponent<PlayerPowerUp>();

        if (powerUp == null)
        {
            powerUp = player.gameObject.AddComponent<PlayerPowerUp>();
        }

        collected = true;
        pickupCollider.enabled = false;
        powerUp.Activate();

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(
                pickupSound,
                transform.position,
                pickupVolume
            );
        }

        Destroy(gameObject);
    }
}
