using UnityEngine;

public class SnailMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float bounceForce = 8f;

    private Rigidbody2D myBody;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Collider2D myCollider;

    private int moveDirection = -1;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        myCollider = GetComponent<Collider2D>();
    }

    private void FixedUpdate()
    {
        // 기절 상태에서는 수평 이동을 멈춤
        if (animator.GetBool("IsStunned"))
        {
            myBody.velocity = new Vector2(
                0f,
                myBody.velocity.y
            );

            return;
        }

        myBody.velocity = new Vector2(
            moveDirection * moveSpeed,
            myBody.velocity.y
        );
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 기절한 상태에서는 충돌 동작을 실행하지 않음
        if (animator.GetBool("IsStunned"))
        {
            return;
        }

        // 플레이어와 충돌한 경우
        if (collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerCollision(collision);
            return;
        }

        // 벽이나 다른 물체의 측면에 부딪힌 경우
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.x) > 0.5f)
            {
                ChangeDirection();
                break;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D target)
    {
        if (!target.CompareTag("Bullet"))
        {
            return;
        }

        // PDF의 동작처럼 첫 피격은 기절, 기절 중의 두 번째 피격은 제거한다.
        if (animator.GetBool("IsStunned"))
        {
            Destroy(gameObject);
            return;
        }

        Stun();
    }

    private void HandlePlayerCollision(Collision2D collision)
    {
        PlayerMovement player =
            collision.gameObject.GetComponent<PlayerMovement>();

        Rigidbody2D playerBody = collision.rigidbody;

        if (player == null || playerBody == null)
        {
            return;
        }

        // 플레이어의 발이 달팽이 중심보다 높은지 확인
        bool playerIsAbove =
            collision.collider.bounds.min.y >=
            myCollider.bounds.center.y;

        // 플레이어가 아래로 내려오는 중인지 확인
        bool playerIsFalling =
            playerBody.velocity.y <= 0f;

        if (playerIsAbove && playerIsFalling)
        {
            // 위에서 밟았으면 달팽이 기절
            Stun();

            // 플레이어를 위로 튕김
            player.Bounce(bounceForce);
        }
        else
        {
            PlayerHealth playerHealth =
                player.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage();
            }

            // 플레이어 반대쪽으로 달팽이 방향 전환
            TurnAwayFromPlayer(collision.transform);
        }
    }

    public void Stun()
    {
        animator.SetBool("IsStunned", true);

        myBody.velocity = new Vector2(
            0f,
            myBody.velocity.y
        );
    }

    private void TurnAwayFromPlayer(Transform playerTransform)
    {
        if (playerTransform.position.x < transform.position.x)
        {
            // 플레이어가 왼쪽에 있으면 오른쪽으로 이동
            moveDirection = 1;
        }
        else
        {
            // 플레이어가 오른쪽에 있으면 왼쪽으로 이동
            moveDirection = -1;
        }

        UpdateDirectionVisual();

        // 충돌한 순간 곧바로 새로운 방향 적용
        myBody.velocity = new Vector2(
            moveDirection * moveSpeed,
            myBody.velocity.y
        );
    }

    private void ChangeDirection()
    {
        moveDirection *= -1;
        UpdateDirectionVisual();
    }

    private void UpdateDirectionVisual()
    {
        // 원본 달팽이는 왼쪽을 보고 있음
        spriteRenderer.flipX = moveDirection > 0;
    }
}
