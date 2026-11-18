using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Walks the player around the block with the keyboard or gamepad.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerWalker : MonoBehaviour
{
    [Tooltip("Metres per second.")]
    public float walkSpeed = 3.2f;

    [Tooltip("How quickly the character turns to face the direction of movement.")]
    public float turnSpeed = 10f;

    [Tooltip("Scene avatar used when the animated Boy resource is unavailable.")]
    public Transform model;

    [SerializeField] Transform girlModel;

    [Tooltip("Walk bob height in metres.")]
    public float bobAmplitude = 0.035f;

    [Tooltip("Walk animation phase speed.")]
    public float bobFrequency = 6f;

    internal CrossingQuiz CrossingRules { get; set; }

    const float GroundedFallSpeed = -2f;
    const string IdleClip = "Idle";
    const string WalkClip = "Walk";

    CharacterController controller;
    float fallSpeed = GroundedFallSpeed;
    float bobPhase;
    float bobWeight;
    Vector3 modelRestPosition;
    Transform boyModel;
    Animation avatarAnimation;
    bool walking;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        boyModel = LoadAvatar("BoyAnimated", model, true);
        girlModel = LoadAvatar("GirlAnimated", girlModel, false);
        model = boyModel;
        PrepareAvatar();
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        Vector3 direction = ReadDirection();
        fallSpeed = controller.isGrounded ? GroundedFallSpeed : fallSpeed + Physics.gravity.y * deltaTime;

        Vector3 start = transform.position;
        Vector3 step = (direction * walkSpeed + Vector3.up * fallSpeed) * deltaTime;
        if (CrossingRules != null)
            step = CrossingRules.ConstrainMovement(start, step);

        CollisionFlags contacts = controller.Move(step);
        bool grounded = (contacts & CollisionFlags.Below) != 0;
        if (grounded)
            fallSpeed = GroundedFallSpeed;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            float turnBlend = 1f - Mathf.Exp(-turnSpeed * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnBlend);
        }

        Vector3 displacement = transform.position - start;
        displacement.y = 0f;
        float walkFraction = grounded && walkSpeed > 0f
            ? Mathf.Clamp01(displacement.magnitude / (walkSpeed * deltaTime))
            : 0f;
        AnimateMovement(walkFraction, deltaTime);
    }

    void OnDisable()
    {
        ResetMotion();
    }

    public void SetGirlModel(Transform avatar)
    {
        girlModel = avatar;
    }

    public void SelectAvatar(bool useGirl)
    {
        Transform selected = useGirl && girlModel != null ? girlModel : boyModel;
        if (selected == model || selected == null)
            return;

        ResetMotion();
        if (model != null)
            model.gameObject.SetActive(false);

        model = selected;
        model.gameObject.SetActive(true);
        PrepareAvatar();
    }

    public void ResetTo(Vector3 position, Quaternion rotation)
    {
        bool collisionEnabled = controller.enabled;
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = collisionEnabled;
        ResetMotion();
    }

    void ResetMotion()
    {
        fallSpeed = GroundedFallSpeed;
        bobPhase = 0f;
        bobWeight = 0f;
        walking = false;

        if (model != null)
            model.localPosition = modelRestPosition;
        if (avatarAnimation != null && avatarAnimation.isActiveAndEnabled)
            avatarAnimation.Play(IdleClip);
    }

    Transform LoadAvatar(string resourceName, Transform fallback, bool active)
    {
        GameObject prefab = Resources.Load<GameObject>($"Avatars/{resourceName}");
        Animation clips = prefab != null ? prefab.GetComponentInChildren<Animation>() : null;
        if (clips == null || clips.GetClip(IdleClip) == null || clips.GetClip(WalkClip) == null)
            return fallback;

        GameObject avatar = Instantiate(prefab, transform, false);
        avatar.name = resourceName;
        avatar.SetActive(active);
        if (fallback != null)
            fallback.gameObject.SetActive(false);
        return avatar.transform;
    }

    void PrepareAvatar()
    {
        avatarAnimation = null;
        if (model != null)
        {
            modelRestPosition = model.localPosition;
            Animation clips = model.GetComponentInChildren<Animation>();
            if (clips != null && clips.GetClip(IdleClip) != null && clips.GetClip(WalkClip) != null)
            {
                avatarAnimation = clips;
                avatarAnimation.wrapMode = WrapMode.Loop;
                avatarAnimation[IdleClip].wrapMode = WrapMode.Loop;
                avatarAnimation[WalkClip].wrapMode = WrapMode.Loop;
            }
        }
        ResetMotion();
    }

    static Vector3 ReadDirection()
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
            input += gamepad.leftStick.ReadValue() + gamepad.dpad.ReadValue();

        // The street runs along +Z, so screen-up is +Z.
        input = Vector2.ClampMagnitude(input, 1f);
        return new Vector3(input.x, 0f, input.y);
    }

    void AnimateMovement(float walkFraction, float deltaTime)
    {
        if (model == null)
            return;

        if (avatarAnimation != null)
        {
            bool moving = walkFraction > 0.08f;
            if (moving)
                avatarAnimation[WalkClip].speed = Mathf.Lerp(0.65f, 1.65f, walkFraction);
            if (walking != moving)
            {
                walking = moving;
                avatarAnimation.CrossFade(moving ? WalkClip : IdleClip, 0.16f);
            }
            return;
        }

        // Older scenes can still use their original unrigged avatars.
        bobWeight = Mathf.MoveTowards(bobWeight, walkFraction, deltaTime * 5f);
        bobPhase = Mathf.Repeat(bobPhase + deltaTime * bobFrequency * bobWeight, Mathf.PI);
        model.localPosition = modelRestPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(bobPhase)) * bobAmplitude * bobWeight);
    }
}
