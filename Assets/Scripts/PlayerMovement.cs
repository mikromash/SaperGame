using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Camera Reference")]
    public Transform cameraTransform;

    private CharacterController controller;
    private PlayerInputActions controls;
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isSprinting;
    private bool isGrounded;
    private bool wasGrounded;
    private bool hasGroundState;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new PlayerInputActions();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        
        controls.Player.Sprint.performed += ctx => isSprinting = true;
        controls.Player.Sprint.canceled += ctx => isSprinting = false;

        controls.Player.Jump.performed += ctx => Jump();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Update()
    {
        wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;
        if (hasGroundState && !wasGrounded && isGrounded)
        {
            AudioController.PlayAt(AudioEvent.PlayerLand, transform.position);
        }
        hasGroundState = true;

        if (isGrounded && velocity.y < 0) velocity.y = -2f;

        MovePlayer();
        ApplyGravity();
    }

    private void MovePlayer()
    {
        // Розрахунок напрямку відносно камери
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * moveInput.y + right * moveInput.x;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);

        // Поворот персонажа за рухом
        if (move != Vector3.zero)
        {
            transform.forward = move;
        }
    }

    private void Jump()
    {
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            AudioController.PlayAt(AudioEvent.PlayerJump, transform.position);
        }
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
