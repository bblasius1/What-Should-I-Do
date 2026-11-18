using System.Collections.Generic;
using UnityEngine;

public enum VehicleKind
{
    SmallCar,
    MediumCar,
    Truck
}

public static class VehicleVisuals
{
    static readonly VehicleMaterials Paint = new();
    static Mesh roundedBox;
    static Mesh cylinder;

    public static Vector3 GetDimensions(VehicleKind kind) => kind switch
    {
        VehicleKind.SmallCar => new Vector3(1.55f, 1.42f, 3.3f),
        VehicleKind.MediumCar => new Vector3(1.8f, 1.55f, 4.3f),
        _ => new Vector3(2.1f, 2.5f, 5.8f)
    };

    public static void Create(Transform parent, VehicleKind kind)
    {
        Vector3 dimensions = GetDimensions(kind);
        // glTFast imports these assets as GameObjects.
        var prefab = Resources.Load<GameObject>($"Vehicles/{kind}");
        if (prefab != null && TryCreateImported(parent, prefab, kind, dimensions))
            return;

        var exterior = new GameObject("Vehicle exterior").transform;
        exterior.SetParent(parent, false);
        if (kind == VehicleKind.Truck)
            BuildTruck(exterior, dimensions);
        else
            BuildCar(exterior, kind == VehicleKind.SmallCar, dimensions);
    }

    static bool TryCreateImported(Transform parent, GameObject prefab, VehicleKind kind, Vector3 dimensions)
    {
        var exterior = new GameObject("Vehicle exterior").transform;
        exterior.SetParent(parent, false);
        var model = Object.Instantiate(prefab, exterior, false);
        // The hatchback and truck import with their noses along +X. Traffic moves along local +Z.
        if (kind is VehicleKind.SmallCar or VehicleKind.Truck)
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * model.transform.localRotation;
        if (!TryGetBounds(exterior, model, out Bounds bounds) ||
            bounds.size.x <= 0f || bounds.size.y <= 0f || bounds.size.z <= 0f)
        {
            exterior.gameObject.SetActive(false);
            Object.Destroy(exterior.gameObject);
            return false;
        }

        float scale = Mathf.Min(dimensions.x / bounds.size.x,
            Mathf.Min(dimensions.y / bounds.size.y, dimensions.z / bounds.size.z));
        Vector3 groundCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        exterior.localScale = Vector3.one * scale;
        exterior.localPosition = -groundCenter * scale;
        return true;
    }

