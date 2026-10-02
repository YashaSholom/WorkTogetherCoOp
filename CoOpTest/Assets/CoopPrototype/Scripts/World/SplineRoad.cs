using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace CoopPrototype
{
    /// <summary>
    /// Builds a road mesh along this object's spline: a flat surface, a glowing edge line and a raised kerb on each
    /// side, side skirts and an underside, plus optional centre dashes. The mesh is rebuilt whenever the spline is
    /// edited (drag the knots in the Scene view) and on enable at runtime, so nothing is stored in the scene.
    /// Where a kerb or dash would land on a neighbouring road it is left out, which makes merges and forks clean.
    /// Sub-meshes / materials: 0 surface, 1 kerb, 2 edge line, 3 dashes.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    public class SplineRoad : MonoBehaviour
    {
        [Min(1)] public float width = 6;
        [Min(0)] public float kerbWidth = .3f, kerbHeight = .08f, lineWidth = .14f;
        [Min(.05f)] public float thickness = .4f;
        [Min(.1f)] public float sampleSpacing = .5f;
        public bool dashes = true;
        [Min(.5f)] public float dashSpacing = 3, dashLength = 1.1f, dashWidth = .2f;
        [Tooltip("Higher priority roads keep their markings where two roads overlap.")]
        public int priority;
        public bool buildCollider = true;
        [Header("Guard rails (for ramps and raised sections)")]
        [Tooltip("Distance along the spline where guard rails start; negative = no rails.")]
        public float railFromDistance = -1;
        [Min(.1f)] public float railHeight = 1.2f, railThickness = .2f;

        Mesh mesh;
        bool dirty;

        void OnEnable() { Spline.Changed += OnSplineChanged; Rebuild(); }
        void OnDisable() { Spline.Changed -= OnSplineChanged; }
        void OnValidate() => dirty = true;
        void OnDestroy() { if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }

        void OnSplineChanged(Spline spline, int knot, SplineModification modification)
        {
            // Any road changing can change where neighbours overlap, so every road refreshes.
            dirty = true;
        }

        void Update() { if (dirty) Rebuild(); }

        /// <summary>World-space centreline samples, used by neighbouring roads to find overlaps.</summary>
        public List<Vector3> Centreline(float spacing)
        {
            var points = new List<Vector3>();
            var container = GetComponent<SplineContainer>();
            if (container == null || container.Spline == null || container.Spline.Count < 2) return points;
            var spline = container.Spline; float length = spline.GetLength();
            int count = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);
            for (int i = 0; i < count; i++)
            {
                float t = spline.ConvertIndexUnit(length * i / (count - 1), PathIndexUnit.Distance, PathIndexUnit.Normalized);
                points.Add(transform.TransformPoint(spline.EvaluatePosition(t)));
            }
            return points;
        }

        public void Rebuild()
        {
            dirty = false;
            var container = GetComponent<SplineContainer>();
            if (container == null || container.Spline == null || container.Spline.Count < 2) return;
            var spline = container.Spline; float length = spline.GetLength();
            int count = Mathf.Max(2, Mathf.CeilToInt(length / sampleSpacing) + 1);
            var centre = new Vector3[count]; var right = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t = spline.ConvertIndexUnit(length * i / (count - 1), PathIndexUnit.Distance, PathIndexUnit.Normalized);
                spline.Evaluate(t, out float3 p, out float3 tangent, out float3 _);
                centre[i] = p;
                var flat = Vector3.ProjectOnPlane(tangent, Vector3.up);
                right[i] = flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : (i > 0 ? right[i - 1] : Vector3.right);
            }

            // Neighbouring roads (siblings), as world-space polylines with their half widths.
            var others = new List<(List<Vector3> line, float half, int priority)>();
            if (transform.parent != null)
                foreach (var other in transform.parent.GetComponentsInChildren<SplineRoad>())
                    if (other != this && other.isActiveAndEnabled) others.Add((other.Centreline(.5f), other.width * .5f, other.priority));
            bool Inside(Vector3 local, bool higherOnly)
            {
                var world = transform.TransformPoint(local);
                foreach (var (line, half, otherPriority) in others)
                {
                    if (higherOnly && otherPriority <= priority) continue;
                    float limit = (half - .05f) * (half - .05f);
                    foreach (var q in line)
                    {
                        float dx = q.x - world.x, dz = q.z - world.z;
                        if (dx * dx + dz * dz < limit && Mathf.Abs(q.y - world.y) < 1f) return true;
                    }
                }
                return false;
            }

            float hw = width * .5f;
            var kerbHere = new bool[2][];
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1 : 1; var at = new bool[count];
                for (int i = 0; i < count; i++) at[i] = !Inside(centre[i] + right[i] * s * (hw + kerbWidth * .5f), false);
                kerbHere[side] = new bool[count - 1];
                for (int i = 0; i < count - 1; i++) kerbHere[side][i] = at[i] && at[i + 1];
            }

            var vertices = new List<Vector3>(); var uvs = new List<Vector2>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            // A strip between two cross-section points; (a, b) are (left, right) as seen from the face's front while travelling.
            void Strip(float offsetA, float yA, float offsetB, float yB, int sub, bool[] mask)
            {
                int start = vertices.Count;
                for (int i = 0; i < count; i++)
                {
                    vertices.Add(centre[i] + right[i] * offsetA + Vector3.up * yA);
                    vertices.Add(centre[i] + right[i] * offsetB + Vector3.up * yB);
                    float v = length * i / (count - 1);
                    uvs.Add(new Vector2(0, v)); uvs.Add(new Vector2(1, v));
                }
                for (int i = 0; i < count - 1; i++)
                {
                    if (mask != null && !mask[i]) continue;
                    int a = start + i * 2, b = a + 1, a2 = a + 2, b2 = a + 3;
                    triangles[sub].Add(a); triangles[sub].Add(a2); triangles[sub].Add(b);
                    triangles[sub].Add(b); triangles[sub].Add(a2); triangles[sub].Add(b2);
                }
            }
            float outer = hw + kerbWidth, lift = .006f;
            Strip(-hw, 0, hw, 0, 0, null);                                     // surface
            Strip(outer, -thickness, -outer, -thickness, 0, null);             // underside
            Strip(outer, 0, outer, -thickness, 0, null);                       // right skirt
            Strip(-outer, -thickness, -outer, 0, 0, null);                     // left skirt
            Strip(hw, lift * .5f, outer, lift * .5f, 0, null);                 // shoulder under a missing kerb (right)
            Strip(-outer, lift * .5f, -hw, lift * .5f, 0, null);               // shoulder (left)
            // Right kerb
            Strip(hw, 0, hw, kerbHeight, 1, kerbHere[1]);
            Strip(hw, kerbHeight, outer, kerbHeight, 1, kerbHere[1]);
            Strip(outer, kerbHeight, outer, 0, 1, kerbHere[1]);
            Strip(hw - lineWidth, lift, hw, lift, 2, kerbHere[1]);
            // Left kerb
            Strip(-hw, kerbHeight, -hw, 0, 1, kerbHere[0]);
            Strip(-outer, kerbHeight, -hw, kerbHeight, 1, kerbHere[0]);
            Strip(-outer, 0, -outer, kerbHeight, 1, kerbHere[0]);
            Strip(-hw, lift, -hw + lineWidth, lift, 2, kerbHere[0]);

            if (railFromDistance >= 0)
            {
                var rail = new bool[count - 1];
                for (int i = 0; i < count - 1; i++) rail[i] = length * i / (count - 1) >= railFromDistance;
                float o1 = outer - railThickness, o2 = outer, h = railHeight;
                Strip(o1, kerbHeight, o1, h, 1, rail); Strip(o1, h, o2, h, 3, rail); Strip(o2, h, o2, 0, 1, rail);
                Strip(-o1, h, -o1, kerbHeight, 1, rail); Strip(-o2, h, -o1, h, 3, rail); Strip(-o2, 0, -o2, h, 1, rail);
            }

            if (dashes)
                for (float d = dashSpacing * .5f; d + dashLength < length; d += dashSpacing)
                {
                    float t0 = spline.ConvertIndexUnit(d, PathIndexUnit.Distance, PathIndexUnit.Normalized), t1 = spline.ConvertIndexUnit(d + dashLength, PathIndexUnit.Distance, PathIndexUnit.Normalized);
                    Vector3 p0 = spline.EvaluatePosition(t0), p1 = spline.EvaluatePosition(t1);
                    if (Inside(p0, true) || Inside(p1, true)) continue;
                    Vector3 forward = Vector3.ProjectOnPlane(p1 - p0, Vector3.up).normalized, side = Vector3.Cross(Vector3.up, forward) * dashWidth * .5f, up = Vector3.up * lift;
                    int start = vertices.Count;
                    vertices.Add(p0 - side + up); vertices.Add(p0 + side + up); vertices.Add(p1 - side + up); vertices.Add(p1 + side + up);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
                    triangles[3].AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
                }

            if (mesh == null) mesh = new Mesh { name = "Spline road", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.subMeshCount = 4;
            for (int i = 0; i < 4; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = GetComponent<MeshCollider>();
            if (buildCollider && collider != null) { collider.sharedMesh = null; collider.sharedMesh = mesh; }
        }
    }
}
