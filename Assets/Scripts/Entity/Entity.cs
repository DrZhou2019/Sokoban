using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEntity", menuName = "Level System/Entity")]

public class Entity : ScriptableObject
{
    public string Name = "Entity";
    public EntityType entityType = EntityType.Untagged;
    public bool isPushable = true;
    public bool isStoppable = false;
    public bool debugRequired = false;
    public GameObject shape;

    /// <summary>
    /// 被其它实体尝试推动时
    /// </summary>
    /// <param name="LevelManager"></param>
    /// <param name="pushingEntity"></param>
    /// <param name="pushingEntityPos"></param>
    /// <param name="targetEntityPos"></param>
    public virtual void BePushed(LevelManager LevelManager,Entity pushingEntity,Vector2Int pushingEntityPos,Vector2Int pushedEntityPos)
    {
        if (debugRequired) Debug.Log($"位于{pushedEntityPos}正在被实体{pushingEntity}尝试推动");
    }
    /// <summary>
    /// 正在推动其它实体
    /// </summary>
    public virtual void Contact(LevelManager LevelManager, Entity pushedEntity, Vector2Int pushingEntityPos, Vector2Int pushedEntityPos)
    {

    }

    public virtual void Move(LevelManager levelLoad, Vector2Int startPos, Vector2Int endPos)
    {

    }

    public virtual void Trigger(LevelManager levelLoad, Entity triggerEntity, Vector2Int effectorPos, bool apply )
    {

    }
}

public enum EntityType
{
    Untagged,
    Player,
    Trigger,
    Wall
}
