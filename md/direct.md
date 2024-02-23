# 直接部署

## 前提

确保操作系统装有 **.Net 8 SDK**

## 开始部署

### 1.填写账号信息

修改配置文件`little_heart_bot_3/AppData/appsettings.json`

```json
{
  "Sqlite": {
    "DataSource": "AppData/database/little_heart_bot_3.db"
  },
  "Bot": {
    "uid": 10086,
    "cookie": "账号的cookie",
    "dev_id": "这个怎么填看下面"
  },
  "Email": {
    "from": "10086@qq.com",
    "to": "10086@qq.com",
    "auth": "qq邮箱的授权码"
  }
}
```

**关于dev_id**  
随便找个人私聊，找send_msg包，payload里有一项msg[dev_id]，里边的内容就是dev_id

**关于Email**  
非必填项，正确填写后，能在发生预料之外错误或Cookie过期时发通知邮件

### 2.Build

在项目根目录下

```
dotnet publish
```

### 3.运行

进入目录`little_heart_bot_3/bin/Release/net8.0/操作系统名称/publish`

目录下会有一个`AppData`文件夹和一个可执行文件

在当前目录下运行可执行文件即可



## 数据

日志、数据库、配置文件都保存在`AppData`中













