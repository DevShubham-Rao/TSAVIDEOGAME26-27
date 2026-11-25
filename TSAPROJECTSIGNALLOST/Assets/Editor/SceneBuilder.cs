using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignalLost.EditorTools
{
    /// <summary>
    /// Tools > Signal Lost > Build Main Scene: creates Assets/Scenes/Main.unity with the hierarchy from
    /// Section 2 (managers, world, UI) and wires every reference.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string VolumeProfilePath = "Assets/Settings/SL_VolumeProfile.asset";

        // Framed on the station base: the antenna grows into the sky band, below the Data counter.
        private static readonly Vector3 CameraTarget = new Vector3(0f, 1.0f, 5.6f);
        private static readonly Vector3 CameraPosition = CameraTarget + Quaternion.Euler(22f, 0f, 0f) * Vector3.back * 20f;

        [MenuItem("Tools/Signal Lost/Build Main Scene")]
        public static void Build()
        {
            GameAssets assets = AssetDatabase.LoadAssetAtPath<GameAssets>(PlaceholderModelBuilder.GameAssetsPath);
            if (assets == null || assets.blobPrefab == null)
            {
                PlaceholderModelBuilder.BuildAll();
                assets = AssetDatabase.LoadAssetAtPath<GameAssets>(PlaceholderModelBuilder.GameAssetsPath);
            }
            PlaceholderModelBuilder.EnsureFolder("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------- Managers ----------------
            Separator("--- Managers ---");
            GameObject gmGo = new GameObject("GameManager");
            GameManager gm = gmGo.AddComponent<GameManager>();
            gmGo.AddComponent<Balance>();
            gmGo.AddComponent<SaveManager>();
            gmGo.AddComponent<BuffSystem>();
            gmGo.AddComponent<SkillTree>();
            gmGo.AddComponent<DayManager>();
            gmGo.AddComponent<SettingsManager>();
            gmGo.AddComponent<InputReader>();
            gmGo.AddComponent<ComboTracker>();
            gmGo.AddComponent<PlayerController>();

            AudioManager audio = new GameObject("AudioManager").AddComponent<AudioManager>();
            PlaceholderModelBuilder.SetRef(audio, "mixer", AudioMixerBuilder.Build());
            EventDirector eventDirector = new GameObject("EventDirector").AddComponent<EventDirector>();
            new GameObject("MailDirector").AddComponent<MailDirector>();
            new GameObject("DroneManager").AddComponent<DroneManager>();
            new GameObject("TutorialDirector").AddComponent<TutorialDirector>();

            GameObject pools = new GameObject("Pools");
            Pool blobPool = MakePool(pools.transform, "Pool_Blob", assets.blobPrefab, 160);
            Pool coinPool = MakePool(pools.transform, "Pool_Coin", assets.coinPrefab, 160);
            CoinFlock coins = coinPool.gameObject.AddComponent<CoinFlock>();
            PlaceholderModelBuilder.SetRef(coins, "pool", coinPool);
            GameObject vfx = new GameObject("VFX");
            vfx.transform.SetParent(pools.transform, false);
            vfx.AddComponent<VFXPlayer>();

            // ---------------- World ----------------
            Separator("--- World ---");
            // Camera rig: CameraDirector moves the rig (menu drift / eased transitions); shake lives on the camera.
            GameObject rigGo = new GameObject("CameraRig");
            rigGo.transform.position = CameraPosition;
            rigGo.transform.rotation = Quaternion.LookRotation(CameraTarget - CameraPosition);
            CameraDirector director = rigGo.AddComponent<CameraDirector>();
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rigGo.transform, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraShake>();
            camGo.AddComponent<CameraFit>();
            // SPEC-ASSUMPTION: no PhysicsRaycaster. World clicks use our own ray + sphere cast, and a
            // PhysicsRaycaster would make blobs count as "pointer over UI" and swallow every click.
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            GameObject sunGo = new GameObject("Directional Light");
            sunGo.transform.rotation = Quaternion.Euler(42f, -30f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Palette.Hex("#C6D6FF");
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            GameObject volGo = new GameObject("Global Volume");
            Volume volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildVolumeProfile();
            volGo.AddComponent<PostFXController>();

            GameObject moon = Spawn("Assets/Prefabs/Moon.prefab", new Vector3(0f, -39f, 4f));
            GameObject stationGo = Spawn("Assets/Prefabs/StationBase.prefab", new Vector3(0f, 0.95f, 5.6f));
            if (stationGo != null) stationGo.name = "Station";
            Station station = stationGo != null ? stationGo.GetComponent<Station>() : null;
            if (stationGo != null) PlaceholderModelBuilder.SetRef(director, "lookTarget", stationGo.transform);
            Spawn("Assets/Prefabs/Compressor.prefab", new Vector3(2.7f, 0.06f, -4.3f));

            GameObject chutes = new GameObject("Chutes");
            MakeChute(chutes.transform, "Chute_Red", BlobColor.Red, new Vector3(-4.9f, 0.06f, -4.3f));
            MakeChute(chutes.transform, "Chute_Blue", BlobColor.Blue, new Vector3(-2.8f, 0.06f, -4.3f));
            MakeChute(chutes.transform, "Chute_Green", BlobColor.Green, new Vector3(-0.7f, 0.06f, -4.3f));

            GameObject spawnerGo = new GameObject("BlobSpawner");
            BlobSpawner spawner = spawnerGo.AddComponent<BlobSpawner>();
            PlaceholderModelBuilder.SetRef(spawner, "pool", blobPool);
            if (station != null) PlaceholderModelBuilder.SetRef(spawner, "absorbPoint", station.AbsorbPoint);

            GameObject vacGo = new GameObject("Vacuum");
            Vacuum vac = vacGo.AddComponent<Vacuum>();
            LineRenderer ring = vacGo.AddComponent<LineRenderer>();
            ring.sharedMaterial = assets.lineMaterial;
            ring.loop = true;
            ring.positionCount = 56;
            ring.widthMultiplier = 0.04f;
            ring.useWorldSpace = true;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            PlaceholderModelBuilder.SetRef(vac, "ring", ring);

            GameObject plane = new GameObject("PlayPlane");
            plane.transform.position = new Vector3(0f, 1.5f, 0f);

            // Gas giant hanging in the sky band above the moon's curved horizon.
            Vector3 camEuler = camGo.transform.rotation.eulerAngles;
            Vector3 dir = Quaternion.Euler(camEuler.x - 13f, -28f, 0f) * Vector3.forward;
            GameObject planet = Spawn("Assets/Prefabs/Planet.prefab", CameraPosition + dir * 190f);
            if (planet != null)
            {
                planet.transform.localScale = Vector3.one * 26f;
                planet.transform.rotation = Quaternion.Euler(0f, 30f, 8f);
            }

            // ---------------- UI ----------------
            Separator("--- UI ---");
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.layer = 5;
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<UIManager>();

            UIChild<HUD>(canvasGo.transform, "HUD");
            GameObject floating = UIChild<Pool>(canvasGo.transform, "FloatingText").gameObject;
            floating.AddComponent<FloatingTextLayer>();
            EventOverlay overlay = UIChild<EventOverlay>(canvasGo.transform, "EventOverlay");
            UIChild<DayTitleCard>(canvasGo.transform, "DayTitleCard");
            UIChild<EndOfDayScreen>(canvasGo.transform, "EndOfDay");
            UIChild<MailUI>(canvasGo.transform, "MailUI");
            UIChild<TutorialUI>(canvasGo.transform, "TutorialUI");
            UIChild<CutscenePlayer>(canvasGo.transform, "Cutscene");
            UIChild<MainMenuUI>(canvasGo.transform, "MainMenu");
            UIChild<EndingScreen>(canvasGo.transform, "EndingScreen");
            UIChild<PauseMenu>(canvasGo.transform, "PauseMenu");
            UIChild<SettingsScreen>(canvasGo.transform, "Settings");
            UIChild<ConfirmDialog>(canvasGo.transform, "ConfirmDialog");

            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.UI.InputSystemUIInputModule module = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            // ---------------- wiring ----------------
            PlaceholderModelBuilder.SetRef(gm, "assets", assets);
            PlaceholderModelBuilder.SetRef(gm, "worldCamera", cam);
            PlaceholderModelBuilder.SetRef(gm, "station", station);
            PlaceholderModelBuilder.SetRef(gm, "playPlane", plane.transform);
            PlaceholderModelBuilder.SetRef(eventDirector, "overlay", overlay);

            // ---------------- lighting ----------------
            RenderSettings.skybox = assets.skyboxMaterial;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.Hex("#2B3466");
            RenderSettings.ambientEquatorColor = Palette.Hex("#191E3F");
            RenderSettings.ambientGroundColor = Palette.Hex("#07091A");
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Signal Lost] Main scene built at " + ScenePath + (moon != null ? "" : " (moon prefab missing!)"));
        }

        private static void Separator(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(true);
        }

        private static GameObject Spawn(string prefabPath, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Signal Lost] Missing prefab " + prefabPath);
                return null;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = position;
            return go;
        }

        private static void MakeChute(Transform parent, string name, BlobColor color, Vector3 pos)
        {
            GameObject go = Spawn("Assets/Prefabs/Chute.prefab", pos);
            if (go == null) return;
            go.name = name;
            go.transform.SetParent(parent, true);
            Chute c = go.GetComponent<Chute>();
            PlaceholderModelBuilder.SetInt(c, "color", (int)color);
        }

        private static Pool MakePool(Transform parent, string name, GameObject prefab, int prewarm)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Pool p = go.AddComponent<Pool>();
            PlaceholderModelBuilder.SetRef(p, "prefab", prefab);
            PlaceholderModelBuilder.SetInt(p, "prewarm", prewarm);
            return p;
        }

        private static T UIChild<T>(Transform canvas, string name) where T : Component
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }

        /// <summary>Bloom (threshold 0.9, intensity 1.5), ACES tonemapping, slight vignette (0.2).</summary>
        public static VolumeProfile BuildVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                VolumeComponent c = profile.components[i];
                profile.components.RemoveAt(i);
                if (c != null) Object.DestroyImmediate(c, true);
            }
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.5f);
            bloom.scatter.Override(0.7f);
            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            Vignette vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.2f);
            DepthOfField dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(30f);
            dof.gaussianEnd.Override(60f);
            dof.gaussianMaxRadius.Override(1f);
            dof.active = false;
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.postExposure.Override(0.15f);
            foreach (VolumeComponent c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
