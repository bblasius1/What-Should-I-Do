using UnityEngine;

/// <summary>
/// Keeps the street aligned with the movement controls and draws the camera in when blocked.
/// </summary>
public class ThirdPersonFollowCamera : MonoBehaviour
{
    [Tooltip("Object the camera follows. Usually the player character.")]
    public Transform target;

    [Tooltip("World-space offset from the target's feet. -Z puts the camera behind the player.")]
    public Vector3 offset = new Vector3(-2.2f, 6.4f, -9.5f);

    [Tooltip("Look-at height above the target's feet.")]
    public float lookHeight = 1.0f;

    [Tooltip("How far ahead of the target the camera aims.")]
    public float lookAhead = 6.5f;

    [Tooltip("Looks toward the road so the whole crossing stays in view.")]
    public float lookSide = -2.75f;

    [Tooltip("Higher follows more tightly. Zero follows immediately.")]
    public float damping = 7f;

    [SerializeField] Vector3 welcomePosition = new Vector3(3.25f, 3.8f, -0.5f);
    [SerializeField] Vector3 welcomeLookAt = new Vector3(1.9f, 2.25f, 16f);
    [SerializeField] float welcomeFieldOfView = 53f;

    const float ObstructionRadius = 0.25f;
    const float ObstructionPadding = 0.08f;
    const float CurbViewHeight = 1.36f;
    const float CurbViewFieldOfView = 68f;

    readonly RaycastHit[] obstructionHits = new RaycastHit[12];
    Vector3 followPosition;
    float armFraction = 1f;
    Camera view;
    float gameplayFieldOfView;
    bool showingWelcome;
    GameLevel gameplayLevel;
    bool reducedMotion;
    int curbLookDirection;
    float curbViewWeight;
    float curbLookYaw = -90f;

    void Awake()
    {
        view = GetComponent<Camera>();
        if (view != null)
            gameplayFieldOfView = view.fieldOfView;
    }

    void Start()
    {
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (target == null || showingWelcome)
            return;

        float blend = reducedMotion || damping <= 0f ? 1f : 1f - Mathf.Exp(-damping * Time.deltaTime);
        followPosition = Vector3.Lerp(followPosition, target.position, blend);
        float lookBlend = reducedMotion ? 1f : 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime);
        UpdateCurbView(lookBlend);
        PositionCamera(blend);
    }

    /// Frames the target immediately, used at edit time and on scene start.
    public void SnapToTarget()
    {
        if (target == null || showingWelcome)
            return;

        followPosition = target.position;
        armFraction = 1f;
        UpdateCurbView(1f);
        PositionCamera(1f);
    }

    public void SetWelcomeView(bool show)
    {
        showingWelcome = show;
        curbLookDirection = 0;
        curbViewWeight = 0f;
        if (view != null)
            view.fieldOfView = show ? welcomeFieldOfView : gameplayFieldOfView;

        if (show)
        {
            transform.SetPositionAndRotation(welcomePosition,
                Quaternion.LookRotation(welcomeLookAt - welcomePosition));
        }
        else
        {
            SnapToTarget();
        }
    }

    public void SetReducedMotion(bool reduce)
    {
        reducedMotion = reduce;
        if (reduce)
            SnapToTarget();
    }

    internal void SetCurbLook(int direction)
    {
        curbLookDirection = direction;
    }

    internal void SetLevelView(GameLevel level)
    {
        gameplayLevel = level;
        SnapToTarget();
    }

    void UpdateCurbView(float blend)
    {
        curbViewWeight = Mathf.Lerp(curbViewWeight, curbLookDirection == 0 ? 0f : 1f, blend);
        if (curbLookDirection != 0)
        {
            float yaw = curbLookDirection < 0 ? -163f : -17f;
            curbLookYaw = Mathf.Lerp(curbLookYaw, yaw, blend);
        }
        else if (curbViewWeight < 0.001f)
        {
            curbViewWeight = 0f;
            curbLookYaw = -90f;
        }
    }

    void PositionCamera(float blend)
    {
        Vector3 viewHeight = Vector3.up * lookHeight;
        Vector3 anchor = target.position + viewHeight;
        bool courtyardView = gameplayLevel != GameLevel.Crossing;
        Vector3 cameraOffset = courtyardView ? new Vector3(0f, 7f, -7.5f) : offset;
        Vector3 focusOffset = courtyardView ? new Vector3(0f, viewHeight.y, 3.8f) :
            new Vector3(lookSide, viewHeight.y, lookAhead);
        Vector3 arm = followPosition + cameraOffset - anchor;
        float safeFraction = UnobstructedFraction(anchor, arm);

        // Pull in as soon as a wall comes into view, then ease back out once it clears.
        armFraction = Mathf.Min(safeFraction, Mathf.Lerp(armFraction, 1f, blend));
        Vector3 position = anchor + arm * armFraction;
        Vector3 focus = followPosition + focusOffset;
        Vector3 lookDirection = focus - position;
        Quaternion rotation = lookDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDirection, Vector3.up)
            : transform.rotation;
        if (curbViewWeight > 0f)
        {
            Quaternion curbRotation = Quaternion.Euler(3f, curbLookYaw, 0f);
            Vector3 eye = target.position + Vector3.up * CurbViewHeight +
                curbRotation * Vector3.forward * 0.45f + Vector3.left * 0.20f;
            position = Vector3.Lerp(position, eye, curbViewWeight);
            rotation = Quaternion.Slerp(rotation, curbRotation, curbViewWeight);
        }
        if (view != null)
            view.fieldOfView = Mathf.Lerp(gameplayFieldOfView, CurbViewFieldOfView, curbViewWeight);
        transform.SetPositionAndRotation(position, rotation);
    }

    float UnobstructedFraction(Vector3 anchor, Vector3 arm)
    {
        float length = arm.magnitude;
        if (length <= 0.001f)
            return 1f;

        int hits = Physics.SphereCastNonAlloc(anchor, ObstructionRadius, arm / length, obstructionHits,
            length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float distance = length;
        for (int i = 0; i < hits; i++)
        {
            RaycastHit hit = obstructionHits[i];
            Transform obstacle = hit.collider.transform;
            if (obstacle == target || obstacle.IsChildOf(target))
                continue;

            distance = Mathf.Min(distance, hit.distance - ObstructionPadding);
        }

        return Mathf.Clamp01(distance / length);
    }
}
