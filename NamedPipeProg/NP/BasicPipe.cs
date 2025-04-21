using System.IO.Pipes;
using System.Text;

namespace NamedPipeProg.NP
{
    public abstract class BasicPipe
    {
        public event EventHandler<PipeEventArgs> DataReceived;
        public event EventHandler<EventArgs> PipeClosed;
        public event EventHandler<EventArgs> PipeConnected;

        protected PipeStream pipeStream;
        protected Action<BasicPipe> asyncReaderStart;

        public bool isConnected;
        public BasicPipe() { }

        public void Close()
        {
            pipeStream.WaitForPipeDrain();
            pipeStream.Close();
            pipeStream.Dispose();
            pipeStream = null;
            isConnected = false;
        }

        public void Flush()
        {
            pipeStream.Flush();
        }

        protected void Connected()
        {
            PipeConnected?.Invoke(this, EventArgs.Empty);
            isConnected = true;
        }

        protected void StartByteReaderAsync(Action<byte[]> packetReceived)
        {
            int intSize = sizeof(int);
            byte[] bDataLength = new byte[intSize];

            pipeStream.ReadAsync(bDataLength, 0, intSize).ContinueWith(t =>
            {
                int len = t.Result;

                if (len == 0)
                {
                    PipeClosed?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    int dataLength = BitConverter.ToInt32(bDataLength, 0);
                    byte[] data = new byte[dataLength];

                    pipeStream.ReadAsync(data, 0, dataLength).ContinueWith(t2 =>
                    {
                        len = t2.Result;

                        if (len == 0)
                        {
                            PipeClosed?.Invoke(this, EventArgs.Empty);
                        }
                        else
                        {
                            packetReceived(data);
                            StartByteReaderAsync(packetReceived);
                        }
                    });
                }
            });
        }
        public void StartByteReaderAsync()
        {
            StartByteReaderAsync((b) =>
              DataReceived?.Invoke(this, new PipeEventArgs(b, b.Length)));
        }
        public void StartStringReaderAsync()
        {
            StartByteReaderAsync((b) =>
            {
                string str = Encoding.UTF8.GetString(b).TrimEnd('\0');
                DataReceived?.Invoke(this, new PipeEventArgs(str));
            });
        }
        public Task WriteString(string str)
        {
            return WriteBytes(Encoding.UTF8.GetBytes(str));
        }

        public Task WriteBytes(byte[] bytes)
        {
            var blength = BitConverter.GetBytes(bytes.Length);
            var bfull = blength.Concat(bytes).ToArray();

            return pipeStream.WriteAsync(bfull, 0, bfull.Length);
        }
        public async Task WriteStringAsync(string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            await WriteBytesAsync(bytes);
        }

        public async Task WriteBytesAsync(byte[] bytes)
        {
            byte[] blength = BitConverter.GetBytes(bytes.Length);
            byte[] bfull = blength.Concat(bytes).ToArray();
            await pipeStream.WriteAsync(bfull, 0, bfull.Length);
        }
    }
}
