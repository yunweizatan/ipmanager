# IPManage

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6)
![UI](https://img.shields.io/badge/UI-WPF-0C54C2)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)

一个基于 `.NET 8 + WPF` 的内网 IP 资产管理工具，支持记录管理、CSV 导入导出和本地备份。

## 开发者

- 抖音：`运维杂谈`

## 项目状态

- 当前版本可用于本地单机场景的 IP 台账管理
- 默认数据存储为本地 JSON 文件，适合中小规模资产维护

## 功能简介

- 资产记录管理：新增、编辑、删除、批量删除
- 多维筛选：按 VLAN、状态、关键字快速检索
- CSV 导入导出：支持导入预览、失败报告导出
- 数据分片存储：按 VLAN/网段拆分 JSON 文件
- 密码保护：存储时使用 Windows DPAPI（当前用户作用域）加密
- 本地备份：一键生成完整数据快照

## 技术栈

- .NET 8
- WPF
- C#

## 目录结构

```text
IPManage/
├─ Models/
├─ Repositories/
├─ Services/
├─ Validators/
├─ ViewModels/
├─ Views/
├─ Themes/
├─ Resources/
├─ MainWindow.xaml
└─ IPManage.csproj
```

## 本地运行

### 1) 环境要求

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 2) 启动项目

```bash
dotnet restore
dotnet build
dotnet run --project IPManage.csproj
```

### 3) 发布（可选）

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

## 数据与安全说明

- 运行时数据默认写入应用目录下的 `data/`（已在 `.gitignore` 中忽略）
- 密码字段在落盘时会自动加密（`enc:` 前缀）
- 不建议将任何真实业务数据、导出 CSV、备份文件提交到仓库

## 常见问题

- 启动后看不到数据：确认是否在当前运行目录下生成了 `data/` 与 `data/index.json`
- 导入 CSV 失败：优先检查列名和列顺序是否与模板一致（见导入提示）
- 密码显示异常：历史明文数据会在再次保存时自动转换为加密格式

## 提交到 GitHub 前建议

- 确认仅提交源代码和必要资源文件
- 确认 `bin/`、`obj/`、`publish/`、`.tmp/`、`data/` 未被提交
- 如需示例数据，请使用脱敏数据并单独放在 `docs/` 或 `samples/` 目录

## Roadmap

- [ ] 增加一键导出 Excel（`.xlsx`）
- [ ] 增加操作日志与审计视图
- [ ] 增加字段级权限（例如密码仅管理员可见）
- [ ] 增加多用户共享存储方案（如数据库后端）

## License

本项目采用 [MIT License](./LICENSE)。

## 更新记录

详见 [CHANGELOG.md](./CHANGELOG.md)。
