ps -ef|grep 'dotnet run'|awk '{print $2}'|xargs kill -9
ps -ef|grep '/home/little_heart_bot_3/little_heart_bot_3/bin/Release/net6.0/little_heart_bot_3'|awk '{print $2}'|xargs kill -9
ps -ef|grep 'node ../../bilibili-pcheartbeat/app.js'|awk '{print $2}'|xargs kill -9