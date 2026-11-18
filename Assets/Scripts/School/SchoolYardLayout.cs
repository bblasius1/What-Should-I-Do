using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds the school grounds for the help lesson and supplies its playable landmarks.</summary>
public sealed class SchoolYardLayout : MonoBehaviour
{
    const float PavingY = 0.12f;
    const float GateZ = -9.5f;
    const string ResourceFolder = "SchoolYard/";
    static readonly Vector3 Origin = new Vector3(-190f, 0f, 0f);

    readonly List<Material> materials = new List<Material>();

    public Transform Root => transform;
    public Vector3 SpawnPoint => World(0f, -10.6f);
    public Vector3 TeacherPosition => World(5.6f, 7.6f);
    public Vector3 StudentPosition => World(-7.36f, -6.4f);
    public Vector3 OtherPersonPosition => World(5.8f, -3f);
    public Bounds WalkableBounds =>
        new Bounds(transform.TransformPoint(new Vector3(0f, 1f, -1.6f)), new Vector3(24.8f, 2f, 22f));

    public static SchoolYardLayout Create(Transform parent)
    {
        var yard = new GameObject("School courtyard").AddComponent<SchoolYardLayout>();
        yard.transform.SetParent(parent, false);
        yard.transform.SetPositionAndRotation(Origin, Quaternion.identity);
        yard.Build();
        return yard;
    }

    Vector3 World(float x, float z) => transform.TransformPoint(new Vector3(x, PavingY, z));

    void Build()
    {
        var paleSand = Paint("Paving", new Color32(218, 205, 174, 255));
        var cream = Paint("Light limestone", new Color32(241, 225, 191, 255));
        var border = Paint("Warm paving joints", new Color32(189, 169, 136, 255));
        var turf = Paint("Campus lawn", new Color32(109, 154, 96, 255));
        var hedge = Paint("Border shrubs", new Color32(75, 134, 94, 255));
        var teal = Paint("School teal", new Color32(42, 94, 91, 255));
        var blue = Paint("Window glass", new Color32(122, 167, 183, 255));
        var ochre = Paint("School roof", new Color32(213, 113, 77, 255));
        var cedar = Paint("Cedar", new Color32(178, 111, 65, 255));
        var clay = Paint("Terracotta", new Color32(177, 90, 65, 255));
        var leaf = Paint("Shrub leaves", new Color32(70, 128, 86, 255));
        var petal = Paint("Coral flowers", new Color32(228, 135, 103, 255));

        BuildGround(paleSand, cream, border, turf);
        BuildBorders(cream, border, teal, hedge);
        BuildSchool(cream, teal, blue, ochre);
        BuildGate(cream, teal);
        BuildBench(new Vector3(-7.5f, PavingY, -6.8f), 90f, cedar, teal);
        BuildBench(new Vector3(-9.4f, PavingY, 3.6f), 90f, cedar, teal);
        BuildBench(new Vector3(7.3f, PavingY, 7.6f), 180f, cedar, teal);
        BuildBicycleRack();

        var planterPositions = new[]
        {
            new Vector2(-3.1f, -8.6f), new Vector2(3.1f, -8.6f),
            new Vector2(-4.7f, 1.6f), new Vector2(4.7f, 1.6f),
            new Vector2(-9.6f, 6.1f), new Vector2(7.6f, -3.3f),
            new Vector2(-5.6f, 6.3f), new Vector2(8.3f, 6.1f)
        };
        foreach (Vector2 position in planterPositions)
            BuildPlanter(new Vector3(position.x, PavingY, position.y), clay, leaf, cream, petal);

        BuildTrees(cedar, hedge, turf);
        Wayfinding("SCHOOL", new Vector3(3.9f, PavingY, -9.95f), 1.85f, cream, teal);
        Wayfinding("SCHOOL OFFICE", new Vector3(5.6f, PavingY, 8.8f), 2.65f, cream, teal);
    }

