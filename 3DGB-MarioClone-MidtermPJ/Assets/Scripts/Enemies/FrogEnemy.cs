using UnityEngine;

public class FrogEnemy : EnemyBase
{
    [Header("Jump Movement")]
    [SerializeField] private float horizontalSpeed = 1.6f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float jumpInterval = 1.5f;
    [SerializeField] private float movementRange = 3f;

    private float initialX;
    private float nextJumpTime;
    private int moveDirection = -1;

    protected override void Awake()
    {
        base.Awake();
        initialX = transform.position.x;
        nextJumpTime = Time.time + jumpInterval;
        UpdateDirectionVisual();
    }

    private void FixedUpdate()
    {
        if (IsDefeated)
        {
            return;
        }

        if (
            transform.position.x <= initialX - movementRange &&
            moveDirection < 0
        )
        {
            TurnAround();
        }
        else if (
            transform.position.x >= initialX + movementRange &&
            moveDirection > 0
        )
        {
            TurnAround();
        }

        bool isNearlyGrounded = Mathf.Abs(MyBody.linearVelocity.y) < 0.05f;

        if (isNearlyGrounded && Time.time >= nextJumpTime)
        {
            MyBody.linearVelocity = new Vector2(
                moveDirection * horizontalSpeed,
                jumpForce
            );

            if (EnemyAnimator != null)
            {
                EnemyAnimator.Play("FrogJump");
            }

            nextJumpTime = Time.time + jumpInterval;
        }
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            if (EnemyAnimator != null)
            {
                EnemyAnimator.Play("FrogIdle");
            }
        }

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.x) > 0.5f)
            {
                TurnAround();
                break;
            }
        }
    }

    private void TurnAround()
    {
        moveDirection *= -1;
        UpdateDirectionVisual();
    }

    private void UpdateDirectionVisual()
    {
        EnemySprite.flipX = moveDirection > 0;
    }
}
