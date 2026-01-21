
# 脚本 ProjectII.Manager.InputManager
继承：MonoBehaviour

管理场景的inputAction。
和GameSceneManager一样，尽量在场景其他物体创建前创建。
使用DefaultExecutionOrder(-99)确保在GameSceneManager之后、其他脚本之前执行。

## 成员 InputAction
初始化的时候创建一个inputAction，其他所有涉及玩家输入的脚本，都从这里拿引用，不要单独创建了。
### 类型
Project_II.InputSystem.InputAction_0

## 成员 CurrentDeviceType
每帧调用，检测玩家的输入设备类型，其他所有涉及玩家输入的脚本，需要判断设备类型的时候，也从这里拿就行。
### 类型
InputDeviceType枚举
### 枚举值
- KeyboardMouse：键鼠输入
- Gamepad：手柄输入

## 静态属性 Instance
InputManager单例实例，用于全局访问。

## 方法 UpdateDeviceType
每帧调用，检测玩家的输入设备类型。
优先检测手柄输入，如果手柄有活动输入则判定为手柄模式，否则判定为键鼠模式。

## 说明
- InputManager会在Awake时创建InputAction_0实例
- InputManager会在OnEnable时启用InputAction，OnDisable时禁用
- InputManager会在OnDestroy时清理InputAction资源
- 其他脚本（如CharacterController、Mouse）应该从InputManager获取InputAction引用，而不是自己创建
- 设备类型检测逻辑：优先检测手柄是否有活动输入，如果没有则判定为键鼠模式

