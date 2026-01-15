using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class VertexColorUpdater : MonoBehaviour
{
    public Color SafeColor = Color.green;
    public Color GrindableColor = Color.white;
    public Color DangerColor = Color.red;

    public float SafeThreshold = 0.1f;
    public float DangerThreshold = 0.2f;
    public Transform ConstraintPlane;
    
    private Color[] _vertexColors;
    private Mesh _workingMesh;

    void Start()
    {
        var grindable = GetComponent<GrindableObject>();
        if (grindable != null)
        {
            InitVertexColors(grindable.GetWorkingMesh());
        }
    }

    public void InitVertexColors(Mesh mesh)
    {
        _workingMesh = mesh;
        var vertices = _workingMesh.vertices;
        _vertexColors = new Color[vertices.Length];
        
        for (int i = 0; i < _vertexColors.Length; i++)
        {
            var worldPos = transform.TransformPoint(vertices[i]);
            if (worldPos.y > ConstraintPlane.position.y + SafeThreshold + DangerThreshold)
            {
                _vertexColors[i] = SafeColor;
            }
            else
            {
                _vertexColors[i] = GrindableColor;
            }
        }
        _workingMesh.colors = _vertexColors;
    }

    // Original method - reads from mesh (use only when mesh is already updated)
    public void UpdateVertexColor(int vid)
    {
        if (_vertexColors == null || vid < 0 || vid >= _vertexColors.Length) return;
        
        var worldPos = transform.TransformPoint(_workingMesh.vertices[vid]);
        UpdateVertexColorInternal(vid, worldPos);
    }

    // New overload - accepts world position directly (use during grinding/drilling)
    public void UpdateVertexColor(int vid, Vector3 worldPos)
    {
        if (_vertexColors == null || vid < 0 || vid >= _vertexColors.Length) return;
        
        UpdateVertexColorInternal(vid, worldPos);
    }

    private void UpdateVertexColorInternal(int vid, Vector3 worldPos)
    {
        float constraintY = ConstraintPlane.position.y;
        
        if (worldPos.y > constraintY + SafeThreshold + DangerThreshold)
        {
            _vertexColors[vid] = SafeColor;
        }
        else if (worldPos.y > constraintY + DangerThreshold)
        {
            float t = (worldPos.y - constraintY - DangerThreshold) / SafeThreshold;
            _vertexColors[vid] = Color.Lerp(GrindableColor, SafeColor, t);
        }
        else
        {
            _vertexColors[vid] = DangerColor;
        }
    }

    public void ApplyColors()
    {
        if (_workingMesh != null && _vertexColors != null)
        {
            _workingMesh.colors = _vertexColors;
        }
    }

    public void ReinitializeColors(Mesh mesh)
    {
        InitVertexColors(mesh);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(VertexColorUpdater))]
public class VertexColorUpdaterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        VertexColorUpdater updater = (VertexColorUpdater)target;
        if (GUILayout.Button("Refresh All Vertex Colors"))
        {
            var grindable = updater.GetComponent<GrindableObject>();
            if (grindable != null)
            {
                updater.InitVertexColors(grindable.GetWorkingMesh());
            }
        }
    }
}
#endif