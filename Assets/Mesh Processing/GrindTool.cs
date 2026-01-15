using UnityEngine;

/// <summary>
/// Represents a grinding tool that can modify grindable meshes.
/// Supports multi-directional grinding based on tool orientation.
/// </summary>
public class GrindTool : MonoBehaviour
{
    public enum GrindAxis
    {
        LocalNegativeY,
        LocalPositiveY,
        LocalNegativeX,
        LocalPositiveX,
        LocalNegativeZ,
        LocalPositiveZ
    }

    [Header("Grinding Direction")]
    [Tooltip("Which local axis defines the grinding direction")]
    [SerializeField] private GrindAxis grindAxis = GrindAxis.LocalNegativeY;

    [Tooltip("Use a custom direction instead of axis-based direction")]
    [SerializeField] private bool useCustomDirection = false;

    [Tooltip("Custom grinding direction in world space (normalized)")]
    [SerializeField] private Vector3 customDirection = Vector3.down;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugBounds = true;
    [SerializeField] private bool showGrindDirection = true;
    [SerializeField] private Color boundsColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color directionColor = Color.yellow;

    /// <summary>
    /// Gets the world-space grinding direction based on tool orientation.
    /// </summary>
    public Vector3 GrindDirection
    {
        get
        {
            if (useCustomDirection)
            {
                return customDirection.normalized;
            }

            return grindAxis switch
            {
                GrindAxis.LocalNegativeY => -transform.up,
                GrindAxis.LocalPositiveY => transform.up,
                GrindAxis.LocalNegativeX => -transform.right,
                GrindAxis.LocalPositiveX => transform.right,
                GrindAxis.LocalNegativeZ => -transform.forward,
                GrindAxis.LocalPositiveZ => transform.forward,
                _ => -transform.up
            };
        }
    }

    /// <summary>
    /// Gets the grinding plane defined by the tool's bottom face.
    /// Returns (planeNormal, planePoint).
    /// </summary>
    public (Vector3 normal, Vector3 point) GrindPlane
    {
        get
        {
            Vector3 direction = GrindDirection;
            // The grind plane normal is opposite to grind direction
            Vector3 planeNormal = -direction;

            // Find the "bottom" point of the tool along the grind direction
            Bounds bounds = WorldBounds;
            Vector3 planePoint = bounds.center;

            // Project to find the point on the surface facing the grind direction
            float extent = Vector3.Dot(bounds.extents, new Vector3(
                Mathf.Abs(direction.x),
                Mathf.Abs(direction.y),
                Mathf.Abs(direction.z)
            ));

            planePoint += direction * extent;

            return (planeNormal, planePoint);
        }
    }

    /// <summary>
    /// Gets the world-space axis-aligned bounding box of the grind tool.
    /// </summary>
    public Bounds WorldBounds
    {
        get
        {
            if (TryGetComponent<Renderer>(out var renderer))
            {
                return renderer.bounds;
            }
            else if (TryGetComponent<Collider>(out var collider))
            {
                return collider.bounds;
            }
            else
            {
                Vector3 worldCenter = transform.position;
                Vector3 worldSize = transform.lossyScale;
                return new Bounds(worldCenter, worldSize);
            }
        }
    }

    /// <summary>
    /// Gets the oriented bounding box corners for precise intersection tests.
    /// </summary>
    public Vector3[] GetOBBCorners()
    {
        Vector3[] corners = new Vector3[8];
        Vector3 extents = transform.lossyScale * 0.5f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 localCorner = new Vector3(
                ((i & 1) == 0) ? -extents.x : extents.x,
                ((i & 2) == 0) ? -extents.y : extents.y,
                ((i & 4) == 0) ? -extents.z : extents.z
            );
            corners[i] = transform.TransformPoint(localCorner);
        }

