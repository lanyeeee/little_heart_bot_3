mkdir log
nohup node ../bilibili-pcheartbeat/app.js >> /dev/null &
nohup dotnet run --configuration Release >> log/exception.log &