using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LevelCheckpoint : MonoBehaviour
{
    [SerializeField] private Vector3 respawnPosition;
    [SerializeField] private Color activatedColor =
        new Color(1f, 0.82f, 0.2f, 1f);
    [SerializeField, Min(0.1f)] private float animationDuration = 0.6f;
    [SerializeField, Min(1f)] private float pulseScale = 1.18f;

    private bool activated;
    private SpriteRenderer spriteRenderer;
    private FallRespawnZone fallRespawnZone;
    private Vector3 normalScale;
    private Color normalColor;

    public void Configure(Vector3 position)
    {
        respawnPosition = position;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        fallRespawnZone = FindObjectOfType<FallRespawnZone>();
        normalScale = transform.localScale;

        if (spriteRenderer != null)
        {
            normalColor = spriteRenderer.color;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated)
        {
            return;
        }

        PlayerMovement player =
            other.GetComponentInParent<PlayerMovement>();

        if (player == null)
        {
            return;
        }

        if (fallRespawnZone == null)
        {
            fallRespawnZone = FindObjectOfType<FallRespawnZone>();
        }

        if (fallRespawnZone == null)
        {
            Debug.LogWarning(
                "Checkpoint could not find a FallRespawnZone."
            );
            return;
        }

        fallRespawnZone.SetRespawnPosition(respawnPosition);
        activated = true;

        if (spriteRenderer != null)
        {
            StartCoroutine(PlayActivationAnimation());
        }

        Debug.Log(
            $"Checkpoint activated at {respawnPosition}."
        );
    }

    private IEnumerator PlayActivationAnimation()
    {
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / animationDuration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            transform.localScale = normalScale * Mathf.Lerp(
                1f,
                pulseScale,
                pulse
            );
            spriteRenderer.color = Color.Lerp(
                normalColor,
                activatedColor,
                progress
            );
            yield return null;
        }

        transform.localScale = normalScale;
        spriteRenderer.color = activatedColor;
    }
}
