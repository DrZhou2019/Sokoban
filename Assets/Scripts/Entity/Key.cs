using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewKey", menuName = "Level System/Key")]

public class Key : Entity
{
    public Entity LockEntity;

    public override void Contact(LevelManager LevelManager, Entity pushedEntity, Vector2Int pushingEntityPos, Vector2Int pushedEntityPos)
    {
        base.Contact(LevelManager, pushedEntity, pushingEntityPos, pushedEntityPos);
        if (pushedEntity == LockEntity)
        {
            LevelManager.RemoveEntityOnPos(pushedEntityPos);

        }
    }
    public override void BePushed(LevelManager LevelManager, Entity pushingEntity, Vector2Int pushingEntityPos, Vector2Int pushedEntityPos)
    {        
        base.BePushed(LevelManager, pushingEntity, pushingEntityPos, pushedEntityPos);
        if (pushingEntity == LockEntity)
        {
            LevelManager.RemoveEntityOnPos(pushedEntityPos);
        }
    }
}
