# 文件备份监控助手 (FileBackupMonitor)

一个智能、高效的文件备份监控工具，自动监测指定文件夹的变化并实时备份新增/修改的文件。支持按需显示实时日志窗口，兼顾前台交互与后台稳定运行。

![Platform](https://img.shields.io/badge/.NET%20Framework-4.8-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![Language](https://img.shields.io/badge/language-C%23-239120)

---

## 📋 功能特性

### 核心功能
- **实时文件夹监控**：使用 `FileSystemWatcher` 监控文件创建、修改、重命名事件
- **自动备份**：检测到变化时自动复制文件到指定备份目录，保留原始目录结构
- **智能重名处理**：备份文件名冲突时自动重命名（`_1`, `_2`...），不覆盖历史版本
- **按需日志窗口**：实时日志窗口只在打开时订阅事件，关闭时释放资源，零性能浪费
- **错误日志持久化**：所有异常自动记录到 `%TEMP%\文件备份监控助手log\`，保留 7 天

### 用户体验
- **MahApps.Metro 风格界面**：现代化 UI，支持深色/浅色主题切换
- **左设计右图表布局**：左侧参数配置，右侧实时监控图表
- **三列数据对比**：支持 100% 负荷、最低负荷、最高负荷三工况对比显示
- **托盘运行**：最小化到系统托盘，后台静默监控
- **实时筛选**：按备份/重命名/新文件/错误等类型过滤日志

---

## 🏗️ 技术架构

### 架构原则
- **功能分离**：文件名忽略规则与文件夹路径忽略规则独立配置，互不干扰
- **按需订阅**：事件监听器仅在 UI 打开时激活，关闭时立即释放（`IDisposable` 模式）
- **线程安全**：`LogService` 使用 `lock` 确保多线程写入 JSONL 日志文件安全
- **资源安全**：所有 `FileStream`/`StreamWriter` 使用 `using` 语句包装，杜绝泄漏

### 技术栈
| 组件 | 说明 |
|------|------|
| **.NET Framework 4.8** | Windows 桌面应用框架 |
| **WPF + MVVM** | 界面与逻辑分离 |
| **FileSystemWatcher** | 实时文件系统事件监控 |
| **System.Text.Json** | JSONL 格式日志序列化 |
| **Costura.Fody** | 依赖项嵌入（单文件发布） |
| **Logging 类库** | 独立的错误日志组件（`FileLogger`） |

### 关键设计

#### 1. 双层日志系统
- **实时日志 (`OnLog` 事件)**: 推送到 UI 显示，仅内存存储，窗口关闭后清空
- **错误日志 (`FileLogger`)**: 独立写入 `%TEMP%\文件备份监控助手log\YYYY-MM-DD.log`，持久化保存
- **备份记录 (`LogService`)**: JSONL 格式保存到 `%APPDATA%\文件备份监控助手\logs\logs.jsonl`

#### 2. 事件驱动模型
```csharp
// BackupService 触发事件
public event Action<string> OnLog;

// LogViewModel 订阅（窗口打开时）
_backupService.OnLog += OnLogReceived;

// 窗口关闭时释放
public void Dispose() => _backupService?.OnLog -= OnLogReceived;
```

#### 3. 日志文件结构
```jsonl
{"timeStamp":"14:30:22","message":"✅ 开始监控: C:\\Data","type":"Backup","color":"#A3EEA3"}
{"timeStamp":"14:31:05","message":"📝 检测到新文件: report.pdf","type":"NewFile","color":"#FFEAA7"}
{"timeStamp":"14:31:12","message":"❌ 备份失败: report.pdf - 文件被占用","type":"Error","color":"#FF8080"}
```

---

## 📁 项目结构

```
FileBackupMonitor/
├── Logging/                          # 独立日志库项目
│   ├── FileLogger.cs                 # 静态错误日志器（Temp 目录）
│   └── Logging.csproj
├── Views/                            # WPF 视图层
│   ├── MainWindow.xaml/.cs           # 主界面（托盘、监控控制）
│   ├── LogWindow.xaml/.cs            # 实时日志窗口（按需显示）
│   └── SettingsWindow.xaml/.cs       # 设置界面
├── ViewModels/                       # MVVM 视图模型
│   ├── MainViewModel.cs              # 主界面逻辑
│   ├── LogViewModel.cs               # 日志筛选、自动滚动
│   └── SettingsViewModel.cs          # 设置项绑定
├── Services/                         # 业务逻辑层
│   ├── BackupService.cs              # 核心监控服务（FileSystemWatcher）
│   ├── LogService.cs                 # 持久化日志（JSONL）
│   └── SettingsService.cs            # 配置读写
├── Models/                           # 数据模型
│   ├── BackupLogEntry.cs             # 备份记录
│   ├── LogEntry.cs                   # 实时日志条目
│   └── AppSettings.cs                # 应用配置
├── Controls/                         # 自定义控件
│   └── MessageboxYesNoCancel.xaml/.cs
├── Themes/                           # UI 主题
│   ├── DarkTheme.xaml
│   └── LightTheme.xaml
├── bin/                              # 编译输出
│   └── Debug/
└── 文件备份监控助手.csproj           # 主项目文件
```

---

## 🚀 快速开始

### 环境要求
- Windows 10 / 11
- .NET Framework 4.8（已包含在 Windows 10 1809+）
- 至少 50MB 可用磁盘空间

### 编译运行
```powershell
# 1. 克隆项目
git clone https://github.com/ppyuehui/FileBackupMonitor.git
cd 文件备份监控助手

# 2. 恢复 NuGet 包（需要 Visual Studio 或 msbuild）
nuget restore 文件备份监控助手.csproj

# 3. 编译
msbuild 文件备份监控助手.csproj /p:Configuration=Debug

# 4. 运行
.\bin\Debug\文件备份监控助手.exe
```


## 📦 依赖

本项目使用 Git 子模块管理 `Logging` 依赖库：

### 克隆包含子模块的仓库
```powershell
# 必须加上 --recursive
git clone --recursive https://github.com/ppyuehui/FileBackupMonitor.git
cd file-backup-monitor
```

### 已有仓库，拉取子模块
```bash
git submodule update --init --recursive
```

### Logging 库独立仓库
- 仓库地址：https://github.com/ppyuehui/Logging 
- 用途：提供 `FileLogger` 静态日志类，独立于主应用

### 首次使用
1. **设置监控目录**：点击「浏览」选择要监控的文件夹
2. **设置备份目录**：选择备份文件保存位置（建议不同磁盘）
3. **设置忽略规则**：
   - 文件名忽略（支持通配符 `*.tmp`, `~*`）
   - 文件夹路径忽略（如 `C:\Data\Temp\`）
4. 点击「开始监控」，最小化到托盘即可后台运行
5. 需要查看日志时，点击托盘图标 → 「打开日志窗口」

---

## ⚙️ 配置说明

### `AppSettings.json`（自动生成于 `%APPDATA%\文件备份监控助手\`）
```json
{
  "SourceFolder": "C:\\Data",
  "BackupFolder": "D:\\Backup",
  "FileNameIgnorePatterns": ["*.tmp", "~*", "*.log"],
  "FolderIgnorePatterns": ["C:\\Data\\Temp"],
  "MaxBackupLogs": 100,
  "Theme": "Dark"
}
```

### 忽略规则语法
| 规则类型 | 示例 | 说明 |
|---------|------|------|
| 文件名忽略 | `*.tmp` | 所有临时文件不备份 |
| 文件名忽略 | `~*` | 所有临时文件（Office 锁定文件） |
| 文件夹忽略 | `C:\Temp\` | 完整路径匹配 |
| 文件夹忽略 | `*\Temp\*` | 任意位置的 Temp 文件夹 |

---

## 🐛 已知问题与限制

### 当前限制
- **仅支持 Windows**：基于 `FileSystemWatcher` 和 WinForms 托盘图标
- **单实例运行**：通过 Mutex 保证只有一个监控实例
- **网络路径延迟**：UNC 路径（`\\server\share`）可能触发延迟事件
- **长路径支持**：需要 Windows 10 1607+ 启用长路径策略

### 性能考虑
- 监控文件数建议 < 10,000，避免内存无限增长
- 实时日志窗口限制显示最近 2000 条（可配置）
- 备份日志文件自动轮转（保留最近 100 条）

---

## 📝 开发日志

### v1.0.0（2026-04）
- ✅ 初始版本发布
- ✅ 基础文件监控与自动备份
- ✅ 实时日志窗口（按需显示 + 资源释放）
- ✅ 错误日志持久化（独立 FileLogger）
- ✅ JSONL 格式备份记录
- ✅ 文件夹忽略与文件名ignore功能分离

---

## 📄 许可证

本项目采用 **MIT 许可证**，详见 [LICENSE](LICENSE) 文件。

---

## 🙏 致谢

- [MahApps.Metro](https://mahapps.com/) - WPF 现代化 UI 框架
- [Costura.Fody](https://github.com/Fody/Costura) - 依赖嵌入解决方案
- [FileSystemWatcher](https://docs.microsoft.com/dotnet/api/system.io.filesystemwatcher) - .NET 文件监控 API

---

**注意**：本项目仅供个人/小团队内部使用，请勿用于非法监控或侵犯隐私的场景。使用前请确保遵守当地法律法规。
