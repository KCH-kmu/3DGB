using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class BossEnemy : MonoBehaviour
{
    private enum BossState
    {
        Patrol,
        Windup,
        Charge,
        Recovery,
        Defeated
    }

    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 10;
    [SerializeField, Min(0)] private int scoreValue = 5000;

    [Header("Arena")]
    [SerializeField] private float patrolLeftX = 96f;
    [SerializeField] private float patrolRightX = 104f;
    [SerializeField, Min(0.1f)] private float patrolSpeed = 1.5f;

    [Header("Charge Attack")]
    [SerializeField, Min(0.1f)] private float detectionRange = 9f;
    [SerializeField, Min(0.1f)] private float chargeSpeed = 6.5f;
    [SerializeField, Min(0f)] private float windupDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float chargeDuration = 1.4f;
    [SerializeField, Min(0f)] private float recoveryDuration = 0.55f;

    [Header("Player Interaction")]
    [SerializeField, Min(0.1f)] private float stompBounceForce = 9f;
    [SerializeField, Min(0f)] private float hitCooldown = 1f;

    private Rigidbody2D myBody;
    private SpriteRenderer bossSprite;
    private Collider2D bossCollider;
    private Transform player;
    private GameHUD hud;
    private BossState state = BossState.Patrol;
    private int currentHealth;
    private int moveDirection = -1;
    private float stateTimer;
    private float nextAllowedHitTime;
    private Coroutine hitFlashRoutine;
    private Coroutine playerPassThroughRoutine;
    private Collider2D ignoredPlayerCollider;
    private bool encounterStarted;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    
    public bool IsTemporarilyInvulnerable =>
        !IsDefeated && Time.time < nextAllowedHitTime;
public bool IsDefeated => state == BossState.Defeated;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        bossSprite = GetComponent<SpriteRenderer>();
        bossCollider = GetComponent<Collider2D>();
        currentHealth = maxHealth;
    }

    private void Start()
    {
        PlayerMovement playerMovement = FindObjectOfType<PlayerMovement>();

        if (playerMovement != null)
        {
            player = playerMovement.transform;
        }

        hud = FindObjectOfType<GameHUD>();
        FaceDirection(moveDirection);
    }

private void FixedUpdate()
    {
        if (state == BossState.Defeated)
        {
            return;
        }

        stateTimer -= Time.fixedDeltaTime;

        switch (state)
        {
            case BossState.Patrol:
                UpdatePatrol();
                break;

            case BossState.Windup:
                myBody.velocity = new Vector2(0f, myBody.velocity.y);

                if (player != null)
                {
                    FaceDirection(
                        player.position.x >= transform.position.x ? 1 : -1
                    );
                }

                if (stateTimer <= 0f)
                {
                    BeginCharge();
                }
                break;

            case BossState.Charge:
                UpdateCharge();
                break;

            case BossState.Recovery:
                myBody.velocity = new Vector2(0f, myBody.velocity.y);

                if (stateTimer <= 0f)
                {
                    state = BossState.Patrol;
                }
                break;
        }
    }

    public void Configure(
        float leftX,
        float rightX,
        int health = 10
    )
    {
        patrolLeftX = Mathf.Min(leftX, rightX);
        patrolRightX = Mathf.Max(leftX, rightX);
        maxHealth = Mathf.Max(1, health);
        currentHealth = maxHealth;
    }

    private void UpdatePatrol()
    {
        float halfWidth = bossCollider.bounds.extents.x;
        float minimumX = patrolLeftX + halfWidth;
        float maximumX = patrolRightX - halfWidth;

        if (transform.position.x <= minimumX)
        {
            moveDirection = 1;
        }
        else if (transform.position.x >= maximumX)
        {
            moveDirection = -1;
        }

        myBody.velocity = new Vector2(
            moveDirection * patrolSpeed,
            myBody.velocity.y
        );
        FaceDirection(moveDirection);

        if (
            player != null &&
            Mathf.Abs(player.position.x - transform.position.x) <=
                detectionRange
        )
        {
            if (!encounterStarted)
            {
                encounterStarted = true;
                UpdateHealthDisplay();
            }

            state = BossState.Windup;
            stateTimer = windupDuration;
            myBody.velocity = new Vector2(0f, myBody.velocity.y);
            FaceDirection(player.position.x >= transform.position.x ? 1 : -1);
        }
    }

    private void BeginCharge()
    {
        moveDirection =
            player != null && player.position.x >= transform.position.x
                ? 1
                : -1;
        FaceDirection(moveDirection);
        state = BossState.Charge;
        stateTimer = chargeDuration;
    }

