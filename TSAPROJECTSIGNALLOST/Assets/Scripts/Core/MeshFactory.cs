using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Procedural low-poly meshes that stand in for ProBuilder shapes (Torus, Cone/funnel, Cylinder, Dish,
    /// Icosphere). SPEC-ASSUMPTION: generated in code so the project has no ProBuilder API dependency;
    /// the results are saved as mesh assets and used inside each prefab's Visual child.
    /// </summary>
    public static class MeshFactory
    {
        public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments, int minorSegments, string name = "Torus")
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<int> t = new List<int>();
            for (int i = 0; i <= majorSegments; i++)
            {
                float a = i * Mathf.PI * 2f / majorSegments;
                Vector3 center = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * majorRadius;
                for (int j = 0; j <= minorSegments; j++)
                {
                    float b = j * Mathf.PI * 2f / minorSegments;
                    Vector3 dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    v.Add(center + dir * minorRadius);
                    n.Add(dir);
                }
            }
            int row = minorSegments + 1;
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a0 = i * row + j, a1 = (i + 1) * row + j, b0 = a0 + 1, b1 = a1 + 1;
                    t.Add(a0); t.Add(b0); t.Add(a1);
                    t.Add(a1); t.Add(b0); t.Add(b1);
                }
            }
            return Build(name, v, n, t, false);
        }

        /// <summary>Open funnel: wide at the top, narrow at the bottom (a flipped cone), double sided.</summary>
        public static Mesh Funnel(float topRadius, float bottomRadius, float height, int segments, string name = "Funnel")
        {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * bottomRadius, 0f, Mathf.Sin(a0) * bottomRadius);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * bottomRadius, 0f, Mathf.Sin(a1) * bottomRadius);
                Vector3 u0 = new Vector3(Mathf.Cos(a0) * topRadius, height, Mathf.Sin(a0) * topRadius);
                Vector3 u1 = new Vector3(Mathf.Cos(a1) * topRadius, height, Mathf.Sin(a1) * topRadius);
                AddQuad(v, t, b0, u0, u1, b1);
                AddQuad(v, t, b1, u1, u0, b0);
            }
            // Bottom cap so the funnel reads as a solid chute.
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                int s = v.Count;
                v.Add(Vector3.zero);
                v.Add(new Vector3(Mathf.Cos(a1) * bottomRadius, 0f, Mathf.Sin(a1) * bottomRadius));
                v.Add(new Vector3(Mathf.Cos(a0) * bottomRadius, 0f, Mathf.Sin(a0) * bottomRadius));
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
            }
            return Build(name, v, null, t, true);
        }

        /// <summary>Flat-shaded cylinder centered on the origin (6 sides makes the hex coin).</summary>
        public static Mesh Cylinder(float radius, float height, int segments, string name = "Cylinder")
        {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            float h = height * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                AddQuad(v, t, p0 + Vector3.down * h, p0 + Vector3.up * h, p1 + Vector3.up * h, p1 + Vector3.down * h);
                int s = v.Count;
                v.Add(Vector3.up * h); v.Add(p1 + Vector3.up * h); v.Add(p0 + Vector3.up * h);
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
                s = v.Count;
                v.Add(Vector3.down * h); v.Add(p0 + Vector3.down * h); v.Add(p1 + Vector3.down * h);
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
            }
            return Build(name, v, null, t, true);
        }

        /// <summary>Shallow spherical cap (satellite dish), open at the top, double sided.</summary>
        public static Mesh Dish(float radius, float depth, int segments, int rings, string name = "Dish")
        {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            for (int r = 0; r < rings; r++)
            {
                float k0 = r / (float)rings, k1 = (r + 1) / (float)rings;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 p00 = DishPoint(radius, depth, k0, a0), p01 = DishPoint(radius, depth, k0, a1);
                    Vector3 p10 = DishPoint(radius, depth, k1, a0), p11 = DishPoint(radius, depth, k1, a1);
                    AddQuad(v, t, p00, p10, p11, p01);
                    AddQuad(v, t, p01, p11, p10, p00);
                }
            }
            return Build(name, v, null, t, true);
        }

        private static Vector3 DishPoint(float radius, float depth, float k, float a)
        {
            float r = radius * k;
            return new Vector3(Mathf.Cos(a) * r, depth * k * k, Mathf.Sin(a) * r);
        }

        /// <summary>Low-poly flat-shaded icosphere (the moon).</summary>
        public static Mesh Icosphere(float radius, int subdivisions, float roughness, int seed, string name = "Icosphere")
        {
            float tt = (1f + Mathf.Sqrt(5f)) / 2f;
            List<Vector3> verts = new List<Vector3>
            {
                new Vector3(-1, tt, 0), new Vector3(1, tt, 0), new Vector3(-1, -tt, 0), new Vector3(1, -tt, 0),
                new Vector3(0, -1, tt), new Vector3(0, 1, tt), new Vector3(0, -1, -tt), new Vector3(0, 1, -tt),
                new Vector3(tt, 0, -1), new Vector3(tt, 0, 1), new Vector3(-tt, 0, -1), new Vector3(-tt, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            List<int> faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            Dictionary<long, int> mid = new Dictionary<long, int>();
            for (int s = 0; s < subdivisions; s++)
            {
                List<int> next = new List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = Mid(verts, mid, a, b), bc = Mid(verts, mid, b, c), ca = Mid(verts, mid, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            System.Random rnd = new System.Random(seed);
            for (int i = 0; i < verts.Count; i++)
            {
                float k = 1f + ((float)rnd.NextDouble() - 0.5f) * roughness;
                verts[i] = verts[i] * radius * k;
            }
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 a = verts[faces[i]], b = verts[faces[i + 1]], c = verts[faces[i + 2]];
                // Unity's front face normal is cross(b - a, c - a); make every face point outward.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                {
                    Vector3 tmp = b;
                    b = c;
                    c = tmp;
                }
                int s = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
            }
            return Build(name, v, null, t, true);
        }

        private static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }

        private static void AddQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
            t.Add(s); t.Add(s + 2); t.Add(s + 3);
        }

        private static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<int> t, bool flat)
        {
            Mesh m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            if (n != null && !flat) m.SetNormals(n);
            else m.RecalculateNormals();
            Vector2[] uv = new Vector2[v.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(v[i].x, v[i].z);
            m.uv = uv;
            m.RecalculateBounds();
            return m;
        }
    }
}
