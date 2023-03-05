namespace little_heart_bot_3.others;

public class Logger
{
    //TextWriter.Synchronized 创建的TextWriter是线程安全的
    private TextWriter _writer;
    private string _fileName;
    private readonly string _name;
    private readonly int _maxSize;
    private int _count;

    public Logger(string name, int maxSize = 256 * 1024)
    {
        _name = name;
        _fileName = _name + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _maxSize = maxSize;
        Directory.CreateDirectory("log");
        _writer = TextWriter.Synchronized(File.AppendText("log/" + _fileName));
    }

    public async Task Log(params object[] args)
    {
        string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]";

        foreach (var arg in args)
        {
            text += $" {arg}";
        }

        await _writer.WriteLineAsync(text);
        await _writer.FlushAsync();

        Interlocked.Increment(ref _count);
        if (_count == 10)
        {
            if (NeedToRoll())
            {
                Roll();
            }

            Interlocked.Exchange(ref _count, 0);
        }
    }

    private bool NeedToRoll()
    {
        return new FileInfo("log/" + _fileName).Length > _maxSize;
    }

    private void Roll()
    {
        _fileName = _name + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _writer = TextWriter.Synchronized(File.AppendText("log/" + _fileName));
    }
}