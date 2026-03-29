using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static UnityEditor.PlayerSettings;
using static UnityEngine.EventSystems.EventTrigger;

[CreateAssetMenu(fileName = "NewPedal", menuName = "Level System/Pedal")]
public class Pedal : Ground
{
    /// <summary>
    /// 可触发踏板的实体，留空表示任何实体都可触发
    /// </summary>
    public List<Entity> triggerEntities;
    /// <summary>
    /// 被影响的实体
    /// </summary>
    public List<Entity> effectEntities;

    public override void OnEntityStepOn(LevelManager LevelManager, Entity entity, Vector2Int lastPos, Vector2Int currentPos)
    {
        
    }

    public override void OnLevelUpdate(LevelManager LevelManager, Entity currentEntity, Vector2Int currentPos)
    {
        if (currentEntity == null)
        {
            Deactivate(LevelManager, currentPos);
            return;
        }
        if (triggerEntities.Count > 0 && !triggerEntities.Contains(currentEntity))
        {
            Deactivate(LevelManager, currentPos);
            return;
        }

        foreach (Entity effector in effectEntities)
        {
            foreach (Vector2Int pos in LevelManager.GetPosListByEntity(effector))
            {
                Debug.Log($"�ɹ�����ʵ��{effector}�ϵ�Trigger");
                effector.Trigger(LevelManager,null, pos,true);
            }
        }

        LevelManager.GetComponent<LevelView>().SetGroundAnimatorTrigger(currentPos, "Activate");
        base.OnLevelUpdate(LevelManager, currentEntity, currentPos);
    }

    void Deactivate(LevelManager LevelManager, Vector2Int currentPos)
    {
        LevelManager.GetComponent<LevelView>().SetGroundAnimatorTrigger(currentPos, "Deactivate");
        foreach (Entity effector in effectEntities)
        {
            Vector2Int[] posList = LevelManager.GetPosListByEntity(effector);
            if (posList.Length == 0)
            {
                effector.Trigger(LevelManager, null, Vector2Int.zero, false);
                continue;
            }
            foreach (Vector2Int pos in posList)
            {
                effector.Trigger(LevelManager, null, pos, false);
            }
        }
    }
}
