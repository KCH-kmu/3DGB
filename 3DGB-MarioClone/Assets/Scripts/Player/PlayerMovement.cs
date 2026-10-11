using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 12f;

    private Rigidbody2D myBody;
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    private bool isGrounded;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        PlayerJump();
    }

    private void FixedUpdate()
    {
        PlayerWalk();
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

            isGrounded = false;
            anim.SetBool("Jump", true);
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
            isGrounded = true;
            anim.SetBool("Jump", false);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.layer ==
            LayerMask.NameToLayer("Ground"))
        {
            isGrounded = false;
            anim.SetBool("Jump", true);
        }
    }
    public void Bounce(float bounceForce)
    {
        myBody.velocity = new Vector2(
            myBody.velocity.x,
            bounceForce
        );

        isGrounded = false;
        anim.SetBool("Jump", true);
    }
}