using UnityEngine;

public class GroundPatrolEnemy : EnemyBase
{
    [Header("Ground Patrol")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float movementRange = 2.5f;

    private float initialX;
    private int moveDirection = -1;

    protected override void Awake()
    {
        base.Awake();
        initialX = transform.position.x;
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

        MyBody.velocity = new Vector2(
            moveDirection * moveSpeed,
            MyBody.velocity.y
        );
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        if (IsDefeated)
        {
            return;
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
