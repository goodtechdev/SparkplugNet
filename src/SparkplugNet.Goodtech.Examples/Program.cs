using System;
using System.Linq;
using System.Threading.Tasks;
using Serilog;


try
{
    Console.WriteLine("SparkplugNetProducerDemo. Press 'q' to exit.");

    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .MinimumLevel.Information()
        .CreateLogger();


    var app = args.Contains("-app");
    var node = args.Contains("-node");
    if (!app && !node) // Run both as default
        app = node = true;

    await Producer.Init();
    if (node)
    {
        await Producer.Run();
        Console.WriteLine("Node started.");
    }

    ConsoleKeyInfo cki;
    do
    {
        while (Console.KeyAvailable == false)
        {
            if (app && !Producer.Restarting) await Producer.Update();
            await Task.Delay(Producer.ScanRate); // Loop until input is entered.
        }

        cki = Console.ReadKey(true);
    } while (cki.Key != ConsoleKey.Q);

    if (app) await Producer.Stop();
}

catch (Exception ex)
{
    Log.Error("An exception occurred: {Exception}", ex);
    Console.ReadKey();
}