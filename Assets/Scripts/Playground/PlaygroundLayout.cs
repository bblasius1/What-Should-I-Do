using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds the play garden, the catch circle, and the props used by the playground lesson.</summary>
internal sealed class PlaygroundLayout : MonoBehaviour
{
    const float FloorY = 0.12f;
    const string ResourcePath = "Playground/";

    static readonly Vector3 Origin = new Vector3(-190f, 0f, -150f);
    static readonly Vector3 CourtCenter = new Vector3(0f, FloorY, 1.2f);
    static readonly Color Navy = new Color32(29, 71, 81, 255);
    static readonly Color Teal = new Color32(47, 142, 140, 255);
    static readonly Color Amber = new Color32(240, 182, 78, 255);
    static readonly Color Coral = new Color32(219, 116, 94, 255);
    static readonly Color Cream = new Color32(249, 233, 193, 255);

    static Mesh cube;
    static Mesh cylinder;
    static Mesh sphere;

    readonly Dictionary<Color, Material> paints = new Dictionary<Color, Material>();
    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    Mesh ringMesh;
    Mesh diskMesh;

    public Vector3 StartPoint => World(0f, -10.3f);
    public Vector3 InvitationPoint => World(-7.0f, -4.25f);
    public Vector3 OpenSpot => World(0f, -2.45f);
    public Bounds WalkableBounds => new Bounds(transform.TransformPoint(new Vector3(0f, 0f, -0.55f)),
        new Vector3(27.5f, 2f, 22.9f));

    public Vector3 CatchSpot(int index) => index switch
    {
        0 => World(-3.25f, 1.8f),
        1 => World(0f, 4.65f),
        _ => World(3.25f, 1.8f)
    };

    public static PlaygroundLayout Create(Transform parent)
    {
        var layout = new GameObject("Play garden").AddComponent<PlaygroundLayout>();
        layout.transform.SetParent(parent, false);
        layout.transform.SetPositionAndRotation(Origin, Quaternion.identity);
        layout.ringMesh = layout.MakeDisk("Playground marking ring", 0.92f);
        layout.diskMesh = layout.MakeDisk("Playground disk", 0f);
        layout.Build();
        return layout;
    }

    public PlaygroundMarker AddMarker(string name, Vector3 position)
    {
        Transform holder = Group(name, transform, transform.InverseTransformPoint(position) + Vector3.up * 0.035f);
        MeshRenderer fill = Disk(holder, "Marker fill", Vector3.zero, 0.73f, new Color32(106, 172, 178, 255));
        MeshRenderer rim = Circle(holder, "Marker rim", Vector3.up * 0.002f, 0.77f, Cream);
        MeshRenderer focus = Circle(holder, "Marker focus", Vector3.up * 0.004f, 0.94f, Amber);
        Transform tick = Group("Received a pass", holder, new Vector3(0f, 0.01f, -0.60f));
        Stroke(tick, new Vector3(-0.11f, 0f, -0.02f), 0.20f, 125f, Cream);
        Stroke(tick, new Vector3(0.105f, 0f, 0.03f), 0.33f, 52f, Cream);

        return new PlaygroundMarker(holder.gameObject, fill, rim, focus, tick.gameObject, this);
    }

    public Transform CreateBall()
    {
        Transform ball = Group("Playground ball", transform, Vector3.zero);
        if (!PlaceProp(ball, "PlaygroundBall", new Vector3(0.30f, 0.30f, 0.30f), centerVertically: true))
        {
            Part(ball, PrimitiveType.Sphere, "Coral rubber", Vector3.zero, Vector3.one * 0.30f, Coral);
            Transform stripe = Group("Cream stripe", ball, Vector3.zero);
            stripe.localRotation = Quaternion.Euler(20f, 0f, 26f);
            Part(stripe, PrimitiveType.Sphere, "Cream band", Vector3.zero,
                new Vector3(0.304f, 0.112f, 0.304f), Cream);
            Part(stripe, PrimitiveType.Sphere, "Teal cap", new Vector3(0f, 0.105f, 0f),
                new Vector3(0.19f, 0.095f, 0.19f), Teal);
        }
        return ball;
    }

