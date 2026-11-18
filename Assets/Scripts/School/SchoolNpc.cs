using UnityEngine;

public enum SchoolNpcRole { Teacher, Student, Classmate }

/// <summary>Fits a school character at a ground pivot and plays its current motion.</summary>
[DisallowMultipleComponent]
public sealed class SchoolNpc : MonoBehaviour
{
    const float BenchSeatHeight = 0.43f;
    const float StudentSeatedContactHeight = 0.246f;
    const float FallbackSeatedContactHeight = 0.176f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int StandardColor = Shader.PropertyToID("_Color");
    static Material fallbackMaterial;
    static Mesh sphere;
    static Mesh cube;

    SchoolNpcRole role;
    Transform visual;
    Vector3 visualRestPosition;
    Animation animationPlayer;
    string idleClip;
    string walkClip;
    string seatedClip;
    string callingClip;
    string playingClip;
    FallbackRig fallback;
    bool walking;
    bool calling;
    float phase;

    public static SchoolNpc Create(Transform parent, SchoolNpcRole role, Vector3 worldPosition, Quaternion worldRotation)
    {
        var actor = new GameObject(role.ToString());
        actor.transform.SetParent(parent, false);
        actor.transform.SetPositionAndRotation(worldPosition, worldRotation);
        var npc = actor.AddComponent<SchoolNpc>();
        npc.role = role;
        npc.BuildVisual();
        return npc;
    }

    public void SetWalking(bool active, float cycleSpeed = 1f)
    {
        if (active && animationPlayer != null && walkClip != null)
            animationPlayer[walkClip].speed = cycleSpeed;

        if (walking == active)
            return;

        walking = active;
        UpdateClip();
    }

    public void SetCalling(bool active)
    {
        if (role != SchoolNpcRole.Student || calling == active)
            return;

        calling = active;
        UpdateClip();
    }

    void BuildVisual()
    {
        visual = Pivot(transform, "Character visual", Vector3.zero);
        float height = role switch
        {
            SchoolNpcRole.Teacher => 1.72f,
            SchoolNpcRole.Student => 1.26f,
            _ => 1.34f
        };

        var prefab = Resources.Load<GameObject>($"School/{role}");
        if (prefab != null && TryUseImported(prefab, height))
        {
            if (role == SchoolNpcRole.Student)
            {
                // The imported action keeps the rig at ground height, with the pelvis above it.
                visualRestPosition = Vector3.up * (BenchSeatHeight - StudentSeatedContactHeight);
                visual.localPosition = visualRestPosition;
            }
            return;
        }

        var body = Pivot(visual, "Fallback character", Vector3.zero);
        body.localScale = Vector3.one * height;
        if (role == SchoolNpcRole.Student)
            visual.localPosition = Vector3.up * (BenchSeatHeight - FallbackSeatedContactHeight);
        fallback = new FallbackRig(body, role);
    }

    bool TryUseImported(GameObject prefab, float height)
    {
        Transform fit = Pivot(visual, "Model alignment", Vector3.zero);
        GameObject model = Instantiate(prefab, fit, false);
        if (!TryGetBounds(fit, out Bounds bounds) || bounds.size.y <= 0.001f)
        {
            fit.gameObject.SetActive(false);
            Destroy(fit.gameObject);
            return false;
        }

        float scale = height / bounds.size.y;
        fit.localScale = Vector3.one * scale;
        fit.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;

        animationPlayer = model.GetComponentInChildren<Animation>();
        if (animationPlayer != null)
        {
            animationPlayer.playAutomatically = false;
            animationPlayer.Stop();
            idleClip = FindClip("Idle", "Idle_Loop");
            walkClip = FindClip("Walk", "Walk_Formal_Loop", "Walk_Loop");
            seatedClip = FindClip("Seated", "Sitting_Idle_Loop");
            callingClip = FindClip("Calling", "Sitting_Talking_Loop");
        }

        if (role == SchoolNpcRole.Student && seatedClip == null)
        {
            fit.gameObject.SetActive(false);
            Destroy(fit.gameObject);
            animationPlayer = null;
            return false;
        }

        UpdateClip();

        return true;
    }

