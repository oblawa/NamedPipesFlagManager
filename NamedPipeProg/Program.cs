using NamedPipeProg.NP;

Console.OutputEncoding = System.Text.Encoding.UTF8;

NamedPipesServerService namedPipesServerService = new NamedPipesServerService();
namedPipesServerService.CreateServer();

NamedPipesClientService namedPipesClientService = new NamedPipesClientService();
var client1 = namedPipesClientService.CreateClient();




client1.SubscribeFlag("flag1", (f, v) =>
{
    Console.WriteLine($"Flag {f} changed the value to {v}");
});

bool suc = await client1.SetFlag("flag1", 1);
if (suc)
    Console.WriteLine("suc");
//Thread.Sleep(15000);


bool s = await client1.ChangeFlag("flag1", 2);
if (s)
    Console.WriteLine("s");

//Console.ReadLine();