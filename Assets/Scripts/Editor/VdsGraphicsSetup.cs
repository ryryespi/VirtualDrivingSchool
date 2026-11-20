using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VirtualDrivingSchool.Rendering;

namespace VirtualDrivingSchool.EditorTools
{
    /// <summary>
    /// One-click, re-runnable graphics setup for low-end PCs:
    /// generates textures + URP materials, remaps Kenney model materials, creates Low/High URP assets,
    /// trims Quality levels to Low (default) + High, builds the High post-processing profile,
    /// writes Resources/VDSArtLibrary.asset and applies sky/ambient/fog to the open scene.
    /// </summary>
    public static class VdsGraphicsSetup
    {
        const string ArtRoot = "Assets/Art";
        const string GenTex = "Assets/Art/Generated/Textures";
        const string GenMat = "Assets/Art/Generated/Materials";
        const string SettingsDir = "Assets/Settings";
        const string LibraryPath = "Assets/Resources/VDSArtLibrary.asset";

        static Shader SimpleLit => Shader.Find("Universal Render Pipeline/Simple Lit");
        static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");
        static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit");

        [MenuItem("Tools/Virtual Driving School/Setup Graphics (URP)")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            var log = new System.Text.StringBuilder();
            EnsureFolder(GenTex); EnsureFolder(GenMat); EnsureFolder(SettingsDir); EnsureFolder("Assets/Resources");

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                log.AppendLine("Color space -> Linear");
            }

            // ---------- URP pipeline assets ----------
            var renderer = LoadOrCreateRenderer(SettingsDir + "/VDS_URP_Renderer.asset");
            var low = LoadOrCreatePipeline(SettingsDir + "/VDS_URP_Low.asset", renderer);
            var high = LoadOrCreatePipeline(SettingsDir + "/VDS_URP_High.asset", renderer);
            ConfigurePipeline(low, false);
            ConfigurePipeline(high, true);
            GraphicsSettings.defaultRenderPipeline = low;
            ConfigureQualityLevels(low, high, log);

            // ---------- Textures ----------
            var asphaltTex = MakeTexture(GenTex + "/asphalt.png", 256, new Color(0.30f, 0.30f, 0.31f), 0.07f, 0.035f, 11);
            var grassTex = MakeTexture(GenTex + "/grass.png", 256, new Color(0.36f, 0.55f, 0.24f), 0.10f, 0.0f, 23);
            var concreteTex = MakeTexture(GenTex + "/concrete.png", 128, new Color(0.70f, 0.70f, 0.68f), 0.05f, 0.01f, 37);

            // ---------- Materials ----------
            var lib = AssetDatabase.LoadAssetAtPath<VdsArtLibrary>(LibraryPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<VdsArtLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }

            lib.grass = Mat("Ground_Grass", SimpleLit, Color.white, grassTex, 0f);
            lib.lotAsphalt = Mat("Ground_LotAsphalt", SimpleLit, new Color(0.95f, 0.95f, 0.97f), asphaltTex, 0.05f);
            lib.roadAsphalt = Mat("Ground_RoadAsphalt", SimpleLit, new Color(0.70f, 0.70f, 0.73f), asphaltTex, 0.05f);
            lib.concrete = Mat("Concrete", SimpleLit, Color.white, concreteTex, 0f);
            lib.lineWhite = Mat("Paint_White", SimpleLit, new Color(0.93f, 0.93f, 0.90f), null, 0.1f);
            lib.lineYellow = Mat("Paint_Yellow", SimpleLit, new Color(0.96f, 0.76f, 0.12f), null, 0.1f);
            lib.interiorDark = Mat("Interior_Dark", SimpleLit, new Color(0.10f, 0.10f, 0.11f), null, 0f);
            lib.interiorTrim = Mat("Interior_Trim", SimpleLit, new Color(0.26f, 0.27f, 0.29f), null, 0f);
            lib.signRed = Mat("Sign_Red", SimpleLit, new Color(0.80f, 0.08f, 0.08f), null, 0f);
            lib.fallbackPaint = Mat("Car_FallbackPaint", LitShader, new Color(0.80f, 0.12f, 0.12f), null, 0.5f);
            lib.mirrorGlass = Mat("Mirror_Glass", UnlitShader, Color.white, null, 0f);
            lib.skybox = MakeSkybox(GenMat + "/Sky_Procedural.mat");

