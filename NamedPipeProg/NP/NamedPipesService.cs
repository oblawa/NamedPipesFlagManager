using System.Collections.Concurrent;

namespace NamedPipeProg.NP
{
    public class NamedPipesServerService
    {

        List<ServerPipe> serverPipes = new List<ServerPipe>();

        ConcurrentDictionary<string, byte> flags = new ConcurrentDictionary<string, byte>();
        ConcurrentDictionary<string, List<ServerPipe>> flagSubs = new ConcurrentDictionary<string, List<ServerPipe>>();

        Func<string, Task> flagChanged;
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

            serverPipe.PipeConnected += (sndr, args) => CreateServer();
            return serverPipe;
        }
        async Task HandleRecieve(string message, ServerPipe pipe)
        {
            string[] messageParts = message.Split(":");
            if (messageParts.Length < 2 || messageParts.Length > 3)
            {
                pipe.WriteStringAsync("A request with an incorrect format was sent to the Pipe server.");
            }

            string command = messageParts[0];
            string flag = messageParts[1];
            byte? value = messageParts.Length == 3 ? Byte.Parse(messageParts[2]) : null;
            /**
             Request response template:
                *Type of response ERROR/SUCCESS/NOTIFY*:*The command used GET/SET/CHANGE/REMOVE/SUB/UNSUB*:*Name of the flag*:*Additional info (on ERROR, return an error message; on GET, return the flag value)*
             Examples:
                SUCCESS:GET:flag1:3
                ERROR:SET:flag1:The flag flag1 already exists
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
                        pipe.WriteStringAsync($"ERROR:{command}:{flag}:SET-request to set the {flag} flag was sent without specifying a value.");
                    else
                    {
                        if (flags.SetFlag(flag, value.Value))                      
                            pipe.WriteStringAsync($"SUCCESS:{command}:{flag}:{value}");
                        else                      
                            pipe.WriteStringAsync($"ERROR:{command}:{flag}:The {flag} flag could not be set.");
                    }
                    break;
                case "CHANGE":
                    if (!value.HasValue)
                        pipe.WriteStringAsync($"ERROR:{command}:{flag}:CHANGE-request to change the flag was sent without specifying a value.");
                    else
                    {
                        if (flags.ChangeFlagValue(flag, value.Value) == value.Value)
                            pipe.WriteStringAsync($"SUCCESS:{command}:{flag}:{value}");
                        else
                            pipe.WriteStringAsync($"ERROR:{command}:{flag}:Failed to update the value of the {flag} to {value.Value}.");
                    }
                    flagChanged.Invoke(flag);
                    break;
                case "REMOVE":
                    if (flags.RemoveFlag(flag))
                        pipe.WriteStringAsync($"SUCCESS:{command}:{flag}");
                    else
                        pipe.WriteStringAsync($"ERROR:{command}:{flag}:Couldn't delete the {flag}");
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
                    pipe.WriteStringAsync($"ERROR:A non-existent command was sent to the Pipe server.");
                    break;
            }
        }
        async Task FlagChanged(string flag) 
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
