using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Colorblind mode: a high-contrast shape on each blob and above its matching chute
    /// (red = triangle, blue = circle, green = square, gold = star, prism = diamond), so sorting is fully
    /// playable in grayscale. Billboards toward the camera; hidden when the setting is off.
    /// </summary>
    public class ShapeGlyph : MonoBehaviour
    {
        public enum Mode { BlobSurface, Floating }

        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static Mesh quad;

        private Transform owner;
        private Mode mode;
        private MeshRenderer meshRenderer;
        private string shape;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            materials.Clear();
        }

        public static string ShapeFor(BlobColor c)
        {
            switch (c)
            {
                case BlobColor.Red: return "triangle";
                case BlobColor.Blue: return "circle";
                case BlobColor.Green: return "square";
                case BlobColor.Gold: return "star";
                default: return "diamond";
            }
        }

        public static ShapeGlyph Create(Transform owner, Mode mode)
        {
            GameObject go = new GameObject("ColorblindGlyph", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(owner, false);
            go.GetComponent<MeshFilter>().sharedMesh = Quad();
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            ShapeGlyph g = go.AddComponent<ShapeGlyph>();
            g.owner = owner;
            g.mode = mode;
            g.meshRenderer = r;
            return g;
        }

        public void SetShape(string s)
        {
            if (s == shape) return;
            shape = s;
            meshRenderer.sharedMaterial = MaterialFor(s);
        }

        private static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "GlyphQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }

        private static Material MaterialFor(string s)
        {
            if (materials.TryGetValue(s, out Material m) && m != null) return m;
            GameAssets a = GameManager.I != null ? GameManager.I.Assets : null;
            Material template = a != null ? a.glyphMaterial : null;
            m = template != null ? new Material(template) : new Material(Shader.Find("SignalLost/GlyphUnlit"));
            m.name = "Glyph_" + s;
            m.mainTexture = UISprites.GlyphTexture(s);
            materials[s] = m;
            return m;
        }

        private void LateUpdate()
        {
            bool on = GameSettings.Colorblind && owner != null && Balance.B != null;
            if (meshRenderer.enabled != on) meshRenderer.enabled = on;
            if (!on) return;
            Camera cam = GameManager.I != null ? GameManager.I.Cam : Camera.main;
            if (cam == null) return;
            AccessibilityConfig cfg = Balance.B.accessibility;
            Vector3 center = owner.position;
            Vector3 toCam = cam.transform.position - center;
            Vector3 pos;
            float worldSize;
            if (mode == Mode.BlobSurface)
            {
                float radius = 0.35f * owner.lossyScale.x;
                pos = center + toCam.normalized * radius * 1.12f;
                worldSize = radius * 2f * cfg.blobGlyphScale * 0.62f;
            }
            else
            {
                pos = center + Vector3.up * cfg.chuteGlyphHeight;
                worldSize = cfg.chuteGlyphSize;
                pos.y += 0.06f * Mathf.Sin(Time.unscaledTime * 2.5f);
            }
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
            float parentScale = Mathf.Max(0.0001f, owner.lossyScale.x);
            transform.localScale = Vector3.one * (worldSize / parentScale);
        }
    }
}
