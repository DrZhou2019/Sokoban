using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

public class LevelView : MonoBehaviour
{
    public bool debugRequired = false;
    LevelManager LevelManager;
    Dictionary<Vector2Int, GameObject> EntityGOList;
    Dictionary<Vector2Int, GameObject> GroundGOList;

    // Start is called before the first frame update
    void Start()
    {
        LevelManager = LevelManager.Instance;
        LevelManager.OnLevelLoadedAction += DrawLevel;
        LevelManager.OnEntityMovedAction += OnEntityMove;
        LevelManager.OnEntityRemoved += DeleteEntityPos;
        LevelManager.OnEntitySet += SetEntityPos;
    }

    void DrawLevel()
    {
        ClearLevelDraw();
        EntityGOList = new Dictionary<Vector2Int, GameObject>();
        GroundGOList = new Dictionary<Vector2Int, GameObject>();
        foreach (Vector2Int vector2Int in LevelManager.GroundList.Keys)
        {
            if (LevelManager.GroundList[vector2Int] != null && LevelManager.GroundList[vector2Int].shape != null)
            {
                GameObject GroundGO = Instantiate(LevelManager.GroundList[vector2Int].shape, new Vector3(vector2Int.x, 0f, vector2Int.y), transform.rotation, transform);
                GroundGOList.Add(vector2Int, GroundGO);
                if (debugRequired) Debug.Log($"{vector2Int}上成功绘制地板{GroundGO}");

            }
        }
        foreach (Vector2Int vector2Int in LevelManager.EntityList.Keys)
        {
            if (LevelManager.EntityList[vector2Int] != null && LevelManager.EntityList[vector2Int].shape != null)
            {
                GameObject EntityGO = Instantiate(LevelManager.EntityList[vector2Int].shape, new Vector3(vector2Int.x, 0f, vector2Int.y), transform.rotation, transform);
                EntityGOList.Add(vector2Int, EntityGO);
                if (debugRequired) Debug.Log($"{vector2Int}上成功绘制实体{EntityGO}");

            }
        }
    }
    /// <summary>
    /// 清除所有绘制的地面与实体
    /// </summary>
    void ClearLevelDraw()
    {
        if (EntityGOList != null)
        {
            foreach (var go in EntityGOList.Values)
            {
                if (go != null) Destroy(go);
            }
            EntityGOList.Clear();
        }
        if (GroundGOList != null)
        {
            foreach (var go in GroundGOList.Values)
            {
                if (go != null) Destroy(go);
            }
            GroundGOList.Clear();
        }
    }

    void DeleteEntityPos(Vector2Int pos)
    {
        if (EntityGOList == null) return;
        if (EntityGOList.TryGetValue(pos, out GameObject go))
        {
            if (debugRequired) Debug.Log($"成功删除位于{pos}的实体游戏对象");
            if (go != null) Destroy(go, 0.5f);
            EntityGOList.Remove(pos);
        }
        else
        {
            if (debugRequired) Debug.Log($"{pos}上没有实体游戏对象");
        }
    }

    void SetEntityPos(Vector2Int pos, Entity entity)
    {
        if (EntityGOList == null) return;
        if (entity == null || entity.shape == null) return;

        if (EntityGOList.TryGetValue(pos, out GameObject go) && go != null)
        {
            Destroy(go);
        }

        GameObject entityGo = Instantiate(entity.shape, new Vector3(pos.x, 0f, pos.y), transform.rotation, transform);
        EntityGOList[pos] = entityGo;
    }

    void OnEntityMove(Vector2Int sourcePos, Vector2Int targetPos)
    {
        if (EntityGOList == null || GroundGOList == null) return;

        if (!EntityGOList.TryGetValue(sourcePos, out GameObject entityGo) || entityGo == null)
        {
            if (debugRequired) Debug.LogWarning($"无法移动实体显示：{sourcePos} 上没有实体游戏对象");
            return;
        }

        if (entityGo.TryGetComponent<Animator>(out Animator animator))
        {
            Vector2Int delta = targetPos - sourcePos;
            if (delta.y > 0) animator.SetTrigger("Up");
            else if (delta.y < 0) animator.SetTrigger("Down");
            else if (delta.x > 0) animator.SetTrigger("Right");
            else if (delta.x < 0) animator.SetTrigger("Left");
        }


        SetGroundAnimatorTrigger(sourcePos, "AnythingLeft");
        SetGroundAnimatorTrigger(targetPos, "AnythingEnter");

        entityGo.transform.DOMove(new Vector3(targetPos.x, 0f, targetPos.y), 0.5f);
        EntityGOList.Remove(sourcePos);
        EntityGOList[targetPos] = entityGo;
    }
    /// <summary>
    /// 尝试触发位于坐标上的地面的AnimatorTrigger
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="triggerName"></param>
    public void SetGroundAnimatorTrigger(Vector2Int pos ,string triggerName)
    {
        if (GroundGOList.TryGetValue(pos, out GameObject groundTargetGo) && groundTargetGo != null && groundTargetGo.TryGetComponent<Animator>(out Animator groundTargetAnimator))
        {
            groundTargetAnimator.SetTrigger(triggerName);
        }
    }
}
