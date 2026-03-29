using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

[CreateAssetMenu(fileName = "NewIce", menuName = "Level System/Ice")]
public class Ice : Ground
{

    public override void OnEntityStepOn(LevelManager LevelManager, Entity entity, Vector2Int lastPos, Vector2Int currentPos)
    {
        Direction direction;
        if (currentPos.x - lastPos.x > 0) direction = Direction.Right;
        else if (currentPos.x - lastPos.x < 0) direction = Direction.Left;
        else if (currentPos.y - lastPos.y > 0) direction = Direction.Up;
        else if (currentPos.y - lastPos.y < 0) direction = Direction.Down;
        else return;

        LevelManager.MoveEntity(currentPos, direction);
    }

    
}