    string FindClip(params string[] names)
    {
        foreach (string name in names)
        {
            AnimationState state = animationPlayer[name];
            if (state != null)
            {
                state.wrapMode = WrapMode.Loop;
                return state.name;
            }
        }

        return null;
    }

    void UpdateClip()
    {
        if (animationPlayer == null)
            return;

        string next = role == SchoolNpcRole.Student
            ? (calling ? callingClip : null) ?? seatedClip ?? idleClip
            : (walking ? walkClip : null) ?? idleClip;
        if (next == playingClip)
            return;

        if (next == null)
            animationPlayer.Stop();
        else if (playingClip == null)
            animationPlayer.Play(next);
        else
            animationPlayer.CrossFade(next, 0.18f);
        playingClip = next;
    }

    void LateUpdate()
    {
        phase += Time.deltaTime * (walking ? 7f : 3.1f);
        if (fallback != null)
        {
            fallback.Pose(phase, walking, calling);
            return;
        }

        // Provider assets that only have a mesh can still communicate a change in state.
        bool missingStep = walking && walkClip == null;
        bool missingCall = calling && callingClip == null;
        float lift = missingStep ? Mathf.Abs(Mathf.Sin(phase)) * 0.04f : 0f;
        float tilt = missingCall ? Mathf.Sin(phase) * 2f : 0f;
        visual.SetLocalPositionAndRotation(visualRestPosition + Vector3.up * lift, Quaternion.Euler(0f, 0f, tilt));
    }

