# Docker 部署

## 前提

- 操作系统：linux(目前只支持amd和arm)
- 装好docker，且docker能正常使用

## 开始部署

### 1.拉取镜像

如果你是amd架构

```
docker pull lanyeeee/little_heart_bot_3:amd
```

如果你是arm架构

```
docker pull lanyeeee/little_heart_bot_3:arm
```

### 2.启动容器

如果你是amd架构

```
docker run -it lanyeeee/little_heart_bot_3:amd
```

如果你是arm架构

```
docker run -it lanyeeee/little_heart_bot_3:arm
```

**提醒一下，容器内的ssh和mysql端口都是默认的，需要使用请自行映射**

### 3.填写账号信息

**mysql的密码是 root**

```
mysql -proot

use little_heart_bot_3;

insert into bot_table(uid) values(自己填);

update bot_table set dev_id = 自己填;

update bot_table set cookie = 自己填;

update bot_table set csrf = 自己填;

quit;
```  

**关于dev_id**  
随便找个人私聊，找send_msg包，payload里有一项msg[dev_id]，里边的内容就是dev_id

**关于csrf**  
cookie里找bili_jct，它的值就是csrf，长度为32

**务必确保所填信息是正确的**

### 4.编译并运行小心心bot

```
cd /home/little_heart_bot_3

sh start.sh
```

### 5.编译完成后查看是否正确运行

``` 
ps
```

609 pts/1 00:00:00 bash  
2396 pts/1 00:00:21 dotnet  
2541 pts/1 00:00:15 dotnet  
2671 pts/1 00:00:06 little_heart_bo  
2805 pts/1 00:00:00 ps

输出类似这样就行，包含 dotnet 和 little_heart... 即可

## 日志

保存在/home/little_heart_bot_3/log里