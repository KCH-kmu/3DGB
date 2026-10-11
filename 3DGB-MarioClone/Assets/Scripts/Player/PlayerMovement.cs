using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 12f;

    private Rigidbody2D myBody;
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    private bool isGrounded;
    private readonly HashSet<Collider2D> groundContacts =
        new HashSet<Collider2D>();

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
            SetGrounded(groundContacts.Count > 0);
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
