using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float groundCheckRadius = 0.12f;

    [Header("Sound")]
    [SerializeField] private AudioClip jumpSound;

    private Rigidbody2D myBody;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Transform groundCheck;
    private int groundLayerMask;

    private bool isGrounded;
    private readonly HashSet<Collider2D> groundContacts =
        new HashSet<Collider2D>();

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        groundCheck = transform.Find("Groundcheck");
        groundLayerMask = LayerMask.GetMask("Ground");
    }

    private void Update()
    {
        PlayerJump();
    }

    private void FixedUpdate()
    {
        RefreshGroundedState();
        PlayerWalk();
    }

    private void RefreshGroundedState()
    {
        if (groundCheck == null)
        {
            SetGrounded(groundContacts.Count > 0);
            return;
        }

        // 상승 중에는 발밑 원이 아직 지면과 겹쳐 있어도 공중 상태로 유지한다.
        if (myBody.velocity.y > 0.1f)
        {
            SetGrounded(false);
            return;
        }

        bool overlapsGround = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayerMask
        ) != null;

        // 타일 경계를 건너는 한 프레임 동안 Trigger Exit가 먼저 발생해도
        // 물리 조회 결과로 접지 상태가 끊기지 않도록 한다.
        SetGrounded(overlapsGround || groundContacts.Count > 0);
    }

    private void PlayerWalk()
    {
        float h = Input.GetAxisRaw("Horizontal");

        if (h > 0)
        {
            myBody.velocity = new Vector2(speed, myBody.velocity.y);
            ChangeDirection(1);
        }
        else if (h < 0)
        {
            myBody.velocity = new Vector2(-speed, myBody.velocity.y);
            ChangeDirection(-1);
        }
        else
        {
            myBody.velocity = new Vector2(0, myBody.velocity.y);
        }

        anim.SetInteger("Speed", Mathf.Abs((int)h));
    }

    private void PlayerJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            myBody.velocity =
                new Vector2(myBody.velocity.x, jumpForce);

            if (jumpSound != null)
            {
                audioSource.PlayOneShot(jumpSound, 0.85f);
            }

            groundContacts.Clear();
            SetGrounded(false);
        }
    }

    private void ChangeDirection(int direction)
    {
        spriteRenderer.flipX = direction < 0;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer ==
            LayerMask.NameToLayer("Ground"))
        {
            groundContacts.Add(collision);
            SetGrounded(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.layer ==
            LayerMask.NameToLayer("Ground"))
        {
            groundContacts.Remove(collision);
        }
    }

    private void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
        anim.SetBool("Jump", !grounded);
    }

    public void Bounce(float bounceForce)
    {
        myBody.velocity = new Vector2(
            myBody.velocity.x,
            bounceForce
        );

        groundContacts.Clear();
        SetGrounded(false);
    }
}
