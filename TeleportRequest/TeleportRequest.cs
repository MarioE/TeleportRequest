using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Timers;
using Hooks;
using Terraria;
using TShockAPI;

namespace TeleportRequest
{
    [APIVersion(1, 12)]
    public class TeleportRequest : TerrariaPlugin
    {
        public override string Author
        {
            get { return "MarioE"; }
        }
        public Config Config = new Config();
        public override string Description
        {
            get { return "Adds teleportation accept commands."; }
        }
        public override string Name
        {
            get { return "Teleport"; }
        }
        private Timer Timer;
        private List<TPRequest> TPRequests = new List<TPRequest>();
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public TeleportRequest(Main game)
            : base(game)
        {
            Order = -5;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= OnInitialize;
                Timer.Dispose();
            }
        }
        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
        }

        void OnElapsed(object sender, ElapsedEventArgs e)
        {
            lock (TPRequests)
            {
                for (int i = TPRequests.Count - 1; i >= 0; i--)
                {
                    TPRequest tpr = TPRequests[i];
                    if (tpr.timeout == 0)
                    {
                        TPRequests.RemoveAt(i);
                        TShock.Players[tpr.src].SendMessage("Your teleport request timed out.", Color.Red);
                    }
                    else if (!TShock.Players[tpr.dst].TPAllow)
                    {
                        TPRequests.RemoveAt(i);
                        TShock.Players[tpr.src].SendMessage(String.Format("{0} denied your request.",
                            TShock.Players[tpr.dst].Name), Color.Red);
                    }
                    else
                    {
                        string msg = "{0} is requesting to teleport to you. (/tpaccept or /tpdeny)";
                        if (tpr.dir)
                        {
                            msg = "You are requested to teleport to {0}. (/tpaccept or /tpdeny)";
                        }
                        TShock.Players[tpr.dst].SendMessage(String.Format(msg, TShock.Players[tpr.src].Name), Color.Yellow);
                    }
                    tpr.timeout--;
                }
            }
        }
        void OnInitialize()
        {
            Commands.ChatCommands.Add(new Command(TPAccept, "tpaccept"));
            Commands.ChatCommands.Add(new Command(TPDeny, "tpdeny"));
            Commands.ChatCommands.Add(new Command("tpahere", TPAHere, "tpahere"));
            Commands.ChatCommands.Add(new Command("tpa", TPA, "tpa"));

            if (File.Exists(Path.Combine(TShock.SavePath, "tpconfig.json")))
            {
                Config = Config.Read(Path.Combine(TShock.SavePath, "tpconfig.json"));
            }
            Config.Write(Path.Combine(TShock.SavePath, "tpconfig.json"));
            Timer = new Timer(Config.Interval * 1000);
            Timer.Elapsed += OnElapsed;
            Timer.Start();
        }

        void TPA(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                e.Player.SendMessage("Invalid syntax! Proper syntax: /tpa <player>");
                return;
            }
            string plrName = String.Join(" ", e.Parameters.ToArray());
            List<TSPlayer> players = TShock.Utils.FindPlayer(plrName);
            if (players.Count == 0)
            {
                e.Player.SendMessage("Invalid player!", Color.Red);
            }
            else if (players.Count > 1)
            {
                e.Player.SendMessage("More than one player matched!", Color.Red);
            }
            else
            {
                lock (TPRequests)
                {
                    foreach (TPRequest tpr in TPRequests)
                    {
                        if (tpr.dst == players[0].Index)
                        {
                            e.Player.SendMessage(String.Format("{0} already has a teleport request.", players[0].Name), Color.Red);
                            return;
                        }
                    }
                    TPRequests.Add(new TPRequest((byte)e.Player.Index, (byte)players[0].Index, false, Config.Timeout));
                }
                e.Player.SendMessage(String.Format("Sent a teleport request to {0}.", players[0].Name), Color.Green);
            }
        }
        void TPAccept(CommandArgs e)
        {
            lock (TPRequests)
            {
                for (int i = TPRequests.Count - 1; i >= 0; i--)
                {
                    TPRequest tpr = TPRequests[i];
                    if (tpr.dst == e.Player.Index)
                    {
                        TSPlayer plr1 = tpr.dir ? e.Player : TShock.Players[tpr.src];
                        TSPlayer plr2 = tpr.dir ? TShock.Players[tpr.src] : e.Player;
                        if (plr1.Teleport(plr2.TileX, plr2.TileY + 3))
                        {
                            plr1.SendMessage(String.Format("Teleported to {0}.", plr2.Name), Color.Green);
                            plr2.SendMessage(String.Format("{0} teleported to you.", plr1.Name), Color.Green);
                        }
                        TPRequests.RemoveAt(i);
                        return;
                    }
                }
            }
            e.Player.SendMessage("There are no teleport requests.", Color.Red);
        }
        void TPAHere(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                e.Player.SendMessage("Invalid syntax! Proper syntax: /tpahere <player>");
                return;
            }
            string plrName = String.Join(" ", e.Parameters.ToArray());
            List<TSPlayer> players = TShock.Utils.FindPlayer(plrName);
            if (players.Count == 0)
            {
                e.Player.SendMessage("Invalid player!", Color.Red);
            }
            else if (players.Count > 1)
            {
                e.Player.SendMessage("More than one player matched!", Color.Red);
            }
            else
            {
                lock (TPRequests)
                {
                    foreach (TPRequest tpr in TPRequests)
                    {
                        if (tpr.dst == players[0].Index)
                        {
                            e.Player.SendMessage(String.Format("{0} already has a teleport request.", players[0].Name), Color.Red);
                            return;
                        }
                    }
                    TPRequests.Add(new TPRequest((byte)e.Player.Index, (byte)players[0].Index, true, Config.Timeout));
                }
                e.Player.SendMessage(String.Format("Sent a teleport request to {0}.", players[0].Name), Color.Green);
            }
        }
        void TPDeny(CommandArgs e)
        {
            lock (TPRequests)
            {
                for (int i = TPRequests.Count - 1; i >= 0; i--)
                {
                    TPRequest tpr = TPRequests[i];
                    if (tpr.dst == e.Player.Index)
                    {
                        TSPlayer plr = TShock.Players[tpr.src];
                        e.Player.SendMessage(String.Format("Denied {0}'s request.", plr.Name), Color.Green);
                        plr.SendMessage(String.Format("{0} denied your request.", e.Player.Name), Color.Red);
                        TPRequests.RemoveAt(i);
                        return;
                    }
                }
            }
            e.Player.SendMessage("There are no teleport requests.", Color.Red);
        }
    }
}
