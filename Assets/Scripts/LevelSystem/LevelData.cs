using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "Level System/LevelData")]
public class LevelData : ScriptableObject
{
    public string levelName = "name";
    public UnitInfo[] unitInfos = null;

}