private void UpdateCharge()
    {
        float halfWidth = bossCollider.bounds.extents.x;
        float minimumX = patrolLeftX + halfWidth;
        float maximumX = patrolRightX - halfWidth;
        bool reachedBoundary =
            (moveDirection < 0 && transform.position.x <= minimumX + 0.02f) ||
            (moveDirection > 0 && transform.position.x >= maximumX - 0.02f);

        if (reachedBoundary)
        {
            float clampedX = Mathf.Clamp(
                transform.position.x,
                minimumX,
                maximumX
            );
            myBody.position = new Vector2(clampedX, myBody.position.y);
            BeginRecovery();
            return;
        }

        myBody.velocity = new Vector2(
            moveDirection * chargeSpeed,
            myBody.velocity.y
        );
        FaceDirection(moveDirection);

        if (stateTimer <= 0f)
        {
            BeginRecovery();
        }
    }

    private void BeginRecovery()
    {
        state = BossState.Recovery;
        stateTimer = recoveryDuration;
        myBody.velocity = new Vector2(0f, myBody.velocity.y);
    }

    private void FaceDirection(int direction)
    {
        // Boss_001 원본 이미지는 오른쪽을 바라본다.
        bossSprite.flipX = direction < 0;
    }

private void OnCollisionEnter2D(Collision2D collision)
    {
        HandlePlayerCollision(collision);
    }

private void OnCollisionStay2D(Collision2D collision)
    {
        HandlePlayerCollision(collision);
    }

private void HandlePlayerCollision(Collision2D collision)
    {
        if (IsDefeated)
        {
            return;
        }

        PlayerMovement playerMovement =
            collision.gameObject.GetComponentInParent<PlayerMovement>();

        if (playerMovement == null)
        {
            return;
        }

        Collider2D playerCollider = FindSolidPlayerCollider(playerMovement);
        Rigidbody2D playerBody = playerMovement.GetComponent<Rigidbody2D>();
        bool movingDownward =
            playerBody == null || playerBody.velocity.y <= 0.1f;
        bool playerIsOnTop =
            playerCollider != null &&
            playerCollider.bounds.center.y > bossCollider.bounds.center.y &&
            playerCollider.bounds.min.y >=
                bossCollider.bounds.max.y - 0.35f;

        // While invulnerable the stomp collider is temporarily ignored.
        // This guard prevents damage if a queued contact callback still runs.
        if (IsTemporarilyInvulnerable)
        {
            return;
        }

        if (movingDownward && playerIsOnTop)
        {
            ReceiveHit(playerCollider);
            playerMovement.Bounce(stompBounceForce);
            return;
        }

        PlayerHealth playerHealth =
            playerMovement.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage();
        }
    }



    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<FireBullet>() != null)
        {
            ReceiveHit();
        }
    }

public void ReceiveHit(
        Collider2D stompingPlayerCollider = null
    )
    {
        if (IsDefeated || IsTemporarilyInvulnerable)
        {
            return;
        }

        nextAllowedHitTime = Time.time + hitCooldown;
        currentHealth = Mathf.Max(0, currentHealth - 1);
        UpdateHealthDisplay();

        if (currentHealth <= 0)
        {
            Defeat();
            return;
        }

        if (stompingPlayerCollider != null)
        {
            BeginPlayerPassThrough(stompingPlayerCollider);
        }

        BeginRecovery();
        stateTimer = Mathf.Max(recoveryDuration, hitCooldown);

        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
        }

        hitFlashRoutine = StartCoroutine(FlashAfterHit());
    }

