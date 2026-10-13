using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Lives")]
    [SerializeField, Min(1)] private int startingLives = 3;
    [SerializeField, Min(0f)] private float invulnerabilityTime = 1f;

    [Header("Sound")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip gameOverSound;

    private int currentLives;
    private bool canTakeDamage = true;

    private PlayerMovement playerMovement;
    private Rigidbody2D myBody;
    private AudioSource audioSource;

    public int CurrentLives => currentLives;
    public event Action<int> LivesChanged;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        myBody = GetComponent<Rigidbody2D>();

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        currentLives = startingLives;
    }

    private void Start()
    {
        LivesChanged?.Invoke(currentLives);
    }

    public void TakeDamage()
    {
        if (!canTakeDamage || currentLives <= 0)
        {
            return;
        }

        currentLives--;
        LivesChanged?.Invoke(currentLives);

        if (hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        Debug.Log($"Player hit. Lives remaining: {currentLives}");

        if (currentLives <= 0)
        {
            Die();
            return;
        }

        StartCoroutine(DamageCooldown());
    }

    private IEnumerator DamageCooldown()
    {
        canTakeDamage = false;
        yield return new WaitForSeconds(invulnerabilityTime);
        canTakeDamage = true;
    }

    private void Die()
    {
        canTakeDamage = false;

        if (myBody != null)
        {
            myBody.velocity = Vector2.zero;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        StartCoroutine(PlayGameOverSoundAfterHit());

        Debug.Log("Player has no lives remaining. Game Over.");
    }

    private IEnumerator PlayGameOverSoundAfterHit()
    {
        if (hitSound != null)
        {
            yield return new WaitForSecondsRealtime(hitSound.length);
        }

        if (gameOverSound != null)
        {
            audioSource.PlayOneShot(gameOverSound);
        }
    }
}
