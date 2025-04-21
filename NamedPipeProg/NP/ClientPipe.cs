using System.Collections.Concurrent;
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
                Console.WriteLine($"Не удалось установить флаг {name}. Отсутствует подключение к именнованному каналу.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("SET", $"{name}:{value}");
            pendingRequests[key] = tcs;
            this.WriteStringAsync($"SET:{name}:{value}");
            try
            {
                var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(":");

                if (res == $"{name}:{value}")
                    return true;
                else 
                {
                    Console.WriteLine($"Ошибка при установке флага {name}: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException) 
            {
                Console.WriteLine("Таймаут ожидания ответа SET-запроса к Pipe-серверу.");
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
                Console.WriteLine($"Не удалось получить флаг {name}. Отсутствует подключение к именнованному каналу.");
                return null;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("GET", name);
            pendingRequests[key] = tcs;
            this.WriteStringAsync($"GET:{name}");
            try
            {
                string res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(":");
                if (Byte.TryParse(resParts[1], out byte flagValue))
                    return flagValue;
                else
                {
                    Console.WriteLine($"Ошибка при попытке получения значения флага {name}: {resParts[1]}");
                    return null;
                }
            }
            catch (TimeoutException) 
            {
                Console.WriteLine("Таймаут ожидания ответа GET-запроса к Pipe-серверу.");
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
                Console.WriteLine($"Не удалось изменить флаг {name}. Отсутствует подключение к именнованному каналу.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("CHANGE", $"{name}:{value}");
            pendingRequests[key] = tcs;
            this.WriteStringAsync($"CHANGE:{name}:{value}");
            try
            {
                var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(':');
                if (res == $"{name}:{value}")
                    return true;
                else
                {
                    Console.WriteLine($"Ошибка при обновлении значения флага {name}: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Таймаут ожидания ответа CHANGE-запроса к Pipe-серверу.");
                return false;
            }
            finally
            {
                pendingRequests.TryRemove(key, out _);
            }
        }
        //async Task<bool> WaitResultAsync(string key, TaskCompletionSource<string> tcs)
        //{
        //    var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        //    var resParts = res.Split(":");

        //}
        public async Task<bool> RemoveFlag(string name)
        {
            if (!isConnected)
            {
                Console.WriteLine($"Не удалось изменить флаг {name}. Отсутствует подключение к именнованному каналу.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("REMOVE", $"{name}");
            pendingRequests[key] = tcs;
            this.WriteStringAsync($"REMOVE:{name}");
            try
            {
                var res = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var resParts = res.Split(':');
                if (res == $"{name}")
                    return true;
                else
                {
                    Console.WriteLine($"Ошибка при удалении флага {name}: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Таймаут ожидания ответа REMOVE-запроса к Pipe-серверу.");
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
                Console.WriteLine($"Не удалось подписаться на флаг {name}. Отсутствует подключение к именнованному каналу.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("SUB", $"{name}");
            pendingRequests[key] = tcs;

            this.WriteStringAsync($"SUB:{name}");

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
                        Console.WriteLine($"Не удалось добавить обработчик событий флага в список обработчиков.");
                        return false;
                    }
                }                    
                else
                {
                    Console.WriteLine($"Ошибка при подписке на флаг {name}: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Таймаут ожидания ответа SUB-запроса к Pipe-серверу.");
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
                Console.WriteLine($"Не удалось отписаться от флага {name}. Отсутствует подключение к именнованному каналу.");
                return false;
            }
            var tcs = new TaskCompletionSource<string>();
            var key = Tuple.Create("UNSUB", $"{name}");
            pendingRequests[key] = tcs;
            this.WriteStringAsync($"UNSUB:{name}");
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
                        Console.WriteLine($"Не удалось удалить обработчик событий флага из списка обработчиков.");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"Ошибка при отписке на флаг {name}: {resParts[1]}");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Таймаут ожидания ответа UNSUB-запроса к Pipe-серверу.");
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
                        Console.WriteLine("Неправильный формат ответа от Pipe-сервера");
                        return;
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
                                        Console.WriteLine($"Ошибка при попытке установить значение (\"{commandType}\"{flag}) словарю pendingRequests.");
                                        return;
                                    }
                                }
                                else if (pendingRequests.TryGetValue(Tuple.Create(commandType, $"{flag}:{value}"), out var tcs2))
                                {
                                    if (!tcs2.TrySetResult($"{flag}:{value}"))
                                    {
                                        Console.WriteLine($"Ошибка при попытке установить значение (\"{commandType}\"{flag}) словарю pendingRequests.");
                                        return;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Не удалось найти ключ (\"{commandType}\", {flag}) в словаре pendingRequests.");
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
                                        Console.WriteLine($"Ошибка при попытке установить значение ключу (\"{commandType}\":{flag}) словарю pendingRequests.");
                                        return;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Не удалось найти ключ (\"{commandType}\":{flag}) в словаре pendingRequests.");
                                    return;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Ответ от сервера на Pipe-запрос не соответствует шаблону.");
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
                                        Console.WriteLine($"Ошибка при попытке установить значение (\"{commandType}\"{flag}) словарю pendingRequests.");
                                        return;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Не удалось найти ключ (\"{commandType}\", {flag}) в словаре pendingRequests.");
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
                            Console.WriteLine("Клиент не смог обработать ответ");
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
