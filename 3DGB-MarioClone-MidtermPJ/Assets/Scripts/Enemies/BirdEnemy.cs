using UnityEngine;

public class BirdEnemy : EnemyBase
{
    private enum BirdState
    {
        Patrol,
        Dive,
        Return
    }

    [Header("Air Patrol")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float movementRange = 4f;
    [SerializeField] private float bobHeight = 0.35f;
    [SerializeField] private float bobFrequency = 2f;

    [Header("Dive Attack")]
    [SerializeField, Min(0.5f)] private float detectionRangeX = 5f;
    [SerializeField, Min(0.5f)] private float diveSpeed = 6f;
    [SerializeField, Min(0.5f)] private float returnSpeed = 3.5f;
    [SerializeField, Min(0f)] private float attackCooldown = 1.1f;
    [SerializeField, Min(0.5f)] private float maximumDiveTime = 2.4f;
    [SerializeField] private float targetHeightOffset = 0.15f;

    private Vector2 initialPosition;
    private Vector2 diveTarget;
    private PlayerMovement player;
    private int moveDirection = -1;
    private BirdState state;
    private float stateTimer;
    private float nextAttackTime;

    protected override void Awake()
    {
        base.Awake();
        initialPosition = MyBody.position;
        player = FindObjectOfType<PlayerMovement>();
        state = BirdState.Patrol;
        UpdateDirectionVisual();
    }

    public void ConfigureAttack(
        float detectionRange,
        float attackSpeed,
        float recoverySpeed,
        float cooldown,
        float maximumAttackTime
    )
    {
        detectionRangeX = Mathf.Max(0.5f, detectionRange);
        diveSpeed = Mathf.Max(0.5f, attackSpeed);
        returnSpeed = Mathf.Max(0.5f, recoverySpeed);
        attackCooldown = Mathf.Max(0f, cooldown);
        maximumDiveTime = Mathf.Max(0.5f, maximumAttackTime);
    }

    private void FixedUpdate()
    {
        if (IsDefeated)
        {
            return;
        }

        if (player == null)
        {
            player = FindObjectOfType<PlayerMovement>();
        }

        switch (state)
        {
            case BirdState.Dive:
                UpdateDive();
                break;
            case BirdState.Return:
                UpdateReturn();
                break;
            default:
                UpdatePatrol();
                break;
        }
    }

    private void UpdatePatrol()
    {
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

        if (
            player != null &&
            Time.time >= nextAttackTime &&
            Mathf.Abs(player.transform.position.x - position.x) <=
                detectionRangeX
        )
        {
            BeginDive();
        }
    }

    private void BeginDive()
    {
        diveTarget = new Vector2(
            player.transform.position.x,
            player.transform.position.y + targetHeightOffset
        );
        state = BirdState.Dive;
        stateTimer = 0f;
    }

    private void UpdateDive()
    {
        stateTimer += Time.fixedDeltaTime;
        Vector2 current = MyBody.position;
        Vector2 next = Vector2.MoveTowards(
            current,
            diveTarget,
            diveSpeed * Time.fixedDeltaTime
        );

        UpdateDirectionVisual(next.x - current.x);
        MyBody.MovePosition(next);

        if (
            Vector2.Distance(next, diveTarget) <= 0.08f ||
            stateTimer >= maximumDiveTime
        )
        {
            BeginReturn();
        }
    }

    private void BeginReturn()
    {
        state = BirdState.Return;
        stateTimer = 0f;
    }

    private void UpdateReturn()
    {
        Vector2 current = MyBody.position;
        Vector2 next = Vector2.MoveTowards(
            current,
            initialPosition,
            returnSpeed * Time.fixedDeltaTime
        );

        UpdateDirectionVisual(next.x - current.x);
        MyBody.MovePosition(next);

        if (Vector2.Distance(next, initialPosition) > 0.05f)
        {
            return;
        }

        MyBody.position = initialPosition;
        state = BirdState.Patrol;
        nextAttackTime = Time.time + attackCooldown;
        UpdateDirectionVisual();
    }

    private void TurnAround()
    {
        moveDirection *= -1;
        UpdateDirectionVisual();
    }

    private void UpdateDirectionVisual()
    {
        EnemySprite.flipX = moveDirection < 0;
    }

    private void UpdateDirectionVisual(float horizontalVelocity)
    {
        if (Mathf.Abs(horizontalVelocity) <= 0.001f)
        {
            return;
        }

        EnemySprite.flipX = horizontalVelocity < 0f;
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        bool hitPlayer =
            collision.gameObject.GetComponentInParent<PlayerMovement>() != null;

        base.OnCollisionEnter2D(collision);

        if (hitPlayer && !IsDefeated && state == BirdState.Dive)
        {
            BeginReturn();
        }
    }
}
