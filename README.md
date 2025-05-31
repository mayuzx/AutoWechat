# 智能消息助手 (AutoWe)  wechatbot

一个基于WPF的智能消息自动回复助手，支持多窗口操作、图片匹配、OCR识别和AI智能回复。

## ✨ 功能特性

### 🎯 核心功能
- **多窗口支持** - 可操作任意应用窗口，不限于微信
- **智能图片匹配** - 支持多张模板图片的精确匹配
- **OCR文字识别** - 基于PaddleOCR的高精度文字识别
- **AI智能回复** - 集成Coze AI，提供智能对话回复
- **自动化操作** - 全自动消息识别和回复流程

### 🔧 高级设置
- **灵活的区域设置** - 支持OCR识别区域的精确调整
- **多重偏移配置** - 消息区域偏移、点击偏移等参数可调
- **匹配阈值调节** - 图片匹配精度可自定义
- **设置持久化** - 所有配置自动保存，重启恢复

### 🛡️ 安全特性
- **敏感信息保护** - Token等关键信息密码框显示
- **ESC快速停止** - 全局热键紧急停止功能
- **窗口自动排除** - 自动排除程序自身窗口

## 🚀 快速开始

### 环境要求
- Windows 10/11
- .NET 9.0 Runtime
- 摄像头权限（用于窗口截图）

### 安装步骤

1. **下载Release**
   ```bash
   # 下载最新发布版本
   https://dwlsockvqmcx.sealosbja.site/software/4
   ```

2. **配置OCR模型**
   - 确保 `inference` 目录包含PaddleOCR模型文件
   - 程序首次运行会自动解压模型

3. **配置Coze API**
   - 获取Coze API Token
   - 创建Coze应用获取AppId和WorkflowId

## 📖 使用指南

### 基础设置

1. **选择目标窗口**
   - 点击"刷新"获取当前可操作窗口
   - 从下拉框选择目标应用（如微信）

2. **配置图片模板**
   - **消息图片**: 用于识别新消息的模板
   - **发送图片**: 用于定位发送按钮的模板  
   - **原点图片**: 用于定位输入框的模板

3. **设置Coze API**
   ```
   Token: 你的Coze API Token
   AppId: 你的Coze应用ID
   WorkflowId: 你的工作流ID
   ```

### 高级配置

#### OCR区域设置
- **左侧偏移**: OCR识别区域左边界偏移
- **右侧偏移**: OCR识别区域右边界偏移  
- **上侧偏移**: OCR识别区域上边界偏移
- **下侧偏移**: OCR识别区域下边界偏移
- **消息分界线**: 区分发送方和接收方的X坐标

#### 窗口设置
- **消息区域偏移**: 截图时左侧裁剪的像素数
- **发送点击Y偏移**: 点击发送按钮时的Y轴偏移

#### 匹配设置
- **匹配阈值**: 图片匹配的相似度阈值（0-1）
- **点击Y偏移**: 点击匹配图片时的Y轴偏移

### 操作流程

1. **启动监控** - 点击"开始"按钮
2. **自动识别** - 程序监控目标窗口的新消息
3. **OCR提取** - 识别消息文字内容
4. **AI处理** - 发送到Coze获取智能回复
5. **自动回复** - 将AI回复发送到聊天窗口

## ⚙️ 技术架构

### 核心依赖
- **WPF** - 用户界面框架
- **OpenCvSharp** - 图像处理和模板匹配
- **PaddleOCRSharp** - 文字识别引擎
- **Newtonsoft.Json** - JSON数据处理
- **CozeApiClient** - Coze AI接口客户端

### 关键算法
- **模板匹配**: 基于OpenCV的多尺度模板匹配
- **ROI提取**: 智能文本区域提取
- **消息分组**: 基于位置的消息气泡分组算法

## 🔧 开发说明

### 项目结构
```
AutoWe/
├── MainWindow.xaml          # 主界面UI
├── MainWindow.xaml.cs       # 主要业务逻辑
├── inference/               # PaddleOCR模型文件
├── dll/                     # 依赖库文件
└── AutoWe.csproj           # 项目配置
```

## 📝 配置文件说明

程序会自动创建 `settings.json` 配置文件：

```json
{
  "SelectedImagePath": "消息图片路径",
  "SelectedImagePath1": "发送图片路径", 
  "SelectedImagePath2": "原点图片路径",
  "CozeToken": "你的Coze Token",
  "CozeAppId": "你的AppId",
  "CozeWorkflowId": "你的WorkflowId",
  "MessageBoundary": 160,
  "LeftOffset": 550,
  "RightOffset": 0,
  "TopOffset": 100,
  "BottomOffset": 100,
  "MatchThreshold": "0.85",
  "ClickYOffset": "-100",
  "ScreenshotLeftOffset": 0,
  "SendClickYOffset": -100
}
```

## 🚨 注意事项

- **合规使用**: 请确保使用场景符合相关应用的服务条款
- **隐私保护**: 程序仅在本地处理消息，不会存储聊天记录
- **稳定性**: 建议在稳定网络环境下使用
- **权限**: 程序需要屏幕截图和鼠标操作权限

## 🤝 贡献指南

欢迎提交Issue和Pull Request来改进项目！

1. Fork 本项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情

## 📞 支持

- 🐛 https://dwlsockvqmcx.sealosbja.site/

---

⭐ 如果这个项目对你有帮助，请给个Star支持一下！ 
