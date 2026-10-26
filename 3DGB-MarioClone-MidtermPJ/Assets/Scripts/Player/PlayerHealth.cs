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
    private bool levelCompleted;
    private bool sceneLoadRequested;

    private Rigidbody2D myBody;
    private AudioSource audioSource;
    private Animator playerAnimator;
    private PlayerPowerUp playerPowerUp;
    private Collider2D[] playerColliders;
    private MonoBehaviour[] playerBehaviours;
    private Coroutine damageCooldownRoutine;
    private FallRespawnZone fallRespawnZone;
    private PlayerMovement playerMovement;
    private Vector3 startPosition;

    public int CurrentLives => currentLives;
    public bool HasEnded => hasEnded;
    public bool LevelCompleted => levelCompleted;
    public event Action<int> LivesChanged;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        playerAnimator = GetComponent<Animator>();
        playerPowerUp = GetComponent<PlayerPowerUp>();
        playerMovement = GetComponent<PlayerMovement>();
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
        startPosition = transform.position;
        fallRespawnZone = FindObjectOfType<FallRespawnZone>();
    }

    private void Update()
    {
        if (
            levelCompleted &&
            (
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter)
            )
        )
        {
            LoadNextLevel();
            return;
        }

        if (hasEnded && !levelCompleted && Input.GetKeyDown(KeyCode.R))
        {
            RestartCurrentScene();
        }
    }

    private void OnGUI()
    {
        if (!levelCompleted || sceneLoadRequested)
        {
            return;
        }

        Event currentEvent = Event.current;

        if (
            currentEvent.type == EventType.KeyDown &&
            (
                currentEvent.keyCode == KeyCode.Return ||
                currentEvent.keyCode == KeyCode.KeypadEnter
            )
        )
        {
            currentEvent.Use();
            LoadNextLevel();
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

        if (playerPowerUp == null)
        {
            playerPowerUp = GetComponent<PlayerPowerUp>();
        }

        if (playerPowerUp != null && playerPowerUp.TryConsume())
        {
            PlayHitSound();
            Debug.Log("Player hit. Fire Flower power-up lost.");
            BeginDamageCooldown();
            return;
        }

        LoseLife("Player hit.");
    }

    public void TakeFallDamage()
    {
        if (hasEnded || currentLives <= 0)
        {
            return;
        }

        if (playerPowerUp == null)
        {
            playerPowerUp = GetComponent<PlayerPowerUp>();
        }

        bool lostPowerUp =
            playerPowerUp != null && playerPowerUp.TryConsume();

        LoseLife(
            lostPowerUp
                ? "Player fell. Fire Flower power-up lost."
                : "Player fell."
        );
    }

    private void LoseLife(string logMessage)
    {
        currentLives--;
        LivesChanged?.Invoke(currentLives);

        PlayHitSound();

        Debug.Log($"{logMessage} Lives remaining: {currentLives}");

        if (currentLives <= 0)
        {
            Die();
            return;
        }

        RespawnAfterLifeLoss();
        BeginDamageCooldown();
    }

    private void RespawnAfterLifeLoss()
    {
        if (fallRespawnZone == null)
        {
            fallRespawnZone = FindObjectOfType<FallRespawnZone>();
        }

        if (fallRespawnZone != null && playerMovement != null)
        {
            fallRespawnZone.RespawnPlayer(playerMovement);
            return;
        }

        if (myBody != null)
        {
            myBody.velocity = Vector2.zero;
            myBody.angularVelocity = 0f;
            myBody.position = startPosition;
            Physics2D.SyncTransforms();
            return;
        }

        transform.position = startPosition;
    }

    public void TimeExpired()
    {
        if (hasEnded)
        {
            return;
        }

        currentLives = 0;
        LivesChanged?.Invoke(currentLives);
        Die();
    }

    private void PlayHitSound()
    {
        if (hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }

    private void BeginDamageCooldown()
    {
        if (damageCooldownRoutine != null)
        {
            StopCoroutine(damageCooldownRoutine);
        }

        damageCooldownRoutine = StartCoroutine(DamageCooldown());
    }

    private IEnumerator DamageCooldown()
    {
        canTakeDamage = false;
        yield return new WaitForSeconds(invulnerabilityTime);

        if (!hasEnded)
        {
            canTakeDamage = true;
        }

        damageCooldownRoutine = null;
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
        levelCompleted = true;
        LockPlayerInteractions();

        Debug.Log("Level completed. Press Enter to load the next level.");
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

    private void LoadNextLevel()
    {
        if (sceneLoadRequested)
        {
            return;
        }

        Time.timeScale = 1f;

        Scene activeScene = SceneManager.GetActiveScene();
        int sceneCount = SceneManager.sceneCountInBuildSettings;

        if (activeScene.buildIndex < 0 || sceneCount <= 0)
        {
            Debug.LogWarning(
                "The next level could not be loaded because the active " +
                "scene is not included in Build Settings."
            );
            return;
        }

        sceneLoadRequested = true;
        int nextSceneIndex = (activeScene.buildIndex + 1) % sceneCount;
        SceneManager.LoadScene(nextSceneIndex);
    }
}
