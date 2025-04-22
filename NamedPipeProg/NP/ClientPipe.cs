using System.Collections.Concurrent;
using System.Data;
using System.IO.Pipes;

namespace NamedPipeProg.NP
{
    public class ClientPipe : BasicPipe
    {
        protected NamedPipeClientStream clientPipeStream;

        private readonly ConcurrentDictionary<Tuple<string,string>, TaskCompletionSource<string>> pendingRequests = new();
        private readonly ConcurrentDictionary<string, Action<string, byte>> flagHandlers = new();
        public ClientPipe(string serverName, string pipeName, Action<BasicPipe> asyncReaderStart)
        {
            this.asyncReaderStart = asyncReaderStart;
            clientPipeStream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);            
            pipeStream = clientPipeStream;
        }
        public async Task<bool> SetFlag(string name, byte value)
        {
            if (!isConnected)
            {
                Console.WriteLine($"The {name} flag could not be set. There is no connection to the named channel.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("SET", $"{name}:{value}");
            pendingRequests[key] = tcs;
            await WriteStringAsync($"SET:{name}:{value}");
            try
            {
                var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(":");

                if (res == $"{name}:{value}")
                    return true;
                else 
                {
                    Console.WriteLine($"Error when setting the {name} flag: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException) 
            {
                Console.WriteLine("Timeout for waiting for the response of a SET request to the Pipe server.");
                return false;
            }
            finally
            {
                pendingRequests.TryRemove(key, out _);
            }
        }
        public async Task<byte?> GetFlagValue(string name)
        {
            if (!isConnected)
            {
                Console.WriteLine($"Couldn't get the {name} flag. There is no connection to the named channel.");
                return null;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("GET", name);
            pendingRequests[key] = tcs;
            await WriteStringAsync($"GET:{name}");
            try
            {
                string res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(":");
                if (Byte.TryParse(resParts[1], out byte flagValue))
                    return flagValue;
                else
                {
                    Console.WriteLine($"Error when trying to get the value of the {{name}} flag: {{rus Parts[1]}}");
                    return null;
                }
            }
            catch (TimeoutException) 
            {
                Console.WriteLine("Timeout for waiting for the response of a GET request to the Pipe server.");
                return null;
            }
            finally
            {
                pendingRequests.TryRemove(key, out _);
            }
        }
        public async Task<bool> ChangeFlag(string name, byte value)
        {
            if (!isConnected)
            {
                Console.WriteLine($"Couldn't change the {name} flag. There is no connection to the named channel.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("CHANGE", $"{name}:{value}");
            pendingRequests[key] = tcs;
            await WriteStringAsync($"CHANGE:{name}:{value}");
            try
            {
                var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(':');
                if (res == $"{name}:{value}")
                    return true;
                else
                {
                    Console.WriteLine($"Error updating the value of the {name} flag: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout for waiting for the response of a CHANGE request to the Pipe server.");
                return false;
            }
            finally
            {
                pendingRequests.TryRemove(key, out _);
            }
        }
        public async Task<bool> RemoveFlag(string name)
        {
            if (!isConnected)
            {
                Console.WriteLine($"Couldn't change the {name} flag. There is no connection to the named channel.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("REMOVE", $"{name}");
            pendingRequests[key] = tcs;
            await WriteStringAsync($"REMOVE:{name}");
            try
            {
                var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(':');
                if (res == $"{name}")
                    return true;
                else
                {
                    Console.WriteLine($"Error deleting the {name} flag: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout for waiting for the response of a REMOVE request to the Pipe server.");
                return false;
            }
            finally
            {
                pendingRequests.TryRemove(key, out _);
            }
        }
        public async Task<bool> SubscribeFlag(string name, Action<string, byte> flagChangedHandler)
        {
            if (!isConnected)
            {
                Console.WriteLine($"Couldn't subscribe to the {name} flag. There is no connection to the named channel.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("SUB", $"{name}");
            pendingRequests[key] = tcs;

            await WriteStringAsync($"SUB:{name}");

            try
            {
                var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(':');
                if (res == $"{name}")
                {
                    if (flagHandlers.TryAdd(name, flagChangedHandler))
                        return true;
                    else
                    {
                        Console.WriteLine($"The flag event handler could not be added to the list of handlers.");
                        return false;
                    }
                }                    
                else
                {
                    Console.WriteLine($"Error subscribing to the flag {name}: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout for waiting for the response of a SUB request to the Pipe server.");
                return false;
            }
            finally
            {
                pendingRequests.TryRemove(key, out _);
            }
        }
        public async Task<bool> UnSubscribeFlag(string name)
        {
            if (!isConnected)
            {
                Console.WriteLine($"Couldn't unsubscribe from the {name} flag. There is no connection to the named channel.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("UNSUB", $"{name}");
            pendingRequests[key] = tcs;
            await WriteStringAsync($"UNSUB:{name}");
            try
            {
                var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(':');
                if (res == $"{name}")
                {
                    if (flagHandlers.TryRemove(name, out Action<string, byte> h))   
                        return true;
                    else
                    {
                        Console.WriteLine($"Failed to remove the flag event handler from the list of handlers.");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"Error when unsubscribing to the {name} flag: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout for waiting for the response of an UNSUB request to the Pipe server.");
                return false;
            }
            finally
            {
                pendingRequests.TryRemove(key, out _);
            }
        }

        public void Connect()
        {
            clientPipeStream.Connect();
            Connected();
            asyncReaderStart(this);
        }
        public void OnDataRecieved(string message)
        {
            Task.Run(() => 
            {
                try
                {
                    var parts = message.Split(':');
                    if (parts.Length < 3)
                    {
                       throw new Exception("Incorrect response format from the Pipe server");
                    }
                    string messageType = parts[0];
                    string commandType = parts[1];
                    string flag = parts[2];

                    switch (messageType)
                    {
                        case "SUCCESS":
                            if (parts.Length == 4
                                && (commandType == "GET"
                                || commandType == "SET"
                                || commandType == "CHANGE"))
                            {
                                var value = parts[3];
                                if (pendingRequests.TryGetValue(Tuple.Create(commandType, flag), out var tcs))
                                {
                                    if (!tcs.TrySetResult($"{flag}:{value}"))
                                    {
                                        Console.WriteLine($"Error when trying to set the value (\"{commandType}\"{flag}) to the pendingRequests dictionary.");
                                        return;
                                    }
                                }
                                else if (pendingRequests.TryGetValue(Tuple.Create(commandType, $"{flag}:{value}"), out var tcs2))
                                {
                                    if (!tcs2.TrySetResult($"{flag}:{value}"))
                                    {
                                        Console.WriteLine($"Error when trying to set the value (\"{commandType}\"{flag}) to the pendingRequests dictionary.");
                                        return;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"The key (\"{commandType}\", {flag}) could not be found in the pendingRequests dictionary.");
                                    return;
                                }
                            }
                            else if (parts.Length == 3
                                && (commandType == "REMOVE"
                                || commandType == "SUB"
                                || commandType == "UNSUB"))
                            {
                                if (pendingRequests.TryGetValue(Tuple.Create(commandType, $"{flag}"), out var tcs))
                                {
                                    if (!tcs.TrySetResult($"{flag}"))
                                    {
                                        Console.WriteLine($"Error when trying to set a value for the key (\"{commandType}\":{flag}) to the pending Requests dictionary.");
                                        return;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Couldn't find the key (\"{commandType}\":{flag}) in the pendingRequests dictionary.");
                                    return;
                                }
                            }
                            else
                            {
                                Console.WriteLine("The response from the server to the Pipe request does not match the template.");
                            }
                            break;
                        case "ERROR":
                            if (parts.Length == 4
                                && (commandType == "GET"
                                || commandType == "SET"
                                || commandType == "CHANGE"
                                || commandType == "REMOVE"
                                || commandType == "SUB"
                                || commandType == "UNSUB"))
                            {
                                var error = parts[3];
                                if (pendingRequests.TryGetValue(Tuple.Create(commandType, flag), out var tcs))
                                {
                                    if (!tcs.TrySetResult($"{flag}:{error}"))
                                    {
                                        Console.WriteLine($"Error when trying to set the value (\"{commandType}\"{flag}) to the pendingRequests dictionary.");
                                        return;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"The key (\"{commandType}\", {flag}) could not be found in the pendingRequests dictionary.");
                                    return;
                                }
                            }
                            break;
                        case "NOTIFY":
                            if (parts.Length == 3)
                            {
                                if (Byte.TryParse(parts[2], out byte newValue))
                                {
                                    if (flagHandlers.TryGetValue(parts[1], out var handler))
                                        handler?.Invoke(parts[1], newValue);
                                }
                                else
                                {
                                    Console.WriteLine($"{parts[2]}");
                                }
                            }
                            break;
                        default:
                            Console.WriteLine("The client was unable to process the response.");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            });
            
        }

    }
}
