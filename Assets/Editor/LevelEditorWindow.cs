using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class LevelEditorWindow : EditorWindow
{
    const string PreviewRootName = "__LevelEditor_PreviewRoot";
    static readonly string DefaultCreateFolder = "Assets/Resources/Levels";
    const int HoverGridRadius = 6;

    readonly List<LevelData> levels = new List<LevelData>();
    Vector2 levelListScroll;
    Vector2 rightPanelScroll;
    bool autoPreview = true;
    bool levelEditMode;
    Vector2 paletteScroll;
    bool showGroundPalette = true;
    bool showEntityPalette = true;
    int groundTypeFilterIndex;
    int entityTypeFilterIndex;
    bool hasUnsavedEdits;
    bool selectionChangedPending;
    bool restoringSelection;
    UnityEngine.Object[] lastNonPreviewSelection = Array.Empty<UnityEngine.Object>();
    Vector2Int hoverCell;
    bool hasHoverCell;
    static Material hoverPreviewMaterialGreen;
    static Material hoverPreviewMaterialRed;
    static Material hoverPreviewMaterialBlue;
    bool moveHasSelection;
    Vector2Int moveFromCell;
    BrushType moveSelectionType;

    LevelData selectedLevel;
    string selectedAssetNameEdit = "";
    string selectedLevelNameEdit = "";

    GameObject previewRoot;
    readonly List<Ground> cachedGrounds = new List<Ground>();
    readonly List<Entity> cachedEntities = new List<Entity>();
    readonly Dictionary<Vector2Int, UnitInfo> workingUnits = new Dictionary<Vector2Int, UnitInfo>();

    enum BrushType
    {
        None,
        Ground,
        Entity
    }

    BrushType brushType;
    Ground activeGroundBrush;
    Entity activeEntityBrush;

    enum EditTool
    {
        Brush,
        Eraser,
        Move
    }

    EditTool editTool = EditTool.Brush;

    [MenuItem("Tools/关卡编辑器")]
    public static void Open()
    {
        var window = GetWindow<LevelEditorWindow>();
        window.titleContent = new GUIContent("关卡编辑器");
        window.Show();
    }

    void OnEnable()
    {
        RefreshLevelList();
        lastNonPreviewSelection = Selection.objects;
        Selection.selectionChanged += OnSelectionChanged;
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= OnSelectionChanged;
        ClearPreview();
    }

    void OnProjectChange()
    {
        RefreshLevelList();
        RefreshPaletteAssets();
    }

    void OnGUI()
    {
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("游戏运行中：关卡编辑器已锁定，停止运行后恢复交互。", MessageType.Info);
            return;
        }

        var leftWidth = Mathf.Clamp(position.width * 0.2f, 220f, 520f);
        EditorGUILayout.BeginHorizontal();
        try
        {
            DrawLeftPanel(leftWidth);
            DrawRightPanel();
        }
        finally
        {
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawLeftPanel(float width)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
        {
            autoPreview = EditorGUILayout.ToggleLeft("自动生成预览", autoPreview);

            levelListScroll = EditorGUILayout.BeginScrollView(levelListScroll);
            foreach (var level in levels)
            {
                if (level == null) continue;

                var isSelected = selectedLevel == level;
                var label = $"{level.name} ({level.levelName})";
                if (GUILayout.Toggle(isSelected, label, "Button"))
                {
                    if (!isSelected)
                    {
                        SelectLevel(level);
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新建"))
                {
                    CreateNewLevelAsset();
                }
                using (new EditorGUI.DisabledScope(selectedLevel == null))
                {
                    if (GUILayout.Button("复制"))
                    {
                        DuplicateSelected();
                    }
                    if (GUILayout.Button("删除"))
                    {
                        DeleteSelected();
                    }
                }
            }
        }
    }

    void DrawRightPanel()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (selectedLevel == null)
            {
                EditorGUILayout.HelpBox("从左侧列表选择一个关卡。", MessageType.Info);
                return;
            }

            rightPanelScroll = EditorGUILayout.BeginScrollView(rightPanelScroll);
            using (new EditorGUI.DisabledScope(levelEditMode))
            {
                EditorGUILayout.LabelField("关卡信息", EditorStyles.boldLabel);

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    selectedAssetNameEdit = EditorGUILayout.TextField("资源名", selectedAssetNameEdit);
                    if (GUILayout.Button("改名", GUILayout.Width(60)))
                    {
                        RenameSelectedAsset(selectedAssetNameEdit);
                    }
                    if (GUILayout.Button("定位", GUILayout.Width(60)))
                    {
                        EditorGUIUtility.PingObject(selectedLevel);
                        Selection.activeObject = selectedLevel;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    selectedLevelNameEdit = EditorGUILayout.TextField("关卡名", selectedLevelNameEdit);
                    if (GUILayout.Button("应用", GUILayout.Width(60)))
                    {
                        ApplySelectedLevelName(selectedLevelNameEdit);
                    }
                }

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("生成预览"))
                    {
                        LoadPreview(selectedLevel);
                    }
                    if (GUILayout.Button("清空预览"))
                    {
                        ClearPreview();
                    }
                    if (GUILayout.Button("校验关卡"))
                    {
                        var report = BuildValidationReport(selectedLevel);
                        EditorUtility.DisplayDialog("关卡校验", report, "确定");
                    }
                }
            }

            EditorGUILayout.Space(6);
            DrawLevelEditArea();

            DrawLevelStats(selectedLevel);

            if (selectionChangedPending)
            {
                selectionChangedPending = false;
                if (autoPreview) LoadPreview(selectedLevel);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    void DrawLevelEditArea()
    {
        EditorGUILayout.LabelField("关卡编辑", EditorStyles.boldLabel);

        var editButtonText = levelEditMode ? "退出关卡编辑" : "进入关卡编辑";
        var prevEditBg = GUI.backgroundColor;
        if (levelEditMode) GUI.backgroundColor = new Color(0.25f, 0.85f, 0.35f, 1f);
        var editClicked = GUILayout.Button(editButtonText);
        GUI.backgroundColor = prevEditBg;
        if (editClicked)
        {
            levelEditMode = !levelEditMode;
            OnLevelEditModeChanged(levelEditMode);
            RefreshPaletteAssets();
            SceneView.RepaintAll();
        }

        if (!levelEditMode) return;

        using (new EditorGUI.DisabledScope(false))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var prevBg = GUI.backgroundColor;
                if (hasUnsavedEdits) GUI.backgroundColor = new Color(1f, 0.6f, 0.15f, 1f);
                var clicked = GUILayout.Button("保存", GUILayout.Height(28), GUILayout.ExpandWidth(true));
                GUI.backgroundColor = prevBg;
                if (clicked && hasUnsavedEdits)
                {
                    SaveEditsToAsset();
                }
            }

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                var brushIcon = GetFirstIconContent(new[] { "Brush", "d_Brush", "Pencil", "d_Pencil", "editicon.sml", "d_editicon.sml" }, "画笔");
                brushIcon.tooltip = "画笔";

                var moveIcon = GetFirstIconContent(new[] { "MoveTool", "d_MoveTool", "MoveTool on", "d_MoveTool on" }, "移动");
                moveIcon.tooltip = "移动";

                var eraserIcon = GetFirstIconContent(new[] { "TreeEditor.Trash", "d_TreeEditor.Trash", "Toolbar Minus", "d_Toolbar Minus" }, "橡皮");
                eraserIcon.tooltip = "橡皮擦";

                var brushOn = editTool == EditTool.Brush;
                var moveOn = editTool == EditTool.Move;
                var eraserOn = editTool == EditTool.Eraser;

                var size = GUILayout.Height(28);
                var width = GUILayout.Width(52);

                var newBrushOn = GUILayout.Toggle(brushOn, brushIcon, EditorStyles.miniButtonLeft, width, size);
                var newEraserOn = GUILayout.Toggle(eraserOn, eraserIcon, EditorStyles.miniButtonMid, width, size);
                var newMoveOn = GUILayout.Toggle(moveOn, moveIcon, EditorStyles.miniButtonRight, width, size);

                if (newBrushOn && !brushOn)
                {
                    editTool = EditTool.Brush;
                    moveHasSelection = false;
                    moveSelectionType = BrushType.None;
                    SceneView.RepaintAll();
                }
                else if (newEraserOn && !eraserOn)
                {
                    editTool = EditTool.Eraser;
                    brushType = BrushType.None;
                    activeGroundBrush = null;
                    activeEntityBrush = null;
                    moveHasSelection = false;
                    moveSelectionType = BrushType.None;
                    SceneView.RepaintAll();
                }
                else if (newMoveOn && !moveOn)
                {
                    editTool = EditTool.Move;
                    brushType = BrushType.None;
                    activeGroundBrush = null;
                    activeEntityBrush = null;
                    moveHasSelection = false;
                    moveSelectionType = BrushType.None;
                    SceneView.RepaintAll();
                }

                GUILayout.FlexibleSpace();
            }

            if (editTool == EditTool.Brush)
            {
                paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.Height(240));

                var groundOptions = GetGroundTypeFilterOptions();
                groundTypeFilterIndex = Mathf.Clamp(groundTypeFilterIndex, 0, groundOptions.Length - 1);
                using (new EditorGUILayout.HorizontalScope())
                {
                    showGroundPalette = EditorGUILayout.Foldout(showGroundPalette, "Ground", true);
                    GUILayout.FlexibleSpace();
                    groundTypeFilterIndex = EditorGUILayout.Popup(groundTypeFilterIndex, groundOptions, GUILayout.Width(120));
                }
                if (showGroundPalette)
                {
                    DrawGroundPalette();
                }

                var entityOptions = GetEntityTypeFilterOptions();
                entityTypeFilterIndex = Mathf.Clamp(entityTypeFilterIndex, 0, entityOptions.Length - 1);
                using (new EditorGUILayout.HorizontalScope())
                {
                    showEntityPalette = EditorGUILayout.Foldout(showEntityPalette, "Entity", true);
                    GUILayout.FlexibleSpace();
                    entityTypeFilterIndex = EditorGUILayout.Popup(entityTypeFilterIndex, entityOptions, GUILayout.Width(120));
                }
                if (showEntityPalette)
                {
                    DrawEntityPalette();
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.HelpBox("画笔模式：选择缩略图后，在 Scene 视图左键点击格子放置。", MessageType.Info);
            }
            else if (editTool == EditTool.Move)
            {
                EditorGUILayout.HelpBox("移动模式：先左键点击选择一个格子，再点击目标格子进行移动/对调。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("橡皮擦模式：鼠标悬停到已有地板/实体会显示红色预览，左键点击删除。", MessageType.Info);
            }
        }
    }

    static string[] GetGroundTypeFilterOptions()
    {
        var values = (GroundType[])Enum.GetValues(typeof(GroundType));
        var opts = new string[values.Length + 1];
        opts[0] = "All";
        for (int i = 0; i < values.Length; i++)
        {
            opts[i + 1] = values[i].ToString();
        }
        return opts;
    }

    static string[] GetEntityTypeFilterOptions()
    {
        var values = (EntityType[])Enum.GetValues(typeof(EntityType));
        var opts = new string[values.Length + 1];
        opts[0] = "All";
        for (int i = 0; i < values.Length; i++)
        {
            opts[i + 1] = values[i].ToString();
        }
        return opts;
    }

    void DrawGroundPalette()
    {
        if (cachedGrounds.Count == 0)
        {
            EditorGUILayout.HelpBox("未找到 Ground 资源。", MessageType.Warning);
            return;
        }

        var groundValues = (GroundType[])Enum.GetValues(typeof(GroundType));
        GroundType? filterType = null;
        if (groundTypeFilterIndex > 0 && groundTypeFilterIndex - 1 < groundValues.Length)
        {
            filterType = groundValues[groundTypeFilterIndex - 1];
        }

        const int cellSize = 72;
        int viewWidth = Mathf.Max(1, (int)EditorGUIUtility.currentViewWidth - 36);
        int columns = Mathf.Max(1, viewWidth / cellSize);
        int index = 0;
        while (index < cachedGrounds.Count)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columns && index < cachedGrounds.Count;)
            {
                var ground = cachedGrounds[index];
                index++;
                if (filterType.HasValue && ParseGroundType(ground) != filterType.Value)
                {
                    continue;
                }
                c++;
                var isActive = brushType == BrushType.Ground && activeGroundBrush == ground;
                DrawPaletteItem(ground, ground != null ? ground.shape : null, isActive, () =>
                {
                    if (isActive)
                    {
                        brushType = BrushType.None;
                        activeGroundBrush = null;
                    }
                    else
                    {
                        brushType = BrushType.Ground;
                        activeGroundBrush = ground;
                        activeEntityBrush = null;
                    }
                    SceneView.RepaintAll();
                });
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawEntityPalette()
    {
        if (cachedEntities.Count == 0)
        {
            EditorGUILayout.HelpBox("未找到 Entity 资源。", MessageType.Warning);
            return;
        }

        var entityValues = (EntityType[])Enum.GetValues(typeof(EntityType));
        EntityType? filterType = null;
        if (entityTypeFilterIndex > 0 && entityTypeFilterIndex - 1 < entityValues.Length)
        {
            filterType = entityValues[entityTypeFilterIndex - 1];
        }

        const int cellSize = 72;
        int viewWidth = Mathf.Max(1, (int)EditorGUIUtility.currentViewWidth - 36);
        int columns = Mathf.Max(1, viewWidth / cellSize);
        int index = 0;
        while (index < cachedEntities.Count)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columns && index < cachedEntities.Count;)
            {
                var entity = cachedEntities[index];
                index++;
                if (filterType.HasValue && (entity == null || entity.entityType != filterType.Value))
                {
                    continue;
                }
                c++;
                var isActive = brushType == BrushType.Entity && activeEntityBrush == entity;
                DrawPaletteItem(entity, entity != null ? entity.shape : null, isActive, () =>
                {
                    if (isActive)
                    {
                        brushType = BrushType.None;
                        activeEntityBrush = null;
                    }
                    else
                    {
                        brushType = BrushType.Entity;
                        activeEntityBrush = entity;
                        activeGroundBrush = null;
                    }
                    SceneView.RepaintAll();
                });
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    static GroundType ParseGroundType(Ground ground)
    {
        if (ground == null) return GroundType.Untagged;
        return ground.groundType;
    }

    void DrawPaletteItem(UnityEngine.Object asset, GameObject shapePrefab, bool isActive, Action onClick)
    {
        var style = new GUIStyle(GUI.skin.button);
        if (isActive) style.normal.background = style.active.background;

        Texture2D tex = null;
        if (shapePrefab != null)
        {
            tex = AssetPreview.GetAssetPreview(shapePrefab);
            if (tex == null) tex = AssetPreview.GetMiniThumbnail(shapePrefab);
        }
        if (tex == null && asset != null)
        {
            tex = AssetPreview.GetMiniThumbnail(asset);
        }

        using (new EditorGUILayout.VerticalScope(GUILayout.Width(72)))
        {
            var size = isActive ? 74 : 68;
            var rect = GUILayoutUtility.GetRect(68, 68, GUILayout.Width(68), GUILayout.Height(68));
            var center = rect.center;
            rect.width = size;
            rect.height = size;
            rect.center = center;

            if (isActive)
            {
                EditorGUI.DrawRect(rect, new Color(0.15f, 0.95f, 0.35f, 0.18f));
            }

            var content = new GUIContent(tex);
            var prevGuiColor = GUI.color;
            if (isActive) GUI.color = new Color(0.75f, 1f, 0.8f, 1f);
            if (GUI.Button(rect, content, style))
            {
                onClick?.Invoke();
            }
            GUI.color = prevGuiColor;
            GUILayout.Label(GetPaletteDisplayName(asset), EditorStyles.miniLabel, GUILayout.Width(68));
        }

        if (tex == null) Repaint();
    }

    static string GetPaletteDisplayName(UnityEngine.Object asset)
    {
        if (asset == null) return "None";
        if (asset is Ground ground)
        {
            if (!string.IsNullOrWhiteSpace(ground.Name)) return ground.Name;
            return ground.name;
        }
        if (asset is Entity entity)
        {
            if (!string.IsNullOrWhiteSpace(entity.Name)) return entity.Name;
            return entity.name;
        }
        return asset.name;
    }

    static void DrawLevelStats(LevelData level)
    {
        if (level == null) return;
        var unitInfos = level.unitInfos;
        if (unitInfos == null)
        {
            EditorGUILayout.HelpBox("unitInfos 为空：该关卡不会生成任何地板/实体。", MessageType.Warning);
            return;
        }

        int groundCount = 0;
        int entityCount = 0;
        int nullGroundCount = 0;
        bool hasPlayerEntity = false;
        bool hasWinFloor = false;
        for (int i = 0; i < unitInfos.Length; i++)
        {
            var u = unitInfos[i];
            if (u.ground == null) nullGroundCount++;
            else
            {
                groundCount++;
                if (u.ground.groundType == GroundType.WinFloor) hasWinFloor = true;
            }
            if (u.entity != null)
            {
                entityCount++;
                if (u.entity.entityType == EntityType.Player) hasPlayerEntity = true;
            }
        }

        EditorGUILayout.LabelField("统计", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("格子数", unitInfos.Length.ToString());
        EditorGUILayout.LabelField("地板数", groundCount.ToString());
        EditorGUILayout.LabelField("实体数", entityCount.ToString());
        if (nullGroundCount > 0) EditorGUILayout.HelpBox($"有 {nullGroundCount} 个格子缺少 ground。", MessageType.Info);
        if (!hasPlayerEntity) EditorGUILayout.HelpBox("该关卡缺少 Player 类型实体。", MessageType.Warning);
        if (!hasWinFloor) EditorGUILayout.HelpBox("该关卡缺少 WinFloor 类型地板。", MessageType.Warning);
    }

    static string BuildValidationReport(LevelData level)
    {
        if (level == null) return "未选择关卡。";

        var unitInfos = level.unitInfos;
        if (unitInfos == null) return "unitInfos 为 null。";

        int duplicatePosCount = 0;
        int groundMissingCount = 0;
        int groundShapeMissingCount = 0;
        int entityShapeMissingCount = 0;
        var used = new HashSet<Vector2Int>();

        for (int i = 0; i < unitInfos.Length; i++)
        {
            var u = unitInfos[i];
            if (!used.Add(u.pos)) duplicatePosCount++;

            if (u.ground == null) groundMissingCount++;
            else if (u.ground.shape == null) groundShapeMissingCount++;

            if (u.entity != null && u.entity.shape == null) entityShapeMissingCount++;
        }

        var lines = new List<string>
        {
            $"关卡资源：{level.name}",
            $"关卡名：{level.levelName}",
            $"格子数：{unitInfos.Length}",
            $"重复坐标：{duplicatePosCount}",
            $"缺少 ground：{groundMissingCount}",
            $"ground.shape 为空：{groundShapeMissingCount}",
            $"entity.shape 为空：{entityShapeMissingCount}",
        };
        return string.Join("\n", lines);
    }

    void RefreshLevelList()
    {
        levels.Clear();
        var guids = AssetDatabase.FindAssets("t:LevelData");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level != null) levels.Add(level);
        }
        levels.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        Repaint();
    }

    void SelectLevel(LevelData level)
    {
        if (levelEditMode)
        {
            levelEditMode = false;
            OnLevelEditModeChanged(false);
        }

        selectedLevel = level;
        selectedAssetNameEdit = level != null ? level.name : "";
        selectedLevelNameEdit = level != null ? level.levelName : "";
        selectionChangedPending = true;
    }

    void EnsurePreviewRoot()
    {
        if (previewRoot != null) return;
        previewRoot = GameObject.Find(PreviewRootName);
        if (previewRoot == null)
        {
            previewRoot = new GameObject(PreviewRootName);
            previewRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy | HideFlags.NotEditable;
        }
    }

    void LoadPreview(LevelData level)
    {
        if (level == null) return;
        LoadPreview(level.unitInfos);
    }

    void LoadPreview(UnitInfo[] unitInfos)
    {
        ClearPreview();
        EnsurePreviewRoot();
        if (unitInfos == null) return;

        for (int i = 0; i < unitInfos.Length; i++)
        {
            var unit = unitInfos[i];
            if (unit == null) continue;
            var worldPos = new Vector3(unit.pos.x, 0f, unit.pos.y);

            if (unit.ground != null && unit.ground.shape != null)
            {
                InstantiatePreview(unit.ground.shape, worldPos, previewRoot.transform);
            }
            if (unit.entity != null && unit.entity.shape != null)
            {
                InstantiatePreview(unit.entity.shape, worldPos, previewRoot.transform);
            }
        }
    }

    static void InstantiatePreview(GameObject prefabOrGo, Vector3 worldPos, Transform parent)
    {
        GameObject instance;
        if (PrefabUtility.IsPartOfPrefabAsset(prefabOrGo))
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabOrGo);
        }
        else
        {
            instance = Instantiate(prefabOrGo);
        }
        instance.transform.SetParent(parent, false);
        instance.transform.position = worldPos;
        instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy | HideFlags.NotEditable;
    }

    void ClearPreview()
    {
        if (previewRoot == null)
        {
            previewRoot = GameObject.Find(PreviewRootName);
        }
        if (previewRoot == null) return;

        if (Application.isPlaying)
        {
            Destroy(previewRoot);
        }
        else
        {
            DestroyImmediate(previewRoot);
        }
        previewRoot = null;
    }

    void CreateNewLevelAsset()
    {
        EnsureCreateFolder(DefaultCreateFolder);

        var asset = CreateInstance<LevelData>();
        asset.levelName = "New Level";
        asset.unitInfos = Array.Empty<UnitInfo>();

        var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultCreateFolder, "NewLevelData.asset"));
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RefreshLevelList();
        SelectLevel(asset);
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }

    static void EnsureCreateFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;

        var parts = assetFolder.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    void DuplicateSelected()
    {
        if (selectedLevel == null) return;
        var srcPath = AssetDatabase.GetAssetPath(selectedLevel);
        var dstPath = AssetDatabase.GenerateUniqueAssetPath(srcPath);
        if (!AssetDatabase.CopyAsset(srcPath, dstPath)) return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshLevelList();

        var copied = AssetDatabase.LoadAssetAtPath<LevelData>(dstPath);
        if (copied != null) SelectLevel(copied);
    }

    void DeleteSelected()
    {
        if (selectedLevel == null) return;
        var path = AssetDatabase.GetAssetPath(selectedLevel);
        var ok = EditorUtility.DisplayDialog("删除关卡", $"确认删除关卡资源？\n{path}", "删除", "取消");
        if (!ok) return;

        ClearPreview();
        selectedLevel = null;
        selectedAssetNameEdit = "";
        selectedLevelNameEdit = "";
        selectionChangedPending = false;

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshLevelList();
    }

    void OnSelectionChanged()
    {
        if (restoringSelection) return;
        var objs = Selection.objects;
        if (objs == null) return;

        if (ContainsPreviewSelection(objs))
        {
            restoringSelection = true;
            try
            {
                Selection.objects = lastNonPreviewSelection;
            }
            finally
            {
                restoringSelection = false;
            }
            return;
        }

        lastNonPreviewSelection = objs;
    }

    void OnEditorUpdate()
    {
        if (EditorApplication.isPlaying) return;
        if (restoringSelection) return;
        var objs = Selection.objects;
        if (objs == null) return;
        if (!ContainsPreviewSelection(objs)) return;

        restoringSelection = true;
        try
        {
            Selection.activeObject = null;
            Selection.objects = lastNonPreviewSelection ?? Array.Empty<UnityEngine.Object>();
        }
        finally
        {
            restoringSelection = false;
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (EditorApplication.isPlaying) return;
        if (restoringSelection) return;
        var e = Event.current;
        if (e == null) return;
        if (levelEditMode)
        {
            UpdateHoverCell(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                DrawHoverOverlay();
            }
        }

        if (e.type != EventType.MouseDown) return;
        if (e.button != 0) return;
        if (e.alt) return;

        if (levelEditMode && selectedLevel != null && editTool == EditTool.Brush && brushType != BrushType.None)
        {
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var enter))
            {
                var world = ray.GetPoint(enter);
                var pos = new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
                if (brushType == BrushType.Entity && activeEntityBrush != null && !HasGroundAt(pos))
                {
                    Debug.LogWarning($"坐标{pos}没有地板，无法放置实体");
                    e.Use();
                    return;
                }
                if (TryApplyBrushToWorking(pos))
                {
                    hasUnsavedEdits = true;
                    LoadPreview(BuildWorkingUnitsArray());
                    restoringSelection = true;
                    try
                    {
                        Selection.activeObject = null;
                        Selection.objects = lastNonPreviewSelection ?? Array.Empty<UnityEngine.Object>();
                    }
                    finally
                    {
                        restoringSelection = false;
                    }
                    e.Use();
                    return;
                }
            }
        }
        else if (levelEditMode && selectedLevel != null && editTool == EditTool.Eraser)
        {
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var enter))
            {
                var world = ray.GetPoint(enter);
                var pos = new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
                if (TryEraseAt(pos))
                {
                    hasUnsavedEdits = true;
                    LoadPreview(BuildWorkingUnitsArray());
                    restoringSelection = true;
                    try
                    {
                        Selection.activeObject = null;
                        Selection.objects = lastNonPreviewSelection ?? Array.Empty<UnityEngine.Object>();
                    }
                    finally
                    {
                        restoringSelection = false;
                    }
                    e.Use();
                    return;
                }
            }
        }
        else if (levelEditMode && selectedLevel != null && editTool == EditTool.Move)
        {
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var enter))
            {
                var world = ray.GetPoint(enter);
                var pos = new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));

                if (!moveHasSelection)
                {
                    if (TryGetMoveCandidateAt(pos, out var t, out _, out _))
                    {
                        moveHasSelection = true;
                        moveFromCell = pos;
                        moveSelectionType = t;
                        SceneView.RepaintAll();
                    }
                    else
                    {
                        Debug.LogWarning($"坐标{pos}没有可移动对象");
                    }
                    e.Use();
                    return;
                }

                if (pos == moveFromCell)
                {
                    moveHasSelection = false;
                    moveSelectionType = BrushType.None;
                    SceneView.RepaintAll();
                    e.Use();
                    return;
                }

                if (TryMove(moveFromCell, pos, moveSelectionType))
                {
                    hasUnsavedEdits = true;
                    moveHasSelection = false;
                    moveSelectionType = BrushType.None;
                    LoadPreview(BuildWorkingUnitsArray());
                    restoringSelection = true;
                    try
                    {
                        Selection.activeObject = null;
                        Selection.objects = lastNonPreviewSelection ?? Array.Empty<UnityEngine.Object>();
                    }
                    finally
                    {
                        restoringSelection = false;
                    }
                    e.Use();
                    return;
                }

                moveHasSelection = false;
                moveSelectionType = BrushType.None;
                SceneView.RepaintAll();
                e.Use();
                return;
            }
        }

        var picked = HandleUtility.PickGameObject(e.mousePosition, false);
        if (picked == null) return;
        if (!IsPreviewGameObject(picked)) return;

        restoringSelection = true;
        try
        {
            Selection.activeObject = null;
            Selection.objects = lastNonPreviewSelection ?? Array.Empty<UnityEngine.Object>();
        }
        finally
        {
            restoringSelection = false;
        }
        e.Use();
    }

    void UpdateHoverCell(Vector2 mousePosition)
    {
        var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out var enter))
        {
            hasHoverCell = false;
            return;
        }

        var world = ray.GetPoint(enter);
        hoverCell = new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
        hasHoverCell = true;
    }

    GameObject GetActiveShapePrefab()
    {
        if (!levelEditMode) return null;

        if (editTool == EditTool.Brush)
        {
            if (brushType == BrushType.Ground) return activeGroundBrush != null ? activeGroundBrush.shape : null;
            if (brushType == BrushType.Entity) return activeEntityBrush != null ? activeEntityBrush.shape : null;
            return null;
        }

        if (editTool == EditTool.Eraser && hasHoverCell)
        {
            if (TryGetUnitAt(hoverCell, out var u))
            {
                if (u.entity != null) return u.entity.shape;
                if (u.ground != null) return u.ground.shape;
            }
        }
        return null;
    }

    void DrawHoverOverlay()
    {
        if (!levelEditMode) return;
        if (!hasHoverCell) return;

        var worldOrigin = new Vector3(hoverCell.x, 0f, hoverCell.y);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        var oldColor = Handles.color;
        DrawHoverGrid(worldOrigin);
        Handles.color = GetHoverWireColor();
        Handles.DrawWireCube(new Vector3(hoverCell.x, worldOrigin.y, hoverCell.y), new Vector3(1f, 0f, 1f));
        Handles.color = oldColor;

        if (editTool == EditTool.Move)
        {
            DrawMoveOverlay(worldOrigin);
            return;
        }

        var prefab = GetActiveShapePrefab();
        if (prefab == null) return;
        var mat = GetHoverPreviewMaterial();
        if (mat == null) return;
        DrawPrefabMeshWithMaterial(prefab, worldOrigin, mat);
    }

    Color GetHoverWireColor()
    {
        if (!levelEditMode) return new Color(0.2f, 1f, 0.2f, 0.9f);

        if (editTool == EditTool.Move)
        {
            if (moveHasSelection) return new Color(0.2f, 0.55f, 1f, 0.95f);
            return TryGetMoveCandidateAt(hoverCell, out _, out _, out _) ? new Color(0.2f, 0.55f, 1f, 0.95f) : new Color(0.8f, 0.8f, 0.8f, 0.35f);
        }

        if (editTool == EditTool.Brush && brushType == BrushType.Entity && activeEntityBrush != null && hasHoverCell)
        {
            if (!HasGroundAt(hoverCell)) return new Color(1f, 0.3f, 0.3f, 0.95f);
            return new Color(0.2f, 1f, 0.2f, 0.9f);
        }

        if (editTool == EditTool.Eraser && hasHoverCell)
        {
            if (TryGetUnitAt(hoverCell, out var u) && (u.entity != null || u.ground != null))
            {
                return new Color(1f, 0.3f, 0.3f, 0.95f);
            }
        }

        if (editTool == EditTool.Brush && brushType != BrushType.None)
        {
            return new Color(0.2f, 1f, 0.2f, 0.9f);
        }

        return new Color(0.8f, 0.8f, 0.8f, 0.35f);
    }

    void DrawMoveOverlay(Vector3 hoverWorldOrigin)
    {
        EnsureHoverPreviewMaterialBlue();
        if (hoverPreviewMaterialBlue == null) return;

        if (!moveHasSelection)
        {
            if (!TryGetMoveCandidateAt(hoverCell, out var t, out var e, out var g)) return;
            var prefab = t == BrushType.Entity ? (e != null ? e.shape : null) : (g != null ? g.shape : null);
            if (prefab == null) return;
            DrawPrefabMeshWithMaterial(prefab, new Vector3(hoverCell.x, hoverWorldOrigin.y, hoverCell.y), hoverPreviewMaterialBlue);
            return;
        }

        if (!TryGetMoveCandidateAt(moveFromCell, out var selType, out var selEntity, out var selGround)) return;
        var selPrefab = selType == BrushType.Entity ? (selEntity != null ? selEntity.shape : null) : (selGround != null ? selGround.shape : null);
        if (selPrefab == null) return;

        var oldColor = Handles.color;
        Handles.color = new Color(0.2f, 0.55f, 1f, 0.95f);
        Handles.DrawWireCube(new Vector3(moveFromCell.x, hoverWorldOrigin.y, moveFromCell.y), new Vector3(1f, 0f, 1f));
        Handles.color = oldColor;

        DrawPrefabMeshWithMaterial(selPrefab, new Vector3(moveFromCell.x, hoverWorldOrigin.y, moveFromCell.y), hoverPreviewMaterialBlue);
        if (hoverCell != moveFromCell)
        {
            DrawPrefabMeshWithMaterial(selPrefab, new Vector3(hoverCell.x, hoverWorldOrigin.y, hoverCell.y), hoverPreviewMaterialBlue);
        }
    }

    Material GetHoverPreviewMaterial()
    {
        if (!levelEditMode) return null;

        if (editTool == EditTool.Brush && brushType == BrushType.Entity && activeEntityBrush != null && hasHoverCell)
        {
            if (!HasGroundAt(hoverCell))
            {
                EnsureHoverPreviewMaterialRed();
                return hoverPreviewMaterialRed;
            }
        }

        if (editTool == EditTool.Eraser)
        {
            EnsureHoverPreviewMaterialRed();
            return hoverPreviewMaterialRed;
        }

        EnsureHoverPreviewMaterialGreen();
        return hoverPreviewMaterialGreen;
    }

    void DrawHoverGrid(Vector3 worldOrigin)
    {
        var color = new Color(1f, 1f, 1f, 0.08f);
        Handles.color = color;

        var startX = hoverCell.x - HoverGridRadius - 0.5f;
        var endX = hoverCell.x + HoverGridRadius + 0.5f;
        var startY = hoverCell.y - HoverGridRadius - 0.5f;
        var endY = hoverCell.y + HoverGridRadius + 0.5f;

        int xLines = (HoverGridRadius * 2) + 2;
        for (int i = 0; i < xLines; i++)
        {
            var x = startX + i;
            var a = new Vector3(x, worldOrigin.y, startY);
            var b = new Vector3(x, worldOrigin.y, endY);
            Handles.DrawLine(a, b);
        }
        int yLines = (HoverGridRadius * 2) + 2;
        for (int i = 0; i < yLines; i++)
        {
            var y = startY + i;
            var a = new Vector3(startX, worldOrigin.y, y);
            var b = new Vector3(endX, worldOrigin.y, y);
            Handles.DrawLine(a, b);
        }
    }

    static void EnsureHoverPreviewMaterialGreen()
    {
        if (hoverPreviewMaterialGreen != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        hoverPreviewMaterialGreen = CreateTintMaterial(shader, new Color(0.2f, 1f, 0.2f, 0.35f));
    }

    static void EnsureHoverPreviewMaterialRed()
    {
        if (hoverPreviewMaterialRed != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        hoverPreviewMaterialRed = CreateTintMaterial(shader, new Color(1f, 0.25f, 0.25f, 0.35f));
    }

    static void EnsureHoverPreviewMaterialBlue()
    {
        if (hoverPreviewMaterialBlue != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        hoverPreviewMaterialBlue = CreateTintMaterial(shader, new Color(0.25f, 0.55f, 1f, 0.35f));
    }

    static Material CreateTintMaterial(Shader shader, Color color)
    {
        var mat = new Material(shader);
        mat.hideFlags = HideFlags.HideAndDontSave;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        if (mat.shader != null && mat.shader.name == "Standard")
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        return mat;
    }

    static void DrawPrefabMeshWithMaterial(GameObject prefab, Vector3 worldOrigin, Material mat)
    {
        if (prefab == null || mat == null) return;
        var root = prefab.transform;
        var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        if (filters == null || filters.Length == 0) return;

        mat.SetPass(0);
        for (int i = 0; i < filters.Length; i++)
        {
            var mf = filters[i];
            if (mf == null) continue;
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;

            var local = GetLocalMatrixToRoot(mf.transform, root);
            var final = Matrix4x4.TRS(worldOrigin, Quaternion.identity, Vector3.one) * local;
            Graphics.DrawMeshNow(mesh, final);
        }
    }

    static Matrix4x4 GetLocalMatrixToRoot(Transform t, Transform root)
    {
        var chain = new List<Transform>();
        var cur = t;
        while (cur != null)
        {
            chain.Add(cur);
            if (cur == root) break;
            cur = cur.parent;
        }
        chain.Reverse();

        var m = Matrix4x4.identity;
        for (int i = 0; i < chain.Count; i++)
        {
            var tr = chain[i];
            m *= Matrix4x4.TRS(tr.localPosition, tr.localRotation, tr.localScale);
        }
        return m;
    }

    bool TryApplyBrushToWorking(Vector2Int pos)
    {
        if (brushType == BrushType.Ground && activeGroundBrush == null) return false;
        if (brushType == BrushType.Entity && activeEntityBrush == null) return false;

        if (!workingUnits.TryGetValue(pos, out var target) || target == null)
        {
            target = new UnitInfo { pos = pos };
            workingUnits[pos] = target;
        }

        if (brushType == BrushType.Ground)
        {
            target.ground = activeGroundBrush;
        }
        else if (brushType == BrushType.Entity)
        {
            target.entity = activeEntityBrush;
        }
        else
        {
            return false;
        }

        return true;
    }

    bool HasGroundAt(Vector2Int pos)
    {
        return TryGetUnitAt(pos, out var u) && u.ground != null;
    }

    bool TryGetUnitAt(Vector2Int pos, out UnitInfo unit)
    {
        if (workingUnits.TryGetValue(pos, out unit) && unit != null) return true;
        unit = null;
        return false;
    }

    bool TryEraseAt(Vector2Int pos)
    {
        if (!workingUnits.TryGetValue(pos, out var u) || u == null) return false;

        if (u.entity != null)
        {
            u.entity = null;
        }
        else if (u.ground != null)
        {
            u.ground = null;
            u.entity = null;
        }
        else
        {
            return false;
        }

        if (u.ground == null && u.entity == null)
        {
            workingUnits.Remove(pos);
        }
        return true;
    }

    bool TryGetMoveCandidateAt(Vector2Int pos, out BrushType type, out Entity entity, out Ground ground)
    {
        type = BrushType.None;
        entity = null;
        ground = null;

        if (!TryGetUnitAt(pos, out var u) || u == null) return false;
        if (u.entity != null)
        {
            type = BrushType.Entity;
            entity = u.entity;
            return true;
        }
        if (u.ground != null)
        {
            type = BrushType.Ground;
            ground = u.ground;
            return true;
        }
        return false;
    }

    bool TryMove(Vector2Int from, Vector2Int to, BrushType selectionType)
    {
        if (from == to) return false;
        if (!TryGetUnitAt(from, out var src) || src == null) return false;

        if (selectionType == BrushType.Entity)
        {
            if (src.entity == null) return false;
            if (!TryGetUnitAt(to, out var dst) || dst == null || dst.ground == null)
            {
                Debug.LogWarning($"坐标{to}没有地板，无法移动实体");
                return false;
            }
            var tmp = dst.entity;
            dst.entity = src.entity;
            src.entity = tmp;
            return true;
        }

        if (selectionType == BrushType.Ground)
        {
            if (src.ground == null) return false;
            if (src.entity != null) return false;

            workingUnits.TryGetValue(to, out var dst);
            if (dst == null)
            {
                dst = new UnitInfo { pos = to, ground = src.ground, entity = null };
                workingUnits[to] = dst;
                src.ground = null;
                if (src.ground == null && src.entity == null) workingUnits.Remove(from);
                return true;
            }

            var tmpGround = dst.ground;
            dst.ground = src.ground;
            src.ground = tmpGround;

            if (src.ground == null && src.entity == null) workingUnits.Remove(from);
            if (dst.ground == null && dst.entity == null) workingUnits.Remove(to);
            return true;
        }

        return false;
    }

    UnitInfo[] BuildWorkingUnitsArray()
    {
        var list = new List<UnitInfo>(workingUnits.Count);
        foreach (var kv in workingUnits)
        {
            var u = kv.Value;
            if (u == null) continue;
            if (u.ground == null && u.entity == null) continue;
            list.Add(u);
        }
        list.Sort((a, b) => a.pos.x != b.pos.x ? a.pos.x.CompareTo(b.pos.x) : a.pos.y.CompareTo(b.pos.y));
        return list.ToArray();
    }

    void OnLevelEditModeChanged(bool enabled)
    {
        if (enabled)
        {
            editTool = EditTool.Brush;
            moveHasSelection = false;
            moveSelectionType = BrushType.None;
            BeginWorkingCopyFromAsset();
            if (!HasPreview())
            {
                LoadPreview(BuildWorkingUnitsArray());
            }
        }
        else
        {
            editTool = EditTool.Brush;
            moveHasSelection = false;
            moveSelectionType = BrushType.None;
            brushType = BrushType.None;
            activeGroundBrush = null;
            activeEntityBrush = null;
            hasHoverCell = false;
            hasUnsavedEdits = false;
            workingUnits.Clear();

            if (HasPreview() && selectedLevel != null)
            {
                LoadPreview(selectedLevel);
            }
        }
    }

    bool HasPreview()
    {
        if (previewRoot != null) return true;
        return GameObject.Find(PreviewRootName) != null;
    }

    void BeginWorkingCopyFromAsset()
    {
        hasUnsavedEdits = false;
        workingUnits.Clear();

        if (selectedLevel == null) return;
        var unitInfos = selectedLevel.unitInfos;
        if (unitInfos == null) return;

        for (int i = 0; i < unitInfos.Length; i++)
        {
            var u = unitInfos[i];
            if (u == null) continue;
            var copy = new UnitInfo
            {
                pos = u.pos,
                ground = u.ground,
                entity = u.entity
            };
            workingUnits[copy.pos] = copy;
        }
    }

    void SaveEditsToAsset()
    {
        if (selectedLevel == null) return;

        var report = ValidateWorkingUnits();
        if (!string.IsNullOrEmpty(report))
        {
            EditorUtility.DisplayDialog("保存失败", report, "确定");
            return;
        }

        Undo.RecordObject(selectedLevel, "Save Level Edits");
        selectedLevel.unitInfos = BuildWorkingUnitsArray();
        EditorUtility.SetDirty(selectedLevel);
        AssetDatabase.SaveAssets();
        hasUnsavedEdits = false;
    }

    string ValidateWorkingUnits()
    {
        int entityWithoutGround = 0;
        foreach (var kv in workingUnits)
        {
            var u = kv.Value;
            if (u == null) continue;
            if (u.entity != null && u.ground == null) entityWithoutGround++;
        }

        if (entityWithoutGround > 0)
        {
            return $"有 {entityWithoutGround} 个格子放置了 Entity 但没有 Ground。";
        }
        return "";
    }

    void RefreshPaletteAssets()
    {
        cachedGrounds.Clear();
        cachedEntities.Clear();

        var groundGuids = AssetDatabase.FindAssets("t:Ground");
        for (int i = 0; i < groundGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(groundGuids[i]);
            var ground = AssetDatabase.LoadAssetAtPath<Ground>(path);
            if (ground != null) cachedGrounds.Add(ground);
        }
        cachedGrounds.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));

        var entityGuids = AssetDatabase.FindAssets("t:Entity");
        for (int i = 0; i < entityGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(entityGuids[i]);
            var entity = AssetDatabase.LoadAssetAtPath<Entity>(path);
            if (entity != null) cachedEntities.Add(entity);
        }
        cachedEntities.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));
    }

    bool ContainsPreviewSelection(UnityEngine.Object[] objs)
    {
        if (objs.Length == 0) return false;

        for (int i = 0; i < objs.Length; i++)
        {
            var obj = objs[i];
            if (obj == null) continue;

            GameObject go = obj as GameObject;
            if (go == null && obj is Component c) go = c.gameObject;
            if (go == null) continue;

            if (IsPreviewGameObject(go)) return true;
        }
        return false;
    }

    bool IsPreviewGameObject(GameObject go)
    {
        if (go == null) return false;
        var root = previewRoot != null ? previewRoot : GameObject.Find(PreviewRootName);
        if (root == null) return false;
        if (go == root) return true;
        return go.transform != null && go.transform.IsChildOf(root.transform);
    }

    static GUIContent GetFirstIconContent(string[] names, string fallbackText)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var tex = EditorGUIUtility.FindTexture(names[i]);
            if (tex != null) return new GUIContent(tex);
        }
        return new GUIContent(fallbackText);
    }

    void RenameSelectedAsset(string newName)
    {
        if (selectedLevel == null) return;
        if (string.IsNullOrWhiteSpace(newName)) return;

        newName = newName.Trim();
        var path = AssetDatabase.GetAssetPath(selectedLevel);
        var error = AssetDatabase.RenameAsset(path, newName);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("改名失败", error, "确定");
            return;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshLevelList();
    }

    void ApplySelectedLevelName(string newLevelName)
    {
        if (selectedLevel == null) return;
        Undo.RecordObject(selectedLevel, "Edit Level Name");
        selectedLevel.levelName = newLevelName ?? "";
        EditorUtility.SetDirty(selectedLevel);
        AssetDatabase.SaveAssets();
        RefreshLevelList();
    }
}
