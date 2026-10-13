using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private bool hasEnded;

    private Rigidbody2D myBody;
    private AudioSource audioSource;
    private Animator playerAnimator;
    private Collider2D[] playerColliders;
    private MonoBehaviour[] playerBehaviours;

    public int CurrentLives => currentLives;
    public event Action<int> LivesChanged;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        playerAnimator = GetComponent<Animator>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        playerBehaviours = GetComponents<MonoBehaviour>();

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        currentLives = startingLives;
    }

    private void Update()
    {
        if (hasEnded && Input.GetKeyDown(KeyCode.R))
        {
            RestartCurrentScene();
        }
    }

    private void Start()
    {
        LivesChanged?.Invoke(currentLives);
    }

    public void TakeDamage()
    {
        if (hasEnded || !canTakeDamage || currentLives <= 0)
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
        if (hasEnded)
        {
            return;
        }

        canTakeDamage = false;
        hasEnded = true;
        LockPlayerInteractions();

        StartCoroutine(PlayGameOverSoundAfterHit());

        Debug.Log("Player has no lives remaining. Game Over.");
    }

    public void CompleteLevel()
    {
        if (hasEnded)
        {
            return;
        }

        canTakeDamage = false;
        hasEnded = true;
        LockPlayerInteractions();
    }

    private void LockPlayerInteractions()
    {
        foreach (MonoBehaviour behaviour in playerBehaviours)
        {
            if (behaviour != null && behaviour != this)
            {
                behaviour.enabled = false;
            }
        }

        if (myBody != null)
        {
            myBody.velocity = Vector2.zero;
            myBody.angularVelocity = 0f;
            myBody.simulated = false;
        }

        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider != null)
            {
                playerCollider.enabled = false;
            }
        }

        if (playerAnimator != null)
        {
            playerAnimator.speed = 0f;
        }

        foreach (FireBullet bullet in FindObjectsOfType<FireBullet>())
        {
            Destroy(bullet.gameObject);
        }
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

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;

        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }
}
