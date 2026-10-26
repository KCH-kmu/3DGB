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
    [SerializeField, Min(0.1f)] private float detectionRange = 7f;
    [SerializeField, Min(0.1f)] private float chargeSpeed = 5f;
    [SerializeField, Min(0f)] private float windupDuration = 0.5f;
    [SerializeField, Min(0.1f)] private float chargeDuration = 1.1f;
    [SerializeField, Min(0f)] private float recoveryDuration = 0.7f;

    [Header("Player Interaction")]
    [SerializeField, Min(0.1f)] private float stompBounceForce = 9f;
    [SerializeField, Min(0f)] private float hitCooldown = 0.15f;

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
    private bool encounterStarted;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
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
        float clampedX = Mathf.Clamp(
            transform.position.x,
            minimumX,
            maximumX
        );

        if (!Mathf.Approximately(clampedX, transform.position.x))
        {
            myBody.position = new Vector2(clampedX, myBody.position.y);
            BeginRecovery();
            return;
        }

        myBody.velocity = new Vector2(
            moveDirection * chargeSpeed,
            myBody.velocity.y
        );

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
        bool landedOnTop =
            playerCollider != null &&
            playerCollider.bounds.min.y >= bossCollider.bounds.max.y - 0.35f;

        if (movingDownward && landedOnTop)
        {
            ReceiveHit();
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

    public void ReceiveHit()
    {
        if (IsDefeated || Time.time < nextAllowedHitTime)
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

        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
        }

        hitFlashRoutine = StartCoroutine(FlashAfterHit());
    }

    private IEnumerator FlashAfterHit()
    {
        Color originalColor = bossSprite.color;
        bossSprite.color = new Color(1f, 0.35f, 0.35f, 1f);
        yield return new WaitForSeconds(0.1f);

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
}
