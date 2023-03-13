# 直接部署

这里只是提供一个思路，理论上你能按照以下思路让小心心bot跑在自己的操作系统上

## 前提

确保操作系统下装有  
| Requirement |
| ----------- |
| .Net 6 |
| mysql |
| nodejs |
| npm |

## 开始部署

### 1.创建数据库并进入

```
在mysql下

create database little_heart_bot_3;

use little_heart_bot_3;
```

### 2.建表

**bot_table**

```
create table bot_table
(
    uid            varchar(20)                            not null comment 'uid'
        primary key,
    cookie         varchar(2000)                          null comment 'cookie',
    csrf           varchar(100) default ''                not null,
    dev_id         varchar(255)                           null comment 'dev_id',
    app_status     int          default 0                 not null comment '0 normal, -1 cooling',
    receive_status int          default 0                 not null comment '0 normal, -1 cooling',
    send_status    int          default 0                 not null comment '0 normal, -1 cooling, -2 forbidden',
    create_time    datetime     default CURRENT_TIMESTAMP null,
    update_time    datetime     default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP
)
    collate = utf8mb4_unicode_ci;
```

**target_table**

```
create table target_table
(
    num         int auto_increment comment 'primary key'
        primary key,
    uid         varchar(20)                        not null comment 'uid',
    target_uid  varchar(20)                        null comment 'target_uid',
    target_name varchar(30)                        null comment 'target name',
    room_id     varchar(20)                        null comment 'room_id',
    like_num    int      default 0                 null comment 'how many like had post',
    share_num   int      default 0                 null comment 'how many time had shared',
    msg_content varchar(30)                        null comment 'content',
    msg_status  int      default 0                 null comment '0 unfinished,1 completed,-1 msg invalid,-2 UL error,-3 cookie invalid,-4 without room,-5 baned',
    completed   int      default 0                 not null comment 'completed or not',
    create_time datetime default CURRENT_TIMESTAMP null,
    update_time datetime default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP
)
    collate = utf8mb4_unicode_ci;
```

**user_table**

```
create table user_table
(
    uid              varchar(20)                             not null comment 'uid'
        primary key,
    cookie           varchar(2000) default ''                not null,
    csrf             varchar(100)  default ''                not null,
    completed        int           default 0                 null comment 'task completed or not',
    cookie_status    int           default 0                 null comment '0 unknow,1 normal,-1 error',
    config_num       int           default 0                 null comment 'how many times the user check the config today',
    target_num       int           default 0                 not null comment 'how many targets the user set',
    msg_timestamp    varchar(255)  default '0'               null comment 'latest message timestamp',
    config_timestamp varchar(255)  default '0'               not null,
    create_time      datetime      default CURRENT_TIMESTAMP null,
    update_time      datetime      default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP
)
    collate = utf8mb4_unicode_ci;
```

### 3.填写账号信息

```
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

### 4.根据自己的情况修改项目根目录下的MysqlOption.json

默认的MysqlOption.json长这样

```
{
  "host": "localhost",
  "database": "little_heart_bot_3",
  "user": "root",
  "password": "root"
}
```

### 5.运行

#### 1)在项目根目录下逐条运行下面的命令，以部署解密服务

```
git clone https://github.com/lkeme/bilibili-pcheartbeat.git ../bilibili-pcheartbeat
cd ../bilibili-pcheartbeat
npm install
cd ../little_heart_bot_3
```

#### 2)如果你的操作系统能用start.sh

```
sh start.sh
```

#### 3)如果不能用start.sh，自行在项目根目录下进行以下操作：

1. 新建一个名为log的文件夹
2. node ../bilibili-pcheartbeat/app.js
3. dotnet run --configuration Release

2是启动解密服务  
3是启动小心心bot