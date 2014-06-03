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
	[ApiVersion(1, 16)]
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
		private TPOverride[] TPOverrides = new TPOverride[256];
		private TPRequest[] TPRequests = new TPRequest[256];
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public TeleportRequest(Main game)
			: base(game)
		{
			for (int i = 0; i < TPRequests.Length; i++)
				TPRequests[i] = new TPRequest();
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
			for (int i = 0; i < TPRequests.Length; i++)
			{
				TPRequest tpr = TPRequests[i];
				if (tpr.timeout > 0)
				{
					TSPlayer dst = TShock.Players[tpr.dst];
					TSPlayer src = TShock.Players[i];
					if (tpr.timeout == 0)
					{
						src.SendErrorMessage("Your teleport request timed out.");
						dst.SendInfoMessage("{0}'s teleport request timed out.", src.Name);
					}
					else
					{
						string msg = "{0} is requesting to teleport to you. (/tpaccept or /tpdeny)";
						if (tpr.dir)
							msg = "You are requested to teleport to {0}. (/tpaccept or /tpdeny)";
						dst.SendInfoMessage(msg, src.Name);
					}
					tpr.timeout--;
				}
			}
		}
		void OnInitialize(EventArgs e)
		{
			Commands.ChatCommands.Add(new Command("tprequest.accept", TPAccept, "tpaccept")
			{
				AllowServer = false,
				HelpText = "Accepts a teleport request."
			});
			Commands.ChatCommands.Add(new Command("tprequest.deny", TPDeny, "tpdeny")
			{
				AllowServer = false,
				HelpText = "Denies a teleport request."
			});
			Commands.ChatCommands.Add(new Command("tprequest.override", TPOverrideCmd, "tpoverride")
			{
				AllowServer = false,
				HelpText = "Automatically overrides teleport requests to either accept or deny them."
			});
			Commands.ChatCommands.Add(new Command("tprequest.tpahere", TPAHere, "tpahere")
			{
				AllowServer = false,
				HelpText = "Sends a request for someone to teleport to you."
			});
			Commands.ChatCommands.Add(new Command("tprequest.tpa", TPA, "tpa")
			{
				AllowServer = false,
				HelpText = "Sends a request to teleport to someone."
			});

			if (File.Exists(Path.Combine(TShock.SavePath, "tpconfig.json")))
				Config = Config.Read(Path.Combine(TShock.SavePath, "tpconfig.json"));
			Config.Write(Path.Combine(TShock.SavePath, "tpconfig.json"));
			Timer = new Timer(Config.Interval * 1000);
			Timer.Elapsed += OnElapsed;
			Timer.Start();
		}
		void OnLeave(LeaveEventArgs e)
		{
			TPRequests[e.Who].timeout = 0;
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
				e.Player.SendErrorMessage("Invalid player!");
			else if (players.Count > 1)
				e.Player.SendErrorMessage("More than one player matched!");
			else if (!players[0].TPAllow && !e.Player.Group.HasPermission(Permissions.tpall))
				e.Player.SendErrorMessage("You cannot teleport to {0}.", players[0].Name);
			else if (TPOverrides[players[0].Index] == TPOverride.DENY)
				e.Player.SendInfoMessage("{0} denied your teleport request.", players[0].Name);
			else
			{
				for (int i = 0; i < TPRequests.Length; i++)
				{
					TPRequest tpr = TPRequests[i];
					if (tpr.timeout > 0 && tpr.dst == players[0].Index)
					{
						e.Player.SendErrorMessage("{0} already has a teleport request.", players[0].Name);
						return;
					}
				}
				TPRequests[e.Player.Index].dir = false;
				TPRequests[e.Player.Index].dst = (byte)players[0].Index;
				TPRequests[e.Player.Index].timeout = Config.Timeout;
				e.Player.SendSuccessMessage("Sent a teleport request to {0}.", players[0].Name);
			}
		}
		void TPAccept(CommandArgs e)
		{
			for (int i = 0; i < TPRequests.Length; i++)
			{
				TPRequest tpr = TPRequests[i];
				if (tpr.timeout > 0 && tpr.dst == e.Player.Index)
				{
					TSPlayer plr1 = tpr.dir ? e.Player : TShock.Players[i];
					TSPlayer plr2 = tpr.dir ? TShock.Players[i] : e.Player;
					if (plr1.Teleport(plr2.X, plr2.Y))
					{
						plr1.SendSuccessMessage("Teleported to {0}.", plr2.Name);
						plr2.SendSuccessMessage("{0} teleported to you.", plr1.Name);
					}
					tpr.timeout = 0;
					return;
				}
			}
			e.Player.SendErrorMessage("You have no pending teleport requests.");
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
				e.Player.SendErrorMessage("Invalid player!");
			else if (players.Count > 1)
				e.Player.SendErrorMessage("More than one player matched!");
			else if (!players[0].TPAllow && !e.Player.Group.HasPermission(Permissions.tpall))
				e.Player.SendErrorMessage("You cannot teleport {0}.", players[0].Name);
			else if (TPOverrides[players[0].Index] == TPOverride.DENY)
				e.Player.SendInfoMessage("{0} denied your teleport request.", players[0].Name);
			else
			{
				for (int i = 0; i < TPRequests.Length; i++)
				{
					TPRequest tpr = TPRequests[i];
					if (tpr.timeout > 0 && tpr.dst == players[0].Index)
					{
						e.Player.SendErrorMessage("{0} already has a teleport request.", players[0].Name);
						return;
					}
				}
				TPRequests[e.Player.Index].dir = true;
				TPRequests[e.Player.Index].dst = (byte)players[0].Index;
				TPRequests[e.Player.Index].timeout = Config.Timeout;
				e.Player.SendSuccessMessage("Sent a teleport request to {0}.", players[0].Name);
			}
		}
		void TPDeny(CommandArgs e)
		{
			for (int i = 0; i < TPRequests.Length; i++)
			{
				TPRequest tpr = TPRequests[i];
				if (tpr.timeout > 0 && tpr.dst == e.Player.Index)
				{
					e.Player.SendSuccessMessage("Denied {0}'s teleport request.", TShock.Players[i].Name);
					TShock.Players[i].SendErrorMessage("{0} denied your teleport request.", e.Player.Name);
					return;
				}
			}
			e.Player.SendErrorMessage("You have no pending teleport requests.");
		}
		void TPOverrideCmd(CommandArgs e)
		{
			if (e.Parameters.Count != 1)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tpoverride <none|deny>");
				return;
			}

			switch (e.Parameters[0].ToLower())
			{
				case "deny":
					TPOverrides[e.Player.Index] = TPOverride.DENY;
					e.Player.SendInfoMessage("Set TP override to deny.");
					return;
				case "none":
					TPOverrides[e.Player.Index] = TPOverride.NONE;
					e.Player.SendInfoMessage("Disabled TP override.");
					return;
				default:
					e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tpoverride <none|accept|deny>");
					return;
			}
		}
	}
}