using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    public GameObject fireBullet;

    [SerializeField] private KeyCode shootKey = KeyCode.X;
    [SerializeField, Min(0f)] private float shotCooldown = 0.25f;

    [Header("Sound")]
    [SerializeField] private AudioClip shootSound;

    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private PlayerPowerUp powerUp;
    private float nextShotTime;

    public bool CanShoot => powerUp != null && powerUp.IsPoweredUp;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        powerUp = GetComponent<PlayerPowerUp>();

        if (powerUp == null)
        {
            powerUp = gameObject.AddComponent<PlayerPowerUp>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (
            CanShoot &&
            Input.GetKeyDown(shootKey) &&
            Time.time >= nextShotTime
        )
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

        float direction = spriteRenderer.flipX ? -1f : 1f;
        Vector3 spawnPosition = transform.position +
            Vector3.right * direction * 0.65f;

        GameObject bullet = Instantiate(
            fireBullet,
            spawnPosition,
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
        bulletMovement.Speed =
            Mathf.Abs(bulletMovement.Speed) * direction;
        nextShotTime = Time.time + shotCooldown;

        if (shootSound != null)
        {
            audioSource.PlayOneShot(shootSound, 0.8f);
        }
    }
}
