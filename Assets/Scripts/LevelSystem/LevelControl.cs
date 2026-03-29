using System.Collections;
using System.Collections.Generic;
using Unity.UOS.COSXML.Model.Bucket;
using UnityEngine;
using UnityEngine.InputSystem;

public class LevelControl : MonoBehaviour
{
    LevelManager levelManager;
    public float inputDuration = 0.5f;//输入间隔

    public float cooldown = 0f;
    private bool isAllowControl = true;
    private void Start()
    {
        levelManager = LevelManager.Instance;
        levelManager.OnLevelFinishAction += DisableControl;
        levelManager.OnLevelLoadedAction += EnableControl;
    }

    // 当通过PlayerInput触发Move动作时调用
    public void OnMove(InputValue value)
    {
        Vector2 moveInput = value.Get<Vector2>();
        if (!enabled)
        {
            Debug.Log("输入锁定中");
            return;
        }
        // 获取Move动作传来的Vector2数据


        // 判断按下的方向并预留空方法
        if (moveInput.y > 0)
        {
            OnMoveUp();
        }
        else if (moveInput.y < 0)
        {
            OnMoveDown();
        }

        if (moveInput.x < 0)
        {
            OnMoveLeft();
        }
        else if (moveInput.x > 0)
        {
            OnMoveRight();
        }

        cooldown = inputDuration;
    }

    private void Update()
    {
        if (cooldown > 0)
        {
            isAllowControl = false;
            cooldown -= Time.deltaTime;
        }
        else
        {
            isAllowControl = true;
        }
    }

    

    public void OnReset(InputValue value)
    {
        levelManager.Reload();
    }

    void MovePlayerEntity(Direction direction)
    {
        var playerPositions = new List<Vector2Int>();

        foreach (var pair in levelManager.EntityList)
        {
            if (pair.Value != null && pair.Value.entityType == EntityType.Player)
            {
                playerPositions.Add(pair.Key);
            }
        }
        if (playerPositions.Count ==0)
        {
            Debug.LogWarning("场上没有可移动对象，你可能输了，按下R重置关卡");
            return;
        }
        for (var i = 0; i < playerPositions.Count; i++)
        {
            var pos = playerPositions[i];
            if (levelManager.EntityList.TryGetValue(pos, out var entity) && entity != null && entity.entityType == EntityType.Player)
            {
                levelManager.MoveEntity(pos, direction);
            }
        }
    }

    private void OnMoveUp()
    {
        MovePlayerEntity(Direction.Up);
    }

    private void OnMoveDown()
    {
        MovePlayerEntity(Direction.Down);
    }

    private void OnMoveLeft()
    {
        MovePlayerEntity(Direction.Left);
    }

    private void OnMoveRight()
    {
        MovePlayerEntity(Direction.Right);
    }

    public void DisableControl()
    {
        enabled = false;
    }
    public void EnableControl()
    {
        enabled = true;
    }
}