            // ---------- Kenney models: shared instanced materials + readable meshes ----------
            RemapKenney("Kenney_CarKit", LitShader, 0.45f, log);
            RemapKenney("Kenney_CityKitRoads", SimpleLit, 0f, log);
            RemapKenney("Kenney_CityKitSuburban", SimpleLit, 0f, log);
            RemapKenney("Kenney_NatureKit", SimpleLit, 0f, log);

            string car = ArtRoot + "/Kenney_CarKit/Models/";
            string roads = ArtRoot + "/Kenney_CityKitRoads/Models/";
            string sub = ArtRoot + "/Kenney_CityKitSuburban/Models/";
            string nat = ArtRoot + "/Kenney_NatureKit/Models/";
            lib.playerCarBody = Model(car + "hatchback-sports.fbx");
            lib.wheel = Model(car + "wheel-default.fbx");
            lib.parkedCars = new[] { "sedan", "suv", "taxi", "van", "police", "delivery" }.Select(n => Model(car + n + ".fbx")).ToArray();
            lib.cone = Model(car + "cone.fbx");
            lib.stopSign = Model(roads + "road-sign-stop.fbx");
            lib.streetLight = Model(roads + "light-square.fbx");
            lib.barrier = Model(roads + "construction-barrier.fbx");
            lib.trafficLight = Model(roads + "traffic-light.fbx");
            lib.houses = new[] { "building-type-a", "building-type-c", "building-type-f", "building-type-k", "building-type-p" }.Select(n => Model(sub + n + ".fbx")).ToArray();
            lib.fence = Model(sub + "fence-1x4.fbx");
            lib.trees = new[] { nat + "tree_default", nat + "tree_oak", nat + "tree_pineRoundA", nat + "tree_pineTallA", nat + "tree_fat", sub + "tree-large" }.Select(n => Model(n + ".fbx")).ToArray();
            lib.bushes = new[] { "plant_bush", "plant_bushLarge", "grass_large", "flower_yellowA" }.Select(n => Model(nat + n + ".fbx")).ToArray();
            lib.rocks = new[] { Model(nat + "rock_largeA.fbx") };

            lib.highQualityProfile = MakeVolumeProfile(SettingsDir + "/VDS_HighQuality_PostFX.asset");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            ApplySceneLighting(lib, log);
            AssetDatabase.SaveAssets();
            log.AppendLine("VDS graphics setup complete. Default pipeline: " + low.name);
            return log.ToString();
        }

        // ------------------------------------------------------------------
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static GameObject Model(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning("[VDS] Missing model " + path);
            return go;
        }

