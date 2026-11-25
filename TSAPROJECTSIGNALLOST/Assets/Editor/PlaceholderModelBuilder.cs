using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalLost.EditorTools
{
    /// <summary>
    /// Tools > Build Placeholder Models: builds every model prefab from Unity primitives + procedural meshes,
    /// assigns materials and saves them into Assets/Prefabs. Each prefab keeps scripts and ONE collider on the
    /// root and its looks under a child named "Visual". Rerunning rebuilds everything.
    /// </summary>
    public static class PlaceholderModelBuilder
    {
        public const string PrefabDir = "Assets/Prefabs";
        public const string MaterialDir = "Assets/Materials";
        public const string MeshDir = "Assets/Models/Generated";
        public const string VfxDir = "Assets/VFX";
        public const string SettingsDir = "Assets/Settings";
        public const string GameAssetsPath = "Assets/Settings/GameAssets.asset";

        private static Material mBlobRed, mBlobBlue, mBlobGreen, mBlobGold;
        private static Material mMoon, mStation, mStationDark, mGlowCyan, mGlass, mFill, mCoin, mChute, mChuteRim, mSolar;
        private static Material mPlanet, mPlanetRing, mSky, mParticle, mParticleRing, mParticleSquare, mLine, mEyeWhite, mEyeBlack;

        [MenuItem("Tools/Build Placeholder Models")]
        public static void BuildAll()
        {
            EnsureFolder(PrefabDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(MeshDir);
            EnsureFolder(VfxDir);
            EnsureFolder(SettingsDir);
            EnsureFolder("Assets/Fonts");

            BuildMaterials();

            GameAssets assets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(assets, GameAssetsPath);
            }

            assets.blobPrefab = BuildBlob();
            assets.coinPrefab = BuildCoin();
            assets.dronePrefab = BuildDrone();
            assets.stationSegmentPrefab = BuildStationSegment();
            BuildCompressor();
            BuildChute();
            BuildStation(assets.stationSegmentPrefab);
            BuildMoon();
            BuildPlanet();

            assets.popVfx = BuildPop();
            assets.sparkleVfx = BuildSparkle();
            assets.confettiVfx = BuildConfetti();
            assets.vacuumSwirlVfx = BuildSwirl();
            assets.shockwaveVfx = BuildShockwave();

            assets.lineMaterial = mLine;
            assets.blobMaterial = mBlobRed;
            assets.skyboxMaterial = mSky;
            assets.glyphMaterial = Custom("M_Glyph", "SignalLost/GlyphUnlit", m => m.SetColor("_Color", Color.white));
            BuildTmpMaterials(assets);

            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Signal Lost] Placeholder models built.");
        }

        // ---------------- helpers ----------------

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static Material GetOrCreateMaterial(string name, Shader shader)
        {
            string path = MaterialDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (shader == null)
            {
                Debug.LogError("[Signal Lost] Missing shader for " + name + ". Is URP installed?");
                shader = Shader.Find("Hidden/InternalErrorShader");
            }
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        private static Material Lit(string name, Color baseColor, float smoothness, Color? emission = null, bool instancing = false)
        {
            Material m = GetOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            m.enableInstancing = instancing;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Custom(string name, string shaderName, Action<Material> setup)
        {
            Material m = GetOrCreateMaterial(name, Shader.Find(shaderName));
            if (setup != null) setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Unlit(string name, Color c)
        {
            Material m = GetOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3? euler = null)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Transform Child(string name, Transform parent, Vector3 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        private static Light PointLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            GameObject go = new GameObject("Light", typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Light l = go.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        private static GameObject SavePrefab(GameObject root, string dir, string name)
        {
            string path = dir + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static void SetRef(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning("[Signal Lost] No field '" + field + "' on " + target.GetType().Name);
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetRefArray(Object target, string field, Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string field, int value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value;
            else p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- materials ----------------

        private static void BuildMaterials()
        {
            Color red = Palette.Red, blue = Palette.Blue, green = Palette.Green, gold = Palette.Gold, cyan = Palette.Cyan;
            mBlobRed = Custom("M_Blob_Red", "SignalLost/BlobWobble", m => m.SetColor("_BaseColor", red));
            mBlobBlue = Custom("M_Blob_Blue", "SignalLost/BlobWobble", m => m.SetColor("_BaseColor", blue));
            mBlobGreen = Custom("M_Blob_Green", "SignalLost/BlobWobble", m => m.SetColor("_BaseColor", green));
            mBlobGold = Custom("M_Blob_Gold", "SignalLost/BlobWobble", m => m.SetColor("_BaseColor", gold));
            mMoon = Lit("M_Moon", Palette.Moon, 0.08f);
            mStation = Lit("M_Station", Palette.Station, 0.2f);
            mStationDark = Lit("M_Station_Dark", Palette.Hex("#2A3150"), 0.25f);
            mGlowCyan = Lit("M_Glow_Cyan", cyan, 0.2f, cyan * 3f);
            mSolar = Lit("M_Solar", Palette.Hex("#1A3A8A"), 0.7f, Palette.Hex("#2A6BFF") * 1.4f);
            mCoin = Lit("M_Coin", gold, 0.6f, gold * 2.4f, true);
            mChute = Lit("M_Chute", Palette.Hex("#3A4466"), 0.35f);
            mChuteRim = Lit("M_ChuteRim", Color.white, 0.2f, Color.white * 2f);
            mGlass = Custom("M_Glass", "SignalLost/Glass", m =>
            {
                m.SetColor("_BaseColor", new Color(0.6f, 0.95f, 1f, 0.2f));
                m.SetColor("_RimColor", cyan);
            });
            mFill = Custom("M_Compressor_Fill", "SignalLost/Glass", m =>
            {
                m.SetColor("_BaseColor", new Color(0.23f, 0.94f, 1f, 0.16f));
                m.SetColor("_RimColor", cyan * 2f);
                m.SetFloat("_RimStrength", 2.2f);
            });
            mPlanet = Custom("M_Planet", "SignalLost/Planet", null);
            mPlanetRing = Custom("M_PlanetRing", "SignalLost/Glass", m =>
            {
                m.SetColor("_BaseColor", new Color(0.95f, 0.75f, 0.6f, 0.35f));
                m.SetColor("_RimColor", new Color(1f, 0.85f, 0.7f, 1f));
                m.SetFloat("_RimStrength", 0.8f);
            });
            mSky = Custom("M_Skybox", "SignalLost/Skybox", null);
            mParticle = Custom("M_Particle_Additive", "SignalLost/ParticleAdditive", m => { m.SetFloat("_Shape", 0f); m.SetFloat("_Intensity", 2.2f); m.enableInstancing = true; });
            mParticleRing = Custom("M_Particle_Ring", "SignalLost/ParticleAdditive", m => { m.SetFloat("_Shape", 1f); m.SetFloat("_Intensity", 2.5f); });
            mParticleSquare = Custom("M_Particle_Square", "SignalLost/ParticleAdditive", m => { m.SetFloat("_Shape", 2f); m.SetFloat("_Intensity", 1.6f); });
            mLine = Custom("M_Line", "SignalLost/ParticleAdditive", m => { m.SetFloat("_Shape", 3f); m.SetFloat("_Intensity", 1.8f); });
            mEyeWhite = Unlit("M_Eye_White", Color.white);
            mEyeBlack = Unlit("M_Eye_Black", new Color(0.03f, 0.03f, 0.06f));
        }

        // ---------------- models ----------------

        /// <summary>1. Blob: one prefab for all types (color/scale/wobble via MaterialPropertyBlock).</summary>
        private static GameObject BuildBlob()
        {
            GameObject root = new GameObject("Blob");
            SphereCollider col = root.AddComponent<SphereCollider>();
            col.radius = 0.35f;
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            Blob blob = root.AddComponent<Blob>();
            int layer = LayerMask.NameToLayer("Blob");
            if (layer >= 0) root.layer = layer;

            Transform visual = Child("Visual", root.transform, Vector3.zero);
            GameObject body = Prim(PrimitiveType.Sphere, "Body", visual, Vector3.zero, Vector3.one * 0.7f, mBlobRed);
            body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Two tiny eyes add charm.
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject eye = Prim(PrimitiveType.Sphere, s < 0 ? "EyeL" : "EyeR", visual, new Vector3(0.1f * s, 0.07f, 0.29f), Vector3.one * 0.13f, mEyeWhite);
                eye.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObject pupil = Prim(PrimitiveType.Sphere, "Pupil", eye.transform, new Vector3(0f, -0.05f, 0.32f), Vector3.one * 0.55f, mEyeBlack);
                pupil.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            SetRef(blob, "visual", visual);
            SetRef(blob, "bodyRenderer", body.GetComponent<MeshRenderer>());
            return SavePrefab(root, PrefabDir, "Blob");
        }

        /// <summary>2. Coin: 6-sided cylinder, radius 0.12, height 0.03, gold emissive, GPU instanced.</summary>
        private static GameObject BuildCoin()
        {
            Mesh hex = SaveMesh(MeshFactory.Cylinder(0.12f, 0.03f, 6), "M_CoinHex");
            GameObject root = new GameObject("Coin");
            root.AddComponent<Coin>();
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            GameObject m = MeshObj("Coin", visual, hex, mCoin, Vector3.zero, Vector3.one, new Vector3(90f, 0f, 0f));
            m.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return SavePrefab(root, PrefabDir, "Coin");
        }

        /// <summary>3. Drone: sphere body + torus ring + 2 cube thrusters + cyan point light.</summary>
        private static GameObject BuildDrone()
        {
            Mesh torus = SaveMesh(MeshFactory.Torus(0.32f, 0.04f, 28, 8), "M_DroneRing");
            GameObject root = new GameObject("Drone");
            Drone drone = root.AddComponent<Drone>();
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            GameObject body = Prim(PrimitiveType.Sphere, "Body", visual, Vector3.zero, Vector3.one * 0.4f, mStation);
            GameObject ring = MeshObj("Ring", visual, torus, mGlowCyan, Vector3.zero, Vector3.one, new Vector3(12f, 0f, 0f));
            Prim(PrimitiveType.Cube, "ThrusterL", visual, new Vector3(-0.27f, -0.06f, 0f), new Vector3(0.1f, 0.1f, 0.18f), mStationDark);
            Prim(PrimitiveType.Cube, "ThrusterR", visual, new Vector3(0.27f, -0.06f, 0f), new Vector3(0.1f, 0.1f, 0.18f), mStationDark);
            Prim(PrimitiveType.Sphere, "Eye", visual, new Vector3(0f, 0.02f, -0.17f), Vector3.one * 0.12f, mGlowCyan);
            Light l = PointLight(root.transform, Vector3.zero, Palette.Cyan, 1.5f, 1.6f);

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mLine;
            trail.time = 0.35f;
            trail.widthMultiplier = 0.09f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;

            GameObject beamGo = new GameObject("Beam", typeof(LineRenderer));
            beamGo.transform.SetParent(root.transform, false);
            LineRenderer beam = beamGo.GetComponent<LineRenderer>();
            beam.sharedMaterial = mLine;
            beam.positionCount = 2;
            beam.widthMultiplier = 0.07f;
            beam.useWorldSpace = true;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.enabled = false;

            SetRef(drone, "visual", visual);
            SetRef(drone, "bodyRenderer", body.GetComponent<MeshRenderer>());
            SetRef(drone, "ringRenderer", ring.GetComponent<MeshRenderer>());
            SetRef(drone, "trail", trail);
            SetRef(drone, "droneLight", l);
            SetRef(drone, "beam", beam);
            return SavePrefab(root, PrefabDir, "Drone");
        }

        /// <summary>4. Compressor: cylinder base + transparent glass + 2 glowing torus bands + cube hopper.</summary>
        private static GameObject BuildCompressor()
        {
            Mesh band = SaveMesh(MeshFactory.Torus(0.6f, 0.055f, 36, 8), "M_CompressorBand");
            GameObject root = new GameObject("Compressor");
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 2.15f, 0f);
            box.size = new Vector3(1.2f, 0.45f, 1.2f);
            Compressor comp = root.AddComponent<Compressor>();
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Base", visual, new Vector3(0f, 0.25f, 0f), new Vector3(1.5f, 0.25f, 1.5f), mStationDark);
            Prim(PrimitiveType.Cylinder, "Foot", visual, new Vector3(0f, 0.06f, 0f), new Vector3(1.8f, 0.06f, 1.8f), mStation);
            GameObject glass = Prim(PrimitiveType.Cylinder, "Glass", visual, new Vector3(0f, 1.2f, 0f), new Vector3(1.12f, 0.7f, 1.12f), mGlass);
            glass.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject b1 = MeshObj("BandLow", visual, band, mGlowCyan, new Vector3(0f, 0.6f, 0f), Vector3.one);
            GameObject b2 = MeshObj("BandHigh", visual, band, mGlowCyan, new Vector3(0f, 1.85f, 0f), Vector3.one);
            Prim(PrimitiveType.Cube, "Hopper", visual, new Vector3(0f, 2.12f, 0f), new Vector3(1.2f, 0.42f, 1.2f), mStation);
            Prim(PrimitiveType.Cube, "HopperMouthInset", visual, new Vector3(0f, 2.33f, 0f), new Vector3(0.86f, 0.04f, 0.86f), mStationDark);
            Transform fillPivot = Child("FillPivot", visual, new Vector3(0f, 0.52f, 0f));
            GameObject fill = Prim(PrimitiveType.Cylinder, "FillGlow", fillPivot, new Vector3(0f, 0.68f, 0f), new Vector3(1.0f, 0.68f, 1.0f), mFill);
            fill.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fillPivot.localScale = new Vector3(1f, 0.001f, 1f);
            Transform interior = Child("Interior", root.transform, new Vector3(0f, 1.2f, 0f));
            Transform mouth = Child("HopperMouth", root.transform, new Vector3(0f, 2.45f, 0f));
            Light l = PointLight(root.transform, new Vector3(0f, 1.3f, -0.9f), Palette.Cyan, 3.5f, 1.2f);

            SetRef(comp, "visual", visual);
            SetRef(comp, "interior", interior);
            SetRef(comp, "hopperMouth", mouth);
            SetRefArray(comp, "bands", new Object[] { b1.GetComponent<MeshRenderer>(), b2.GetComponent<MeshRenderer>() });
            SetRef(comp, "fillGlow", fillPivot);
            SetRef(comp, "fillRenderer", fill.GetComponent<MeshRenderer>());
            SetRef(comp, "glowLight", l);
            return SavePrefab(root, PrefabDir, "Compressor");
        }

        /// <summary>5. Chute: flipped cone (funnel) + glowing torus rim. ONE prefab, color set per instance.</summary>
        private static GameObject BuildChute()
        {
            Mesh funnel = SaveMesh(MeshFactory.Funnel(0.66f, 0.2f, 1.15f, 20), "M_Funnel");
            Mesh rim = SaveMesh(MeshFactory.Torus(0.66f, 0.07f, 32, 8), "M_ChuteRim");
            GameObject root = new GameObject("Chute");
            SphereCollider trig = root.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 0.8f;
            trig.center = new Vector3(0f, 1.2f, 0f);
            Chute chute = root.AddComponent<Chute>();
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Pipe", visual, new Vector3(0f, 0.1f, 0f), new Vector3(0.44f, 0.1f, 0.44f), mStationDark);
            MeshObj("Funnel", visual, funnel, mChute, new Vector3(0f, 0.05f, 0f), Vector3.one);
            GameObject r = MeshObj("Rim", visual, rim, mChuteRim, new Vector3(0f, 1.2f, 0f), Vector3.one);
            Transform mouth = Child("Mouth", root.transform, new Vector3(0f, 1.3f, 0f));
            Light l = PointLight(root.transform, new Vector3(0f, 1.6f, -0.4f), Color.white, 2.6f, 1.2f);
            SetRef(chute, "rimRenderer", r.GetComponent<MeshRenderer>());
            SetRef(chute, "catchTrigger", trig);
            SetRef(chute, "mouth", mouth);
            SetRef(chute, "glowLight", l);
            return SavePrefab(root, PrefabDir, "Chute");
        }

        /// <summary>Antenna segment: thin cylinder + small torus light. One added per Antenna level.</summary>
        private static GameObject BuildStationSegment()
        {
            Mesh ring = SaveMesh(MeshFactory.Torus(0.16f, 0.03f, 20, 6), "M_SegmentRing");
            GameObject root = new GameObject("StationSegment");
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Mast", visual, new Vector3(0f, 0.11f, 0f), new Vector3(0.14f, 0.11f, 0.14f), mStation);
            MeshObj("Light", visual, ring, mGlowCyan, new Vector3(0f, 0.19f, 0f), Vector3.one);
            Prim(PrimitiveType.Cube, "Strut", visual, new Vector3(0f, 0.07f, 0f), new Vector3(0.42f, 0.03f, 0.03f), mStationDark);
            return SavePrefab(root, PrefabDir, "StationSegment");
        }

        /// <summary>6. Station: cube base + cylinder hab, antenna root (segments), dish, solar panels.</summary>
        private static GameObject BuildStation(GameObject segmentPrefab)
        {
            Mesh dishMesh = SaveMesh(MeshFactory.Dish(0.75f, 0.32f, 24, 5), "M_Dish");
            GameObject root = new GameObject("Station");
            Station st = root.AddComponent<Station>();
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            Prim(PrimitiveType.Cube, "Base", visual, new Vector3(0f, 0.4f, 0f), new Vector3(2.6f, 0.8f, 1.8f), mStation);
            Prim(PrimitiveType.Cube, "Trim", visual, new Vector3(0f, 0.84f, 0f), new Vector3(2.7f, 0.1f, 1.9f), mStationDark);
            Prim(PrimitiveType.Cylinder, "Hab", visual, new Vector3(0f, 1.4f, 0f), new Vector3(1.5f, 0.55f, 1.5f), mStation);
            Prim(PrimitiveType.Sphere, "Dome", visual, new Vector3(0f, 1.95f, 0f), new Vector3(1.5f, 1.1f, 1.5f), mStation);
            Renderer w1 = Prim(PrimitiveType.Cube, "Window1", visual, new Vector3(-0.35f, 1.4f, -0.72f), new Vector3(0.18f, 0.18f, 0.05f), mGlowCyan).GetComponent<Renderer>();
            Renderer w2 = Prim(PrimitiveType.Cube, "Window2", visual, new Vector3(0f, 1.4f, -0.76f), new Vector3(0.18f, 0.18f, 0.05f), mGlowCyan).GetComponent<Renderer>();
            Renderer w3 = Prim(PrimitiveType.Cube, "Window3", visual, new Vector3(0.35f, 1.4f, -0.72f), new Vector3(0.18f, 0.18f, 0.05f), mGlowCyan).GetComponent<Renderer>();
            Renderer bl = Prim(PrimitiveType.Sphere, "BeaconL", visual, new Vector3(-1.25f, 0.95f, -0.85f), Vector3.one * 0.14f, mGlowCyan).GetComponent<Renderer>();
            Renderer br = Prim(PrimitiveType.Sphere, "BeaconR", visual, new Vector3(1.25f, 0.95f, -0.85f), Vector3.one * 0.14f, mGlowCyan).GetComponent<Renderer>();
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cube, "Strut", visual, new Vector3(1.55f * s, 0.75f, 0f), new Vector3(0.6f, 0.06f, 0.06f), mStationDark);
                Prim(PrimitiveType.Cube, "SolarPanel", visual, new Vector3(2.35f * s, 0.8f, 0f), new Vector3(1.5f, 0.05f, 0.95f), mSolar, new Vector3(-18f, 0f, 6f * s));
            }

            Transform antenna = Child("AntennaRoot", root.transform, new Vector3(0f, 2.4f, 0f));
            Transform dish = Child("Dish", antenna, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "DishMount", dish, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.12f, 0.12f), mStationDark);
            Transform dishTilt = Child("DishTilt", dish, new Vector3(0f, 0.25f, 0f));
            dishTilt.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            MeshObj("DishBowl", dishTilt, dishMesh, mStation, Vector3.zero, Vector3.one);
            Prim(PrimitiveType.Cylinder, "Feed", dishTilt, new Vector3(0f, 0.3f, 0f), new Vector3(0.04f, 0.3f, 0.04f), mStationDark);
            Renderer feedLight = Prim(PrimitiveType.Sphere, "FeedLight", dishTilt, new Vector3(0f, 0.62f, 0f), Vector3.one * 0.12f, mGlowCyan).GetComponent<Renderer>();

            Transform absorb = Child("AbsorbPoint", root.transform, new Vector3(0f, 0.5f, -1.25f));
            Transform leaks = Child("LeakPoints", root.transform, Vector3.zero);
            Vector3[] leakPositions =
            {
                new Vector3(-1.0f, 0.55f, -0.95f), new Vector3(0.9f, 0.45f, -0.95f), new Vector3(-0.5f, 1.45f, -0.8f),
                new Vector3(0.55f, 1.2f, -0.8f), new Vector3(0f, 2.05f, -0.6f), new Vector3(1.2f, 0.85f, -0.7f),
                new Vector3(-2.2f, 0.85f, -0.3f), new Vector3(2.2f, 0.85f, -0.3f),
            };
            Transform[] leakTs = new Transform[leakPositions.Length];
            for (int i = 0; i < leakPositions.Length; i++) leakTs[i] = Child("Leak" + i, leaks, leakPositions[i]);

            SetRef(st, "antennaRoot", antenna);
            SetRef(st, "dish", dish);
            SetRef(st, "absorbPoint", absorb);
            SetRef(st, "segmentPrefab", segmentPrefab);
            SetFloat(st, "segmentHeight", 0.22f);
            SetRefArray(st, "leakPoints", leakTs);
            SetRefArray(st, "lights", new Object[] { w1, w2, w3, bl, br, feedLight });
            return SavePrefab(root, PrefabDir, "StationBase");
        }

        /// <summary>7. Moon ground: very large flat-shaded sphere (scale 80) with crater rims.</summary>
        private static GameObject BuildMoon()
        {
            Mesh ico = SaveMesh(MeshFactory.Icosphere(40f, 4, 0.006f, 7), "M_MoonIco");
            Mesh crater = SaveMesh(MeshFactory.Torus(1f, 0.16f, 24, 6), "M_CraterRim");
            GameObject root = new GameObject("Moon");
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            GameObject sphere = MeshObj("Surface", visual, ico, mMoon, Vector3.zero, Vector3.one);
            sphere.GetComponent<MeshRenderer>().receiveShadows = true;
            Vector3[] craters = { new Vector3(-6.5f, 0f, -6f), new Vector3(6f, 0f, -9f), new Vector3(-10f, 0f, 2.5f), new Vector3(9.5f, 0f, -1f), new Vector3(2.5f, 0f, -12.5f), new Vector3(-3.5f, 0f, 6.5f) };
            float[] sizes = { 1.6f, 2.2f, 1.3f, 1.8f, 2.6f, 1.1f };
            for (int i = 0; i < craters.Length; i++)
            {
                Vector3 c = craters[i];
                float y = Mathf.Sqrt(Mathf.Max(0f, 40f * 40f - c.x * c.x - c.z * c.z));
                Vector3 pos = new Vector3(c.x, y - 0.03f, c.z);
                GameObject cr = MeshObj("Crater" + i, visual, crater, mMoon, pos, new Vector3(sizes[i], sizes[i] * 0.45f, sizes[i]));
                cr.transform.localRotation = Quaternion.FromToRotation(Vector3.up, pos.normalized);
            }
            return SavePrefab(root, PrefabDir, "Moon");
        }

        /// <summary>Background gas giant: banded sphere + flat ring.</summary>
        private static GameObject BuildPlanet()
        {
            Mesh ringMesh = SaveMesh(MeshFactory.Torus(1.75f, 0.32f, 64, 6), "M_PlanetRing");
            GameObject root = new GameObject("Planet");
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            GameObject s = Prim(PrimitiveType.Sphere, "Globe", visual, Vector3.zero, Vector3.one * 2f, mPlanet);
            s.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject r = MeshObj("Ring", visual, ringMesh, mPlanetRing, Vector3.zero, new Vector3(1f, 0.04f, 1f), new Vector3(16f, 0f, -12f));
            r.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return SavePrefab(root, PrefabDir, "Planet");
        }

        // ---------------- particles ----------------

        private static ParticleSystem NewSystem(string name, out GameObject go, out ParticleSystemRenderer r)
        {
            go = new GameObject(name);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 4000;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            return ps;
        }

        private static Gradient Fade(float holdUntil)
        {
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static GameObject BuildPop()
        {
            ParticleSystem ps = NewSystem("PS_Pop", out GameObject go, out ParticleSystemRenderer r);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.17f);
            main.gravityModifier = 0.6f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.12f;
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.6f);
            ParticleSystem.VelocityOverLifetimeModule vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));
            r.sharedMaterial = mParticle;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.maxParticleSize = 0.6f;
            return SavePrefab(go, VfxDir, "PS_Pop");
        }

        private static GameObject BuildSparkle()
        {
            ParticleSystem ps = NewSystem("PS_Sparkle", out GameObject go, out ParticleSystemRenderer r);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.gravityModifier = -0.1f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.18f;
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve bell = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, bell);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.5f);
            r.sharedMaterial = mParticle;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return SavePrefab(go, VfxDir, "PS_Sparkle");
        }

        private static GameObject BuildConfetti()
        {
            ParticleSystem ps = NewSystem("PS_Confetti", out GameObject go, out ParticleSystemRenderer r);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f, 1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.1f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 35f;
            sh.radius = 0.3f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            ParticleSystem.LimitVelocityOverLifetimeModule lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.drag = 1.2f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.7f);
            r.sharedMaterial = mParticleSquare;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return SavePrefab(go, VfxDir, "PS_Confetti");
        }

        private static GameObject BuildSwirl()
        {
            ParticleSystem ps = NewSystem("PS_VacuumSwirl", out GameObject go, out ParticleSystemRenderer r);
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = Palette.Cyan;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 70f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 1f;
            sh.radiusThickness = 0f;
            sh.rotation = new Vector3(90f, 0f, 0f);
            ParticleSystem.VelocityOverLifetimeModule vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.orbitalY = new ParticleSystem.MinMaxCurve(5f);
            vol.radial = new ParticleSystem.MinMaxCurve(-1.4f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            r.sharedMaterial = mParticle;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return SavePrefab(go, VfxDir, "PS_VacuumSwirl");
        }

        private static GameObject BuildShockwave()
        {
            ParticleSystem ps = NewSystem("PS_Shockwave", out GameObject go, out ParticleSystemRenderer r);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 0.45f;
            main.startSpeed = 0f;
            main.startSize = 4f;
            main.maxParticles = 200;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = false;
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve grow = new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, grow);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.2f);
            r.sharedMaterial = mParticleRing;
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.maxParticleSize = 50f;
            return SavePrefab(go, VfxDir, "PS_Shockwave");
        }

        // ---------------- TextMeshPro materials ----------------

        private static void BuildTmpMaterials(GameAssets assets)
        {
            // Load by path: touching TMP_Settings before the essentials exist would open the importer window.
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null)
            {
                Debug.LogWarning("[Signal Lost] TMP font not found yet; glow/outline text materials will be created at runtime.");
                return;
            }
            assets.tmpGlowMaterial = TmpMaterial("TMP_Glow", font, m =>
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.23f, 0.94f, 1f, 0.55f));
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.7f);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.5f);
            });
            assets.tmpOutlineMaterial = TmpMaterial("TMP_Outline", font, m =>
            {
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.03f, 0.1f, 1f));
                m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
            });
        }

        private static Material TmpMaterial(string name, TMP_FontAsset font, Action<Material> setup)
        {
            string path = "Assets/Fonts/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.CopyPropertiesFromMaterial(font.material);
                m.shader = font.material.shader;
            }
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
