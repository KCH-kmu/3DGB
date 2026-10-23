using UnityEngine;

public class BirdEnemy : EnemyBase
{
    [Header("Air Patrol")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float movementRange = 4f;
    [SerializeField] private float bobHeight = 0.35f;
    [SerializeField] private float bobFrequency = 2f;

    private Vector2 initialPosition;
    private int moveDirection = -1;

    protected override void Awake()
    {
        base.Awake();
        initialPosition = MyBody.position;
        UpdateDirectionVisual();
    }

    private void FixedUpdate()
    {
        if (IsDefeated)
        {
            return;
        }

        Vector2 position = MyBody.position;
        position.x += moveDirection * moveSpeed * Time.fixedDeltaTime;

        if (position.x <= initialPosition.x - movementRange)
        {
            position.x = initialPosition.x - movementRange;
            TurnAround();
        }
        else if (position.x >= initialPosition.x + movementRange)
        {
            position.x = initialPosition.x + movementRange;
            TurnAround();
        }

        position.y = initialPosition.y +
            Mathf.Sin(Time.time * bobFrequency) * bobHeight;

        MyBody.MovePosition(position);
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
