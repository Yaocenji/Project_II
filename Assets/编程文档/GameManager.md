
# 脚本 ProjectII.Manager.GameSceneManager
继承：MonoBehaviour

游戏场景管理器。

## 成员 CharacterController CurrentPlayerCharacter
当前玩家角色对象。
玩家对象创建的时候注册，用于替代其他脚本访问玩家对象时的单例系统。

## 成员 Mouse CurrentMouse
当前的鼠标对象。
鼠标对象创建时注册。

## 静态属性 Instance
GameSceneManager单例实例，用于全局访问。

## 方法 RegisterPlayerCharacter(CharacterController characterController)
注册玩家角色对象。
玩家对象创建的时候调用此方法注册。

## 方法 UnregisterPlayerCharacter(CharacterController characterController)
注销玩家角色对象。

## 方法 RegisterMouse(Mouse mouse)
注册鼠标对象。
鼠标对象创建时调用此方法注册。

## 方法 UnregisterMouse(Mouse mouse)
注销鼠标对象。
