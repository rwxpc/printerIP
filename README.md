# 网络打印机 IP 修改工具（PrinterIpTool）

绿色单文件 Windows 工具：一键检查/启动 Print Spooler、列出本地网络打印机、修改并保存打印机端口 IP、设为默认打印机、安装未列出的打印机驱动包。

> © by 任我行电脑工作室　当前版本 **v1.4.5.0**

---

## 运行方式

1. 双击 `PrinterIpTool/dist/PrinterIpTool.exe`。
2. 程序清单已声明 `requireAdministrator`，打开时自动请求管理员权限（UAC 弹窗点「是」即可）。
3. 启动后先弹出提示窗：**「请根据本办公室的打印机型号及 ip 进行修改。」** 点击【确认】关闭后进入主界面。
4. 主窗口尺寸固定，不可拉伸 / 最大化（仍可最小化）。
5. 无需安装、无需注册表预置，拷到任意目录 / U 盘均可运行。

## 兼容范围

| 项目 | 说明 |
| --- | --- |
| 系统 | Windows 7 SP1（32/64 位）/ 8 / 8.1 / 10 / 11 |
| 运行库 | .NET Framework 4.8（Win10/11 系统自带；Win7 SP1 需先安装） |
| 架构 | AnyCPU，管理员权限运行 |

## 功能清单

1. **打开即自动检查 Print Spooler**：显示运行状态与启动类型
   - 启动类型按颜色区分：自动（绿色）、手动（蓝色）、禁用（红色）
   - 非「自动 / 禁用」时会自动设为「自动」并启动服务，日志有记录
2. **列出本地打印机**：默认仅显示网络 TCP/IP 打印机，可勾选「显示全部」查看 USB / 虚拟打印机
3. **修改 IP**：选中打印机 → 输入新 IP → 保存（实时生效，无需重启服务）
   - 打开程序会自动选中第一台，并把其当前 IP 填入输入框
   - 改 IP 时**端口名保持原名不变**，只改端口实际指向的地址；只有在原端口实在改不动时，才会弹窗询问是否新建端口（端口名会变），选「否」即放弃修改
   - 保存时会自动检查 Print Spooler：未运行则设为「自动」并立即启动
   - 保存后日志会复核真实地址，显示「✓ 复核通过」即确认生效
4. **设为默认打印机**：选中后点【设为默认打印机】
5. **安装未列出打印机**：点【安装未列出打印机…】选择 `.exe` 安装程序（也支持 `.zip` v4 驱动包、`.inf`）

## 目录结构

```
printerIP/
├─ icon.ico / icon.png          原始图标素材
├─ printer-ip-tool.html         工程交付文档（技术方案 + 完整源码 + 编译/部署 + FAQ）
├─ README.md                    本文件
└─ PrinterIpTool/
   ├─ Program.cs                主程序源码（WinForms + WMI + winspool P/Invoke）
   ├─ PrinterIpTool.csproj      项目文件（.NET Framework 4.8，AnyCPU）
   ├─ Properties/AssemblyInfo.cs
   ├─ app.manifest              requireAdministrator + 兼容声明
   ├─ app.ico                   应用图标（已嵌入 exe）
   ├─ build.bat                 一键编译打包脚本（探测 MSBuild → Release 编译 → 输出到 dist）
   ├─ 说明.txt                  使用说明（与 README 同步）
   └─ dist/                     绿色包产物：PrinterIpTool.exe + app.ico + 说明.txt
```

## 本地编译

需要安装 Visual Studio（2019 / 2022 / 2026 任一版本，含「.NET 桌面开发」工作负载）：

```bat
cd PrinterIpTool
build.bat
```

脚本会自动探测 MSBuild 引擎、以 Release / AnyCPU 编译，并把产物复制到 `PrinterIpTool\dist\`。

## 注意事项

- 修改打印端口属于系统级写操作，批量变更前建议先用 `regedit` 导出 `HKLM\SYSTEM\CurrentControlSet\Control\Print` 留底。
- 安装驱动包前请确认来源可信，避免植入恶意驱动。
- 应用图标 `app.ico` 已内嵌进 exe：即使只拷贝单个 `PrinterIpTool.exe`（旁边没有 `app.ico`），主界面标题栏（左上角）与任务栏也正常显示图标；加载顺序为「exe 内嵌资源 → 同目录 app.ico → exe 自身图标」。随包附带的 `app.ico` 副本可删除。

## 版权

by **任我行电脑工作室**
