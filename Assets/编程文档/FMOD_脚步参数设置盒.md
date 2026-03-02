
# 脚本 ProjectII.FX.FMOD_FootStepPlayer_Parameter_Trigger
继承：MonoBehaviour

仿照着FMOD_Snapshot_Trigger
脚步声触发器盒

说明传参方法：
参考1：
@Assets/Weapon/Revolver.cs:227
参考2
footstepInstance.setParameterByName("PlayerMove", (int)currentState);

## 移动类型
参数名 PlayerMove
参数可能值
0-Walk
1-Run
……

## 地面类型
参数名 GroundMaterial
参数可能值
0-Wood
1-Tile
……

## 公有数据 在场景中序列化
这个Trigger的参数（对应上面两类参数的组合）

## 方法 玩家进入触发器
获取玩家的脚步声（是玩家gameobject下面的一个gameobject，挂载了一个FMODUnity.StudioEventEmitter），根据这个Trigger的参数设置emmiter参数

说明：玩家离开触发器时就不用设置了，这部分由另外的触发器覆盖设置就可以

## 绘制（运行时代码）
在Scene里面画出来触发器范围（类似于选中是Trigger组件的绘制，不过需要没选中的时候也绘制）