    static bool TryGetBounds(Transform root, GameObject model, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Matrix4x4 worldToRoot = root.worldToLocalMatrix;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
                continue;
            Bounds local = renderer.localBounds;
            Matrix4x4 toRoot = worldToRoot * renderer.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z);
                point = toRoot.MultiplyPoint3x4(point);
                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }
        return hasBounds;
    }

    static void BuildCar(Transform parent, bool compact, Vector3 dimensions)
    {
        float width = dimensions.x;
        float length = dimensions.z;
        float cabinLength = compact ? 1.68f : 2.04f;
        float cabinCenter = compact ? -0.27f : -0.10f;
        float cabinY = dimensions.y - 0.34f;
        var body = compact ? Paint.Turquoise : Paint.Coral;

        Box(parent, "Chassis", new Vector3(0f, 0.32f, 0f), new Vector3(width * 0.77f, 0.28f, length * 0.89f), Paint.Trim);
        Box(parent, "Body", new Vector3(0f, 0.58f, 0f), new Vector3(width * 0.95f, 0.58f, length - 0.09f), body);
        Box(parent, "Windows", new Vector3(0f, cabinY - 0.055f, cabinCenter),
            new Vector3(width * 0.78f, 0.60f, cabinLength), Paint.Glass);
        Box(parent, "Roof", new Vector3(0f, dimensions.y - 0.075f, cabinCenter - 0.04f),
            new Vector3(width * 0.76f, 0.15f, cabinLength - 0.28f), body);
        Box(parent, "Grille", new Vector3(0f, 0.56f, length * 0.5f - 0.085f),
            new Vector3(width * 0.34f, 0.13f, 0.14f), Paint.Trim);

        for (int side = -1; side <= 1; side += 2)
        {
            float sideOffset = side * width * 0.39f;
            Box(parent, "Window pillar", new Vector3(sideOffset, cabinY - 0.08f, cabinCenter),
                new Vector3(0.055f, 0.51f, 0.085f), body);
            Box(parent, "Side mirror", new Vector3(side * width * 0.441f, cabinY - 0.18f, cabinCenter + cabinLength * 0.38f),
                new Vector3(width * 0.117f, 0.10f, 0.17f), body);
        }

        Lights(parent, width, length, 0.67f);
        float axle = compact ? 1.02f : 1.35f;
        AddWheels(parent, width, compact ? 0.30f : 0.33f, 0.24f, axle, -axle);
    }

    static void BuildTruck(Transform parent, Vector3 dimensions)
    {
        float width = dimensions.x;
        float length = dimensions.z;
        Box(parent, "Chassis", new Vector3(0f, 0.50f, 0f), new Vector3(width * 0.81f, 0.36f, length - 0.13f), Paint.Trim);
        Box(parent, "Cab", new Vector3(0f, 0.95f, 1.97f), new Vector3(width * 0.96f, 0.91f, 1.77f), Paint.Gold);
        Box(parent, "Cab windows", new Vector3(0f, 1.62f, 1.91f), new Vector3(width * 0.85f, 0.59f, 1.39f), Paint.Glass);
        Box(parent, "Cab roof", new Vector3(0f, 1.93f, 1.88f), new Vector3(width * 0.89f, 0.16f, 1.37f), Paint.Gold);
        Box(parent, "Cargo box", new Vector3(0f, 1.60f, -0.84f), new Vector3(width * 0.98f, 1.80f, 3.86f), Paint.Ivory);
        Box(parent, "Rear door seam", new Vector3(0f, 1.61f, -2.77f), new Vector3(0.035f, 1.51f, 0.04f), Paint.Trim);
        Box(parent, "Grille", new Vector3(0f, 0.90f, 2.84f), new Vector3(width * 0.37f, 0.20f, 0.10f), Paint.Trim);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(parent, "Cargo stripe", new Vector3(side * width * 0.486f, 1.35f, -0.84f),
                new Vector3(0.045f, 0.23f, 3.57f), Paint.Gold);
            Box(parent, "Side mirror", new Vector3(side * width * 0.443f, 1.45f, 2.39f),
                new Vector3(width * 0.11f, 0.23f, 0.12f), Paint.Trim);
        }
        Lights(parent, width, length, 0.91f);
        AddWheels(parent, width, 0.39f, 0.32f, 1.90f, -1.74f);
    }

    static void Lights(Transform parent, float width, float length, float height)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * width * 0.33f;
            Box(parent, "Headlight", new Vector3(x, height, length * 0.5f - 0.075f),
                new Vector3(width * 0.20f, 0.17f, 0.14f), Paint.Ivory);
            Box(parent, "Tail light", new Vector3(x, Mathf.Min(height, 0.68f), -length * 0.5f + 0.075f),
                new Vector3(width * 0.16f, 0.16f, 0.14f), Paint.Tail);
        }
    }

    static void AddWheels(Transform parent, float width, float radius, float tireWidth, float front, float rear)
    {
        if (cylinder == null)
            cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        Quaternion axleRotation = Quaternion.Euler(0f, 0f, 90f);
        for (int side = -1; side <= 1; side += 2)
        {
            float wheelX = side * (width - tireWidth) * 0.5f;
            float hubX = side * (width * 0.5f - 0.006f);
            for (int axle = 0; axle < 2; axle++)
            {
                float z = axle == 0 ? front : rear;
                Part(parent, "Tire", cylinder, new Vector3(wheelX, radius, z),
                    new Vector3(2f * radius, tireWidth * 0.5f, 2f * radius), axleRotation, Paint.Rubber);
                Part(parent, "Wheel hub", cylinder, new Vector3(hubX, radius, z),
                    new Vector3(1.20f * radius, 0.012f, 1.20f * radius), axleRotation, Paint.Alloy);
            }
        }
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        if (roundedBox == null)
            roundedBox = CreateRoundedBox();
        Part(parent, name, roundedBox, position, scale, Quaternion.identity, material);
    }

    static void Part(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale,
        Quaternion rotation, Material material)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.layer = parent.gameObject.layer;
        part.transform.SetParent(parent, false);
        part.transform.SetLocalPositionAndRotation(position, rotation);
        part.transform.localScale = scale;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static Mesh CreateRoundedBox()
    {
        const float coreExtent = 0.43f;
        const float bevelRadius = 0.5f - coreExtent;
        var coordinates = new[] { -0.5f, -coreExtent, coreExtent, 0.5f };
        var vertices = new List<Vector3>(96);
        var normals = new List<Vector3>(96);
        var triangles = new List<int>(324);

        AddFace(Vector3.right, Vector3.back, Vector3.up);
        AddFace(Vector3.left, Vector3.forward, Vector3.up);
        AddFace(Vector3.up, Vector3.right, Vector3.back);
        AddFace(Vector3.down, Vector3.right, Vector3.forward);
        AddFace(Vector3.forward, Vector3.right, Vector3.up);
        AddFace(Vector3.back, Vector3.left, Vector3.up);

        var mesh = new Mesh { name = "Vehicle rounded box" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        return mesh;

        void AddFace(Vector3 normal, Vector3 horizontal, Vector3 vertical)
        {
            int start = vertices.Count;
            foreach (float y in coordinates)
            {
                foreach (float x in coordinates)
                {
                    Vector3 point = normal * 0.5f + horizontal * x + vertical * y;
                    var core = new Vector3(
                        Mathf.Clamp(point.x, -coreExtent, coreExtent),
                        Mathf.Clamp(point.y, -coreExtent, coreExtent),
                        Mathf.Clamp(point.z, -coreExtent, coreExtent));
                    Vector3 smoothNormal = (point - core).normalized;
                    vertices.Add(core + smoothNormal * bevelRadius);
                    normals.Add(smoothNormal);
                }
            }

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    int corner = start + row * 4 + column;
                    triangles.Add(corner);
                    triangles.Add(corner + 1);
                    triangles.Add(corner + 5);
                    triangles.Add(corner);
                    triangles.Add(corner + 5);
                    triangles.Add(corner + 4);
                }
            }
        }
    }

    sealed class VehicleMaterials
    {
        Material turquoise, coral, gold, glass, trim, rubber, alloy, ivory, tail;

        public Material Turquoise => turquoise ??= Make("Turquoise exterior", new Color32(39, 167, 163, 255), 0.40f);
        public Material Coral => coral ??= Make("Coral exterior", new Color32(223, 100, 79, 255), 0.40f);
        public Material Gold => gold ??= Make("Gold exterior", new Color32(242, 182, 55, 255), 0.40f);
        public Material Glass => glass ??= Make("Tinted vehicle windows", new Color32(54, 87, 104, 255), 0.52f);
        public Material Trim => trim ??= Make("Dark vehicle trim", new Color32(57, 61, 66, 255), 0.20f);
        public Material Rubber => rubber ??= Make("Rubber tires", new Color32(35, 41, 45, 255), 0.09f);
        public Material Alloy => alloy ??= Make("Alloy wheel hubs", new Color32(171, 184, 186, 255), 0.42f);
        public Material Ivory => ivory ??= Make("Ivory vehicle trim", new Color32(245, 235, 196, 255), 0.34f);
        public Material Tail => tail ??= Make("Tail light lens", new Color32(172, 41, 46, 255), 0.35f);

        static Material Make(string name, Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }
    }
}
