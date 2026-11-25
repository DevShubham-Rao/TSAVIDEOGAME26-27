using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SignalLost.EditorTools
{
    /// <summary>
    /// One-click project setup (Section 2): Blob layer, TMP Essentials, URP asset with HDR, linear color,
    /// placeholder prefabs and the Main scene. Runs automatically the first time the project opens without
    /// Assets/Scenes/Main.unity, and any time from Tools > Signal Lost > Setup Everything.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string SessionKey = "SignalLost.SetupAttempted";
        private const string TmpEssentialsFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string UrpAssetPath = "Assets/Settings/SL_URP.asset";
        private const string RendererPath = "Assets/Settings/SL_Renderer.asset";
        private const string UrpPackage = "Packages/com.unity.render-pipelines.universal";

        /// <summary>Bump when generated content (scene, prefabs) changes so open projects rebuild it once.</summary>
        public const int SetupVersion = 3;
        private const string VersionMarker = "Assets/Settings/SL_SetupVersion.txt";

        private static int InstalledVersion
        {
            get
            {
                try
                {
                    return File.Exists(VersionMarker) ? int.Parse(File.ReadAllText(VersionMarker).Trim()) : 0;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        static ProjectSetup()
        {
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (Application.isBatchMode) return;
            bool upToDate = File.Exists(SceneBuilder.ScenePath) && InstalledVersion >= SetupVersion;
            if (upToDate) return;
            if (SessionState.GetBool(SessionKey + SetupVersion, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoRun;
                return;
            }
            SessionState.SetBool(SessionKey + SetupVersion, true);
            Debug.Log(File.Exists(SceneBuilder.ScenePath)
                ? "[Signal Lost] Generated content is out of date: rebuilding prefabs and Main scene (v" + SetupVersion + ")."
                : "[Signal Lost] First open detected: running project setup.");
            RunAll();
        }

        private static bool exitWhenDone;
        private static bool buildWhenDone;

        [MenuItem("Tools/Signal Lost/Setup Everything (URP, prefabs, scene)")]
        public static void RunAll()
        {
            EnsureLayer("Blob");
            // TMP Essentials import is asynchronous: continue once it has finished.
            if (!File.Exists(TmpEssentialsFont) && StartTmpImport()) return;
            ContinueSetup();
        }

        /// <summary>
        /// Batch entry point (run Unity WITHOUT -quit): full setup, optional Windows build with -slBuild,
        /// then exits. Example: Unity -batchmode -projectPath . -executeMethod SignalLost.EditorTools.ProjectSetup.BatchSetup -slBuild
        /// </summary>
        public static void BatchSetup()
        {
            exitWhenDone = true;
            buildWhenDone = Array.IndexOf(Environment.GetCommandLineArgs(), "-slBuild") >= 0;
            RunAll();
        }

        private static void ContinueSetup()
        {
            try
            {
                EnsureUrp();
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.companyName = "Relay Omega";
                PlayerSettings.productName = "Signal Lost - Static Sweeper";
                PlayerSettings.bundleVersion = "2.0.0";
                PlaceholderModelBuilder.BuildAll();
                SceneBuilder.Build();
                File.WriteAllText(VersionMarker, SetupVersion.ToString());
                AssetDatabase.ImportAsset(VersionMarker);
                AssetDatabase.SaveAssets();
                if (!Application.isBatchMode) EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
                Debug.Log("[Signal Lost] Setup complete. Open Assets/Scenes/Main.unity and press Play.");
                if (buildWhenDone) BuildWindows();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (exitWhenDone) EditorApplication.Exit(1);
                return;
            }
            if (exitWhenDone) EditorApplication.Exit(0);
        }

        private static bool StartTmpImport()
        {
            string package = Path.GetFullPath("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(package))
            {
                Debug.LogWarning("[Signal Lost] TMP Essential Resources package not found at " + package + ". Import it from Window > TextMeshPro.");
                return false;
            }
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.importPackageFailed += OnTmpFailed;
            AssetDatabase.ImportPackage(package, false);
            Debug.Log("[Signal Lost] Importing TMP Essential Resources...");
            return true;
        }

        private static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            AssetDatabase.importPackageFailed -= OnTmpFailed;
            AssetDatabase.Refresh();
            EditorApplication.delayCall += ContinueSetup;
        }

        private static void OnTmpFailed(string packageName, string error)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            AssetDatabase.importPackageFailed -= OnTmpFailed;
            Debug.LogWarning("[Signal Lost] TMP import failed (" + error + "); continuing without it.");
            EditorApplication.delayCall += ContinueSetup;
        }

        [MenuItem("Tools/Signal Lost/Build Windows Player")]
        public static void BuildWindows()
        {
            string output = "Builds/Windows/SignalLost.exe";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-slBuildPath") output = args[i + 1];
            }
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Signal Lost] Build result: " + report.summary.result + " -> " + output);
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        // ---------------- steps ----------------

        public static void EnsureLayer(string name)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return;
            }
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[Signal Lost] Added layer '" + name + "' at index " + i);
                    return;
                }
            }
            Debug.LogWarning("[Signal Lost] No free layer slot for '" + name + "'. Blob clicks will use the Default layer.");
        }

        private static void EnsureUrp()
        {
            UniversalRenderPipelineAsset existing = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (existing == null) existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (existing == null)
            {
                PlaceholderModelBuilder.EnsureFolder("Assets/Settings");
                ScriptableRendererData data = CreateRendererData();
                existing = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(existing, UrpAssetPath);
            }
            existing.supportsHDR = true;
            existing.msaaSampleCount = 4;
            existing.shadowDistance = 45f;
            existing.renderScale = 1f;
            EditorUtility.SetDirty(existing);

            // Quality presets for Settings > Graphics: Low / Medium / High each get their own URP asset.
            ScriptableRendererData renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            UniversalRenderPipelineAsset low = QualityAsset("Assets/Settings/SL_URP_Low.asset", renderer, 1, 0f, 0.85f);
            UniversalRenderPipelineAsset medium = QualityAsset("Assets/Settings/SL_URP_Medium.asset", renderer, 2, 25f, 1f);
            UniversalRenderPipelineAsset high = existing;

            GraphicsSettings.defaultRenderPipeline = high;
            int current = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                string n = names[i].ToLowerInvariant();
                QualitySettings.renderPipeline = n.Contains("low") ? (low ?? high) : (n.Contains("medium") ? (medium ?? high) : high);
            }
            int highIndex = Array.IndexOf(names, "High");
            QualitySettings.SetQualityLevel(highIndex >= 0 ? highIndex : current, false);
            AssetDatabase.SaveAssets();
        }

        private static UniversalRenderPipelineAsset QualityAsset(string path, ScriptableRendererData renderer, int msaa, float shadowDistance, float renderScale)
        {
            if (renderer == null) return null;
            UniversalRenderPipelineAsset a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (a == null)
            {
                a = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(a, path);
            }
            a.supportsHDR = true;
            a.msaaSampleCount = msaa;
            a.shadowDistance = shadowDistance;
            a.renderScale = renderScale;
            EditorUtility.SetDirty(a);
            return a;
        }

        private static ScriptableRendererData CreateRendererData()
        {
            ScriptableRendererData existing = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (existing != null) return existing;

            // Preferred: URP's own (internal) factory, which fills in shaders and post-process data.
            MethodInfo create = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
            if (create != null)
            {
                try
                {
                    ParameterInfo[] ps = create.GetParameters();
                    object[] args = new object[ps.Length];
                    args[0] = RendererPath;
                    if (ps.Length > 1) args[1] = Enum.Parse(ps[1].ParameterType, "UniversalRenderer");
                    if (ps.Length > 2) args[2] = false;
                    if (ps.Length > 3) args[3] = "Renderer";
                    ScriptableRendererData made = create.Invoke(null, args) as ScriptableRendererData;
                    if (made != null)
                    {
                        AssetDatabase.SaveAssets();
                        return AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath) ?? made;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Signal Lost] URP renderer factory failed, using fallback: " + e.Message);
                }
            }

            // Fallback: public API + resource reload.
            UniversalRendererData data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, RendererPath);
            data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(UrpPackage + "/Runtime/Data/PostProcessData.asset");
            ReloadNullResources(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void ReloadNullResources(object target)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType("UnityEngine.Rendering.ResourceReloader");
                if (t == null) continue;
                MethodInfo m = t.GetMethod("ReloadAllNullIn", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(object), typeof(string) }, null);
                if (m == null) continue;
                try
                {
                    m.Invoke(null, new object[] { target, UrpPackage });
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Signal Lost] Resource reload failed: " + e.Message);
                }
                return;
            }
        }
    }
}
