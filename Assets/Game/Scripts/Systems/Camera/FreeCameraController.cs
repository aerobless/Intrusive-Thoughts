using UnityEngine;
using UnityEngine.InputSystem;

public class FreeCameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float fastMultiplier = 3f;
    public float slowMultiplier = 0.5f;

    [Header("Look")]
    public float sensitivity = 0.15f;
    public float smoothing = 5f;
    public bool invertY = false;

    Vector2 smoothedMouse;
    Vector2 mouseDelta;
    float rotationX;
    float rotationY;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        HandleCursorToggle();
    }

    void HandleLook()
    {
        if (Mouse.current == null) return;

        // Raw mouse delta * sensitivity
        var rawDelta = Mouse.current.delta.ReadValue() * sensitivity;
        if (invertY) rawDelta.y = -rawDelta.y;

        // Smooth it out
        mouseDelta = Vector2.Lerp(mouseDelta, rawDelta, 1f / smoothing);

        rotationX += mouseDelta.x;
        rotationY -= mouseDelta.y;
        rotationY = Mathf.Clamp(rotationY, -90f, 90f);

        transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0f);
    }

    void HandleMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        var speed = moveSpeed;
        if (kb.leftShiftKey.isPressed) speed *= fastMultiplier;
        if (kb.leftCtrlKey.isPressed) speed *= slowMultiplier;

        var dir = Vector3.zero;
        if (kb.wKey.isPressed) dir += Vector3.forward;
        if (kb.sKey.isPressed) dir += Vector3.back;
        if (kb.aKey.isPressed) dir += Vector3.left;
        if (kb.dKey.isPressed) dir += Vector3.right;
        if (kb.eKey.isPressed) dir += Vector3.up;
        if (kb.qKey.isPressed) dir += Vector3.down;

        transform.Translate(dir.normalized * speed * Time.deltaTime, Space.Self);
    }

    void HandleCursorToggle()
    {
        var kb = Keyboard.current;
        var m = Mouse.current;
        if (kb == null || m == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (m.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
