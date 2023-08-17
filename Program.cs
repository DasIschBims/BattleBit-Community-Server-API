using System.Net;
using BattleBitAPI;
using BattleBitAPI.Common;
using BattleBitAPI.Server;
using CommunityServerAPI;

class Program
{
    static void Main(string[] args)
    {
        var listener = new ServerListener<MyPlayer, MyGameServer>();
        listener.Start(30001);
        listener.OnGameServerConnecting += OnGameServerConnecting;
        listener.OnValidateGameServerToken += OnValidateGameServerToken;

        Console.WriteLine("API started!");

        Thread.Sleep(-1);
    }

    private static async Task<bool> OnValidateGameServerToken(IPAddress ip, ushort gameport, string sentToken)
    {
        await Console.Out.WriteLineAsync(ip + ":" + gameport + " sent token: " + sentToken);
        return sentToken == "123";
    }
    
    private static async Task<bool> OnGameServerConnecting(IPAddress arg)
    {
        await Console.Out.WriteLineAsync(arg.ToString() + " connecting");
        return true;
    }
}

public class MyPlayer : Player<MyPlayer>
{
    public bool IsAdmin = false;
    public int Kills;
    public int Deaths;
    public PlayerLoadout spawnLoadout;
}

class MyGameServer : GameServer<MyPlayer>
{
    public static List<ApiCommand> ApiCommands = new()
    {
        new HelpCommand(),
        new StatsCommand(),
        new KillCommand(),
        new StartCommand()
    };
    
    private CommandHandler handler = new();

    private async Task SetupServer()
    {
        ServerSettings.PlayerCollision = true;
        MapRotation.ClearRotation();
        MapRotation.AddToRotation("Azagor");
        GamemodeRotation.ClearRotation();
        GamemodeRotation.AddToRotation("TDM");

        if (RoundSettings.State == GameState.WaitingForPlayers)
            ForceStartGame();
        
        if (RoundSettings.State == GameState.CountingDown)
            RoundSettings.SecondsLeft = 1;
    }

    public override async Task OnTick()
    {
        ServerSettings.PlayerCollision = true;
        foreach (var player in AllPlayers)
        {
            player.Modifications.JumpHeightMultiplier = 1.5f;
            player.Modifications.RunningSpeedMultiplier = 1.25f;
            player.Modifications.FallDamageMultiplier = 0f;
            player.Modifications.CanSpectate = false;
            player.Modifications.ReloadSpeedMultiplier = 1.25f;
            player.Modifications.RespawnTime = 1;
        }
    }

    public override async Task OnPlayerJoiningToServer(ulong steamID, PlayerJoiningArguments args)
    {
        var stats = args.Stats;
        
        stats.Progress.Rank = 200;
        stats.Progress.Prestige = 10;

        if (steamID == 76561198395073327)
            stats.Roles = Roles.Admin;
        
        if (RoundSettings.State == GameState.WaitingForPlayers)
            ForceStartGame();
    }

    public override async Task OnConnected()
    {
        Console.WriteLine($"Gameserver connected! {this.GameIP}:{this.GamePort}");

        await SetupServer();
    }
    
    public override async Task OnDisconnected()
    {
        Console.WriteLine($"Gameserver disconnected! {this.GameIP}:{this.GamePort}");
    }

    public override async Task OnReconnected()
    {
        Console.WriteLine($"Gameserver reconnected! {this.GameIP}:{this.GamePort}");

        await SetupServer();
    }
    
    public override async Task OnPlayerConnected(MyPlayer player)
    {
        SayToChat("<color=green>" + player.Name + " joined the game!</color>");
        await Console.Out.WriteLineAsync("Connected: " + player);
    }
    

    public override async Task OnPlayerDisconnected(MyPlayer player)
    {
        SayToChat("<color=orange>" + player.Name + " left the game!</color>");
        await Console.Out.WriteLineAsync("Disconnected: " + player);
    }
    
    public override async Task OnAPlayerDownedAnotherPlayer(OnPlayerKillArguments<MyPlayer> args)
    {
        if (args.Killer == args.Victim)
        {
            SayToChat("<color=red>" + args.Killer.Name + " killes themselves!</color>");
            args.Victim.Kill();
            args.Victim.Deaths++;
        }
        else
        {
            SayToChat("<color=red>" + args.Killer.Name + " killed " + args.Victim.Name +"!</color>");
            args.Victim.Kill();
            args.Killer.SetHP(100);
            args.Killer.Kills++;
            args.Victim.Deaths++;
        }
    }

    public override async Task<OnPlayerSpawnArguments> OnPlayerSpawning(MyPlayer player, OnPlayerSpawnArguments request)
    {
        player.spawnLoadout = request.Loadout;
        return request;
    }

    public override async Task<bool> OnPlayerTypedMessage(MyPlayer player, ChatChannel channel, string msg)
    {
        if (player.SteamID == 76561198395073327)
            player.IsAdmin = true;
        
        var splits = msg.Split(" ");
        var cmd = splits[0].ToLower();
        if (!cmd.StartsWith("/")) return true;
        
        foreach(var apiCommand in ApiCommands)
        {
            if (apiCommand.CommandString == cmd || apiCommand.Aliases.Contains(cmd))
            {
                var command = apiCommand.ChatCommand(player, channel, msg);
                if (apiCommand.AdminOnly && !player.IsAdmin)
                    return true;
                    
                await handler.handleCommand(player, command);
                return false;
            }
        }

        return true;
    }
}