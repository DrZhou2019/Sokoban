using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGround", menuName = "Level System/Ground")]
public class Ground : ScriptableObject
{
    public string Name;
    public GameObject shape;
    public GroundType groundType = GroundType.Untagged;

    /// <summary>
    /// 当实体踏上该地板时调用
    /// </summary>
    /// <param name="LevelManager"></param>
    /// <param name="entity"></param>
    /// <param name="lastPos"></param>
    /// <param name="currentPos"></param>
    public virtual void OnEntityStepOn(LevelManager LevelManager,Entity entity,Vector2Int lastPos, Vector2Int currentPos)
    {

    }
    /// <summary>
    /// 当关卡更新时调用
    /// </summary>
    /// <param name="LevelManager"></param>
    /// <param name="entity"></param>
    /// <param name="currentPos"></param>
    public virtual void OnLevelUpdate(LevelManager LevelManager, Entity currentEntity, Vector2Int currentPos)
    {

    }
}

public enum GroundType
{
    Untagged,
    Normal,
    Special,
    Trigger,
    Danger,
    WinFloor

}
