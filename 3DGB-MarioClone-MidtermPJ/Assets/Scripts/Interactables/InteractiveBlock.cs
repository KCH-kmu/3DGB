using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class InteractiveBlock : MonoBehaviour
{
    private enum BlockKind
    {
        Breakable,
        Question
    }

    private const string HitSensorName = "Underside_HitSensor";

    [SerializeField] private BlockKind blockKind;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private AudioClip coinSound;
    [SerializeField, Min(0.1f)] private float coinRiseDistance = 0.9f;
    [SerializeField, Min(0.1f)] private float coinAnimationDuration = 0.7f;
    [SerializeField] private bool used;

    private BoxCollider2D blockCollider;
    private SpriteRenderer blockRenderer;
    private bool resolved;

    public bool IsQuestionBlock => blockKind == BlockKind.Question;
    public bool IsUsed => used;

    public void ConfigureQuestion(Sprite popupCoin, AudioClip sound)
    {
        blockKind = BlockKind.Question;
        coinSprite = popupCoin;
        coinSound = sound;
        used = false;
        resolved = false;
    }

    public void ConfigureBreakable()
    {
        blockKind = BlockKind.Breakable;
        coinSprite = null;
        coinSound = null;
        used = false;
        resolved = false;
    }

    private void Awake()
    {
        blockCollider = GetComponent<BoxCollider2D>();
        blockRenderer = GetComponent<SpriteRenderer>();
        EnsureUndersideSensor();
    }

public bool TryHitFromBelow(PlayerMovement player)
    {
        if (player == null || resolved || blockCollider == null)
        {
            return false;
        }

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

        if (playerBody == null)
        {
            return false;
        }

        Collider2D playerCollider = FindSolidPlayerCollider(player);

        if (playerCollider == null)
        {
            return false;
        }

        Bounds blockBounds = blockCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        const float contactTolerance = 0.035f;
        const float minimumHorizontalOverlap = 0.02f;

        float undersideGap = blockBounds.min.y - playerBounds.max.y;
        float horizontalOverlap = Mathf.Min(
            blockBounds.max.x,
            playerBounds.max.x
        ) - Mathf.Max(
            blockBounds.min.x,
            playerBounds.min.x
        );
        bool hasPhysicalContact =
            blockCollider.IsTouching(playerCollider);

        if (
            playerBounds.center.y >= blockBounds.center.y ||
            undersideGap < -contactTolerance ||
            (!hasPhysicalContact && undersideGap > contactTolerance) ||
            horizontalOverlap <= minimumHorizontalOverlap
        )
        {
            return false;
        }

        if (blockKind == BlockKind.Question)
        {
            if (used)
            {
                return false;
            }

            used = true;
            AwardCoin();
            StartCoroutine(AnimateBlockBump());
            return true;
        }

        PlayerPowerUp powerUp = player.GetComponent<PlayerPowerUp>();

        if (powerUp == null || !powerUp.IsPoweredUp)
        {
            return false;
        }

        resolved = true;

        float downwardSpeed = Mathf.Max(
            2f,
            Mathf.Abs(playerBody.linearVelocity.y) * 0.3f
        );
        playerBody.linearVelocity = new Vector2(
            playerBody.linearVelocity.x,
            -downwardSpeed
        );

        StartCoroutine(DestroyAfterCollisionResponse());
        return true;
    }

private void EnsureUndersideSensor()
    {
        Transform sensorTransform = transform.Find(HitSensorName);
        GameObject sensorObject;

        if (sensorTransform == null)
        {
            sensorObject = new GameObject(HitSensorName);
            sensorObject.transform.SetParent(transform, false);
        }
        else
        {
            sensorObject = sensorTransform.gameObject;
        }

        sensorObject.layer = LayerMask.NameToLayer("Default");

        BoxCollider2D sensorCollider =
            sensorObject.GetComponent<BoxCollider2D>();

        if (sensorCollider == null)
        {
            sensorCollider = sensorObject.AddComponent<BoxCollider2D>();
        }

        sensorCollider.isTrigger = true;
        sensorCollider.size = new Vector2(
            blockCollider.size.x * 1.05f,
            0.55f
        );

        float blockBottom =
            blockCollider.offset.y - blockCollider.size.y * 0.5f;
        sensorCollider.offset = new Vector2(
            blockCollider.offset.x,
            blockBottom - sensorCollider.size.y * 0.45f
        );

        BlockUndersideSensor sensor =
            sensorObject.GetComponent<BlockUndersideSensor>();

        if (sensor == null)
        {
            sensor = sensorObject.AddComponent<BlockUndersideSensor>();
        }

        sensor.Configure(this);
    }

    private void AwardCoin()
    {
        GameHUD hud = FindObjectOfType<GameHUD>();

        if (hud != null)
        {
            hud.AddCoin();
        }

        PlayCoinSound();

        if (blockRenderer != null)
        {
            blockRenderer.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        if (coinSprite != null)
        {
            StartCoroutine(AnimateCoinPopup());
        }
    }

    private void PlayCoinSound()
    {
        if (coinSound == null)
        {
            return;
        }

        GameObject soundObject = new GameObject("QuestionBlockCoinSound");
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = coinSound;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0.75f;
        source.Play();
        Destroy(soundObject, coinSound.length + 0.1f);
    }

    private IEnumerator AnimateCoinPopup()
    {
        GameObject popup = new GameObject("QuestionBlock_CoinPopup");
        SpriteRenderer popupRenderer =
            popup.AddComponent<SpriteRenderer>();
        popupRenderer.sprite = coinSprite;
        popupRenderer.sortingLayerID = blockRenderer.sortingLayerID;
        popupRenderer.sortingOrder = blockRenderer.sortingOrder + 3;

        const float coinScale = 0.55f;
        popup.transform.localScale =
            new Vector3(coinScale, coinScale, 1f);

        Vector3 startPosition = new Vector3(
            blockRenderer.bounds.center.x,
            blockRenderer.bounds.max.y +
                coinSprite.bounds.extents.y * coinScale +
                0.05f,
            transform.position.z
        );
        popup.transform.position = startPosition;

        float elapsed = 0f;

        while (elapsed < coinAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(
                elapsed / coinAnimationDuration
            );
            float easedRise = 1f - Mathf.Pow(1f - progress, 2f);
            popup.transform.position =
                startPosition + Vector3.up * coinRiseDistance * easedRise;

            Color color = popupRenderer.color;
            color.a = 1f - progress;
            popupRenderer.color = color;
            yield return null;
        }

        Destroy(popup);
    }

    private IEnumerator AnimateBlockBump()
    {
        Vector3 startPosition = transform.position;
        const float bumpHeight = 0.12f;
        const float duration = 0.16f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float bump = Mathf.Sin(progress * Mathf.PI) * bumpHeight;
            transform.position = startPosition + Vector3.up * bump;
            yield return null;
        }

        transform.position = startPosition;
    }

private IEnumerator DestroyAfterCollisionResponse()
    {
        yield return new WaitForFixedUpdate();
        Destroy(gameObject);
    }


    private static Collider2D FindSolidPlayerCollider(
        PlayerMovement player
    )
    {
        foreach (
            Collider2D collider in
            player.GetComponentsInChildren<Collider2D>(true)
        )
        {
            if (collider.enabled && !collider.isTrigger)
            {
                return collider;
            }
        }

        return null;
    }
}
