using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LevelManager : MonoBehaviour
{
    public LevelData currentLevel;

    public int currentLevelNum;
    public LevelData[] LevelList;

    public static LevelManager Instance;
    public event UnityAction OnLevelLoadedAction;//当关卡加载成功
    public event UnityAction<Vector2Int> OnEntityRemoved;//当实体被移除
    public event UnityAction<Vector2Int, Entity> OnEntitySet;//当实体被修改
    public event UnityAction OnLevelFinishAction;//当当前关卡通关

    public bool debugRequired = false;
    /// <summary>
    /// 当实体被成功从A移动到B时触发
    /// </summary>
    public event UnityAction<Vector2Int, Vector2Int> OnEntityMovedAction;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.Log("场景上已存在关卡加载器，已删除当前加载器");
            Destroy(gameObject);
        }
    }

    public Dictionary<Vector2Int,Entity> EntityList;
    public Dictionary<Vector2Int,Ground> GroundList;
    public void Load(LevelData levelData)
    {
        EntityList = new Dictionary<Vector2Int,Entity>();
        GroundList = new Dictionary<Vector2Int,Ground>();
        foreach (UnitInfo unitInfo in levelData.unitInfos)
        {
            if (unitInfo.ground == null)
            {
                if (debugRequired) Debug.Log($"{unitInfo.pos}没有地板，这里将不会生成对象");
            }
            else
            {
                GroundList.Add(unitInfo.pos, unitInfo.ground);
                EntityList.Add(unitInfo.pos, unitInfo.entity);
                if (debugRequired) Debug.Log($"{unitInfo.pos}成功已设置地板{unitInfo.ground}，设置实体{unitInfo.entity}");

            }
        }
        OnLevelLoaded();
    }

    public void Reload()
    {
        Load(currentLevel);
    }

    private void Start()
    {
        SwitchLevel(currentLevelNum);
    }

    private void OnLevelLoaded()
    {
        OnLevelLoadedAction?.Invoke();
        Debug.Log("关卡加载成功");
    }

    public void MoveEntity(Vector2Int targetPos, Direction direction, bool debugRequired = true)
    {
        if (TryMoveSingleEntity(targetPos, direction))
        {
            foreach (Vector2Int g in GroundList.Keys)
            {
                GroundList[g].OnLevelUpdate(this, GetEntityOnPos(g), g);
            }
        }
    }
    private bool TryMoveSingleEntity(Vector2Int targetPos, Direction direction)
    {
        if (EntityList.TryGetValue(targetPos, out Entity entity))
        {
            if (entity == null)
            {
                if (debugRequired) Debug.Log($"坐标{targetPos}上没有实体");
                return false;
            }

            Vector2Int dir = Vector2Int.zero;
            switch (direction)
            {
                case Direction.Up:dir = Vector2Int.up; break;
                case Direction.Down:dir = Vector2Int.down; break;
                case Direction.Left:dir = Vector2Int.left; break;
                case Direction.Right:dir = Vector2Int.right; break;
                default:dir = Vector2Int.zero;break;
            }
            if (GroundList.TryGetValue(targetPos+dir,out Ground ground))
            {
                if (EntityList.TryGetValue(targetPos+ dir, out Entity entity2))
                {

                    if (entity2 != null)
                    {
                        entity2.BePushed(this, entity, targetPos, targetPos + dir);//执行实体被推动函数
                    }
                    if (entity2 == null || GetEntityOnPos(targetPos+dir) == null)
                    {
                        
                        EntityList[targetPos + dir] = entity;
                        EntityList[targetPos] = null;
                        if (debugRequired) Debug.Log($"成功将坐标{targetPos}上的实体推到坐标{targetPos + dir}");
                        OnEntityMovedAction?.Invoke(targetPos, targetPos + dir);
                        entity.Contact(this, entity2, targetPos, targetPos + dir);
                        if (ground != null)
                        {
                            ground.OnEntityStepOn(this, entity, targetPos, targetPos + dir);
                        }
                        entity.Move(this,targetPos,targetPos+dir);
                        return true;
                    }

                    entity.Contact(this, entity2, targetPos, targetPos);
                    //if (debugRequired) Debug.Log($"坐标{targetPos + dir}上有其它实体，无法向目标位置推动");
                    if (entity2.isPushable)
                    {
                        if (TryMoveSingleEntity(targetPos + dir, direction))
                        {
                            return TryMoveSingleEntity(targetPos, direction);
                        }
                    }

                    if (entity == null || GetEntityOnPos(targetPos) == null)
                    {
                        if (debugRequired) Debug.Log("推动主体消失，上一步二次执行");
                        return TryMoveSingleEntity(targetPos-dir, direction);

                    }
                    return false;
                }
                else
                {
                    Debug.LogWarning("不太可能发生？但还是发生了");
                    return false;
                }
            }
            else
            {
                if (debugRequired) if (debugRequired) Debug.Log($"坐标{targetPos+dir}没有地板，无法向目标位置推动");
                return false;
            }
        }
        else
        {
            if (debugRequired) Debug.Log($"坐标{targetPos}上没有实体");
            return false;
        }
    }
    /// <summary>
    /// 获取坐标上的实体，不存在则返回空
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public Entity GetEntityOnPos(Vector2Int pos)
    {
        if(EntityList.TryGetValue(pos,out Entity entity))
        {
            return entity;
        }
        else
        {
            return null;
        }
    }
    /// <summary>
    /// 获取Entity所在的所有坐标点
    /// </summary>
    /// <returns></returns>
    public Vector2Int[] GetPosListByEntity(Entity entity)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (Vector2Int pos in EntityList.Keys)
        {
            if (EntityList[pos] == entity)
            {
                list.Add(pos);
            }
        } 
        return list.ToArray();
    }
    public void RemoveEntityOnPos(Vector2Int pos)
    {
        if (GetEntityOnPos(pos) != null)
        {
            OnEntityRemoved.Invoke(pos);
        }
        SetEntityOnPos(null, pos, true);
    }

    public bool SetEntityOnPos(Entity addedEntity, Vector2Int pos, bool forceReplace = false)
    {
        if (EntityList.TryGetValue(pos, out Entity entity))
        {
            if (entity != null)
            {
                if (!forceReplace)
                {
                    return false;
                }
            }
            EntityList[pos] = addedEntity;
            if (addedEntity != null)
            {
                OnEntitySet?.Invoke(pos, addedEntity);
            }
            return true;
        }

        return false;
    }

    public void LevelFinished()
    {
        Debug.Log($"{currentLevel.name}成功通关");
        OnLevelFinishAction.Invoke();
    }

    public void NextLevel()
    {
        currentLevelNum++;
        SwitchLevel(currentLevelNum);
    }

    public void SwitchLevel(int levelNum)//切换关卡
    {
        if (levelNum >= LevelList.Length||levelNum< 0)
        {
            Debug.LogWarning("关卡列表内没有此关卡，可能是你已经完成了所有关卡");
            return;
        }

        currentLevel = LevelList[levelNum];
        Reload();
    }
    private void LevelFail()
    {
    }
}

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}
public struct RuntimeEntityInfo
{
    public bool Pushable;
}