    void BuildGround(Material paving, Material cream, Material joints, Material grass)
    {
        var ground = Part(transform, PrimitiveType.Cube, "Campus lawn", new Vector3(0f, -0.36f, 14f),
            new Vector3(120f, 0.5f, 125f), grass);
        ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Part(transform, PrimitiveType.Cube, "Courtyard paving", new Vector3(0f, PavingY - 0.15f, -1.6f),
            new Vector3(25.5f, 0.3f, 22.55f), paving, solid: true);

        var inlays = Group("Paving inlays", transform);
        float top = PavingY + 0.002f;
        Part(inlays, PrimitiveType.Cube, "Main path", new Vector3(0f, top, -1.5f),
            new Vector3(4.4f, 0.002f, 22.6f), cream);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(inlays, PrimitiveType.Cube, "Path border", new Vector3(side * 2.2f, top + 0.001f, -1.55f),
                new Vector3(0.075f, 0.004f, 22.4f), joints);
            Part(inlays, PrimitiveType.Cube, "Garden edge", new Vector3(side * 10.8f, top, -0.15f),
                new Vector3(0.14f, 0.003f, 19.6f), joints);
        }

        for (float z = -11f; z <= 9f; z += 2f)
            Part(inlays, PrimitiveType.Cube, "Tile joint", new Vector3(0f, top + 0.001f, z),
                new Vector3(24.9f, 0.003f, 0.028f), joints);