    Vector3 World(float x, float z) => transform.TransformPoint(new Vector3(x, FloorY, z));

    void Build()
    {
        Color meadow = new Color32(109, 160, 117, 255);
        Color darkMeadow = new Color32(95, 142, 102, 255);
        Color sandstone = new Color32(236, 213, 177, 255);
        Color floor = new Color32(224, 199, 161, 255);
        Color court = new Color32(111, 181, 193, 255);

        Part(transform, PrimitiveType.Cube, "Meadow backdrop", new Vector3(0f, -0.62f, 26f),
            new Vector3(150f, 0.6f, 142f), darkMeadow);
        Part(transform, PrimitiveType.Cube, "Play garden turf", new Vector3(0f, -0.24f, 0.2f),
            new Vector3(41f, 0.23f, 36f), meadow);
        Part(transform, PrimitiveType.Cube, "Ground slab", new Vector3(0f, FloorY - 0.15f, -0.5f),
            new Vector3(28.6f, 0.30f, 24.4f), floor, collision: true);

        // Painted court pieces stay above the shared flat collider.
        Disk(transform, "Court coral apron", CourtCenter + Vector3.up * 0.003f, 6.35f, Coral);
        Disk(transform, "Court ivory border", CourtCenter + Vector3.up * 0.005f, 6.04f, Cream);
        Disk(transform, "Court blue surface", CourtCenter + Vector3.up * 0.008f, 5.92f, court);
        Circle(transform, "Court interior line", CourtCenter + Vector3.up * 0.010f, 5.56f,
            new Color32(179, 221, 215, 255));
        Circle(transform, "Center circle", CourtCenter + Vector3.up * 0.012f, 1.18f,
            new Color32(175, 219, 215, 255));

        Transform paths = Group("Play garden paths", transform, Vector3.zero);
        Part(paths, PrimitiveType.Cube, "Entrance paving", new Vector3(0f, FloorY + 0.002f, -8.2f),
            new Vector3(4.3f, 0.004f, 5.1f), sandstone);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(paths, PrimitiveType.Cube, "Garden border", new Vector3(side * 13.25f, FloorY + 0.005f, -0.35f),
                new Vector3(0.25f, 0.01f, 23.2f), sandstone);
            Part(paths, PrimitiveType.Cube, "Entrance trim", new Vector3(side * 2.11f, FloorY + 0.011f, -8.2f),
                new Vector3(0.082f, 0.012f, 5.1f), Amber);
            Wall(new Vector3(side * 14.2f, FloorY, 0f), 22.8f, 90f, Cream);
            Wall(new Vector3(side * 8.55f, FloorY, -11.1f), 10.8f, 0f, Cream);
            Wall(new Vector3(side * 8.8f, FloorY, 11.3f), 10.3f, 0f, Cream);
        }

        BuildClimber();
        BuildShade();
        BuildBench(new Vector3(-8.0f, FloorY, -4.9f), 28f);
        BuildBench(new Vector3(9.1f, FloorY, 5.2f), 180f);
        BuildBench(new Vector3(8.4f, FloorY, -6.7f), -33f);

        foreach (Vector2 point in new[]
        {
            new Vector2(-3.25f, -9.9f), new Vector2(3.25f, -9.9f), new Vector2(-11.8f, -6.7f),
            new Vector2(11.3f, -7.5f), new Vector2(-11.7f, 0.2f), new Vector2(11.6f, 0.4f),
            new Vector2(-4.4f, 8.25f), new Vector2(4.25f, 8.3f), new Vector2(11.6f, 8.9f)
        })
            BuildPlanter(new Vector3(point.x, FloorY, point.y));

