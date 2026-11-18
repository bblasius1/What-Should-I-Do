using System;
using UnityEngine;

/// <summary>Fits a standing playmate and adds the visible catch gesture to its arm joints.</summary>
internal sealed class PlaygroundChild : MonoBehaviour
{
    Transform figure;
    Animation clips;
    string walkClip;
    string idleClip;
    Limb leftArm;
    Limb rightArm;
    bool walking;
    bool reaching;
    float reachWeight;
    float stepPhase;

    public Vector3 Hands => transform.position + Vector3.up * 0.98f + transform.forward * 0.43f;

    public static PlaygroundChild Create(Transform parent, string name, string resource,
        Vector3 position, Color jersey, float height)
    {
        var child = new GameObject(name).AddComponent<PlaygroundChild>();
        child.transform.SetParent(parent, false);
        child.transform.SetPositionAndRotation(position, Quaternion.identity);
        child.Build(resource, jersey, height);
        return child;
    }

    void Build(string resource, Color jersey, float height)
    {
        figure = new GameObject("Playmate model").transform;
        figure.SetParent(transform, false);

        var prefab = Resources.Load<GameObject>(resource);
        if (prefab != null)
        {
            var model = Instantiate(prefab, figure, false);
            if (PlaygroundLayout.TryMeasure(figure, model, out Bounds bounds) && bounds.size.y > 0.0001f)
            {
                float scale = height / bounds.size.y;
                model.transform.localScale *= scale;
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                clips = model.GetComponentInChildren<Animation>();
                if (clips != null)
                {
                    clips.playAutomatically = false;
                    clips.Stop();
                    idleClip = Clip("Idle", "Idle_Loop");
                    walkClip = Clip("Walk", "Walk_Loop", "Walk_Formal_Loop");
                    if (idleClip != null)
                        clips.Play(idleClip);
                }
                leftArm = FindArm(model.transform, "Left");
                rightArm = FindArm(model.transform, "Right");
                return;
            }

            model.SetActive(false);
            Destroy(model);
        }

        BuildFallback(jersey, height);
    }

    string Clip(params string[] aliases)
    {
        foreach (string alias in aliases)
        {
            AnimationState state = clips[alias];
            if (state == null)
                continue;

            state.wrapMode = WrapMode.Loop;
            return alias;
        }

        return null;
    }

    public void SetWalking(bool value)
    {
        if (walking == value)
            return;

        walking = value;
        if (clips == null)
            return;

        string next = walking ? walkClip ?? idleClip : idleClip;
        if (next != null && !clips.IsPlaying(next))
            clips.CrossFade(next, 0.16f);
        else if (next == null)
            clips.Stop();
    }

    public void SetReaching(bool value) => reaching = value;

    void LateUpdate()
    {
        if (figure == null)
            return;

        bool useWalkingMotion = walking && walkClip != null;
        stepPhase = Mathf.Repeat(stepPhase + Time.deltaTime * 13f, 2f * Mathf.PI);
        Vector3 stride = walking && !useWalkingMotion
            ? Vector3.up * (Mathf.Abs(Mathf.Sin(stepPhase)) * 0.045f)
            : Vector3.zero;
        figure.localPosition = Vector3.Lerp(figure.localPosition, stride, 1f - Mathf.Exp(-14f * Time.deltaTime));

        reachWeight = Mathf.MoveTowards(reachWeight, reaching ? 1f : 0f, Time.deltaTime * 5.5f);
        Pose(leftArm, reachWeight);
        Pose(rightArm, reachWeight);
    }

    void Pose(Limb limb, float weight)
    {
        if (limb == null)
            return;

        // Each frame starts from the clip pose. A still model uses its saved bind angles.
        bool hasPoseClip = clips != null && clips.isPlaying;
        if (!hasPoseClip)
        {
            limb.Shoulder.localRotation = limb.RestShoulder;
            limb.Elbow.localRotation = limb.RestElbow;
        }

        if (weight <= 0.0001f)
            return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right * limb.Side;
        Aim(limb.Shoulder, limb.Elbow.position, forward * 0.75f + Vector3.up * 0.24f + right * 0.34f, weight);
        Aim(limb.Elbow, limb.Hand.position, forward * 0.83f + Vector3.up * 0.48f - right * 0.29f, weight);
    }

    static void Aim(Transform joint, Vector3 endpoint, Vector3 direction, float weight)
    {
        Vector3 origin = endpoint - joint.position;
        if (origin.sqrMagnitude < 0.000001f)
            return;

        Quaternion target = Quaternion.FromToRotation(origin, direction) * joint.rotation;
        joint.rotation = Quaternion.Slerp(joint.rotation, target, weight);
    }

