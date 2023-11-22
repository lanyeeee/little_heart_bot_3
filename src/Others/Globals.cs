using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace little_heart_bot_3.Others;

public static class Globals
{
    public static int AppStatus { get; set; }
    public static int ReceiveStatus { get; set; }
    public static int SendStatus { get; set; }

    public static readonly HttpClient HttpClient;
    public static readonly string ConnectionString;
    public static readonly ServiceProvider ServiceProvider;
    public static readonly JsonSerializerOptions JsonSerializerOptions;

    static Globals()
    {
        HttpClient = new HttpClient();

        ConnectionString = Program.GetMysqlConnectionString();

        ServiceProvider = Program.ConfigService();

        JsonSerializerOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    }
}