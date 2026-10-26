using UnityEngine;

public class GroundPatrolEnemy : EnemyBase
{
    [Header("Ground Patrol")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float movementRange = 2.5f;

    private float initialX;
    
    private bool sourceFacesLeft;
    private bool directionIndependentVisual;
private int moveDirection = -1;

protected override void Awake()
    {
        base.Awake();
        initialX = transform.position.x;

        string spriteName =
            EnemySprite != null && EnemySprite.sprite != null
                ? EnemySprite.sprite.name
                : string.Empty;

        sourceFacesLeft = spriteName == "Enemies_025";
        directionIndependentVisual =
            spriteName == "Enemies_052" ||
            spriteName == "Enemies_056";

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

        MyBody.linearVelocity = new Vector2(
            moveDirection * moveSpeed,
            MyBody.linearVelocity.y
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
        if (EnemySprite == null)
        {
            return;
        }

        if (directionIndependentVisual)
        {
            EnemySprite.flipX = false;
            return;
        }

        bool movingRight = moveDirection > 0;
        EnemySprite.flipX = sourceFacesLeft
            ? movingRight
            : !movingRight;
    }
}
