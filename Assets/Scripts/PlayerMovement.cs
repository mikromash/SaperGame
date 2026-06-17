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
    private Animator animator;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        
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

        // Обробка приземлення
        if (hasGroundState && !wasGrounded && isGrounded)
        {
            AudioController.PlayAt(AudioEvent.PlayerLand, transform.position);
        }
        hasGroundState = true;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Притискання до землі
        }

        // 1. Розраховуємо горизонтальний рух
        Vector3 movement = CalculateMovement();
        
        // 2. Розраховуємо гравітацію (вертикальний рух)
        ApplyGravity();

        // 3. РОБИМО ОДИН ВИКЛИК MOVE ДЛЯ ВСЬОГО
        controller.Move((movement + velocity) * Time.deltaTime);

        // 4. Поворот персонажа (тільки якщо є рух)
        if (movement != Vector3.zero)
        {
            transform.forward = movement.normalized;
        }

        // 5. Оновлення анімацій
        UpdateAnimations();
    }

    private Vector3 CalculateMovement()
    {
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        return moveDirection * currentSpeed;
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
    }

    private void Jump()
    {
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            AudioController.PlayAt(AudioEvent.PlayerJump, transform.position);
            
            // ДОДАНО: Тригер стрибка для аніматора (створіть параметр типу Trigger "Jump" в Animator)
            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        float speed = moveInput.magnitude;
        animator.SetFloat("Speed", speed);

        // ДОДАНО: Персонаж біжить ТІЛЬКИ якщо натиснутий шифт І є рух (magnitude > 0.1f)
        bool isActuallyRunning = isSprinting && speed > 0.1f;
        animator.SetBool("IsRunning", isActuallyRunning);
        
        animator.SetBool("IsGrounded", isGrounded);
    }
}