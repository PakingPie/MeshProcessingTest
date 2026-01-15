using UnityEngine;

/// <summary>
/// Represents a cylindrical drill tool that can carve into grindable meshes.
/// The drill projects vertices inside its cylinder outward to the surface.
/// </summary>
public class DrillTool : MonoBehaviour
{
    [Header("Drill Geometry")]
    [Tooltip("Radius of the drill cylinder")]
    [SerializeField] private float radius = 0.1f;

    [Tooltip("Length of the drill cylinder")]
    [SerializeField] private float length = 1f;

    [Header("Axis Configuration")]
    [Tooltip("Local axis that points in the drilling direction (tip direction)")]
    [SerializeField] private DrillAxisDirection drillAxis = DrillAxisDirection.NegativeY;

    [Header("Depth Control")]
    [Tooltip("If true, uses configurable depth instead of full cylinder length")]
    [SerializeField] private bool useConfigurableDepth = false;

    [Tooltip("Custom drilling depth (only used if useConfigurableDepth is true)")]
    [SerializeField] private float configurableDepth = 0.5f;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugCylinder = true;
    [SerializeField] private Color cylinderColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private int debugSegments = 32;

    public enum DrillAxisDirection
    {
        PositiveX,
        NegativeX,
        PositiveY,
        NegativeY,
        PositiveZ,
        NegativeZ
    }

    /// <summary>
    /// Gets the drill radius.
    /// </summary>
    public float Radius => radius;

    /// <summary>
    /// Gets the effective drill length/depth.
    /// </summary>
    public float EffectiveLength => useConfigurableDepth ? configurableDepth : length;

    /// <summary>
    /// Gets the full cylinder length.
    /// </summary>
    public float Length => length;

    /// <summary>
    /// Gets the world-space direction the drill is pointing (from base to tip).
    /// </summary>
    public Vector3 DrillDirection
    {
        get
        {
            switch (drillAxis)
            {
                case DrillAxisDirection.PositiveX: return transform.right;
                case DrillAxisDirection.NegativeX: return -transform.right;
                case DrillAxisDirection.PositiveY: return transform.up;
                case DrillAxisDirection.NegativeY: return -transform.up;
                case DrillAxisDirection.PositiveZ: return transform.forward;
                case DrillAxisDirection.NegativeZ: return -transform.forward;
                default: return -transform.up;
            }
        }
    }

    /// <summary>
    /// Gets the world-space position of the drill base (opposite end from tip).
    /// </summary>
    public Vector3 DrillBase => transform.position;

    /// <summary>
    /// Gets the world-space position of the drill tip.
    /// </summary>
    public Vector3 DrillTip => transform.position + DrillDirection * EffectiveLength;

    /// <summary>
    /// Gets the local axis index (0=X, 1=Y, 2=Z) and sign for the drill direction.
    /// </summary>
    public (int axisIndex, float sign) GetLocalAxisInfo()
    {
        switch (drillAxis)
        {
            case DrillAxisDirection.PositiveX: return (0, 1f);
            case DrillAxisDirection.NegativeX: return (0, -1f);
            case DrillAxisDirection.PositiveY: return (1, 1f);
            case DrillAxisDirection.NegativeY: return (1, -1f);
            case DrillAxisDirection.PositiveZ: return (2, 1f);
            case DrillAxisDirection.NegativeZ: return (2, -1f);
            default: return (1, -1f);
        }
    }

    /// <summary>
    /// Transforms a world point to drill-local space where:
    /// - The drill axis is always the local Y axis
    /// - Y=0 is at the drill base, Y=length is at the tip
    /// </summary>
    public Vector3 WorldToDrillSpace(Vector3 worldPoint)
    {
        // First transform to object local space
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        // Then rotate so drill axis becomes Y
        var (axisIndex, sign) = GetLocalAxisInfo();

        Vector3 drillSpacePoint;
        switch (axisIndex)
        {
            case 0: // X axis
                drillSpacePoint = new Vector3(localPoint.y, localPoint.x * sign, localPoint.z);
                break;
            case 1: // Y axis
                drillSpacePoint = new Vector3(localPoint.x, localPoint.y * sign, localPoint.z);
                break;
            case 2: // Z axis
                drillSpacePoint = new Vector3(localPoint.x, localPoint.z * sign, localPoint.y);
                break;
            default:
                drillSpacePoint = localPoint;
                break;
        }

        return drillSpacePoint;
    }

    /// <summary>
    /// Transforms a drill-space point back to world space.
    /// </summary>
    public Vector3 DrillSpaceToWorld(Vector3 drillSpacePoint)
    {
        var (axisIndex, sign) = GetLocalAxisInfo();

        Vector3 localPoint;
        switch (axisIndex)
        {
            case 0: // X axis
                localPoint = new Vector3(drillSpacePoint.y * sign, drillSpacePoint.x, drillSpacePoint.z);
                break;
            case 1: // Y axis
                localPoint = new Vector3(drillSpacePoint.x, drillSpacePoint.y * sign, drillSpacePoint.z);
                break;
            case 2: // Z axis
                localPoint = new Vector3(drillSpacePoint.x, drillSpacePoint.z, drillSpacePoint.y * sign);
                break;
            default:
                localPoint = drillSpacePoint;
                break;
        }

        return transform.TransformPoint(localPoint);
    }

