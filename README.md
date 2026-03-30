# BoxPusher
技策笔试题，以Baba is You为蓝本参考，制作的简易推箱子原型
## 如何游玩
* 下载项目并用Unity打开，进入位于Asset/Scene的SimpleScene场景，直接运行即可
## 如何操作
* WASD：上下左右移动
* R：重试关卡
## 关卡机制
-- 注：当前投放的关卡仅有7个，所有关卡通关后没有反应是正常的 --
* 已实现：
  * 冰面地板：进入冰面上方的实体会沿着自身移动方向滑动到冰面尽头
  * 机关踏板/电子门：当指定的实体踩在踏板上方时会触发对应配置的机关，当前配置下，任何实体在机关踏板时会打开与关闭电子门
  * 钥匙/锁：在当前配置下，钥匙碰到对应的锁时，锁会打开门，同时两者都会消失
* 待实现：
  * 岩浆：任何踩在上方的实体都会被摧毁
  * 传送地毯：任何踩在上方的实体都会被传送到对应的地面上

## 关卡编辑器食用指南
* 编辑器位于上方Tools菜单栏内，点击即可呼出编辑器窗口
* 编辑器已用AI工具调教成用户友好和符合直觉的开袋即食的使用方式，核心功能如下：
  * 笔刷工具：选择工具后，从下方选择需要的实体或者地面，在场景视图点击即可放置
  * 橡皮擦工具：选择工具后，在场景视图内点击需要删除的对象即可从关卡中去除
  * 移动工具：选择工具后，在场景视图选择想要移动的对象，再次点击其它位置即可将对象移动或替换到指定位置

* 如何测试关卡？
 * 当前的测试方式相对原始，需将配置好的关卡文件数据文件拖入场景的LevelManger的关卡列表中，并修改current level num的数值
   <img width="574" height="388" alt="image" src="https://github.com/user-attachments/assets/ae55c96a-a99f-47ac-9870-cf21d8606ba8" />


* 其它小功能：
 * 关卡文件的增加、删除、复制、改名、关卡完整性验证

## 设计思路
* 参考了MVC框架，整体结构分成规则层、输入层、表现层
* 规则层：
  * 使用ScriptableObject存储关卡、实体、地面。其中实体与地面通过继承重写父方法实现不同的功能效果。秉持涌现设计哲学，实体的代码设计保持灵活性，尽可能的通过配置的方式来实现功能。
  * LevelManager负责加载关卡数据的同时，也负责整体的运行逻辑。
  * 基本逻辑：
    
    <img width="553" height="474" alt="image" src="https://github.com/user-attachments/assets/527aac24-760b-4fe3-8e92-437be2feb6fa" />

  *地图加载的核心代码如下：
~~~csharp
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
~~~ 
  * 移动的核心代码如下：
~~~csharp
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
~~~
* 输入层：
  * 输入层很简单捏，使用Unity官方支持的Input Manager识别并参数输入进LevelControl，消化输入后调用LeveManager的移动方法。
* 表现层：
 * 核心脚本LevelView，通过订阅LevelManager的各种事件，将实体上配置的shape绘制在场景中，并实时跟进。
~~~csharp
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
~~~
 * 同时支持向实体上的Animator发送动画事件，支持后续增加各种动画
~~~csharp
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
~~~