private IEnumerator FlashAfterHit()
    {
        Color originalColor = bossSprite.color;
        Color fadedColor = new Color(
            originalColor.r,
            originalColor.g,
            originalColor.b,
            0.28f
        );
        const float blinkInterval = 0.09f;

        while (!IsDefeated && Time.time < nextAllowedHitTime)
        {
            bossSprite.color = fadedColor;
            yield return new WaitForSeconds(blinkInterval);

            if (IsDefeated || Time.time >= nextAllowedHitTime)
            {
                break;
            }

            bossSprite.color = originalColor;
            yield return new WaitForSeconds(blinkInterval);
        }

        if (!IsDefeated)
        {
            bossSprite.color = originalColor;
        }

        hitFlashRoutine = null;
    }

private void Defeat()
    {
        if (IsDefeated)
        {
            return;
        }

        state = BossState.Defeated;
        RestoreIgnoredPlayerCollision();
        myBody.velocity = Vector2.zero;
        myBody.simulated = false;
        bossCollider.enabled = false;

        if (hud == null)
        {
            hud = FindObjectOfType<GameHUD>();
        }

        if (hud != null)
        {
            hud.AddScore(scoreValue);
            hud.ShowBossVictory();
        }

        PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.CompleteLevel();
        }

        StartCoroutine(PlayDefeatEffect());
    }

    private IEnumerator PlayDefeatEffect()
    {
        Vector3 initialScale = transform.localScale;
        Color initialColor = bossSprite.color;
        float elapsed = 0f;
        const float duration = 0.8f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(
                initialScale,
                initialScale * 0.15f,
                progress
            );
            bossSprite.color = new Color(
                initialColor.r,
                initialColor.g,
                initialColor.b,
                1f - progress
            );
            yield return null;
        }

        Destroy(gameObject);
    }

    private void UpdateHealthDisplay()
    {
        if (hud == null)
        {
            hud = FindObjectOfType<GameHUD>();
        }

        if (hud != null)
        {
            hud.ShowBossHealth(currentHealth, maxHealth);
        }
    }

    private static Collider2D FindSolidPlayerCollider(
        PlayerMovement playerMovement
    )
    {
        Collider2D[] colliders =
            playerMovement.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider in colliders)
        {
            if (collider.enabled && !collider.isTrigger)
            {
                return collider;
            }
        }

        return null;
    }


private void OnDisable()
    {
        RestoreIgnoredPlayerCollision();
    }


private void RestoreIgnoredPlayerCollision()
    {
        if (playerPassThroughRoutine != null)
        {
            StopCoroutine(playerPassThroughRoutine);
            playerPassThroughRoutine = null;
        }

        if (
            ignoredPlayerCollider != null &&
            bossCollider != null
        )
        {
            Physics2D.IgnoreCollision(
                ignoredPlayerCollider,
                bossCollider,
                false
            );
        }

        ignoredPlayerCollider = null;
    }


private IEnumerator RestorePlayerCollisionAfterInvulnerability(
        Collider2D playerCollider
    )
    {
        while (!IsDefeated && Time.time < nextAllowedHitTime)
        {
            yield return null;
        }

        while (
            !IsDefeated &&
            playerCollider != null &&
            bossCollider != null &&
            playerCollider.enabled &&
            bossCollider.enabled &&
            playerCollider.bounds.Intersects(bossCollider.bounds)
        )
        {
            yield return null;
        }

        if (
            ignoredPlayerCollider == playerCollider &&
            playerCollider != null &&
            bossCollider != null
        )
        {
            Physics2D.IgnoreCollision(
                playerCollider,
                bossCollider,
                false
            );
            ignoredPlayerCollider = null;
        }

        playerPassThroughRoutine = null;
    }


private void BeginPlayerPassThrough(
        Collider2D playerCollider
    )
    {
        if (playerCollider == null || bossCollider == null)
        {
            return;
        }

        RestoreIgnoredPlayerCollision();
        ignoredPlayerCollider = playerCollider;
        Physics2D.IgnoreCollision(
            ignoredPlayerCollider,
            bossCollider,
            true
        );
        playerPassThroughRoutine = StartCoroutine(
            RestorePlayerCollisionAfterInvulnerability(playerCollider)
        );
    }
}
