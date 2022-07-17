using little_heart_bot_3.repository;
using MySqlConnector;
using Newtonsoft.Json.Linq;

namespace little_heart_bot_3.others;

public static class Globals
{
    public static int AppStatus { get; set; }
    public static int ReceiveStatus { get; set; }
    public static int SendStatus { get; set; }

    public static readonly HttpClient HttpClient;
    public static readonly BotRepository BotRepository;
    public static readonly MessageRepository MessageRepository;
    public static readonly TargetRepository TargetRepository;
    public static readonly UserRepository UserRepository;
    public static readonly string ConnectionString;

    static Globals()
    {
        string jsonString = File.ReadAllText("MysqlOption.json");
        JObject json = JObject.Parse(jsonString);
        var builder = new MySqlConnectionStringBuilder
        {
            Server = (string?)json["host"],
            Database = (string?)json["database"],
            UserID = (string?)json["user"],
            Password = (string?)json["password"]
        };
        ConnectionString = builder.ConnectionString;

        BotRepository = new BotRepository();
        MessageRepository = new MessageRepository();
        TargetRepository = new TargetRepository();
        UserRepository = new UserRepository();
        HttpClient = new HttpClient();
    }
}