    static bool TryGetBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z);
                point = toRoot.MultiplyPoint3x4(point);
                if (hasBounds)
                    bounds.Encapsulate(point);
                else
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
            }
        }

        return hasBounds;
    }

    static Transform Pivot(Transform parent, string name, Vector3 offset)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = offset;
        return pivot;
    }

    static void Part(Transform parent, string name, Vector3 position, Vector3 size, Color color, bool box = false)
    {
        if (fallbackMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            fallbackMaterial = new Material(shader) { name = "School character fallback" };
        }

        sphere ??= Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        cube ??= Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = size;
        piece.GetComponent<MeshFilter>().sharedMesh = box ? cube : sphere;
        var renderer = piece.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = fallbackMaterial;
        var paint = new MaterialPropertyBlock();
        paint.SetColor(BaseColor, color);
        paint.SetColor(StandardColor, color);
        renderer.SetPropertyBlock(paint);
    }

    sealed class FallbackRig
    {
        static readonly Color Trousers = new Color32(45, 69, 96, 255);
        static readonly Color Shoes = new Color32(63, 64, 78, 255);
        static readonly Color White = new Color32(248, 243, 218, 255);
        static readonly Color Eyes = new Color32(49, 35, 52, 255);
        readonly Transform body;
        readonly Transform leftArm;
        readonly Transform rightArm;
        readonly Transform rightElbow;
        readonly Transform leftLeg;
        readonly Transform rightLeg;
        readonly bool seated;

        public FallbackRig(Transform root, SchoolNpcRole role)
        {
            body = root;
            seated = role == SchoolNpcRole.Student;
            Color shirt = role switch
            {
                SchoolNpcRole.Teacher => new Color32(104, 51, 85, 255),
                SchoolNpcRole.Student => new Color32(241, 174, 67, 255),
                _ => new Color32(70, 167, 137, 255)
            };
            Color skin = role == SchoolNpcRole.Classmate
                ? new Color32(150, 91, 64, 255) : new Color32(227, 164, 125, 255);
            Color hair = role == SchoolNpcRole.Teacher
                ? new Color32(86, 54, 60, 255) : new Color32(68, 50, 46, 255);
            Transform torso = Pivot(body, "Torso", new Vector3(0f, seated ? 0.17f : 0.39f, 0f));
            Part(torso, "Shirt", new Vector3(0f, 0.17f, 0f), new Vector3(0.35f, 0.40f, 0.25f), shirt);
            Part(torso, "Neck", new Vector3(0f, 0.36f, 0f), new Vector3(0.13f, 0.14f, 0.13f), skin);
            Part(torso, "Face", new Vector3(0f, 0.47f, 0.012f), new Vector3(0.25f, 0.27f, 0.24f), skin);
            Part(torso, "Hair", new Vector3(0f, 0.531f, -0.022f), new Vector3(0.261f, 0.16f, 0.24f), hair);
            Part(torso, "Left eye", new Vector3(-0.045f, 0.47f, 0.121f), new Vector3(0.024f, 0.03f, 0.024f), Eyes);
            Part(torso, "Right eye", new Vector3(0.045f, 0.47f, 0.121f), new Vector3(0.024f, 0.03f, 0.024f), Eyes);
            if (role == SchoolNpcRole.Teacher)
            {
                Part(torso, "Collar", new Vector3(0f, 0.285f, 0.106f), new Vector3(0.083f, 0.108f, 0.04f), White);
                Part(torso, "Staff badge", new Vector3(0.075f, 0.19f, 0.127f), new Vector3(0.048f, 0.061f, 0.012f), White, true);
            }

            leftArm = Arm(torso, -1, shirt, skin, out _);
            rightArm = Arm(torso, 1, shirt, skin, out rightElbow);
            leftLeg = Leg(torso, -1, seated);
            rightLeg = Leg(torso, 1, seated);
            Pose(0f, false, false);
        }

        public void Pose(float phase, bool walking, bool calling)
        {
            if (seated)
            {
                leftArm.localRotation = Quaternion.Euler(-25f, 0f, -14f);
                rightArm.localRotation = calling
                    ? Quaternion.Euler(-13f, 0f, 116f + Mathf.Sin(phase * 1.4f) * 7f)
                    : Quaternion.Euler(-24f, 0f, 14f);
                rightElbow.localRotation = Quaternion.Euler(0f, 0f, calling ? 29f + Mathf.Sin(phase * 2f) * 18f : 0f);
                return;
            }

            float swing = walking ? Mathf.Sin(phase) * 24f : 0f;
            body.localPosition = Vector3.up * ((walking ? Mathf.Abs(Mathf.Sin(phase)) * 0.018f : 0f));
            leftLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            leftArm.localRotation = Quaternion.Euler(swing * 0.65f, 0f, -8f);
            rightArm.localRotation = Quaternion.Euler(-swing * 0.65f, 0f, 8f);
        }

        static Transform Arm(Transform torso, int side, Color shirt, Color skin, out Transform elbow)
        {
            Transform shoulder = Pivot(torso, side < 0 ? "Left arm" : "Right arm", new Vector3(side * 0.159f, 0.275f, 0f));
            Part(shoulder, "Sleeve", new Vector3(0f, -0.08f, 0f), new Vector3(0.102f, 0.211f, 0.105f), shirt);
            elbow = Pivot(shoulder, "Elbow", new Vector3(0f, -0.156f, 0f));
            Part(elbow, "Forearm", new Vector3(0f, -0.057f, 0f), new Vector3(0.078f, 0.14f, 0.079f), shirt);
            Part(elbow, "Hand", new Vector3(0f, -0.135f, 0f), new Vector3(0.081f, 0.093f, 0.083f), skin);
            return shoulder;
        }

        static Transform Leg(Transform torso, int side, bool seated)
        {
            Transform thigh = Pivot(torso, side < 0 ? "Left leg" : "Right leg", new Vector3(side * 0.081f, 0.02f, 0f));
            Part(thigh, "Knee", new Vector3(0f, -0.09f, 0f), new Vector3(0.135f, 0.225f, 0.137f), Trousers);
            Transform ankle = Pivot(thigh, "Lower leg", new Vector3(0f, -0.172f, 0f));
            Part(ankle, "Trouser leg", new Vector3(0f, -0.079f, 0f), new Vector3(0.106f, 0.187f, 0.107f), Trousers);
            Transform foot = Pivot(ankle, "Foot", new Vector3(0f, -0.162f, 0f));
            Part(foot, "Shoe", new Vector3(0f, -0.026f, 0.037f), new Vector3(0.119f, 0.102f, 0.206f), Shoes);

            if (seated)
            {
                thigh.localRotation = Quaternion.Euler(-100f, 0f, side * 7f);
                ankle.localRotation = Quaternion.Euler(59f, 0f, 0f);
                foot.localRotation = Quaternion.Euler(41f, 0f, 0f);
            }

            return thigh;
        }
    }
}
