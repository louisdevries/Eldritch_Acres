using UnityEngine;
using EldritchFarm.Player;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private PlayerKnockback knockback;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        knockback = GetComponent<PlayerKnockback>(); // optional — null if not present
    }

    void Update()
    {
        // Read input — but skip it during stun so knockback feels solid.
        Vector3 inputMove = Vector3.zero;
        if (knockback == null || !knockback.IsStunned)
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            inputMove = (transform.right * x + transform.forward * z) * speed;
        }

        // Add knockback velocity if present. Combining input + knockback into a single
        // movement vector means the CharacterController processes them together —
        // no fighting between the two systems.
        Vector3 knockbackMove = knockback != null ? knockback.CurrentVelocity : Vector3.zero;
        Vector3 horizontalMove = inputMove + knockbackMove;

        controller.Move(horizontalMove * Time.deltaTime);

        // Gravity
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}