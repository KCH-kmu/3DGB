using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class FireBullet : MonoBehaviour
{
    public float Speed = 8f;

    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float explosionDuration = 0.25f;

    private Rigidbody2D myBody;
    private Animator animator;
    private bool hasExploded;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // 화면 밖으로 나간 총알이 계속 남아 있지 않도록 제거한다.
        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        if (hasExploded)
        {
            myBody.linearVelocity = Vector2.zero;
            return;
        }

        myBody.linearVelocity = new Vector2(Speed, 0f);
    }

    private void OnTriggerEnter2D(Collider2D target)
    {
        if (hasExploded)
        {
            return;
        }

        // 직선으로 날아가는 파이어볼은 벽, 블록, 지형에 닿으면 즉시 제거한다.
        if (target.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Destroy(gameObject);
            return;
        }

        // Beetle은 자식 충돌 영역을 가지고 있으므로 부모에서도 찾는다.
        bool hitBeetle =
            target.GetComponentInParent<BeetleMovement>() != null;

        bool hitSnail =
            target.GetComponentInParent<SnailMovement>() != null;

        bool hitAdditionalEnemy =
            target.GetComponentInParent<EnemyBase>() != null;

        bool hitBoss =
            target.GetComponentInParent<BossEnemy>() != null;

        if (!hitBeetle && !hitSnail && !hitAdditionalEnemy && !hitBoss)
        {
            return;
        }

        Explode();
    }

    private void Explode()
    {
        hasExploded = true;
        myBody.linearVelocity = Vector2.zero;
        animator.Play("FireBulletExplode");
        Destroy(gameObject, explosionDuration);
    }
}
