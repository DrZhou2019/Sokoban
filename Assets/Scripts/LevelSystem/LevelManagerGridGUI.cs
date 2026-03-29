using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelManagerGridGUI : MonoBehaviour
{
    // 中文注释：按 F1 可以显示/隐藏调试面板
    public bool Visible = true;

    // 中文注释：每个格子的显示尺寸（像素）
    public int CellSize = 44;

    // 中文注释：网格左上角起始偏移（像素）
    public Vector2 ScreenOffset = new Vector2(12, 64);

    // 中文注释：是否在格子里显示坐标
    public bool ShowCoordinates;

    private LevelManager LevelManager;
    private GUIStyle cellStyle;
    private GUIStyle titleStyle;


    private void EnsureInitialized()
    {
        LevelManager = GetComponent<LevelManager>();
        if (LevelManager == null)
        {
            LevelManager = LevelManager.Instance;
        }

        if (cellStyle == null)
        {
            cellStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14
            };
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Visible = !Visible;
        }
    }

    private void OnGUI()
    {
        EnsureInitialized();

        if (!Visible)
        {
            return;
        }

        if (LevelManager == null || LevelManager.GroundList == null || LevelManager.EntityList == null)
        {
            GUI.Label(new Rect(12, 12, 600, 24), "未找到 LevelManager 或关卡尚未加载（F1 显示/隐藏）", titleStyle);
            return;
        }

        if (LevelManager.GroundList.Count == 0)
        {
            GUI.Label(new Rect(12, 12, 600, 24), "当前 GroundList 为空（F1 显示/隐藏）", titleStyle);
            return;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var pos in LevelManager.GroundList.Keys)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y > maxY) maxY = pos.y;
        }

        GUI.Label(new Rect(12, 12, 900, 24), "单位格子可视化：显示实体名字，空格显示 .（F1 显示/隐藏）", titleStyle);

        for (var y = maxY; y >= minY; y--)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var pos = new Vector2Int(x, y);
                var screenX = ScreenOffset.x + (x - minX) * CellSize;
                var screenY = ScreenOffset.y + (maxY - y) * CellSize;
                var rect = new Rect(screenX, screenY, CellSize, CellSize);

                var hasGround = LevelManager.GroundList.TryGetValue(pos, out var ground) && ground != null;
                if (!hasGround)
                {
                    GUI.Box(rect, "", cellStyle);
                    continue;
                }

                var symbol = ".";
                if (LevelManager.EntityList.TryGetValue(pos, out var entity) && entity != null)
                {
                    symbol = string.IsNullOrEmpty(entity.Name) ? "未命名" : entity.Name;
                }

                if (ShowCoordinates)
                {
                    GUI.Box(rect, $"{symbol}\n{x},{y}", cellStyle);
                }
                else
                {
                    GUI.Box(rect, symbol, cellStyle);
                }
            }
        }
    }
}
