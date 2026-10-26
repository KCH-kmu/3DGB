using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Common Collision")]
    [SerializeField] private bool canBeStomped = true;
    [SerializeField] private float bounceForce = 8f;
    [SerializeField, Min(1)] private int bulletHitsToDefeat = 1;
    [SerializeField, Min(0)] private int scoreValue = 200;

    protected Rigidbody2D MyBody { get; private set; }
    protected SpriteRenderer EnemySprite { get; private set; }
    protected Animator EnemyAnimator { get; private set; }
    protected Collider2D EnemyCollider { get; private set; }
    protected bool IsDefeated { get; private set; }
    public bool CanBeStomped => canBeStomped;

    private int bulletHitCount;

    private const float StompHeightTolerance = 0.15f;

protected virtual void Awake()
    {
        MyBody = GetComponent<Rigidbody2D>();

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        EnemySprite = rootRenderer;

        if (rootRenderer == null || !rootRenderer.enabled)
        {
            SpriteRenderer[] renderers =
                GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != rootRenderer && renderer.enabled)
                {
                    EnemySprite = renderer;
                    break;
                }
            }
        }

        EnemyAnimator = GetComponent<Animator>();
        EnemyCollider = GetComponent<Collider2D>();
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsDefeated)
        {
            return;
        }

        PlayerMovement player =
            collision.gameObject.GetComponentInParent<PlayerMovement>();

        if (player == null)
        {
            return;
        }

        Collider2D playerCollider = FindSolidPlayerCollider(player);

        bool playerIsAbove =
            playerCollider != null &&
            playerCollider.bounds.center.y > EnemyCollider.bounds.center.y &&
            playerCollider.bounds.min.y >=
                EnemyCollider.bounds.center.y - StompHeightTolerance;

        if (canBeStomped && playerIsAbove)
        {
            Defeat();
            player.Bounce(bounceForce);
            return;
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage();
        }
    }

    public void ConfigureStomp(bool allowed)
    {
        canBeStomped = allowed;
    }

    private static Collider2D FindSolidPlayerCollider(
        PlayerMovement player
    )
    {
        Collider2D[] colliders =
            player.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider in colliders)
        {
            if (collider.enabled && !collider.isTrigger)
            {
                return collider;
            }
        }

        return null;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Bullet"))
        {
            ReceiveBulletHit();
        }
    }

    public virtual void ReceiveBulletHit()
    {
        if (IsDefeated)
        {
            return;
        }

        bulletHitCount++;

        if (bulletHitCount >= bulletHitsToDefeat)
        {
            Defeat();
        }
    }

    protected virtual void Defeat()
    {
        if (IsDefeated)
        {
            return;
        }

        IsDefeated = true;
        MyBody.velocity = Vector2.zero;
        EnemyCollider.enabled = false;

        GameHUD hud = FindObjectOfType<GameHUD>();

        if (hud != null)
        {
            hud.AddScore(scoreValue);
        }

        Destroy(gameObject, 0.05f);
    }
}
