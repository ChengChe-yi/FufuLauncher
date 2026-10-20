目录说明
--------
本目录存放"周期挑战"页面用到的徽章 / 塔罗 / 星级图标，通过 ms-appx:///Assets/CycleChallenge/Icons/<文件名>.png 访问
（映射代码见 FufuLauncher/Services/CycleChallenge/CycleChallengeIcons.cs）。

许可
----------
仅用于 FufuLauncher 在本地展示米游社游戏记录接口返回的数据，不对外分发、不用于商业用途。
游戏素材（UI 图标）版权归 miHoYo / HoYoverse 所有；本项目只在 MIT 代码许可范围内引用，
如版权方要求，应随时移除本目录。

维护提示
--------
- 新增图标时保持文件名与 API 返回的 icon 名一致（不带 .png 也可以，CycleChallengeIcons.GetLocal 会自动补扩展名）。
- 删除/改名会导致 CycleChallengeIcons 生成的 ms-appx 路径失效（控件会退化为占位图，不会崩溃）。
