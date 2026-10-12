using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    public GameObject fireBullet;

    [SerializeField] private KeyCode shootKey = KeyCode.X;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(shootKey))
        {
            ShootBullet();
        }
    }

    private void ShootBullet()
    {
        if (fireBullet == null)
        {
            Debug.LogWarning("PlayerShoot에 FireBullet 프리팹이 연결되지 않았습니다.");
            return;
        }

        GameObject bullet = Instantiate(
            fireBullet,
            transform.position,
            Quaternion.identity
        );

        FireBullet bulletMovement = bullet.GetComponent<FireBullet>();

        if (bulletMovement == null)
        {
            Debug.LogWarning("FireBullet 프리팹에 FireBullet 스크립트가 없습니다.");
            Destroy(bullet);
            return;
        }

        // 현재 PlayerMovement는 localScale 대신 SpriteRenderer.flipX를 사용한다.
        float direction = spriteRenderer.flipX ? -1f : 1f;
        bulletMovement.Speed =
            Mathf.Abs(bulletMovement.Speed) * direction;
    }
}
