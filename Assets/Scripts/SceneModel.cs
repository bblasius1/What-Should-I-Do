using UnityEngine;

/// <summary>Sizes decorative models to the footprint and floor height of a scene prop.</summary>
internal static class SceneModel
{
    internal static bool TryPlace(Transform holder, GameObject prefab, Vector3 dimensions, bool alignFront = false,
        bool fitWideProp = false)
    {
        if (prefab == null)
            return false;

        GameObject model = Object.Instantiate(prefab, holder, false);
        model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;
        Bounds bounds = Measure(holder, model);
        bool turnWideProp = fitWideProp && bounds.size.z > bounds.size.x * 1.1f;
        if (turnWideProp)
        {
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            bounds = Measure(holder, model);
        }

        if (bounds.size.x < 0.0001f || bounds.size.y < 0.0001f || bounds.size.z < 0.0001f)
        {
            model.SetActive(false);
            Object.Destroy(model);
            return false;
        }

        float scale = Mathf.Min(dimensions.x / bounds.size.x, dimensions.y / bounds.size.y, dimensions.z / bounds.size.z);
        float widthScale = fitWideProp ? dimensions.x / bounds.size.x : scale;
        Vector3 anchor = new Vector3(bounds.center.x, bounds.min.y, alignFront ? bounds.max.z : bounds.center.z);
        model.transform.localScale = turnWideProp ? new Vector3(scale, scale, widthScale) : new Vector3(widthScale, scale, scale);
        model.transform.localPosition = -Vector3.Scale(anchor, new Vector3(widthScale, scale, scale));
        foreach (Collider collider in model.GetComponentsInChildren<Collider>())
            collider.enabled = false;

        return true;
    }

    static Bounds Measure(Transform relative, GameObject model)
    {
        Bounds bounds = default;
        bool started = false;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
                continue;

            Matrix4x4 toLocal = relative.worldToLocalMatrix * renderer.localToWorldMatrix;
            Bounds extent = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                var local = new Vector3((i & 1) == 0 ? extent.min.x : extent.max.x,
                    (i & 2) == 0 ? extent.min.y : extent.max.y, (i & 4) == 0 ? extent.min.z : extent.max.z);
                Vector3 point = toLocal.MultiplyPoint3x4(local);
                if (!started)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    started = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }
        return bounds;
    }
}
