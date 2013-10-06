using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace TeleportRequest
{
    [ApiVersion(1, 14)]
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
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                Timer.Dispose();
            }
        }
        public override void Initialize()
        {
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        void OnElapsed(object sender, ElapsedEventArgs e)
        {
            lock (TPRequests)
            {
                for (int i = TPRequests.Count - 1; i >= 0; i--)
                {
                    TPRequest tpr = TPRequests[i];
                    TSPlayer dst = TShock.Players[tpr.dst];
                    TSPlayer src = TShock.Players[tpr.src];
                    if (tpr.timeout == 0)
                    {
                        TPRequests.RemoveAt(i);
                        src.SendErrorMessage("Your teleport request timed out.");
                        dst.SendInfoMessage("{0}'s teleport request timed out.", src.Name);
                    }
                    else
                    {
                        string msg = "{0} is requesting to teleport to you. (/tpaccept or /tpdeny)";
                        if (tpr.dir)
                        {
                            msg = "You are requested to teleport to {0}. (/tpaccept or /tpdeny)";
                        }
                        dst.SendInfoMessage(msg, src.Name);
                    }
                    tpr.timeout--;
                }
            }
        }
        void OnInitialize(EventArgs e)
        {
            Commands.ChatCommands.Add(new Command("tprequest.accept", TPAccept, "tpaccept"));
            Commands.ChatCommands.Add(new Command("tprequest.deny", TPDeny, "tpdeny"));
            Commands.ChatCommands.Add(new Command("tprequest.tpahere", TPAHere, "tpahere"));
            Commands.ChatCommands.Add(new Command("tprequest.tpa", TPA, "tpa"));

            if (File.Exists(Path.Combine(TShock.SavePath, "tpconfig.json")))
            {
                Config = Config.Read(Path.Combine(TShock.SavePath, "tpconfig.json"));
            }
            Config.Write(Path.Combine(TShock.SavePath, "tpconfig.json"));
            Timer = new Timer(Config.Interval * 1000);
            Timer.Elapsed += OnElapsed;
            Timer.Start();
        }
        void OnLeave(LeaveEventArgs e)
        {
            lock (TPRequests)
            {
                TPRequests.RemoveAll(tpr => tpr.dst == e.Who || tpr.src == e.Who);
            }
        }

        void TPA(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tpa <player>");
                return;
            }

            string plrName = String.Join(" ", e.Parameters.ToArray());
            List<TSPlayer> players = TShock.Utils.FindPlayer(plrName);
            if (players.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid player!");
            }
            else if (players.Count > 1)
            {
                e.Player.SendErrorMessage("More than one player matched!");
            }
            else if (!players[0].TPAllow && !e.Player.Group.HasPermission(Permissions.tpall))
            {
                e.Player.SendErrorMessage("You cannot teleport to {0}.", players[0].Name);
            }
            else
            {
                lock (TPRequests)
                {
                    if (TPRequests.Any(tpr => tpr.dst == players[0].Index))
                    {
                        e.Player.SendErrorMessage("{0} already has a teleport request.", players[0].Name);
                        return;
                    }
                    TPRequests.Add(new TPRequest((byte)e.Player.Index, (byte)players[0].Index, false, Config.Timeout));
                }
                e.Player.SendSuccessMessage("Sent a teleport request to {0}.", players[0].Name);
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
                        if (plr1.Teleport(plr2.X, plr2.Y))
                        {
                            plr1.SendSuccessMessage("Teleported to {0}.", plr2.Name);
                            plr2.SendSuccessMessage("{0} teleported to you.", plr1.Name);
                        }
                        TPRequests.RemoveAt(i);
                        return;
                    }
                }
            }
            e.Player.SendErrorMessage("There are no pending teleport requests.");
        }
        void TPAHere(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tpahere <player>");
                return;
            }

            string plrName = String.Join(" ", e.Parameters.ToArray());
            List<TSPlayer> players = TShock.Utils.FindPlayer(plrName);
            if (players.Count == 0)
            {
                e.Player.SendErrorMessage("Invalid player!");
            }
            else if (players.Count > 1)
            {
                e.Player.SendErrorMessage("More than one player matched!");
            }
            else if (!players[0].TPAllow && !e.Player.Group.HasPermission(Permissions.tpall))
            {
                e.Player.SendErrorMessage("You cannot teleport {0}.", players[0].Name);
            }
            else
            {
                lock (TPRequests)
                {
                    if (TPRequests.Any(tpr => tpr.dst == players[0].Index))
                    {
                        e.Player.SendErrorMessage("{0} already has a teleport request.", players[0].Name);
                        return;
                    }
                    TPRequests.Add(new TPRequest((byte)e.Player.Index, (byte)players[0].Index, true, Config.Timeout));
                }
                e.Player.SendSuccessMessage("Sent a teleport request to {0}.", players[0].Name);
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
                        e.Player.SendSuccessMessage("Denied {0}'s request.", plr.Name);
                        plr.SendErrorMessage("{0} denied your request.", e.Player.Name);
                        TPRequests.RemoveAt(i);
                        return;
                    }
                }
            }
            e.Player.SendErrorMessage("There are no pending teleport requests.");
        }
    }
}
