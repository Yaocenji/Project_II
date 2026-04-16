# 任务 4：最简 HUD

> 优先级：🟡 P1
> 预计工期：1~2 天
> 前置依赖：任务 1（Damageable 组件，提供 HP 数据）
> 命名空间：`ProjectII.UI`
> 文件位置：`Assets/UI/`

---

## 一、概述

本任务的目标是在屏幕上显示关键的游戏数据，让玩家能看到自己的状态。**不需要精美的美术，能看到数据就行**。使用 Unity 默认 UI 组件，后期再替换为像素风美术 UI。

---

## 二、HUD 需要显示的内容

### 2.1 内容清单

| 内容 | 优先级 | 数据来源 | 更新频率 |
|------|--------|----------|----------|
| HP 血条 | P0 | 玩家 `Damageable.CurrentHP / MaxHP` | 事件驱动（受伤/回血时） |
| HP 数字 | P0 | 同上 | 同上 |
| 弹药显示 | P0 | 当前武器的弹药数据 | 事件驱动（射击/换弹时） |
| 换弹提示 | P1 | 武器换弹状态 | 换弹开始/结束时 |
| 快捷栏格子 | P1 | `Hotbar.items[]` + `Hotbar.currentSlotIndex` | 切换装备时 |
| 当前装备图标 | P1 | `Hotbar.CurrentItem.icon` | 切换装备时 |
| 交互提示 | P2 | `GameSceneManager.NearbyInteractables` | 后期再做 |

### 2.2 屏幕布局

```
┌─────────────────────────────────────────────────┐
│                                                 │
│                                                 │
│                                                 │
│                   游戏画面                        │
│                                                 │
│                                                 │
│                                                 │
│                                                 │
├─────────────────────────────────────────────────┤
│ [HP Bar]████████░░ 80/100                       │
│                                                 │
│ [1][2][3][4][5]        弹药: 4 / 6             │
│  ▲                     [换弹中...]              │
│ 当前                                            │
└─────────────────────────────────────────────────┘
```

**布局说明：**
- **左下角**：HP 血条 + 数字
- **左下角下方**：快捷栏格子（水平排列）
- **右下角**：弹药显示 + 换弹提示

---

## 三、各 UI 元素详细设计

### 3.1 HP 血条 🔴 P0

#### 视觉设计

```
HP 血条结构：
┌──────────────────────────┐
│ ██████████░░░░░░  80/100 │
└──────────────────────────┘
  ↑ 填充部分    ↑ 空白部分  ↑ 数字

填充颜色规则：
  HP > 60%  → 绿色 (#4CAF50)
  HP > 30%  → 黄色 (#FFC107)
  HP ≤ 30%  → 红色 (#F44336)
```

#### UI 层级结构

```
HPBar (GameObject)
├── Background (Image)
│   └── 深灰色底条，Sprite: 纯色方块
│       Size: 200 x 20
│
├── Fill (Image)
│   └── 填充条，Image Type: Filled, Fill Method: Horizontal
│       Size: 200 x 20
│       Color: 根据 HP 比例变色
│
└── HPText (TextMeshProUGUI)
    └── "80 / 100"
        Font Size: 14
        Alignment: Right
```

#### 数据绑定

```
数据来源：玩家 Damageable 组件

绑定方式：事件驱动
    Damageable.OnHPChanged_CSharp += UpdateHPBar

UpdateHPBar(int currentHP, int maxHP):
    float ratio = (float)currentHP / maxHP
    fillImage.fillAmount = ratio
    hpText.text = $"{currentHP} / {maxHP}"
    
    // 变色
    if (ratio > 0.6f)
        fillImage.color = greenColor
    else if (ratio > 0.3f)
        fillImage.color = yellowColor
    else
        fillImage.color = redColor
```

#### 可选增强效果

- **受伤抖动**：HP 变化时血条轻微抖动（DOTween 或手写 Lerp）
- **延迟扣血条**：先扣绿色血条，白色"虚血"延迟 0.5 秒再跟上（常见于动作游戏）
- **低血量闪烁**：HP < 30% 时血条闪烁提醒

以上均为 P2，第一版不做。

### 3.2 弹药显示 🔴 P0

#### 视觉设计

```
弹药显示：
  4 / 6          ← 当前弹药 / 最大弹药
  [换弹中...]    ← 换弹时显示（可选 P1）
```

#### 数据来源问题

**当前问题**：`Revolver` 的 `currentAmmo` 和 `maxAmmo` 是 private 字段，HUD 无法读取。

**需要在 Revolver（或 WeaponBase）中新增的公有接口：**

```csharp
// 方案 A：公有属性（简单直接）
public int CurrentAmmo => currentAmmo;
public int MaxAmmo => maxAmmo;
public bool IsReloading => isReloading;

// 方案 B：事件驱动（推荐，与 Damageable 风格一致）
public event System.Action<int, int> OnAmmoChanged;  // (current, max)
public event System.Action OnReloadStart;
public event System.Action OnReloadEnd;

// 在射击时触发：
currentAmmo--;
OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);

// 在换弹协程中触发：
OnReloadStart?.Invoke();
// ... yield return ...
OnReloadEnd?.Invoke();
OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
```