        BuildTrees(meadow);
        BuildBanner();
    }

    void BuildClimber()
    {
        Transform mount = Group("Climbing equipment", transform, new Vector3(-8.6f, FloorY, 6.8f), 24f);
        if (PlaceProp(mount, "PlaygroundClimber", new Vector3(6.0f, 3.95f, 4.8f)))
        {
            AddCollision(mount, new Vector3(0f, 0.85f, 0f), new Vector3(4.1f, 1.7f, 2.7f));
            return;
        }

        Color wood = new Color32(178, 115, 72, 255);
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(mount, PrimitiveType.Cube, "Tower column", new Vector3(x * 0.95f, 1.3f, z * 0.9f),
                new Vector3(0.21f, 2.6f, 0.21f), wood);

        Part(mount, PrimitiveType.Cube, "Platform", new Vector3(0f, 1.39f, 0f),
            new Vector3(2.12f, 0.20f, 2.02f), Cream);
        Part(mount, PrimitiveType.Cube, "Canopy", new Vector3(0f, 2.67f, 0f),
            new Vector3(2.40f, 0.16f, 2.25f), Teal);
        for (int i = 0; i < 5; i++)
            Part(mount, PrimitiveType.Cube, "Climbing step", new Vector3(-0.05f, 0.32f + i * 0.25f, -1.9f + i * 0.16f),
                new Vector3(1.10f, 0.11f, 0.31f), wood);

        var slide = Group("Slide", mount, new Vector3(1.72f, 0.77f, 0.21f), 10f);
        slide.localRotation *= Quaternion.Euler(0f, 0f, -29f);
        Part(slide, PrimitiveType.Cube, "Slide bed", Vector3.zero, new Vector3(1.98f, 0.14f, 0.84f), Amber);
        Part(slide, PrimitiveType.Cube, "Slide rail", new Vector3(0f, 0.11f, -0.42f),
            new Vector3(2.01f, 0.23f, 0.12f), Amber);
        Part(slide, PrimitiveType.Cube, "Slide rail", new Vector3(0f, 0.11f, 0.42f),
            new Vector3(2.01f, 0.23f, 0.12f), Amber);
        AddCollision(mount, new Vector3(0.20f, 0.82f, 0f), new Vector3(3.7f, 1.65f, 2.65f));
    }

    void BuildShade()
    {
        Transform shade = Group("Garden awning", transform, new Vector3(9.15f, FloorY, 6.4f), -9f);
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(shade, PrimitiveType.Cylinder, "Awning post", new Vector3(x * 2.27f, 1.49f, z * 1.65f),
                new Vector3(0.22f, 1.49f, 0.22f), Navy);

        for (int i = 0; i < 7; i++)
        {
            var slat = Part(shade, PrimitiveType.Cube, "Striped canvas",
                new Vector3((i - 3) * 0.72f, 3.02f, 0f), new Vector3(0.74f, 0.12f, 3.7f),
                i % 2 == 0 ? Teal : Cream);
            slat.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
        }

        Disk(shade, "Shade paving", Vector3.up * 0.005f, 2.55f, new Color32(202, 179, 147, 255));
    }

    void BuildBench(Vector3 point, float yaw)
    {
        Transform bench = Group("Garden bench", transform, point, yaw);
        if (!PlaceProp(bench, "PlaygroundBench", new Vector3(2.20f, 1.05f, 0.90f)))
        {
            Part(bench, PrimitiveType.Cube, "Seat", new Vector3(0f, 0.43f, 0f),
                new Vector3(1.94f, 0.11f, 0.66f), Teal);
            Part(bench, PrimitiveType.Cube, "Back", new Vector3(0f, 0.69f, -0.25f),
                new Vector3(1.94f, 0.34f, 0.13f), Coral);
            for (int side = -1; side <= 1; side += 2)
                Part(bench, PrimitiveType.Cube, "Foot", new Vector3(side * 0.71f, 0.22f, 0f),
                    new Vector3(0.12f, 0.44f, 0.55f), Navy);
        }
        AddCollision(bench, new Vector3(0f, 0.30f, 0f), new Vector3(1.9f, 0.62f, 0.63f));
    }

    void BuildPlanter(Vector3 point)
    {
        Transform planter = Group("Flower pot", transform, point, point.x * 14f);
        if (!PlaceProp(planter, "PlaygroundPlanter", new Vector3(1.18f, 1.03f, 1.18f)))
        {
            Part(planter, PrimitiveType.Cylinder, "Clay pot", new Vector3(0f, 0.22f, 0f),
                new Vector3(0.78f, 0.23f, 0.78f), Coral);
            Part(planter, PrimitiveType.Cylinder, "Pot rim", new Vector3(0f, 0.42f, 0f),
                new Vector3(0.87f, 0.07f, 0.87f), Coral);
            Part(planter, PrimitiveType.Sphere, "Leaves", new Vector3(0f, 0.50f, 0f),
                new Vector3(0.79f, 0.45f, 0.79f), new Color32(92, 147, 89, 255));
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                Part(planter, PrimitiveType.Sphere, "Flower",
                    new Vector3(Mathf.Cos(angle) * 0.24f, 0.69f, Mathf.Sin(angle) * 0.24f),
                    new Vector3(0.19f, 0.17f, 0.19f), i % 2 == 0 ? Amber : Cream);
            }
        }
        AddCollision(planter, new Vector3(0f, 0.28f, 0f), new Vector3(0.78f, 0.56f, 0.78f));
    }

    void BuildBanner()
    {
        Transform sign = Group("Playground entrance sign", transform, new Vector3(4.6f, FloorY, -10.3f));
        Part(sign, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.73f, 0f),
            new Vector3(0.13f, 0.74f, 0.13f), Navy);
        Part(sign, PrimitiveType.Cube, "Teal frame", new Vector3(0f, 1.51f, 0f),
            new Vector3(2.38f, 0.54f, 0.13f), Teal);
        Part(sign, PrimitiveType.Cube, "Painted sign", new Vector3(0f, 1.51f, -0.075f),
            new Vector3(2.26f, 0.42f, 0.034f), Cream);

        Font font = GameLanguage.FontFor(Resources.Load<Font>("Fonts/LiberationSans-Bold"));
        if (font == null)
            return;

        var lettering = Group("Entrance words", sign, new Vector3(0f, 1.52f, -0.097f))
            .gameObject.AddComponent<TextMesh>();
        lettering.font = font;
        lettering.text = GameLanguage.Text("PLAY TOGETHER");
        lettering.color = Navy;
        lettering.fontSize = 56;
        lettering.characterSize = 0.052f;
        lettering.anchor = TextAnchor.MiddleCenter;
        lettering.alignment = TextAlignment.Center;
        var renderer = lettering.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font.material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    void BuildTrees(Color meadow)
    {
        GameObject roundTree = GameObject.Find("StreetLayout/Trees/TreeThree 02");
        GameObject tallTree = GameObject.Find("StreetLayout/Trees/TreeFive 01");
        var positions = new[]
        {
            new Vector2(-17f, -11.0f), new Vector2(18f, -9f), new Vector2(-19f, -3f),
            new Vector2(17f, -0.7f), new Vector2(-17.3f, 5.2f), new Vector2(17.8f, 6.8f),
            new Vector2(-15.2f, 13.7f), new Vector2(-8.0f, 16.4f), new Vector2(-0.9f, 15.3f),
            new Vector2(7.2f, 17f), new Vector2(15.8f, 14.1f), new Vector2(23f, 18f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            Vector2 point = positions[i];
            float height = 5.25f + (i % 3) * 0.62f;
            Transform tree = Group("Parks tree", transform, new Vector3(point.x, -0.09f, point.y), i * 73f);
            if (PlaceVisual(tree, i % 3 == 1 ? tallTree : roundTree, new Vector3(7f, height, 7f), false))
                continue;

            Part(tree, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, height * 0.22f, 0f),
                new Vector3(0.53f, height * 0.22f, 0.53f), new Color32(140, 98, 66, 255));
            Part(tree, PrimitiveType.Sphere, "Crown", new Vector3(0f, height * 0.67f, 0f),
                new Vector3(height * 0.65f, height * 0.62f, height * 0.64f), meadow);
        }
    }

    void Wall(Vector3 position, float width, float yaw, Color pale)
    {
        Transform wall = Group("Park fence", transform, position, yaw);
        AddCollision(wall, new Vector3(0f, 0.40f, 0f), new Vector3(width, 0.80f, 0.25f));
        Part(wall, PrimitiveType.Cube, "Stone footing", new Vector3(0f, 0.14f, 0f),
            new Vector3(width, 0.28f, 0.37f), pale);
        Part(wall, PrimitiveType.Cube, "Top rail", new Vector3(0f, 0.78f, 0f),
            new Vector3(width, 0.083f, 0.083f), Navy);

        int sections = Mathf.CeilToInt(width / 0.82f);
        for (int i = 0; i <= sections; i++)
            Part(wall, PrimitiveType.Cube, "Rail picket", new Vector3(width * (i / (float)sections - 0.5f), 0.52f, 0f),
                new Vector3(0.058f, 0.51f, 0.058f), Navy);
    }

    bool PlaceProp(Transform holder, string asset, Vector3 limits, bool centerVertically = false) =>
        PlaceVisual(holder, Resources.Load<GameObject>(ResourcePath + asset), limits, centerVertically);

    static bool PlaceVisual(Transform holder, GameObject source, Vector3 limits, bool centerVertically)
    {
        if (source == null)
            return false;

        var clone = Instantiate(source, holder, false);
        clone.name = source.name;
        clone.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        clone.transform.localScale = Vector3.one;
        if (!TryMeasure(holder, clone, out Bounds bounds) ||
            Mathf.Min(bounds.size.x, bounds.size.y, bounds.size.z) <= 0.00001f)
        {
            clone.SetActive(false);
            Destroy(clone);
            return false;
        }

        float scale = Mathf.Min(limits.x / bounds.size.x,
            limits.y / bounds.size.y, limits.z / bounds.size.z);
        clone.transform.localScale = Vector3.one * scale;
        clone.transform.localPosition = -new Vector3(bounds.center.x, centerVertically ? bounds.center.y : bounds.min.y,
            bounds.center.z) * scale;
        foreach (Collider item in clone.GetComponentsInChildren<Collider>())
            item.enabled = false;
        return true;
    }

    internal static bool TryMeasure(Transform relative, GameObject model, out Bounds bounds)
    {
        bounds = default;
        bool initialized = false;
        Matrix4x4 inverse = relative.worldToLocalMatrix;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
                continue;

            Bounds box = renderer.localBounds;
            Matrix4x4 convert = inverse * renderer.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 vertex = convert.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? box.min.x : box.max.x,
                    (i & 2) == 0 ? box.min.y : box.max.y,
                    (i & 4) == 0 ? box.min.z : box.max.z));
                if (initialized)
                    bounds.Encapsulate(vertex);
                else
                {
                    bounds = new Bounds(vertex, Vector3.zero);
                    initialized = true;
                }
            }
        }
        return initialized;
    }

    Material Paint(Color color)
    {
        if (paints.TryGetValue(color, out Material existing))
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { color = color, name = "Play garden paint" };
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.18f);
        paints.Add(color, material);
        return material;
    }

    void OnDestroy()
    {
        foreach (Material material in paints.Values)
            Destroy(material);
        foreach (Mesh mesh in ownedMeshes)
            Destroy(mesh);
    }

    static Transform Group(string label, Transform parent, Vector3 localPosition, float yaw = 0f)
    {
        Transform group = new GameObject(label).transform;
        group.SetParent(parent, false);
        group.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));
        return group;
    }

    static void AddCollision(Transform parent, Vector3 center, Vector3 size)
    {
        BoxCollider collider = parent.gameObject.AddComponent<BoxCollider>();
        collider.center = center;
        collider.size = size;
    }

    GameObject Part(Transform parent, PrimitiveType type, string label, Vector3 point,
        Vector3 scale, Color color, bool collision = false)
    {
        Mesh shape = type switch
        {
            PrimitiveType.Cylinder => cylinder ??= Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"),
            PrimitiveType.Sphere => sphere ??= Resources.GetBuiltinResource<Mesh>("Sphere.fbx"),
            _ => cube ??= Resources.GetBuiltinResource<Mesh>("Cube.fbx")
        };
        var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        item.transform.SetParent(parent, false);
        item.transform.localPosition = point;
        item.transform.localScale = scale;
        item.GetComponent<MeshFilter>().sharedMesh = shape;
        var renderer = item.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = Paint(color);
        if (scale.y <= 0.015f)
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (collision)
            item.AddComponent<BoxCollider>();
        return item;
    }

    void Stroke(Transform parent, Vector3 point, float length, float yaw, Color color)
    {
        GameObject stroke = Part(parent, PrimitiveType.Cube, "Check mark stroke", point,
            new Vector3(0.058f, 0.009f, length), color);
        stroke.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    MeshRenderer Disk(Transform parent, string label, Vector3 point, float radius, Color color) =>
        Marking(parent, label, point, radius, diskMesh, color);

    MeshRenderer Circle(Transform parent, string label, Vector3 point, float radius, Color color) =>
        Marking(parent, label, point, radius, ringMesh, color);

    MeshRenderer Marking(Transform parent, string label, Vector3 point, float radius, Mesh mesh, Color color)
    {
        Transform holder = Group(label, parent, point);
        holder.localScale = new Vector3(radius, 1f, radius);
        holder.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = holder.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Paint(color);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return renderer;
    }

    Mesh MakeDisk(string name, float innerRadius)
    {
        const int segments = 64;
        var vertices = new Vector3[(segments + 1) * 2];
        var indices = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * (2f * Mathf.PI / segments);
            Vector3 point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i * 2] = point;
            vertices[i * 2 + 1] = point * innerRadius;
            if (i == segments)
                continue;

            int v = i * 2, t = i * 6;
            indices[t] = v;
            indices[t + 1] = v + 1;
            indices[t + 2] = v + 2;
            indices[t + 3] = v + 1;
            indices[t + 4] = v + 3;
            indices[t + 5] = v + 2;
        }

        var mesh = new Mesh { name = name, vertices = vertices, triangles = indices };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        ownedMeshes.Add(mesh);
        return mesh;
    }

    internal sealed class PlaygroundMarker
    {
        static readonly Color WaitingFill = new Color32(98, 157, 170, 255);
        static readonly Color ServedFill = new Color32(78, 132, 98, 255);
        static readonly Color ServedRim = new Color32(224, 246, 199, 255);

        readonly GameObject holder;
        readonly MeshRenderer fill;
        readonly MeshRenderer rim;
        readonly MeshRenderer focus;
        readonly GameObject tick;
        readonly PlaygroundLayout layout;

        public PlaygroundMarker(GameObject holder, MeshRenderer fill, MeshRenderer rim,
            MeshRenderer focus, GameObject tick, PlaygroundLayout layout)
        {
            this.holder = holder;
            this.fill = fill;
            this.rim = rim;
            this.focus = focus;
            this.tick = tick;
            this.layout = layout;
            Set(false, false);
        }

        public void Show(bool show) => holder.SetActive(show);

        public void Set(bool received, bool highlighted)
        {
            fill.sharedMaterial = layout.Paint(received ? ServedFill : WaitingFill);
            rim.sharedMaterial = layout.Paint(received ? ServedRim : Cream);
            focus.gameObject.SetActive(highlighted);
            tick.SetActive(received);
        }
    }
}
