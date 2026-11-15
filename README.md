# Unity BVH 骨架播放器

Unity 2022.3 项目。可读入本地 `.bvh` 文件，显示骨架动画，播放、暂停、重播、拖动进度和调节 0.1 到 3 倍速度。右键拖动旋转视角，滚轮缩放。

## 运行

1. 在 Unity Hub 中选择“从磁盘添加项目”，选择本目录 `BVHUnity`，用 Unity 2022.3 打开。
2. 打开任意空场景并点击 Play。程序会自动创建相机、骨架和控制面板，并播放 `Assets/StreamingAssets/sample_walk.bvh`。
3. 在左上角输入 BVH 文件的绝对路径并点击“读取”；在 Unity 编辑器中也可点击“浏览”选择文件。“样例”按钮可随时重新载入内置动作。

BVH 文件须包含 `HIERARCHY` 和 `MOTION`，支持标准的 `ROOT`、`JOINT`、`End Site`、`OFFSET`、`CHANNELS`、`Frames`、`Frame Time`。平移和旋转通道按文件声明顺序处理。演示程序用单位长度直接显示 BVH 坐标，并对 Z 轴做坐标系转换。文件以 UTF-8 或系统默认编码读取；Windows 下非 UTF-8 的 BVH 若读取失败，请先转换为 UTF-8。

源码：`Assets/Scripts/BvhClip.cs` 负责解析和姿态计算；`Assets/Scripts/BvhDemo.cs` 负责播放、骨架显示与操作界面。

## 上传到 GitHub

将 **本目录 `BVHUnity`** 作为 GitHub Desktop 的本地仓库根目录。需要上传 `Assets`、`ProjectSettings`、本 README、`.gitignore`，以及 Unity 打开项目后生成的 `.meta` 文件。如果 Unity 生成了 `Packages/manifest.json` 和 `Packages/packages-lock.json`，也应上传。`.gitignore` 会排除 `Library`、`Temp`、`Obj`、`Logs`、`UserSettings`、构建输出和 IDE 临时文件。

在 GitHub Desktop 中选择 **File > Add local repository...**，指定此目录；如果提示这里不是 Git 仓库，点击 **create a repository here**。创建后检查 Changes 列表，确认只包含上述源文件和配置文件，再填写提交信息、点击 **Commit to main**，最后点击 **Publish repository**。发布时可选择公开或私有仓库。
