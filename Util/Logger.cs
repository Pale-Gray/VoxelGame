using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace VoxelGame.Util;

public static class Logger
{
    public static bool DoDisplayMessages = true;
    private static List<string> _messages = new();
    
    public static void Info(string message)
    {
        string msg = $"[{Thread.CurrentThread.Name} : INFO {Config.StartTime.Elapsed}] {message}";
        
        if (DoDisplayMessages) Console.WriteLine(msg);
        _messages.Add(msg);
    }

    public static void Warning(string message)
    {
        string msg = $"[{Thread.CurrentThread.Name} : WARN {Config.StartTime.Elapsed}] {message}";
        
        if (DoDisplayMessages) Console.WriteLine(msg);
        _messages.Add(msg);
    }

    public static void Error(Exception exception)
    {
        _messages.Add(exception.Message);
        if (exception.StackTrace != null) _messages.Add(exception.StackTrace);
    }

    public static void WriteToFile(string path = "log.txt")
    {
        using (StreamWriter stream = new StreamWriter(File.Open(path, FileMode.Create)))
        {
            foreach (string msg in _messages) stream.WriteLine(msg);
        }
    }
}