**推荐方案 B**，事件驱动避免每帧轮询，且与 Damageable 的设计风格一致。

**但第一版可以先用方案 A**（公有属性 + 每帧读取），快速实现。

#### UI 层级结构

```
AmmoDisplay (GameObject)
├── AmmoText (TextMeshProUGUI)
│   └── "4 / 6"
│       Font Size: 18
│       Alignment: Right
│
└── ReloadText (TextMeshProUGUI)  // P1
    └── "换弹中..."
        Font Size: 12
        Color: 黄色
        默认隐藏
```

#### 数据绑定

```
// 方案 A：每帧轮询（第一版）
Update():
    WeaponBase weapon = hotbar.CurrentItem as WeaponBase
    if (weapon is Revolver revolver):
        ammoText.text = $"{revolver.CurrentAmmo} / {revolver.MaxAmmo}"
    else:
        ammoText.text = ""  // 非武器物品不显示弹药

// 方案 B：事件驱动（推荐后期改造）
// 订阅 Revolver.OnAmmoChanged
```

### 3.3 快捷栏格子 🟡 P1

#### 视觉设计

```
快捷栏：
[1] [2] [3] [4] [5]
 ▲
当前选中（高亮边框）

每个格子：
┌─────┐
│     │  ← 物品图标（如果有）
│  1  │  ← 格子编号
└─────┘
  ↑ 选中时边框变亮/变色
```

#### UI 层级结构

```
HotbarUI (GameObject)
├── Slot_0 (GameObject)
│   ├── Background (Image) → 格子背景（深色半透明）
│   ├── Icon (Image) → 物品图标（如果有物品）
│   ├── SlotNumber (TextMeshProUGUI) → "1"
│   └── SelectionBorder (Image) → 选中高亮边框（默认隐藏）
│
├── Slot_1 ...
├── Slot_2 ...
├── Slot_3 ...
└── Slot_4 ...
```

#### 数据绑定

```
数据来源：Hotbar 组件

需要从 Hotbar 获取的数据：
1. items[] 数组 → 每个格子是否有物品、物品图标
2. currentSlotIndex → 当前选中的格子

绑定方式：
- 第一版可以每帧轮询 Hotbar 状态
- 后期可以给 Hotbar 添加事件：OnSlotChanged, OnItemChanged

UpdateHotbarUI():
    for (int i = 0; i < slotCount; i++):
        if (hotbar.items[i] != null):
            slots[i].icon.sprite = hotbar.items[i].icon
            slots[i].icon.enabled = true
        else:
            slots[i].icon.enabled = false
        
        // 高亮当前选中格子
        slots[i].selectionBorder.enabled = (i == hotbar.currentSlotIndex)
```

**注意**：`Hotbar.currentSlotIndex` 目前是 private，需要添加公有属性：

```csharp
public int CurrentSlotIndex => currentSlotIndex;
```

### 3.4 换弹提示 🟡 P1

#### 视觉设计

换弹时在弹药显示下方出现"换弹中..."文字，可选加一个简单的进度条。

```
弹药: 0 / 6
[换弹中... ████░░░░]  ← 进度条（可选）
```

#### 技术方案

```
// 简单方案：文字提示
OnReloadStart():
    reloadText.gameObject.SetActive(true)

OnReloadEnd():
    reloadText.gameObject.SetActive(false)

// 进阶方案：进度条
// 需要 Revolver 暴露 reloadProgress（0~1）
// 或者 HUD 自己根据 reloadTime 计时
```

---

## 四、GameHUD 主控组件

### 4.1 组件职责

`GameHUD` 是 HUD 系统的主控组件，负责：
1. 获取所有数据源的引用
2. 管理所有 HUD 子元素的更新
3. 处理 HUD 的显示/隐藏

### 4.2 成员变量

```
[Header("引用")]
[SerializeField] hpFillImage     : Image              // HP 填充条
[SerializeField] hpText          : TextMeshProUGUI     // HP 数字
[SerializeField] ammoText        : TextMeshProUGUI     // 弹药数字
[SerializeField] reloadText      : TextMeshProUGUI     // 换弹提示
[SerializeField] hotbarSlots     : HotbarSlotUI[]      // 快捷栏格子 UI 数组

[Header("颜色设置")]
[SerializeField] hpColorHigh     : Color = #4CAF50     // HP > 60%
[SerializeField] hpColorMid      : Color = #FFC107     // HP > 30%
[SerializeField] hpColorLow      : Color = #F44336     // HP ≤ 30%

// 运行时引用（在 Start 中获取）
playerDamageable : Damageable
hotbar           : Hotbar
```

### 4.3 生命周期