        static UniversalRendererData LoadOrCreateRenderer(string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data != null) return data;
            var m = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
            if (m != null)
            {
                data = (UniversalRendererData)m.Invoke(null, new object[] { path, RendererType.UniversalRenderer, false, "Renderer" });
            }
            else
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, path);
            }
            var so = new SerializedObject(data);
            SetProp(so, "m_RenderingMode", 0);       // Forward
            SetProp(so, "m_DepthPrimingMode", 0);    // Disabled
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        static UniversalRenderPipelineAsset LoadOrCreatePipeline(string path, UniversalRendererData renderer)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null) return asset;
            asset = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void ConfigurePipeline(UniversalRenderPipelineAsset asset, bool high)
        {
            var so = new SerializedObject(asset);
            SetProp(so, "m_SupportsHDR", high);
            SetProp(so, "m_MSAA", high ? 2 : 1);
            SetProp(so, "m_RenderScale", 1f);
            SetProp(so, "m_RequireDepthTexture", false);
            SetProp(so, "m_RequireOpaqueTexture", false);
            SetProp(so, "m_SupportsTerrainHoles", false);
            SetProp(so, "m_MainLightShadowsSupported", true);
            SetProp(so, "m_MainLightShadowmapResolution", high ? 2048 : 1024);
            SetProp(so, "m_AdditionalLightsRenderingMode", high ? 1 : 0); // PerPixel : Disabled
            SetProp(so, "m_AdditionalLightShadowsSupported", false);
            SetProp(so, "m_ReflectionProbeBlending", false);
            SetProp(so, "m_ShadowDistance", high ? 60f : 40f);
            SetProp(so, "m_ShadowCascadeCount", high ? 2 : 1);
            SetProp(so, "m_Cascade2Split", 0.3f);
            SetProp(so, "m_SoftShadowsSupported", high);
            SetProp(so, "m_SoftShadowQuality", 1); // Low
            SetProp(so, "m_UseSRPBatcher", true);
            SetProp(so, "m_EnableLODCrossFade", false);
            SetProp(so, "m_ColorGradingMode", 0); // LDR grading (cheaper)
            SetProp(so, "m_ColorGradingLutSize", 16);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void SetProp(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[VDS] Property not found: " + name); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: if (p.propertyType == SerializedPropertyType.Enum) p.intValue = i; else p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case Object o: p.objectReferenceValue = o; break;
            }
        }

        static void ConfigureQualityLevels(RenderPipelineAsset low, RenderPipelineAsset high, System.Text.StringBuilder log)
        {
            var qAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (qAsset == null) { log.AppendLine("QualitySettings asset not found"); return; }
            var so = new SerializedObject(qAsset);
            var levels = so.FindProperty("m_QualitySettings");
            // Keep exactly two levels: "Low" then "High"
            for (int i = levels.arraySize - 1; i >= 0; i--)
            {
                string n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (n != "Low" && n != "High") levels.DeleteArrayElementAtIndex(i);
            }
            if (levels.arraySize != 2) { log.AppendLine("Unexpected quality level layout (" + levels.arraySize + ")"); }
            for (int i = 0; i < levels.arraySize; i++)
            {
                var e = levels.GetArrayElementAtIndex(i);
                bool isHigh = e.FindPropertyRelative("name").stringValue == "High";
                e.FindPropertyRelative("customRenderPipeline").objectReferenceValue = isHigh ? high : low;
                SetRel(e, "vSyncCount", 1);
                SetRel(e, "antiAliasing", 0);
                SetRel(e, "anisotropicTextures", isHigh ? 2 : 1);
                SetRel(e, "lodBias", isHigh ? 1.5f : 1f);
                SetRel(e, "realtimeReflectionProbes", false);
                SetRel(e, "softParticles", false);
                SetRel(e, "pixelLightCount", isHigh ? 2 : 1);
            }
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null && perPlatform.isArray)
            {
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var second = perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second");
                    if (second != null) second.intValue = 0;
                }
            }
            so.FindProperty("m_CurrentQuality").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            log.AppendLine("Quality levels: " + string.Join(", ", QualitySettings.names));
        }

        static void SetRel(SerializedProperty e, string name, object v)
        {
            var p = e.FindPropertyRelative(name);
            if (p == null) return;
            if (v is int i) p.intValue = i; else if (v is float f) p.floatValue = f; else if (v is bool b) p.boolValue = b;
        }

        static Texture2D MakeTexture(string path, int size, Color baseColor, float noiseAmp, float speckle, int seed)
        {
            var rng = new System.Random(seed);
            // periodic value noise (tileable)
            int cells = 16;
            float[,] grid = new float[cells, cells];
            for (int y = 0; y < cells; y++) for (int x = 0; x < cells; x++) grid[x, y] = (float)rng.NextDouble();
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = (float)x / size * cells, fy = (float)y / size * cells;
                    int x0 = (int)fx, y0 = (int)fy; float tx = fx - x0, ty = fy - y0;
                    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                    float a = grid[x0 % cells, y0 % cells], b = grid[(x0 + 1) % cells, y0 % cells];
                    float c = grid[x0 % cells, (y0 + 1) % cells], d = grid[(x0 + 1) % cells, (y0 + 1) % cells];
                    float low = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty) - 0.5f;
                    float fine = (float)rng.NextDouble() - 0.5f;
                    float v = low * noiseAmp + fine * noiseAmp * 0.6f;
                    if (speckle > 0f && rng.NextDouble() < 0.04) v += speckle * 2f;
                    Color col = new Color(baseColor.r + v, baseColor.g + v * (noiseAmp > 0.08f ? 1.3f : 1f), baseColor.b + v);
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 2;
            imp.maxTextureSize = size;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(string name, Shader shader, Color color, Texture tex, float smoothness)
        {
            string path = GenMat + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MakeSkybox(string path)
        {
            var sh = Shader.Find("Skybox/Procedural");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_SunDisk", 1f);          // simple sun
            m.SetFloat("_SunSize", 0.035f);
            m.SetFloat("_SunSizeConvergence", 6f);
            m.SetFloat("_AtmosphereThickness", 0.85f);
            m.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.70f));
            m.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.40f));
            m.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void RemapKenney(string pack, Shader shader, float smoothness, System.Text.StringBuilder log)
        {
            string folder = ArtRoot + "/" + pack + "/Models";
            Texture2D colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/colormap.png");
            if (colormap != null)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(colormap));
                if (ti.mipmapEnabled || ti.filterMode != FilterMode.Bilinear)
                {
                    ti.mipmapEnabled = false; ti.filterMode = FilterMode.Bilinear; ti.SaveAndReimport();
                }
            }
            var shared = new Dictionary<string, Material>();
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                // Source material names: from existing remaps (re-run) or the embedded FBX materials (first run)
                var embedded = new Dictionary<string, Color>();
                var existingMap = imp.GetExternalObjectMap();
                foreach (var p in existingMap)
                    if (p.Key.type == typeof(Material) && p.Value is Material pm)
                        embedded[p.Key.name] = pm.HasProperty("_BaseColor") ? pm.GetColor("_BaseColor") : Color.white;
                if (embedded.Count == 0)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null)
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                            foreach (var sm in r.sharedMaterials)
                                if (sm != null && !embedded.ContainsKey(sm.name))
                                    embedded[sm.name] = sm.HasProperty("_BaseColor") ? sm.GetColor("_BaseColor") : (sm.HasProperty("_Color") ? sm.color : Color.white);
                }
                foreach (var kv in embedded)
                {
                    string matName = kv.Key;
                    if (!shared.TryGetValue(matName, out var mat))
                    {
                        bool isColormap = matName.ToLower().Contains("colormap");
                        Color c = isColormap ? Color.white : kv.Value;
                        if (ColorOverrides.TryGetValue(matName, out Color oc)) c = oc;
                        mat = Mat(pack + "_" + matName, shader, c, isColormap ? colormap : null, smoothness);
                        shared[matName] = mat;
                    }
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
                }
                imp.isReadable = true;          // needed for runtime static batching
                imp.importCameras = false;
                imp.importLights = false;
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.addCollider = false;
                imp.SaveAndReimport();
                count++;
            }
            log.AppendLine(pack + ": " + count + " models, materials: " + string.Join(",", shared.Keys));
        }

        // Softer, more natural greens than the kit defaults (which read teal in linear lighting)
        static readonly Dictionary<string, Color> ColorOverrides = new Dictionary<string, Color>
        {
            { "leafsGreen", new Color(0.42f, 0.70f, 0.30f) },
            { "leafsDark", new Color(0.24f, 0.50f, 0.28f) },
            { "grass", new Color(0.45f, 0.70f, 0.30f) },
            { "woodBark", new Color(0.45f, 0.30f, 0.20f) },
            { "woodBarkDark", new Color(0.36f, 0.25f, 0.18f) },
        };

        static VolumeProfile MakeVolumeProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c))
                {
                    c = profile.Add<T>(true);
                    c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
                return c;
            }
            var bloom = Get<Bloom>();
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);
            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var ca = Get<ColorAdjustments>();
            ca.postExposure.Override(0.1f);
            ca.contrast.Override(8f);
            ca.saturation.Override(10f);
            var vig = Get<Vignette>();
            vig.intensity.Override(0.18f);
            vig.smoothness.Override(0.4f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void ApplySceneLighting(VdsArtLibrary lib, System.Text.StringBuilder log)
        {
            var scene = EditorSceneManager.GetActiveScene();
            RenderSettings.skybox = lib.skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.52f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.27f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.80f, 0.88f);
            RenderSettings.fogStartDistance = 110f;
            RenderSettings.fogEndDistance = 380f;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                light.color = new Color(1f, 0.95f, 0.86f);
                light.intensity = 1.3f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.8f;
                EditorUtility.SetDirty(light);
                RenderSettings.sun = light;
            }
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 320f;
                if (cam.GetComponent<UniversalAdditionalCameraData>() == null) cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                EditorUtility.SetDirty(cam);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("Scene lighting applied + saved: " + scene.path);
        }
    }
}
