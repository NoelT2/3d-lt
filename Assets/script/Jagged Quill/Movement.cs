using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Movement : MonoBehaviour
{
    private CharacterController controller;
    private Vector2 moveInput;

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float gravity = -19.81f;
    [SerializeField] private float jumpHeight = 2f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1.5f;

    [Header("Camera Reference")]
    [SerializeField] private Transform playerCamera;

    private Vector3 velocity;
    private bool isGrounded;

    private bool isDashing = false;
    private float dashTimeRemaining = 0f;
    private float nextDashTime = 0f;
    private Vector3 dashDirection;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector3 move = Vector3.zero;
        if (!isDashing && Keyboard.current != null)
        {
            float moveX = 0f;
            float moveZ = 0f;

            if (Keyboard.current.wKey.isPressed) moveZ = 1f;
            if (Keyboard.current.sKey.isPressed) moveZ = -1f;
            if (Keyboard.current.aKey.isPressed) moveX = -1f;
            if (Keyboard.current.dKey.isPressed) moveX = 1f;

            move = transform.right * moveX + transform.forward * moveZ;
            controller.Move(move * walkSpeed * Time.deltaTime);

            if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            if (Keyboard.current.leftShiftKey.wasPressedThisFrame && Time.time >= nextDashTime)
            {
                StartDash();
            }
        }

        if (isDashing)
        {
            dashTimeRemaining -= Time.deltaTime;
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);

            if (dashTimeRemaining <= 0f)
            {
                isDashing = false;
                velocity = Vector3.zero;
            }
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimeRemaining = dashDuration;
        nextDashTime = Time.time + dashCooldown;

        if (playerCamera != null)
        {
            dashDirection = playerCamera.forward;
        }
        else
        {
            dashDirection = transform.forward;
        }
        
        dashDirection.y = Mathf.Clamp(dashDirection.y, -0.2f, 0.5f);
        dashDirection = dashDirection.normalized;
    }
}