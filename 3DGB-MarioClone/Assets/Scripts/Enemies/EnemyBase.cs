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

    protected Rigidbody2D MyBody { get; private set; }
    protected SpriteRenderer EnemySprite { get; private set; }
    protected Animator EnemyAnimator { get; private set; }
    protected Collider2D EnemyCollider { get; private set; }
    protected bool IsDefeated { get; private set; }

    private int bulletHitCount;

    protected virtual void Awake()
    {
        MyBody = GetComponent<Rigidbody2D>();
        EnemySprite = GetComponent<SpriteRenderer>();
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

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        Collider2D playerCollider = player.GetComponent<Collider2D>();

        bool playerIsFalling =
            playerBody != null && playerBody.velocity.y <= 0f;

        bool playerIsAbove =
            playerCollider != null &&
            playerCollider.bounds.min.y >= EnemyCollider.bounds.center.y;

        if (canBeStomped && playerIsFalling && playerIsAbove)
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
        Destroy(gameObject, 0.05f);
    }
}
