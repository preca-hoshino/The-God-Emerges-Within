# 暂停菜单镜头转盘验证

规范版本为 Unity 6.3（6000.3.22f1），当前工程记录为 6000.3.24f1。将 [PauseCarouselValidation.cs](PauseCarouselValidation.cs) 复制到**隔离工程**的 `Assets/Editor`，同步当前工程的 Assets、Packages、ProjectSettings 后执行；选择与隔离工程版本匹配的编辑器：

```powershell
& '<Unity editor>/Unity.exe' -batchmode -projectPath '<isolated project>' -executeMethod PauseCarouselValidation.Run -logFile '<test log>'
```

验证入口打开真实 `GameFramework` 场景，等待开场动画完成，从 `F007` 进入局内，然后通过 UI Toolkit 指针事件点击转盘和当前选项。保存操作使用验证输出目录中的独立检查点，不覆盖玩家存档。

验证内容：

- 停稳显示两个物镜和一个可执行动作；交接时显示两个或三个物镜、两个不可执行的交接动作；隐藏动作不能触发。
- 六个物理镜头槽位每次旋转 60°；继续游戏、保存检查点、设置、返回标题、退出游戏五个业务动作循环对应。
- 连续点击立即推进下一动作并重新开始转动计时，设置返回及取消返回标题/退出确认后保持原档位。
- 暂停期间游戏时钟保持冻结，转盘使用非缩放时间；继续后恢复游戏时钟。
- 背景为 50% 黑色遮罩且空白区域拦截输入；文字沿用主菜单 Zpix 字体、字号，按钮无色块和边框。
- 在 1920×1080、1280×720、1024×768、2560×1440 下验证完整循环、指针命中及布局。

结果写入隔离工程的 `UIExports/PauseCarousel_20261023`，包括 JSON 报告及五档截图。本文描述工具覆盖范围，本次实际执行情况见[工程重整执行记录](../../Docs/工程重整执行记录.md)。

## 场景和配置

沿用 [GameFramework 场景](../../Assets/Via/Scenes/GameFramework.unity)中预挂载的 [MicroscopeMenuController](../../Assets/Via/Scripts/UI/Pause/MicroscopeMenuController.cs)，`document` 显式绑定原 `UIDocument`。`turnDuration` 配置为 **0.25 秒/档**，无需手动挂载组件或重新绑定。

六个物镜槽位及五个文字按钮均预先定义在 [GameUI.uxml](../../Assets/Via/UI/Documents/GameUI.uxml)；控制器负责显示、旋转和画布适配，实际保存、设置及流程操作继续由 [GameUIController](../../Assets/Via/Scripts/UI/Shell/GameUIController.cs)转发。
