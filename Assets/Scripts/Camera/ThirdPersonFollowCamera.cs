using UnityEngine;

/// <summary>
/// Keeps the camera on a fixed world-space offset behind a target so the view stays
/// pointed down the street, no matter which way the target is facing.
/// </summary>
public class ThirdPersonFollowCamera : MonoBehaviour
{
    [Tooltip("Object the camera follows. Usually the player character.")]
    public Transform target;

    [Tooltip("World-space offset from the target's feet. -Z puts the camera behind the player.")]
    public Vector3 offset = new Vector3(0.5f, 3.2f, -7f);

    [Tooltip("Look-at height above the target's feet.")]
    public float lookHeight = 1.4f;

    [Tooltip("How far ahead of the target the camera aims.")]
    public float lookAhead = 5f;

    [Tooltip("Higher follows more tightly. 0 snaps instantly.")]
    public float damping = 6f;

    void Start()
    {
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        float blend = damping <= 0f ? 1f : 1f - Mathf.Exp(-damping * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target.position + offset, blend);
        transform.rotation = Quaternion.Slerp(transform.rotation, LookRotation(), blend);
    }

    /// Frames the target immediately, used at edit time and on scene start.
    public void SnapToTarget()
    {
        if (target == null)
            return;

        transform.position = target.position + offset;
        transform.rotation = LookRotation();
    }

    Quaternion LookRotation()
    {
        Vector3 aim = target.position + Vector3.up * lookHeight + Vector3.forward * lookAhead;
        return Quaternion.LookRotation(aim - transform.position, Vector3.up);
    }
}
