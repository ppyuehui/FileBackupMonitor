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
- **多文件夹对**：支持同时监控多组文件夹，每对独立运作
- **批量导入**：支持批量导入文件夹对（`|`、`→`、`->`、Tab 分隔）

### 文件过滤
- **双模式过滤**：排除模式（排除匹配项）/ 包含模式（只备份匹配项）
- **分类快捷勾选**：文档、图片、源码、压缩包、音视频、工程软件、辉哥软件、数据库、临时文件
- **分类扩展名持久化**：分类定义保存到 `categories.json`，右键可编辑，修改后自动保存
- **恢复默认**：排除模式下一键恢复默认排除列表；包含模式下一键清空
- **恢复所有分类**：一键恢复所有分类扩展名到默认值，弹窗显示具体变更
- **扩展名扫描**：扫描监控目录，列出所有文件格式及数量，按需勾选

### 用户体验
- **深色/浅色主题**：支持主题切换，UI 全面适配两种模式
- **托盘运行**：最小化到系统托盘，后台静默监控
- **开机自启**：注册表路径与当前 exe 绑定，换位置后自动识别
- **实时日志窗口**：按需打开，支持按类型筛选（备份/重命名/新文件/错误）
- **中文支持**：配置文件和日志文件直接显示中文

---

## 🏗️ 技术架构

### 技术栈
| 组件 | 说明 |
|------|------|
| **.NET Framework 4.8** | Windows 桌面应用框架 |
| **WPF + MVVM** | 界面与逻辑分离 |
| **FileSystemWatcher** | 实时文件系统事件监控 |
| **System.Text.Json** | JSON 序列化（支持中文直接显示） |
| **Costura.Fody** | 依赖项嵌入（单文件发布） |

### 关键设计

#### 1. 三层配置持久化
| 文件 | 路径 | 内容 |
|------|------|------|
| `settings.json` | `%AppData%\文件备份监控助手\` | 主配置（文件夹对、过滤规则、主题等） |
| `categories.json` | `%AppData%\文件备份监控助手\` | 分类扩展名定义（可编辑、可恢复默认） |
| `logs.jsonl` | `%AppData%\文件备份监控助手\logs\` | 备份记录（JSONL 格式） |

#### 2. 开机自启逻辑
- 启动时读取注册表 `HKCU\...\Run\文件备份监控助手`
- 比较注册表路径与当前 exe 路径
- 匹配 → 勾选；不匹配 → 取消勾选
- 保存时写入/删除注册表项

#### 3. 双层日志系统
- **实时日志 (`OnLog` 事件)**：推送到 UI 显示，窗口关闭后清空
- **备份记录 (`LogService`)**：JSONL 格式持久化到 AppData

---

## 📁 项目结构

```
FileBackupMonitor/
├── Views/                            # WPF 视图层
│   ├── MainWindow.xaml/.cs           # 主界面（托盘、监控控制）
│   ├── LogWindow.xaml/.cs            # 实时日志窗口
│   ├── SettingsWindow.xaml/.cs       # 设置界面
│   └── InputDialog.xaml/.cs          # 输入对话框（编辑分类扩展名）
├── ViewModels/                       # MVVM 视图模型
│   ├── MainViewModel.cs              # 主界面逻辑
│   ├── LogViewModel.cs               # 日志筛选、自动滚动
│   └── SettingsViewModel.cs          # 设置项绑定、分类管理
├── Services/                         # 业务逻辑层
│   ├── BackupService.cs              # 核心监控服务
│   ├── LogService.cs                 # 持久化日志（JSONL）
│   └── SettingsService.cs            # 配置读写（settings.json + categories.json）
├── Models/                           # 数据模型
│   ├── AppSettings.cs                # 应用配置
│   └── BackupLogEntry.cs             # 备份记录
├── Converters/                       # XAML 转换器
│   └── Converters.cs                 # BoolToVisibility、CategoryToBrush 等
├── Themes/                           # UI 主题
│   ├── DarkTheme.xaml                # 深色主题
│   ├── LightTheme.xaml               # 浅色主题
│   └── ThemeStyles.xaml              # 公共样式（按钮、输入框等）
├── Controls/                         # 自定义控件
│   └── Messagebox*.xaml/.cs          # 消息框控件
└── 文件备份监控助手.csproj           # 主项目文件
```

---

## 🚀 快速开始

### 环境要求
- Windows 10 / 11
- .NET Framework 4.8

### 编译运行
```powershell
# 1. 克隆项目（含子模块）
git clone --recursive https://github.com/ppyuehui/FileBackupMonitor.git
cd FileBackupMonitor

# 2. 编译（需要 Visual Studio MSBuild）
msbuild 文件备份监控助手.csproj /p:Configuration=Debug

# 3. 运行
.\bin\Debug\文件备份监控助手.exe
```

---

## ⚙️ 配置说明

### 过滤模式
| 模式 | 说明 |
|------|------|
| **排除模式** | 匹配的文件不备份（默认），可点击「恢复默认」还原 |
| **包含模式** | 只有匹配的文件才备份，可点击「清空」清除所有规则 |

### 分类扩展名
默认分类定义保存在 `%AppData%\文件备份监控助手\categories.json`，支持：
- 右键编辑分类扩展名（自动保存）
- 点击「恢复所有分类拓展名」恢复默认值

### 忽略文件夹
支持通配符，用逗号分隔：
- `.vs`, `.git`, `node_modules`, `__pycache__`, `obj`, `.idea`, `.svn`, `bin`

---

## 📝 更新日志

### v1.1.0（2026-06）
- ✅ 分类扩展名持久化到 `categories.json`
- ✅ 新增「恢复所有分类拓展名」按钮，显示变更详情
- ✅ 排除模式「恢复默认」/ 包含模式「清空」按钮
- ✅ 分类按钮扫描前隐藏，扫描后才显示
- ✅ 深色/浅色模式 UI 全面适配
- ✅ JSON 文件中文直接显示（`UnsafeRelaxedJsonEscaping`）
- ✅ 开机自启注册表路径与 exe 绑定
- ✅ 清理重复排除规则、修复编译警告

### v1.0.0（2026-04）
- ✅ 初始版本发布
- ✅ 基础文件监控与自动备份
- ✅ 实时日志窗口（按需显示 + 资源释放）
- ✅ 双模式文件过滤（排除/包含）
- ✅ 多文件夹对支持
- ✅ 批量导入文件夹对

---

## 📄 许可证

本项目采用 **MIT 许可证**，详见 [LICENSE](LICENSE) 文件。
