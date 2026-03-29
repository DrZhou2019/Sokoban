using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDoor", menuName = "Level System/Door")]
public class Door : Entity//当前的开关门设定暂且为生成与销毁实体
{
    public Entity keyEntity;
    Vector2Int[] doorPosList;

    public override void BePushed(LevelManager LevelManager, Entity pushingEntity, Vector2Int pushingEntityPos, Vector2Int targetEntityPos)
    {
        base.BePushed(LevelManager, pushingEntity, pushingEntityPos, targetEntityPos);
        if (pushingEntity == keyEntity)
        {
            OpenDoor(targetEntityPos);
        }
    }
    public override void Trigger(LevelManager LevelManager, Entity triggerEntity, Vector2Int effectorPos, bool apply)
    {
        if (apply)//开门
        {
            doorPosList = LevelManager.GetPosListByEntity(this);
            OpenDoor(effectorPos);
            Debug.Log("门已被成功打开");
        }else//关门
        {
            if (doorPosList == null || doorPosList.Length == 0)
            {
                return;
            }
            foreach(Vector2Int entityPos in doorPosList)
            {
                CloseDoor(entityPos);
            }
            doorPosList = new Vector2Int[0];
        }
        base.Trigger(LevelManager, triggerEntity,effectorPos,apply);
    }
    /// <summary>
    /// 开门
    /// </summary>
    /// <param name="doorPos"></param>
    public void OpenDoor(Vector2Int doorPos)
    {
        LevelManager.Instance.RemoveEntityOnPos(doorPos);
    }
    /// <summary>
    /// 关门
    /// </summary>
    /// <param name="doorPos"></param>
    public void CloseDoor(Vector2Int doorPos)
    {
        if (!LevelManager.Instance.SetEntityOnPos(this, doorPos))
        {
            Debug.Log($"关门失败，位置{doorPos} 上已有其它实体");
        }
    }
}
