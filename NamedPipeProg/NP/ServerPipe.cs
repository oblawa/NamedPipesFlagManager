using System.IO.Pipes;

namespace NamedPipeProg.NP
{
    public class ServerPipe : BasicPipe
    {
        protected NamedPipeServerStream serverPipeStream;
        protected string PipeName { get; set; }

        public ServerPipe(string pipeName, Action<BasicPipe> asyncReaderStart)
        {
            this.asyncReaderStart = asyncReaderStart;
            PipeName = pipeName;

            serverPipeStream = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);

            pipeStream = serverPipeStream;
            serverPipeStream.BeginWaitForConnection(new AsyncCallback(Connected), null);

        }

        protected void Connected(IAsyncResult ar)
        {
            try
            {
                serverPipeStream.EndWaitForConnection(ar);
                Connected();
                asyncReaderStart(this);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
                Close();
            }
        }
    }
}
