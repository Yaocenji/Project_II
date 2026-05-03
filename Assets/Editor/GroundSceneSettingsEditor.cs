using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RadianceCascadesWorldBVH;

namespace ProjectII.Render
{
    [CustomEditor(typeof(GroundSceneSettings))]
    public class GroundSceneSettingsEditor : Editor
    {
        private GroundSceneSettings m_Target;
        private bool m_BrushActive;

        private const string k_BakedChildName = "_GroundBaked";

        private static readonly string[] k_ChannelNames = { "R", "G", "B", "A" };
        private static readonly Color[] k_ChannelColors =
        {
            new Color(1f, 0.3f, 0.3f),   // R
            new Color(0.3f, 1f, 0.3f),   // G
            new Color(0.3f, 0.5f, 1f),   // B
            new Color(1f, 1f, 0.3f),     // A
        };

        private void OnEnable()
        {
            m_Target = (GroundSceneSettings)target;
        }

        private void OnDisable()
        {
            if (m_BrushActive)
            {
                GroundBrushTool.Deactivate();
                m_BrushActive = false;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── 场景 AABB（从 RCWB 系统读取，只读显示）──
            EditorGUILayout.LabelField("场景 AABB（来自 RCWB）", EditorStyles.boldLabel);
            Vector4 aabbDisplay = GroundSceneSettings.GetSceneAABB();
            EditorGUILayout.Vector4Field("AABB (minX, minY, maxX, maxY)", aabbDisplay);

            EditorGUILayout.Space(6);

            // ── SplatLayer 配置 ──
            EditorGUILayout.LabelField("Splatmap 层（最多4层，对应 RGBA）", EditorStyles.boldLabel);
            var layersProp = serializedObject.FindProperty("splatLayers");

            for (int i = 0; i < layersProp.arraySize && i < 4; i++)
            {
                var layerProp = layersProp.GetArrayElementAtIndex(i);
                string ch = i < k_ChannelNames.Length ? k_ChannelNames[i] : "?";
                Color chColor = i < k_ChannelColors.Length ? k_ChannelColors[i] : Color.white;

                EditorGUILayout.BeginVertical("HelpBox");

                // 标题行：通道标记 + 层名
                EditorGUILayout.BeginHorizontal();
                GUI.color = chColor;
                GUILayout.Label($"■ {ch}", EditorStyles.boldLabel, GUILayout.Width(30));
                GUI.color = Color.white;
                EditorGUILayout.PropertyField(layerProp.FindPropertyRelative("layerName"), GUIContent.none);
                EditorGUILayout.EndHorizontal();

                // 地砖 Sprite 列表
                var spritesProp = layerProp.FindPropertyRelative("tileSprites");
                EditorGUILayout.PropertyField(spritesProp, new GUIContent("地砖 Sprite 列表"), true);

                // Hex-Tile 开关
                EditorGUILayout.PropertyField(layerProp.FindPropertyRelative("useHexTile"));

                // 整体旋转
                EditorGUILayout.PropertyField(layerProp.FindPropertyRelative("tileRotation"));

                // 地砖世界尺寸
                EditorGUILayout.PropertyField(layerProp.FindPropertyRelative("tileWorldSize"));

                // 校验提示
                if (spritesProp.arraySize > 0)
                {
                    // 检查 Sprite 是否共享同一 Texture
                    Texture firstTex = null;
                    bool valid = true;
                    for (int si = 0; si < spritesProp.arraySize; si++)
                    {
                        var sprObj = spritesProp.GetArrayElementAtIndex(si).objectReferenceValue;
                        if (sprObj == null) continue;
                        Sprite spr = sprObj as Sprite;
                        if (spr == null) continue;
                        if (firstTex == null)
                            firstTex = spr.texture;
                        else if (spr.texture != firstTex)
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid)
                        EditorGUILayout.HelpBox("同层内所有 Sprite 必须共享同一 Atlas Texture！", MessageType.Error);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // 添加/删除层按钮
            EditorGUILayout.BeginHorizontal();
            if (layersProp.arraySize < 4)
            {
                if (GUILayout.Button("+ 添加层"))
                {
                    layersProp.InsertArrayElementAtIndex(layersProp.arraySize);
                    var newLayer = layersProp.GetArrayElementAtIndex(layersProp.arraySize - 1);
                    newLayer.FindPropertyRelative("layerName").stringValue = $"Layer {layersProp.arraySize}";
                }
            }
            if (layersProp.arraySize > 0)
            {
                if (GUILayout.Button("- 删除末层"))
                    layersProp.DeleteArrayElementAtIndex(layersProp.arraySize - 1);
            }
            EditorGUILayout.EndHorizontal();

            if (layersProp.arraySize > 4)
                EditorGUILayout.HelpBox("最多4层（对应 RGBA 四通道），多余层将被忽略。", MessageType.Warning);

            EditorGUILayout.Space(6);

            // ── Splatmap 权重图 ──
            EditorGUILayout.LabelField("Splatmap 权重图", EditorStyles.boldLabel);
            var splatmapProp = serializedObject.FindProperty("splatmap");
            EditorGUILayout.PropertyField(splatmapProp);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("previewMaterial"));

            Texture2D splatmap = m_Target.splatmap;
            if (splatmap == null)
            {
                EditorGUILayout.HelpBox("未指定 Splatmap。点击下方按钮创建。", MessageType.Info);
                if (GUILayout.Button("创建 Splatmap", GUILayout.Height(24)))
                {
                    GroundBrushTool.CreateSplatmap(m_Target);
                    GroundBrushTool.ForceRefreshPreview(m_Target);
                    Debug.Log("[GroundSceneSettings] Splatmap 已创建。");
                }
            }
            else
            {
                if (!splatmap.isReadable)
                    EditorGUILayout.HelpBox("Splatmap 不可读！请在导入设置中启用 Read/Write。", MessageType.Error);

                // 预览（用 DrawPreviewTexture 正确显示 RGBA）
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("预览");
                var previewRect = GUILayoutUtility.GetRect(128, 128, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(previewRect, splatmap, null, ScaleMode.ScaleToFit);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"尺寸: {splatmap.width}×{splatmap.height}  格式: {splatmap.format}");
            }

            EditorGUILayout.Space(6);

            // ── 笔刷工具 ──
            EditorGUILayout.LabelField("笔刷工具", EditorStyles.boldLabel);
            if (m_BrushActive)
            {
                GUI.color = new Color(0.4f, 1f, 0.4f);
                if (GUILayout.Button("关闭笔刷", GUILayout.Height(24)))
                {
                    GroundBrushTool.Deactivate();
                    m_BrushActive = false;
                }
                GUI.color = Color.white;

                GroundBrushTool.mode = (GroundBrushTool.BrushMode)EditorGUILayout.EnumPopup("模式", GroundBrushTool.mode);
                GroundBrushTool.brushRadius = EditorGUILayout.Slider("半径", GroundBrushTool.brushRadius, 0.1f, 20f);
                GroundBrushTool.brushStrength = EditorGUILayout.Slider("强度", GroundBrushTool.brushStrength, 0.01f, 1f);

                // 通道选择（带颜色标记）
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("通道");
                for (int i = 0; i < 4; i++)
                {
                    GUI.color = (GroundBrushTool.selectedChannel == i) ? k_ChannelColors[i] : Color.gray;
                    if (GUILayout.Toggle(GroundBrushTool.selectedChannel == i, k_ChannelNames[i], EditorStyles.miniButton))
                        GroundBrushTool.selectedChannel = i;
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "左键拖拽绘制 | Ctrl+左键 擦除 | 场景视图中操作\n权重归一化：R+G+B+A=1",
                    MessageType.Info);
            }
            else
            {
                if (GUILayout.Button("开启笔刷", GUILayout.Height(24)))
                {
                    if (m_Target.splatmap == null)
                    {
                        EditorUtility.DisplayDialog("无法开启笔刷", "请先创建或指定 Splatmap。", "OK");
                    }
                    else if (!m_Target.splatmap.isReadable)
                    {
                        EditorUtility.DisplayDialog("无法开启笔刷", "Splatmap 不可读，请在导入设置中启用 Read/Write。", "OK");
                    }
                    else
                    {
                        GroundBrushTool.Activate(m_Target);
                        m_BrushActive = true;
                    }
                }
            }

            EditorGUILayout.Space(6);

            // ── 烘焙设置 ──
            EditorGUILayout.LabelField("烘焙设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pixelsPerUnit"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rcwbMaterial"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sortingOrder"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("randomSeed"));

            EditorGUILayout.Space(8);

            // ── 烘焙按钮 ──
            GUI.color = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("烘焙地面 Sprite", GUILayout.Height(32)))
                BakeGround();
            GUI.color = Color.white;

            var child = m_Target.transform.Find(k_BakedChildName);
            if (child != null)
            {
                if (GUILayout.Button("删除烘焙结果"))
                    DeleteBaked();
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                GroundBrushTool.ForceRefreshPreview(m_Target);
            }
        }

        // ── 烘焙入口 ──────────────────────────────────────────────────────────

        private void BakeGround()
        {
            if (m_Target.splatLayers == null || m_Target.splatLayers.Count == 0)
            {
                EditorUtility.DisplayDialog("烘焙失败", "至少需要1个 SplatLayer。", "OK");
                return;
            }

            Vector4 aabb = GroundSceneSettings.GetSceneAABB();
            if (aabb.z - aabb.x < 0.001f || aabb.w - aabb.y < 0.001f)
            {
                EditorUtility.DisplayDialog("烘焙失败", "场景 AABB 范围无效。请检查 RCWBSceneSettings 或 PolygonManagerSettings。", "OK");
                return;
            }

            if (m_Target.splatmap == null)
            {
                EditorUtility.DisplayDialog("烘焙失败", "请先创建或指定 Splatmap。", "OK");
                return;
            }

            for (int i = 0; i < m_Target.splatLayers.Count && i < 4; i++)
            {
                var layer = m_Target.splatLayers[i];
                if (layer.tileSprites == null || layer.tileSprites.Count == 0) continue;
                if (layer.tileSprites[0] == null || layer.tileSprites[0].texture == null)
                {
                    EditorUtility.DisplayDialog("烘焙失败",
                        $"SplatLayer[{i}] \"{layer.layerName}\" 的 Sprite 无效。", "OK");
                    return;
                }
            }

            var regions = new List<GroundRegion>(FindObjectsOfType<GroundRegion>());

            Vector3 ls = m_Target.transform.lossyScale;
            if (Mathf.Abs(ls.x - 1f) > 0.001f || Mathf.Abs(ls.y - 1f) > 0.001f || Mathf.Abs(ls.z - 1f) > 0.001f)
            {
                EditorUtility.DisplayDialog("烘焙失败",
                    $"物体世界 Scale 必须为 (1,1,1)，当前为 ({ls.x:F3}, {ls.y:F3}, {ls.z:F3})。",
                    "OK");
                return;
            }

            Texture2D bumpTex;
            Texture2D tex = GroundBuilder.Build(
                aabb,
                m_Target.splatLayers,
                m_Target.splatmap,
                regions,
                m_Target.pixelsPerUnit,
                m_Target.randomSeed,
                out bumpTex);

            if (tex == null)
            {
                EditorUtility.DisplayDialog("烘焙失败", "纹理生成失败，请检查参数。", "OK");
                return;
            }

            string pngPath = GetAssetSavePath(m_Target.gameObject.scene.path,
                m_Target.gameObject.name + "_GroundTex.png");
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            // 保存法线纹理
            string bumpPath = null;
            if (bumpTex != null)
            {
                bumpPath = GetAssetSavePath(m_Target.gameObject.scene.path,
                    m_Target.gameObject.name + "_GroundBumpTex.png");
                File.WriteAllBytes(bumpPath, bumpTex.EncodeToPNG());
                Object.DestroyImmediate(bumpTex);
                AssetDatabase.ImportAsset(bumpPath, ImportAssetOptions.ForceUpdate);

                var bumpImporter = AssetImporter.GetAtPath(bumpPath) as TextureImporter;
                if (bumpImporter != null)
                {
                    bumpImporter.textureType = TextureImporterType.Default;
                    bumpImporter.sRGBTexture = false;
                    bumpImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    bumpImporter.isReadable = true;
                    bumpImporter.filterMode = FilterMode.Point;
                    bumpImporter.wrapMode = TextureWrapMode.Clamp;
                    bumpImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                    bumpImporter.SaveAndReimport();
                }
            }

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType  = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePackingTag = string.Empty;
                importer.filterMode  = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable    = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;

                float rangeX = aabb.z - aabb.x;
                int texW = Mathf.Max(1, Mathf.CeilToInt(rangeX * m_Target.pixelsPerUnit));
                importer.spritePixelsPerUnit = texW / rangeX;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0f, 0f);
                importer.SetTextureSettings(settings);

                // 附加 _BumpMap secondary texture
                if (bumpPath != null)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();

                    // 通过 SerializedObject 设置 secondaryTextures（兼容旧版 Unity）
                    var importer2 = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                    if (importer2 != null)
                    {
                        var so = new SerializedObject(importer2);
                        var secTexProp = so.FindProperty("m_SpriteSheet.secondaryTextures");
                        if (secTexProp != null)
                        {
                            secTexProp.ClearArray();
                            secTexProp.InsertArrayElementAtIndex(0);
                            var elem = secTexProp.GetArrayElementAtIndex(0);
                            elem.FindPropertyRelative("name").stringValue = "_BumpMap";
                            elem.FindPropertyRelative("texture").objectReferenceValue =
                                AssetDatabase.LoadAssetAtPath<Texture2D>(bumpPath);
                            so.ApplyModifiedProperties();
                        }
                        EditorUtility.SetDirty(importer2);
                        importer2.SaveAndReimport();
                    }
                }
                else
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(pngPath);
            Sprite savedSprite = null;
            foreach (var a in assets)
                if (a is Sprite s) { savedSprite = s; break; }

