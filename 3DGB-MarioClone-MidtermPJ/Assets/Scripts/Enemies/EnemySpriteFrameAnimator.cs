using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class EnemySpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 6f;
    [SerializeField] private bool alignToBottom;
    [SerializeField] private bool[] mirrorFrameHorizontally;

    private SpriteRenderer spriteRenderer;
    private Vector3 baseLocalScale;
    private int frameIndex;
    private float nextFrameTime;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        frameIndex = 0;
        ApplyFrame(frameIndex);
        ScheduleNextFrame();
    }

    private void Update()
    {
        if (frames == null || frames.Length <= 1)
        {
            return;
        }

        if (Time.time < nextFrameTime)
        {
            return;
        }

        frameIndex = (frameIndex + 1) % frames.Length;
        ApplyFrame(frameIndex);
        ScheduleNextFrame();
    }

    public void Configure(
        Sprite[] animationFrames,
        float animationFramesPerSecond,
        bool keepBottomAnchored,
        bool[] frameMirrors = null
    )
    {
        frames = animationFrames;
        framesPerSecond = Mathf.Max(1f, animationFramesPerSecond);
        alignToBottom = keepBottomAnchored;
        mirrorFrameHorizontally = frameMirrors;

        Initialize();
        frameIndex = 0;
        ApplyFrame(frameIndex);
        ScheduleNextFrame();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        baseLocalScale = transform.localScale;
        initialized = true;
    }

    private void ApplyFrame(int index)
    {
        if (
            spriteRenderer == null ||
            frames == null ||
            frames.Length == 0 ||
            index < 0 ||
            index >= frames.Length ||
            frames[index] == null
        )
        {
            return;
        }

        Sprite frame = frames[index];
        spriteRenderer.sprite = frame;

        Vector3 localPosition = transform.localPosition;
        localPosition.x = -frame.bounds.center.x;
        localPosition.y = alignToBottom
            ? -frame.bounds.min.y
            : -frame.bounds.center.y;
        transform.localPosition = localPosition;

        Vector3 localScale = baseLocalScale;
        bool mirror =
            mirrorFrameHorizontally != null &&
            index < mirrorFrameHorizontally.Length &&
            mirrorFrameHorizontally[index];

        localScale.x = Mathf.Abs(baseLocalScale.x) * (mirror ? -1f : 1f);
        transform.localScale = localScale;
    }

    private void ScheduleNextFrame()
    {
        nextFrameTime = Time.time + 1f / Mathf.Max(1f, framesPerSecond);
    }
}
