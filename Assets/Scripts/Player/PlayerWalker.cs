using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Walks the player around the block with WASD, the arrow keys or a gamepad stick.
/// There is no jump on purpose: the game is about getting to the kerb, so movement stays grounded.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerWalker : MonoBehaviour
{
    [Tooltip("Metres per second.")]
    public float walkSpeed = 3.2f;

    [Tooltip("How quickly the character turns to face the direction he walks.")]
    public float turnSpeed = 10f;

    [Tooltip("Model that bobs while walking. Leave empty to skip the bob.")]
    public Transform model;

    [Tooltip("Walk bob height in metres.")]
    public float bobAmplitude = 0.035f;

    [Tooltip("Bobs per second while walking.")]
    public float bobFrequency = 6f;

    CharacterController controller;
    float fallSpeed;
    float bobPhase;
    float bobWeight;
    Vector3 modelRestPosition;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (model != null)
            modelRestPosition = model.localPosition;
    }

    void Update()
    {
        Vector3 direction = ReadDirection();
        bool moving = direction.sqrMagnitude > 0.0001f;

        if (controller.isGrounded && fallSpeed < 0f)
            fallSpeed = -2f; // keeps the capsule pressed onto the pavement instead of hovering
        else
            fallSpeed += Physics.gravity.y * Time.deltaTime;

        controller.Move((direction * walkSpeed + Vector3.up * fallSpeed) * Time.deltaTime);

        if (moving)
        {
            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        AnimateBob(moving);
    }

    Vector3 ReadDirection()
    {
        Vector2 input = Vector2.zero;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
            input += gamepad.leftStick.ReadValue();

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        // The street runs along +Z, so screen-up is +Z.
        return new Vector3(input.x, 0f, input.y);
    }

    void AnimateBob(bool moving)
    {
        if (model == null)
            return;

        bobWeight = Mathf.MoveTowards(bobWeight, moving ? 1f : 0f, Time.deltaTime * 5f);
        bobPhase += Time.deltaTime * bobFrequency * bobWeight;
        model.localPosition = modelRestPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(bobPhase)) * bobAmplitude * bobWeight);
    }
}
