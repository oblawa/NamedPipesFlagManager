using NamedPipeProg.NP;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var serverService = new NamedPipesServerService();
serverService.CreateServer();

var clientService = new NamedPipesClientService();
var client1 = clientService.CreateClient();

await client1.SubscribeFlag("flag1", (f, v) =>
{
    Console.WriteLine($"The client1 has detected a flag {f} change to the value {v}");
});

if (await client1.SetFlag("flag1", 1))
    Console.WriteLine($"flag1 has been successfully set");

var client2 = clientService.CreateClient();

byte? result = await client2.GetFlagValue("flag1");
if (result != null)
    Console.WriteLine($"flag1 = {result}");

if (await client2.ChangeFlag("flag1", 2))
    Console.WriteLine("flag1 has been successfully changed");

if (await client1.RemoveFlag("flag1"))
    Console.WriteLine("flag1 has been successfully removed");