            if (savedSprite == null)
            {
                var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                if (savedTex != null)
                {
                    float rangeX = aabb.z - aabb.x;
                    int texW = Mathf.Max(1, Mathf.CeilToInt(rangeX * m_Target.pixelsPerUnit));
                    float ppu = texW / rangeX;
                    savedSprite = Sprite.Create(savedTex,
                        new Rect(0, 0, savedTex.width, savedTex.height),
                        Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
                    savedSprite.name = m_Target.gameObject.name + "_GroundSprite";
                    AssetDatabase.AddObjectToAsset(savedSprite, pngPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    assets = AssetDatabase.LoadAllAssetsAtPath(pngPath);
                    foreach (var a in assets)
                        if (a is Sprite s) { savedSprite = s; break; }
                }
            }

            if (savedSprite == null)
            {
                EditorUtility.DisplayDialog("烘焙失败", "Sprite 创建失败。", "OK");
                return;
            }

            var child = m_Target.transform.Find(k_BakedChildName);
            GameObject childGO;
            if (child != null)
            {
                childGO = child.gameObject;
                Undo.RegisterCompleteObjectUndo(childGO, "Rebake Ground");
            }
            else
            {
                childGO = new GameObject(k_BakedChildName);
                Undo.RegisterCreatedObjectUndo(childGO, "Bake Ground");
                childGO.transform.SetParent(m_Target.transform, false);
            }

            Vector3 origin = new Vector3(aabb.x, aabb.y, 0f);
            childGO.transform.localPosition = m_Target.transform.InverseTransformPoint(origin);
            childGO.transform.localRotation = Quaternion.identity;
            childGO.transform.localScale    = Vector3.one;

            var sr = childGO.GetComponent<SpriteRenderer>();
            if (sr == null) sr = childGO.AddComponent<SpriteRenderer>();
            sr.sprite = savedSprite;
            sr.sharedMaterial = m_Target.rcwbMaterial;
            sr.sortingLayerID = SortingLayer.NameToID("Default");
            sr.sortingOrder   = m_Target.sortingOrder;

            var rcwb = childGO.GetComponent<RCWBObject>();
            if (rcwb == null) rcwb = childGO.AddComponent<RCWBObject>();
            rcwb.IsWall = false;

            EditorUtility.SetDirty(childGO);
            EditorUtility.SetDirty(m_Target);
            SceneView.RepaintAll();

            Debug.Log($"[GroundSceneSettings] 烘焙完成 → {pngPath}，包含 {regions.Count} 个 GroundRegion。");
        }

        private void DeleteBaked()
        {
            var child = m_Target.transform.Find(k_BakedChildName);
            if (child != null)
                Undo.DestroyObjectImmediate(child.gameObject);
            EditorUtility.SetDirty(m_Target);
        }

        private static string GetAssetSavePath(string scenePath, string fileName)
        {
            if (string.IsNullOrEmpty(scenePath))
                return "Assets/" + fileName;
            string dir = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            return dir + "/" + fileName;
        }
    }
}
