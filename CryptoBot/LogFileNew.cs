using System;
using System.IO;
using System.Text;

namespace CryptoBot
{
    public class LogFileNew
    {
        private FileStream LFile;

        private string CurrentFile;

        private string fileName;

        public LogFileNew(string _fileName, string _exten = ".log")
        {
            fileName = _fileName;
            CurrentFile = string.Format("{0}_{1}", DateTime.Now.ToString("yyyy-MM-dd"), fileName);
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "/log/"))
                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "/log/");

            //LFile = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "/log/" + CurrentFile + ".log", FileMode.Append);
            LFile = new FileStream(string.Format("{0}/log/{1}{2}", AppDomain.CurrentDomain.BaseDirectory, CurrentFile, _exten), FileMode.Append);
        }

        public void WriteLine(string logLine)
        {
            try
            {
                if (!(CurrentFile.Contains(string.Format("{0}", DateTime.Now.ToString("yyyy-MM-dd")))))
                {
                    CurrentFile = string.Format("{0}_{1}", DateTime.Now.ToString("yyyy-MM-dd"), fileName);
                    LFile = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "/log/" + CurrentFile + ".log", FileMode.Append);
                }
                byte[] line = Encoding.UTF8.GetBytes(DateTime.Now.ToString("yyyy-MM-dd H:mm:ss.fff zzz") + "\t" + logLine + "\r\n");
                LFile.Write(line, 0, line.Length);
                LFile.Flush();
            }
            catch (Exception ex)
            {

            }
        }
    }
}
