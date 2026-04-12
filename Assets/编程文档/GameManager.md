# 脚本 ProjectII.Manager.GameSceneManager
继承：MonoBehaviour

游戏场景管理器。

## 成员 CharacterController CurrentPlayerCharacter
当前玩家角色对象。
玩家对象创建的时候注册，用于替代其他脚本访问玩家对象时的单例系统。

## 成员 Mouse CurrentMouse
当前的鼠标对象。
鼠标对象创建时注册。

## 成员 IReadOnlyList<InteractableBase> NearbyInteractables
当前玩家附近的可交互物品列表，只读。
该列表由各个可交互物品在进入/退出可交互状态时维护。
通常通过交互物品基类中的“进入可交互状态 / 退出可交互状态”方法间接注册与注销。

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

## 方法 RegisterNearbyInteractable(InteractableBase item)
注册附近可交互物品。
当某个交互物品进入可交互状态时调用，将其加入 NearbyInteractables 列表。
若该物品已经在列表中，则不会重复加入。

## 方法 UnregisterNearbyInteractable(InteractableBase item)
注销附近可交互物品。
当某个交互物品离开可交互状态，或被销毁时调用，将其从 NearbyInteractables 列表移除。
