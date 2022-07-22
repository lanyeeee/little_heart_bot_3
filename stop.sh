ps -ef|grep 'dotnet run'|awk '{print $2}'|xargs kill -9
ps -ef|grep '/home/bin/Release/net6.0/little_heart_bot_3'|awk '{print $2}'|xargs kill -9