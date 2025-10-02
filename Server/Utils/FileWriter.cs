using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Utils
{
    public class FileWriter : IDisposable
    {
        private StreamWriter _sw;
        private bool _disposed;

        public string FilePath { get; }

        public FileWriter(string path, bool append = true)
        {
            FilePath = path;
            _sw = new StreamWriter(path, append);
            _sw.AutoFlush = true;
        }

        public void WriteLine(string line)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileWriter));
            _sw.WriteLine(line);
        }

        public void Flush()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileWriter));
            _sw.Flush();
        }

        public void Dispose()
        {
            if (_disposed) return;
            try { _sw?.Close(); _sw?.Dispose(); }
            finally { _sw = null; _disposed = true; }
        }
    }

}