    static Limb FindArm(Transform root, string side)
    {
        Transform shoulder = null;
        Transform elbow = null;
        Transform hand = null;
        foreach (Transform joint in root.GetComponentsInChildren<Transform>())
        {
            if (joint.name.EndsWith(side + "Arm", StringComparison.OrdinalIgnoreCase))
                shoulder = joint;
            if (joint.name.EndsWith(side + "ForeArm", StringComparison.OrdinalIgnoreCase))
                elbow = joint;
            if (joint.name.EndsWith(side + "Hand", StringComparison.OrdinalIgnoreCase))
                hand = joint;
        }

        if (shoulder == null || elbow == null || hand == null)
            return null;

        // Some rigs report handedness in model space even after the importer mirrors X.
        int sign = Vector3.Dot(root.right, shoulder.position - root.position) < 0f ? -1 : 1;
        return new Limb(shoulder, elbow, hand, sign);
    }

    void BuildFallback(Color jersey, float height)
    {
        Color skin = new Color32(184, 130, 99, 255);
        Color hair = new Color32(52, 39, 39, 255);
        Color denim = new Color32(50, 73, 109, 255);
        Color shoes = new Color32(246, 227, 183, 255);
        float factor = height / 1.32f;
        Transform body = Pivot(figure, "Fallback body", Vector3.zero);
        body.localScale = Vector3.one * factor;

        Part(body, "Sweatshirt", new Vector3(0f, 0.81f, 0f), new Vector3(0.43f, 0.49f, 0.28f), jersey);
        Part(body, "Face", new Vector3(0f, 1.15f, 0.02f), new Vector3(0.39f, 0.37f, 0.33f), skin);
        Part(body, "Hair", new Vector3(0f, 1.27f, -0.038f), new Vector3(0.40f, 0.20f, 0.34f), hair);
        foreach (int side in new[] { -1, 1 })
        {
            Part(body, "Eye", new Vector3(side * 0.076f, 1.17f, 0.169f), new Vector3(0.028f, 0.034f, 0.021f), hair);
            Part(body, "Trouser leg", new Vector3(side * 0.094f, 0.34f, 0f), new Vector3(0.16f, 0.56f, 0.17f), denim);
            Part(body, "Shoe", new Vector3(side * 0.092f, 0.076f, 0.055f), new Vector3(0.18f, 0.152f, 0.29f), shoes);
        }

        leftArm = MakeArm(body, -1, jersey, skin);
        rightArm = MakeArm(body, 1, jersey, skin);
    }

    Limb MakeArm(Transform parent, int side, Color jersey, Color skin)
    {
        Transform shoulder = Pivot(parent, "Shoulder", new Vector3(side * 0.21f, 0.95f, 0f));
        shoulder.localRotation = Quaternion.Euler(0f, 0f, side * 15f);
        Part(shoulder, "Sleeve", new Vector3(0f, -0.103f, 0f), new Vector3(0.14f, 0.26f, 0.14f), jersey);
        Transform elbow = Pivot(shoulder, "Elbow", new Vector3(0f, -0.195f, 0f));
        Part(elbow, "Arm", new Vector3(0f, -0.09f, 0f), new Vector3(0.10f, 0.21f, 0.10f), skin);
        Transform hand = Pivot(elbow, "Hand", new Vector3(0f, -0.18f, 0f));
        Part(hand, "Palm", Vector3.zero, new Vector3(0.11f, 0.11f, 0.103f), skin);
        return new Limb(shoulder, elbow, hand, side);
    }

    static Transform Pivot(Transform parent, string label, Vector3 point)
    {
        Transform pivot = new GameObject(label).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = point;
        return pivot;
    }

    void OnDestroy()
    {
        if (fallbackPaint != null)
            Destroy(fallbackPaint);
    }

    Material fallbackPaint;

    // All the parts created by one fallback character share a paint shader and vary the color.
    void Part(Transform parent, string label, Vector3 point, Vector3 dimensions, Color color)
    {
        var piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        piece.name = label;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = point;
        piece.transform.localScale = dimensions;
        Collider collision = piece.GetComponent<Collider>();
        collision.enabled = false;
        Destroy(collision);
        if (fallbackPaint == null)
            fallbackPaint = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

        Renderer renderer = piece.GetComponent<Renderer>();
        renderer.sharedMaterial = fallbackPaint;
        var paint = new MaterialPropertyBlock();
        paint.SetColor("_BaseColor", color);
        paint.SetColor("_Color", color);
        renderer.SetPropertyBlock(paint);
    }

    sealed class Limb
    {
        public readonly Transform Shoulder;
        public readonly Transform Elbow;
        public readonly Transform Hand;
        public readonly Quaternion RestShoulder;
        public readonly Quaternion RestElbow;
        public readonly int Side;

        public Limb(Transform shoulder, Transform elbow, Transform hand, int side)
        {
            Shoulder = shoulder;
            Elbow = elbow;
            Hand = hand;
            RestShoulder = shoulder.localRotation;
            RestElbow = elbow.localRotation;
            Side = side;
        }
    }
}
