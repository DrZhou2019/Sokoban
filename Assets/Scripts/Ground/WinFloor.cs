using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewWinFloor", menuName = "Level System/WinFloor")]

public class WinFloor : Ground
{
    public GameObject WinUiPanel;
    public Entity triggerEntity;

    public override void OnEntityStepOn(LevelManager LevelManager, Entity entity, Vector2Int lastPos, Vector2Int currentPos)
    {
        if (entity == triggerEntity)
        {
            LevelManager.LevelFinished();
            Instantiate(WinUiPanel, FindFirstObjectByType<Canvas>().transform);
        }
    }
}
