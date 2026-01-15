using UnityEngine;

/// <summary>
/// Extension methods for GrindableObject to support GPU grinding.
/// </summary>
public static class GrindableObjectExtensions
{
    /// <summary>
    /// Sets the vertices directly on the grindable's working mesh.
    /// Used by GPU grinding to apply results.
    /// </summary>
    public static void SetVertices(this GrindableObject grindable, Vector3[] vertices)
    {
        var meshFilter = grindable.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            meshFilter.mesh.vertices = vertices;
            meshFilter.mesh.RecalculateNormals();
            meshFilter.mesh.RecalculateBounds();
        }
    }
}