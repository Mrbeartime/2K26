using UnityEngine;

// Small, collider-free placeholder models for the example traps.
public static class TurnTrapVisuals
{
    public static void Tint(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }

    public static GameObject Box(Transform parent, string name, Vector3 position,
        Vector3 scale, Material material, Color color)
    {
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.name = name;
        model.transform.SetParent(parent, false);
        model.transform.localPosition = position;
        model.transform.localScale = scale;
        // Disable immediately so same-frame arrow queries cannot hit the visual.
        model.GetComponent<Collider>().enabled = false;
        var renderer = model.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;
        Tint(renderer, color);
        return model;
    }

    public static Mesh SpikeMesh()
    {
        // Four triangular sides, with separate vertices for sharp normals.
        Vector3 a = new(-0.1f, 0, -0.1f), b = new(0.1f, 0, -0.1f);
        Vector3 c = new(0.1f, 0, 0.1f), d = new(-0.1f, 0, 0.1f);
        Vector3 tip = Vector3.up * 0.6f;
        var mesh = new Mesh { name = "Turn trap spike" };
        mesh.vertices = new[] { a, tip, b, b, tip, c, c, tip, d, d, tip, a };
        mesh.triangles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
