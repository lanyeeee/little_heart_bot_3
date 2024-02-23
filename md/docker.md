# Docker 部署

## 前提

装好docker，且docker能正常使用

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

### 2.构建docker镜像

在项目根目录下

```
docker build . -t little_heart_bot_3
```

### 3.启动容器

如果你的系统是`Linux`在项目根目录下，使用以下命令来启动容器

```
docker run -d -v $(pwd)/little_heart_bot_3/AppData:/app/AppData little_heart_bot_3:latest
```

如果你的系统是`Windows`，在项目根目录下，使用以下命令来启动容器

```
docker run -d -v $pwd/little_heart_bot_3/AppData:/app/AppData little_heart_bot_3:latest
```



## 数据


如果你按照上面的命令启动容器，那么数据库、日志、配置文件都保存在主机的`little_heart_bot_3/AppData`中。