        Part(inlays, PrimitiveType.Cube, "Front bench paving", new Vector3(-7.65f, top, -6.8f),
            new Vector3(2.75f, 0.002f, 3.2f), cream);
        Part(inlays, PrimitiveType.Cube, "School office paving", new Vector3(6.45f, top, 7.6f),
            new Vector3(4.8f, 0.002f, 3.0f), cream);
    }

    void BuildBorders(Material stone, Material cap, Material rail, Material hedge)
    {
        var walls = Group("Courtyard borders", transform);
        for (int side = -1; side <= 1; side += 2)
        {
            Fence(walls, new Vector3(side * 12.4f, PavingY, 0.05f), 19.4f, 90f, stone, cap, rail);
            Fence(walls, new Vector3(side * 7.2f, PavingY, GateZ), 10.4f, 0f, stone, cap, rail);
            for (int i = 0; i < 9; i++)
            {
                float z = -8.1f + i * 2.1f;
                Part(walls, PrimitiveType.Sphere, "Soft green hedge", new Vector3(side * 13.65f, 0.5f, z),
                    new Vector3(1.9f, 1.35f, 2.85f), hedge);
            }
        }
    }

    void Fence(Transform parent, Vector3 position, float width, float yaw, Material stone, Material cap, Material rail)
    {
        var fence = Group("Low courtyard fence", parent, position, yaw);
        Part(fence, PrimitiveType.Cube, "Stone base", new Vector3(0f, 0.25f, 0f),
            new Vector3(width, 0.5f, 0.30f), stone, solid: true);
        Part(fence, PrimitiveType.Cube, "Coping", new Vector3(0f, 0.53f, 0f),
            new Vector3(width, 0.11f, 0.39f), cap);
        Part(fence, PrimitiveType.Cube, "Upper rail", new Vector3(0f, 0.96f, 0f),
            new Vector3(width, 0.08f, 0.075f), rail);
        int sections = Mathf.CeilToInt(width / 0.7f);
        for (int i = 0; i <= sections; i++)
            Part(fence, PrimitiveType.Cube, "Baluster", new Vector3(width * (i / (float)sections - 0.5f), 0.77f, 0f),
                new Vector3(0.065f, 0.43f, 0.065f), rail);
    }

    void BuildSchool(Material cream, Material teal, Material blue, Material ochre)
    {
        GameObject source = GameObject.Find("StreetLayout/Buildings/School");
        var school = Group("School building", transform, new Vector3(0f, PavingY, 9.95f), 180f);
        if (SceneModel.TryPlace(school, source, new Vector3(26f, 10.7f, 20f), alignFront: true))
            return;

        Part(school, PrimitiveType.Cube, "School walls", new Vector3(0f, 2.65f, -2f),
            new Vector3(22f, 5.3f, 4f), cream);
        Part(school, PrimitiveType.Cube, "Roof fascia", new Vector3(0f, 5.18f, -2f),
            new Vector3(22.8f, 0.5f, 4.5f), ochre);
        Part(school, PrimitiveType.Cube, "School entrance", new Vector3(0f, 1.4f, 0.03f),
            new Vector3(2.8f, 2.8f, 0.09f), teal);
        for (float x = -8.4f; x <= 8.5f; x += 4.2f)
        {
            Part(school, PrimitiveType.Cube, "Window trim", new Vector3(x, 3.86f, 0.05f),
                new Vector3(2.1f, 1.45f, 0.14f), teal);
            Part(school, PrimitiveType.Cube, "Window", new Vector3(x, 3.86f, 0.128f),
                new Vector3(1.9f, 1.23f, 0.035f), blue);
        }
    }

    void BuildGate(Material stone, Material paint)
    {
        var gate = Group("School gate", transform, new Vector3(0f, PavingY, GateZ), 180f);
        // Only the columns are solid. The player must always be able to walk through the gate.
        var prefab = Resources.Load<GameObject>(ResourceFolder + "SchoolGate");
        bool hasVisual = SceneModel.TryPlace(gate, prefab, new Vector3(3.88f, 2.95f, 1.1f), fitWideProp: true);
        for (int side = -1; side <= 1; side += 2)
        {
            if (hasVisual)
            {
                float x = side * 1.42f;
                var column = Group("Gate column collision", gate, new Vector3(x, 1.19f, 0f));
                column.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.95f, 2.36f, 0.76f);
            }
            else
            {
                float x = side * 1.78f;
                Part(gate, PrimitiveType.Cube, "Gate pier", new Vector3(x, 1.26f, 0f),
                    new Vector3(0.32f, 2.52f, 0.42f), stone, solid: true);
                Part(gate, PrimitiveType.Cube, "Pier cap", new Vector3(x, 2.55f, 0f),
                    new Vector3(0.46f, 0.16f, 0.53f), paint);
                Part(gate, PrimitiveType.Cube, "Pier foot", new Vector3(x, 0.12f, 0f),
                    new Vector3(0.46f, 0.24f, 0.54f), paint);
            }
        }

        if (!hasVisual)
        {
            Part(gate, PrimitiveType.Cube, "Open gateway lintel", new Vector3(0f, 2.80f, 0f),
                new Vector3(3.96f, 0.34f, 0.47f), paint);
            Part(gate, PrimitiveType.Cube, "Lintel trim", new Vector3(0f, 2.97f, 0f),
                new Vector3(4.08f, 0.09f, 0.55f), stone);
        }
    }

    void BuildBench(Vector3 position, float yaw, Material cedar, Material legs)
    {
        var bench = Group("Courtyard bench", transform, position, yaw);
        if (!SceneModel.TryPlace(bench, Resources.Load<GameObject>(ResourceFolder + "SchoolBench"),
            new Vector3(1.93f, 0.90f, 0.72f), fitWideProp: true))
        {
            for (int i = 0; i < 4; i++)
            {
                Part(bench, PrimitiveType.Cube, "Seat slat", new Vector3(0f, 0.42f, (i - 1.5f) * 0.14f),
                    new Vector3(1.90f, 0.08f, 0.128f), cedar);
                Part(bench, PrimitiveType.Cube, "Backrest slat", new Vector3(0f, 0.52f + i * 0.10f, -0.29f),
                    new Vector3(1.90f, 0.085f, 0.075f), cedar);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Part(bench, PrimitiveType.Cube, "Back post", new Vector3(side * 0.76f, 0.43f, -0.33f),
                    new Vector3(0.10f, 0.86f, 0.10f), legs);
                Part(bench, PrimitiveType.Cube, "Front foot", new Vector3(side * 0.76f, 0.19f, 0.22f),
                    new Vector3(0.11f, 0.38f, 0.11f), legs);
                Part(bench, PrimitiveType.Cube, "Support", new Vector3(side * 0.76f, 0.37f, -0.06f),
                    new Vector3(0.10f, 0.10f, 0.62f), legs);
            }
        }

        var seat = Group("Seat collision", bench, new Vector3(0f, 0.33f, 0f));
        seat.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.86f, 0.66f, 0.63f);
    }

    void BuildBicycleRack()
    {
        var prefab = Resources.Load<GameObject>(ResourceFolder + "BicycleRack");
        if (prefab == null)
            return;

        var rack = Group("Bicycle rack", transform, new Vector3(-8.9f, PavingY, 8.15f), 180f);
        if (!SceneModel.TryPlace(rack, prefab, new Vector3(2.5f, 1.25f, 1.9f)))
        {
            Destroy(rack.gameObject);
            return;
        }

        var collider = rack.gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(1.70f, 1.15f, 1.65f);
        collider.center = new Vector3(0f, 0.575f, 0f);
    }

    void BuildPlanter(Vector3 position, Material terracotta, Material leaves, Material cream, Material coral)
    {
        var planter = Group("Courtyard planter", transform, position, 28f);
        if (!SceneModel.TryPlace(planter, Resources.Load<GameObject>(ResourceFolder + "SchoolPlanter"),
            new Vector3(0.85f, 0.83f, 0.85f)))
        {
            Part(planter, PrimitiveType.Cylinder, "Pot", new Vector3(0f, 0.18f, 0f),
                new Vector3(0.65f, 0.18f, 0.65f), terracotta);
            Part(planter, PrimitiveType.Cylinder, "Rim", new Vector3(0f, 0.35f, 0f),
                new Vector3(0.80f, 0.06f, 0.80f), terracotta);
            Part(planter, PrimitiveType.Sphere, "Shrub", new Vector3(0f, 0.51f, 0f),
                new Vector3(0.72f, 0.46f, 0.72f), leaves);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 0.4f;
                Part(planter, PrimitiveType.Sphere, "Flower",
                    new Vector3(Mathf.Cos(angle) * 0.22f, 0.66f, Mathf.Sin(angle) * 0.22f),
                    new Vector3(0.15f, 0.105f, 0.15f), i % 2 == 0 ? coral : cream);
            }
        }

        var pot = Group("Pot collision", planter, new Vector3(0f, 0.28f, 0f));
        pot.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.66f, 0.56f, 0.66f);
    }

    void BuildTrees(Material bark, Material leaves, Material brightLeaves)
    {
        GameObject courtyardTree = GameObject.Find("StreetLayout/Trees/TreeThree 02");
        GameObject gateTree = GameObject.Find("StreetLayout/Trees/TreeFive 01");
        var positions = new[]
        {
            new Vector2(-14.5f, -7.5f), new Vector2(14.5f, -5.5f),
            new Vector2(-15.6f, 2.3f), new Vector2(14f, 6.6f),
            new Vector2(-16.1f, 13f), new Vector2(14.3f, 14.2f),
            new Vector2(-13f, 26f), new Vector2(12f, 24.8f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            float height = 5.8f + i % 3 * 0.7f;
            var tree = Group("Boundary tree", transform, new Vector3(positions[i].x, -0.10f, positions[i].y), i * 81f);
            if (SceneModel.TryPlace(tree, i % 2 == 0 ? courtyardTree : gateTree, new Vector3(8f, height, 8f)))
                continue;

            Part(tree, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, height * 0.27f, 0f),
                new Vector3(0.54f, height * 0.27f, 0.54f), bark);
            Part(tree, PrimitiveType.Sphere, "Crown", new Vector3(0f, height * 0.70f, 0f),
                new Vector3(height * 0.64f, height * 0.64f, height * 0.61f), i % 2 == 0 ? leaves : brightLeaves);
        }
    }

    void Wayfinding(string message, Vector3 position, float width, Material boardMaterial, Material postMaterial)
    {
        var sign = Group(message, transform, position);
        Part(sign, PrimitiveType.Cube, "Signpost", new Vector3(0f, 0.68f, 0f),
            new Vector3(0.10f, 1.36f, 0.13f), postMaterial);
        Part(sign, PrimitiveType.Cube, "Trim", new Vector3(0f, 1.44f, 0f),
            new Vector3(width + 0.09f, 0.49f, 0.13f), postMaterial);
        Part(sign, PrimitiveType.Cube, "Sign face", new Vector3(0f, 1.44f, -0.074f),
            new Vector3(width, 0.40f, 0.034f), boardMaterial);

        Font font = GameLanguage.FontFor(Resources.Load<Font>("Fonts/LiberationSans-Bold"));
        if (font == null)
            return;

        Transform textObject = Group("Text", sign, new Vector3(0f, 1.44f, -0.094f));
        var label = textObject.gameObject.AddComponent<TextMesh>();
        label.text = GameLanguage.Text(message);
        label.font = font;
        label.color = new Color32(25, 59, 56, 255);
        label.fontStyle = FontStyle.Normal;
        label.fontSize = 48;
        label.characterSize = message.Length > 8 ? 0.064f : 0.076f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        var renderer = label.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font.material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    Material Paint(string name, Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
        {
            name = "School courtyard · " + name,
            color = color
        };
        material.SetFloat("_Smoothness", 0.14f);
        materials.Add(material);
        return material;
    }

    static Transform Group(string name, Transform parent, Vector3 position = default, float yaw = 0f)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return group;
    }

    static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 size,
        Material material, bool solid = false)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = solid;
        if (!solid)
            Destroy(collider);
        return part;
    }

    void OnDestroy()
    {
        foreach (Material material in materials)
            Destroy(material);
    }
}