    /// <summary>
    /// Checks if a world-space point is inside the drill cylinder.
    /// </summary>
    public bool IsPointInsideCylinder(Vector3 worldPoint)
    {
        Vector3 drillSpace = WorldToDrillSpace(worldPoint);

        // Check height bounds (0 to effective length)
        if (drillSpace.y < 0 || drillSpace.y > EffectiveLength)
            return false;

        // Check radial distance
        float radialDistSqr = drillSpace.x * drillSpace.x + drillSpace.z * drillSpace.z;
        return radialDistSqr < radius * radius;
    }

    /// <summary>
    /// Projects a point inside the cylinder to the cylinder surface.
    /// Returns the original point if it's outside the cylinder.
    /// </summary>
    /// <param name="worldPoint">The world-space point to project</param>
    /// <param name="wasProjected">True if the point was inside and got projected</param>
    public Vector3 ProjectToSurface(Vector3 worldPoint, out bool wasProjected)
    {
        Vector3 drillSpace = WorldToDrillSpace(worldPoint);

        // Check height bounds
        if (drillSpace.y < 0 || drillSpace.y > EffectiveLength)
        {
            wasProjected = false;
            return worldPoint;
        }

        // Check radial distance
        float radialDistSqr = drillSpace.x * drillSpace.x + drillSpace.z * drillSpace.z;
        float radiusSqr = radius * radius;

        if (radialDistSqr >= radiusSqr)
        {
            wasProjected = false;
            return worldPoint;
        }

        // Point is inside cylinder - project to surface
        wasProjected = true;

        float radialDist = Mathf.Sqrt(radialDistSqr);

        // Handle case where point is exactly on the axis
        if (radialDist < 0.0001f)
        {
            // Push in arbitrary direction (positive X in drill space)
            drillSpace.x = radius;
            drillSpace.z = 0;
        }
        else
        {
            // Scale outward to radius
            float scale = radius / radialDist;
            drillSpace.x *= scale;
            drillSpace.z *= scale;
        }

        return DrillSpaceToWorld(drillSpace);
    }

    /// <summary>
    /// Projects a point to the cylinder surface, with bounds clamping.
    /// </summary>
    /// <param name="worldPoint">The world-space point to project</param>
    /// <param name="originalBounds">The original mesh bounds in world space</param>
    /// <param name="wasProjected">True if the point was inside and got projected</param>
    public Vector3 ProjectToSurfaceClamped(Vector3 worldPoint, Bounds originalBounds, out bool wasProjected)
    {
        Vector3 projected = ProjectToSurface(worldPoint, out wasProjected);

        if (wasProjected)
        {
            // Clamp to original mesh bounds
            projected.x = Mathf.Clamp(projected.x, originalBounds.min.x, originalBounds.max.x);
            projected.y = Mathf.Clamp(projected.y, originalBounds.min.y, originalBounds.max.y);
            projected.z = Mathf.Clamp(projected.z, originalBounds.min.z, originalBounds.max.z);
        }

        return projected;
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.001f, radius);
        length = Mathf.Max(0.001f, length);
        configurableDepth = Mathf.Clamp(configurableDepth, 0.001f, length);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugCylinder) return;

        DrawCylinderGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        DrawCylinderGizmo();
    }

    private void DrawCylinderGizmo()
    {
        Gizmos.color = cylinderColor;

        Vector3 baseCenter = DrillBase;
        Vector3 tipCenter = DrillTip;
        Vector3 direction = DrillDirection;

        // Get perpendicular vectors for drawing circles
        Vector3 perpendicular1 = Vector3.Cross(direction, Vector3.up).normalized;
        if (perpendicular1.sqrMagnitude < 0.001f)
        {
            perpendicular1 = Vector3.Cross(direction, Vector3.right).normalized;
        }
        Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular1).normalized;

        // Draw circles at base and tip
        DrawCircle(baseCenter, perpendicular1, perpendicular2, radius);
        DrawCircle(tipCenter, perpendicular1, perpendicular2, radius);

        // Draw lines connecting the circles
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f;
            Vector3 offset = (perpendicular1 * Mathf.Cos(angle) + perpendicular2 * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(baseCenter + offset, tipCenter + offset);
        }

        // Draw direction arrow
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(tipCenter, tipCenter + direction * 0.1f);
    }

    private void DrawCircle(Vector3 center, Vector3 axis1, Vector3 axis2, float circleRadius)
    {
        Vector3 prevPoint = center + axis1 * circleRadius;
        for (int i = 1; i <= debugSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / debugSegments;
            Vector3 point = center + (axis1 * Mathf.Cos(angle) + axis2 * Mathf.Sin(angle)) * circleRadius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
}