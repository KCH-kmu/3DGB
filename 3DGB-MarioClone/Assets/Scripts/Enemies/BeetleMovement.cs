using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class BeetleMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Player Collision")]
    [SerializeField] private float bounceForce = 8f;
    [SerializeField] private float disappearDelay = 1f;

    private Rigidbody2D myBody;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private int moveDirection = -1;
    private bool isFrozen;
    private float nextTurnTime;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        UpdateDirectionVisual();
    }

    private void FixedUpdate()
    {
        if (isFrozen || animator.GetBool("IsFrozen"))
        {
            myBody.velocity = new Vector2(0f, myBody.velocity.y);
            return;
        }

        myBody.velocity = new Vector2(
            moveDirection * moveSpeed,
            myBody.velocity.y
        );
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isFrozen)
        {
            return;
        }

        PlayerMovement player =
            collision.gameObject.GetComponentInParent<PlayerMovement>();

        // PDF의 아래쪽 충돌 영역에 플레이어가 닿은 경우이다.
        if (player != null)
        {
            HandlePlayerDamage(player);
            return;
        }

        // 벽이나 다른 Rigidbody의 측면에 부딪히면 이동 방향을 바꾼다.
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.x) > 0.5f)
            {
                TurnAround();
                break;
            }
        }
    }

    public void TryFreezeFromHead(
        PlayerMovement player,
        Collider2D playerCollider,
        Collider2D headZoneCollider
    )
    {
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

        if (
            isFrozen ||
            playerBody == null ||
            playerCollider == null ||
            headZoneCollider == null
        )
        {
            return;
        }

        // 걷는 중의 Y 속도 0을 낙하로 판단하지 않는다.
        bool playerIsFalling = playerBody.velocity.y < -0.1f;

        // 플레이어의 실제 발이 머리 판정 영역 위에 있을 때만 밟기로 처리한다.
        bool playerFeetAreAboveHead =
            playerCollider.bounds.min.y >=
            headZoneCollider.bounds.center.y;

        if (!playerIsFalling || !playerFeetAreAboveHead)
        {
            return;
        }

        Freeze();
        player.Bounce(bounceForce);
    }

    public void HandlePlayerDamage(PlayerMovement player)
    {
        if (isFrozen)
        {
            return;
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage();
        }

        TurnAwayFromPlayer(player.transform);
    }

    public void Freeze()
    {
        if (isFrozen)
        {
            return;
        }

        isFrozen = true;
        animator.SetBool("IsFrozen", true);
        myBody.velocity = new Vector2(0f, myBody.velocity.y);

        StartCoroutine(DisappearAfterDelay());
    }

    private IEnumerator DisappearAfterDelay()
    {
        yield return new WaitForSeconds(disappearDelay);
        Destroy(gameObject);
    }

    private void TurnAwayFromPlayer(Transform playerTransform)
    {
        moveDirection =
            playerTransform.position.x < transform.position.x ? 1 : -1;

        UpdateDirectionVisual();
        myBody.velocity = new Vector2(
            moveDirection * moveSpeed,
            myBody.velocity.y
        );
    }

    public void TurnAround()
    {
        // 한 ReturnCollision에 여러 Collider가 동시에 닿아 두 번 뒤집히는 것을 방지한다.
        if (Time.time < nextTurnTime || isFrozen)
        {
            return;
        }

        nextTurnTime = Time.time + 0.1f;
        moveDirection *= -1;
        UpdateDirectionVisual();
    }

    private void UpdateDirectionVisual()
    {
        // 원본 비틀 스프라이트는 왼쪽을 바라본다.
        spriteRenderer.flipX = moveDirection > 0;
    }

}
