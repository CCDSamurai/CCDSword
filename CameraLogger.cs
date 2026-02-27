using System;
using System.IO;

namespace HikCameraCapture
{
    // 简单的文件日志器，线程安全
    public static class CameraLogger
    {
        private static readonly object _lock = new object();
        private static string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "camera.log");

        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // 忽略日志写入异常，避免影响主流程
            }
        }

        public static void SetLogPath(string path)
        {
            lock (_lock)
            {
                _logPath = path;
            }
        }
    }
}