        return corners;
    }

    // Legacy properties for backward compatibility
    public float BottomY => WorldBounds.min.y;
    public float MinX => WorldBounds.min.x;
    public float MaxX => WorldBounds.max.x;
    public float MinZ => WorldBounds.min.z;
    public float MaxZ => WorldBounds.max.z;

    /// <summary>
    /// Checks if a world-space point is within the tool's volume.
    /// </summary>
    public bool IsPointInVolume(Vector3 worldPoint)
    {
        // Transform point to local space
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 halfExtents = Vector3.one * 0.5f;

        return Mathf.Abs(localPoint.x) <= halfExtents.x &&
               Mathf.Abs(localPoint.y) <= halfExtents.y &&
               Mathf.Abs(localPoint.z) <= halfExtents.z;
    }

    /// <summary>
    /// Checks if a point should be ground (in XZ footprint and ahead of grind plane).
    /// Uses oriented bounds for multi-directional support.
    /// </summary>
    public bool ShouldGrindPoint(Vector3 worldPoint)
    {
        // Transform to local space
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 halfExtents = Vector3.one * 0.5f;

        // Get local grind axis
        int axisIndex;
        float axisSign;
        switch (grindAxis)
        {
            case GrindAxis.LocalNegativeY: axisIndex = 1; axisSign = -1; break;
            case GrindAxis.LocalPositiveY: axisIndex = 1; axisSign = 1; break;
            case GrindAxis.LocalNegativeX: axisIndex = 0; axisSign = -1; break;
            case GrindAxis.LocalPositiveX: axisIndex = 0; axisSign = 1; break;
            case GrindAxis.LocalNegativeZ: axisIndex = 2; axisSign = -1; break;
            case GrindAxis.LocalPositiveZ: axisIndex = 2; axisSign = 1; break;
            default: axisIndex = 1; axisSign = -1; break;
        }

        // Check if in the cross-section perpendicular to grind axis
        for (int i = 0; i < 3; i++)
        {
            if (i == axisIndex) continue;
            if (Mathf.Abs(localPoint[i]) > halfExtents[i])
                return false;
        }

        // Check if ahead of the grind surface (in the direction we're grinding toward)
        float grindSurfaceLocal = axisSign * halfExtents[axisIndex];
        float pointOnAxis = localPoint[axisIndex];

        // Point should be on the opposite side of where we're grinding toward
        return (axisSign > 0) ? (pointOnAxis < grindSurfaceLocal) : (pointOnAxis > grindSurfaceLocal);
    }

    /// <summary>
    /// Projects a point onto the grind plane.
    /// </summary>
    public Vector3 ProjectToGrindPlane(Vector3 worldPoint)
    {
        var (planeNormal, planePoint) = GrindPlane;
        float distance = Vector3.Dot(worldPoint - planePoint, planeNormal);
        return worldPoint - planeNormal * distance;
    }

    /// <summary>
    /// Gets the grind surface position for a point (projects onto grind plane along grind direction).
    /// </summary>
    public Vector3 GetGrindTargetPosition(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 halfExtents = Vector3.one * 0.5f;

        int axisIndex;
        float axisSign;
        switch (grindAxis)
        {
            case GrindAxis.LocalNegativeY: axisIndex = 1; axisSign = -1; break;
            case GrindAxis.LocalPositiveY: axisIndex = 1; axisSign = 1; break;
            case GrindAxis.LocalNegativeX: axisIndex = 0; axisSign = -1; break;
            case GrindAxis.LocalPositiveX: axisIndex = 0; axisSign = 1; break;
            case GrindAxis.LocalNegativeZ: axisIndex = 2; axisSign = -1; break;
            case GrindAxis.LocalPositiveZ: axisIndex = 2; axisSign = 1; break;
            default: axisIndex = 1; axisSign = -1; break;
        }

        // Move point to the grind surface
        localPoint[axisIndex] = axisSign * halfExtents[axisIndex];

        return transform.TransformPoint(localPoint);
    }

    /// <summary>
    /// Gets local axis info for compute shader.
    /// </summary>
    public (int axisIndex, float axisSign) GetLocalAxisInfo()
    {
        return grindAxis switch
        {
            GrindAxis.LocalNegativeY => (1, -1f),
            GrindAxis.LocalPositiveY => (1, 1f),
            GrindAxis.LocalNegativeX => (0, -1f),
            GrindAxis.LocalPositiveX => (0, 1f),
            GrindAxis.LocalNegativeZ => (2, -1f),
            GrindAxis.LocalPositiveZ => (2, 1f),
            _ => (1, -1f)
        };
    }

    public bool UseCustomDirection => useCustomDirection;
    public Vector3 CustomDirection => customDirection.normalized;

    private void OnDrawGizmos()
    {
        if (showDebugBounds)
        {
            Gizmos.color = boundsColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = Matrix4x4.identity;
        }

        if (showGrindDirection)
        {
            Gizmos.color = directionColor;
            Vector3 start = transform.position;
            Vector3 end = start + GrindDirection * 1f;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.05f);

            // Draw grind plane
            var (normal, point) = GrindPlane;
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            DrawPlaneGizmo(point, normal, 0.5f);
        }
    }

    private void DrawPlaneGizmo(Vector3 center, Vector3 normal, float size)
    {
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.01f)
            tangent = Vector3.Cross(normal, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent);

        Vector3 c1 = center + (tangent + bitangent) * size;
        Vector3 c2 = center + (tangent - bitangent) * size;
        Vector3 c3 = center + (-tangent - bitangent) * size;
        Vector3 c4 = center + (-tangent + bitangent) * size;

        Gizmos.DrawLine(c1, c2);
        Gizmos.DrawLine(c2, c3);
        Gizmos.DrawLine(c3, c4);
        Gizmos.DrawLine(c4, c1);
    }
}