```
Start():
    // 从 GameSceneManager 获取玩家引用
    var player = GameSceneManager.Instance.CurrentPlayerCharacter
    playerDamageable = player.GetComponent<Damageable>()
    hotbar = player.GetComponentInChildren<Hotbar>()
    
    // 订阅事件
    playerDamageable.OnHPChanged_CSharp += UpdateHP
    
    // 初始化显示
    UpdateHP(playerDamageable.CurrentHP, playerDamageable.MaxHP)

Update():
    // 更新弹药显示（第一版轮询）
    UpdateAmmo()
    // 更新快捷栏显示（第一版轮询）
    UpdateHotbar()

OnDestroy():
    // 取消订阅
    if (playerDamageable != null)
        playerDamageable.OnHPChanged_CSharp -= UpdateHP
```

### 4.4 HotbarSlotUI 辅助类

```
[System.Serializable]
public class HotbarSlotUI
{
    public Image background;        // 格子背景
    public Image icon;              // 物品图标
    public TextMeshProUGUI number;  // 格子编号
    public Image selectionBorder;   // 选中高亮
}
```

---

## 五、需要修改的现有代码

### 5.1 Revolver.cs — 暴露弹药数据

```csharp
// 新增公有属性
public int CurrentAmmo => currentAmmo;
public int MaxAmmo => maxAmmo;
public bool IsReloading => isReloading;
```

### 5.2 Hotbar.cs — 暴露当前格子索引

```csharp
// 新增公有属性
public int CurrentSlotIndex => currentSlotIndex;
public int SlotCount => slotCount;
```

### 5.3 WeaponBase.cs — 可选：统一弹药接口

如果后期有多种武器，建议在 `WeaponBase` 中定义弹药接口：

```csharp
// 在 WeaponBase 中添加（可选，后期再做）
public virtual int CurrentAmmo => 0;
public virtual int MaxAmmo => 0;
public virtual bool IsReloading => false;
public virtual bool HasAmmo => true;  // 近战武器返回 true
```

第一版可以不做，直接在 HUD 中 cast 到 `Revolver` 类型读取。

---

## 六、Canvas 配置

```
Canvas (GameObject)
├── Canvas 组件
│   ├── Render Mode: Screen Space - Overlay
│   ├── Sort Order: 10（确保在最上层）
│   └── Canvas Scaler:
│       ├── UI Scale Mode: Scale With Screen Size
│       ├── Reference Resolution: 1920 x 1080
│       └── Match Width Or Height: 0.5
│
├── HPBar (左下角)
│   ├── Anchor: Bottom-Left
│   ├── Position: (20, 60)
│   └── ... HP 血条子元素
│
├── HotbarUI (左下角下方)
│   ├── Anchor: Bottom-Left
│   ├── Position: (20, 20)
│   └── ... 快捷栏格子
│
├── AmmoDisplay (右下角)
│   ├── Anchor: Bottom-Right
│   ├── Position: (-20, 40)
│   └── ... 弹药显示子元素
│
├── DamageOverlay (全屏，任务 3 的受击闪红)
│   ├── Anchor: Stretch-Stretch
│   └── Raycast Target: false（不拦截点击）
│
└── FadeOverlay (全屏，任务 3 的死亡黑屏)
    ├── Anchor: Stretch-Stretch
    └── Raycast Target: false
```

---

## 七、文件清单

| 文件 | 说明 | 优先级 |
|------|------|--------|
| `Assets/UI/GameHUD.cs` | HUD 主控组件 | P0 |
| `Assets/Weapon/Revolver.cs` | 修改：新增 CurrentAmmo / MaxAmmo 公有属性 | P0 |
| `Assets/Item/Hotbar.cs` | 修改：新增 CurrentSlotIndex 公有属性 | P1 |
| HUD Canvas Prefab | 在场景中搭建 Canvas + 所有 UI 元素 | P0 |

---

## 八、验收标准

- [ ] 屏幕左下角显示 HP 血条和数字
- [ ] 玩家受伤后血条实时更新，颜色随 HP 比例变化
- [ ] 屏幕右下角显示弹药数（如 "4 / 6"）
- [ ] 射击后弹药数减少，换弹后恢复
- [ ] 快捷栏格子显示在屏幕下方，当前选中格子有高亮
- [ ] 切换快捷栏格子时高亮跟随
- [ ] 非武器物品装备时，弹药显示隐藏

---

## 九、注意事项

1. **不要做精美 UI**：使用 Unity 默认 UI 组件 + 纯色/简单形状，后期再替换美术资源
2. **TextMeshPro**：建议使用 TextMeshProUGUI 而非 Unity 原生 Text，渲染质量更好
3. **事件 vs 轮询**：第一版弹药和快捷栏用轮询（简单），后期改为事件驱动（性能好）
4. **Raycast Target**：所有不需要交互的 UI 元素都关闭 Raycast Target，避免拦截游戏输入
5. **Canvas Sort Order**：HUD Canvas 的 Sort Order 要低于 DamageOverlay 和 FadeOverlay
6. **分辨率适配**：使用 Canvas Scaler 的 Scale With Screen Size 模式，Reference Resolution 设为 1920x1080
