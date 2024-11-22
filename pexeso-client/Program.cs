using System;

class pexesoclient
{
    static void Main(string[] args)
    {
        Random rand = new Random();
        string url = "localhost:8181";
        string username = "guest" + rand.Next(0, 9999);
        if(args.Length > 0) url = args[0];
        if(args.Length > 1) username = args[1];
        Console.WriteLine(args.Length);
        var game = new pexeso_client.MultiplayerManager(url, username);
    }
}