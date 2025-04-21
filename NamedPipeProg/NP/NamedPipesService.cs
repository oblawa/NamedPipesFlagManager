using System.Collections.Concurrent;

namespace NamedPipeProg.NP
{
    public class NamedPipesServerService
    {

        List<ServerPipe> serverPipes = new List<ServerPipe>();
        ServerPipe nextServer;

        ConcurrentDictionary<string, byte> flags = new ConcurrentDictionary<string, byte>();
        ConcurrentDictionary<string, List<ServerPipe>> flagSubs = new ConcurrentDictionary<string, List<ServerPipe>>();

        Action<string> flagChanged;
        public NamedPipesServerService()
        {
            flagChanged += FlagChanged;            
        }
        public ServerPipe CreateServer()
        {
            int serverIdx = serverPipes.Count;
            ServerPipe serverPipe = new ServerPipe("Test", p => p.StartStringReaderAsync());
            serverPipes.Add(serverPipe);

            serverPipe.DataReceived += (sndr, args) => HandleRecieve(args.String, serverPipe);

            serverPipe.PipeConnected += (sndr, args) => nextServer = CreateServer();
            return serverPipe;
        }
        void SendError(ServerPipe pipe, string errorMessage)
        {
            pipe.WriteStringAsync($"ERROR:{errorMessage}");
        }
        void HandleRecieve(string message, ServerPipe pipe)
        {
            string[] messageParts = message.Split(":");
            if (messageParts.Length < 2 || messageParts.Length > 3)
            {
                SendError(pipe, "Pipe-серверу был отправлен запрос с некорректным форматом.");
            }

            string command = messageParts[0];
            string flag = messageParts[1];
            byte? value = messageParts.Length == 3 ? Byte.Parse(messageParts[2]) : null;
            /**
             Шаблон ответа на запрос: 
                *Тип ответа ERROR/SUCCESS/NOTIFY*:*Использованная команда GET/SET/CHANGE/REMOVE/SUB/UNSUB*:*Название флага*:*Доп. информация (если ERROR, то сообщение об ошибке; если GET то значение флага)*
             Пример:
                SUCCESS:GET:flag1:3
                ERROR:SET:flag1:Флаг flag1 уже существует
                NOTIFY:CHANGE:flag1:4
                SUCCESS:SET:flag2
                SUCCESS:REMOVE:flag2
             **/
            switch (command)
            {
                case "GET":
                    string getResult = flags.TryGetValue(flag, out byte val) ? $"SUCCESS:{command}:{flag}:{val}" : $"ERROR:{command}:{flag}:Флаг {flag} не найден";
                    pipe.WriteStringAsync(getResult);
                    break;
                case "SET":
                    if (!value.HasValue)
                        pipe.WriteStringAsync($"ERROR:{command}:{flag}:SET-запрос на установку флага {flag} отправлен без указания значения.");
                    else
                    {
                        if (flags.SetFlag(flag, value.Value))                      
                            pipe.WriteStringAsync($"SUCCESS:{command}:{flag}:{value}");
                        else                      
                            pipe.WriteStringAsync($"ERROR:{command}:{flag}:Не удалось установить флаг {flag}.");
                    }
                    break;
                case "CHANGE":
                    if (!value.HasValue)
                        pipe.WriteStringAsync($"ERROR:{command}:{flag}:CHANGE-запрос на изменение флага отправлен без указания значения");
                    else
                    {
                        if (flags.ChangeFlagValue(flag, value.Value) == value.Value)
                            pipe.WriteStringAsync($"SUCCESS:{command}:{flag}:{value}");
                        else
                            pipe.WriteStringAsync($"ERROR:{command}:{flag}:Не удалось обновить значение флага {flag} на {value.Value}.");
                    }
                    flagChanged.Invoke(flag);
                    break;
                case "REMOVE":
                    if (flags.RemoveFlag(flag))
                        pipe.WriteStringAsync($"SUCCESS:{command}:{flag}");
                    else
                        pipe.WriteStringAsync($"ERROR:{command}:{flag}:Не удалось удалить флаг {flag}");
                    flagChanged.Invoke(flag);
                    break;
                case "SUB":
                    flagSubs.AddOrUpdate(flag, new List<ServerPipe> { pipe }, (key, list) =>
                    {
                        if (!list.Contains(pipe))
                        {
                            list.Add(pipe);
                        }
                        return list;
                    });
                    pipe.WriteStringAsync($"SUCCESS:{command}:{flag}");
                    break;
                case "UNSUB":
                    flagSubs.AddOrUpdate(flag, new List<ServerPipe> { pipe }, (key, list) =>
                    {
                        list.Remove(pipe);
                        return list;
                    });
                    pipe.WriteStringAsync($"SUCCESS:{command}:{flag}");
                    break;
                default:
                    //SendError(pipe, "Pipe-серверу была отправлена не существующая команда");
                    pipe.WriteStringAsync($"ERROR:Pipe-серверу была отправлена не существующая команда");
                    break;
            }
        }
        void FlagChanged(string flag) 
        {
            foreach (var fs in flagSubs) 
            {
                if (fs.Key.Equals(flag))
                {
                    foreach(var p in fs.Value)
                    {
                        p.WriteStringAsync($"NOTIFY:{flag}:{flags[flag]}");
                    }
                }
            }    
        }
    }
    public class NamedPipesClientService
    {

        List<ClientPipe> clientPipes = new List<ClientPipe>();
        public ClientPipe CreateClient()
        {
            int clientIdx = clientPipes.Count;
            ClientPipe clientPipe = new ClientPipe(".", "Test", p => p.StartStringReaderAsync());
            clientPipes.Add(clientPipe);

            //clientPipe.DataReceived += (sndr, args) => Console.WriteLine($"Клиент {clientIdx} принял сообщение: {args.String}");
            clientPipe.DataReceived += (sndr, args) => clientPipe.OnDataRecieved(args.String);
            clientPipe.Connect();
            return clientPipe;
        }
    }

    public static class ConcurrentDictionary_FlagExtentions
    {
        public static byte GetFlagValue(this ConcurrentDictionary<string, byte> dict, string flag)
        {
            return dict[flag];
        }
        public static byte ChangeFlagValue(this ConcurrentDictionary<string, byte> dict, string flag, byte _value)
        {
            byte res = dict.AddOrUpdate(flag, _value, (_, _) => _value);
            return res;
        }
        public static bool SetFlag(this ConcurrentDictionary<string, byte> dict, string flag, byte value)
        {
            return dict.TryAdd(flag, value);
        }
        public static bool RemoveFlag(this ConcurrentDictionary<string, byte> dict, string flag)
        {
            return dict.TryRemove(flag, out _);
        }

    }
}
