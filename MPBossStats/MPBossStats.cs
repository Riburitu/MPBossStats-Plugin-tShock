// MPBossStats - TShock plugin: boss fight statistics / estadisticas de jefes
// Copyright (C) 2026 Riburitu
//
// Inspired by the tModLoader mod "MPBossStats". / Inspirado en el mod de tModLoader "MPBossStats".
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace MPBossStats
{
    [ApiVersion(2, 1)]
    public class MPBossStatsPlugin : TerrariaPlugin
    {
        public override string Author => "Riburitu";
        public override string Description => "Per-player boss fight statistics visible to everyone (EN/ES) / Estadísticas por jugador de peleas contra jefes, visibles para todos (EN/ES).";
        public override string Name => "MPBossStats";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public MPBossStatsPlugin(Main game) : base(game) { }

        private const string Tag = "MPBossStats";

        private class Config
        {
            public bool Enabled { get; set; } = true;
            public int EndGraceTicks { get; set; } = 120;
            public string Language { get; set; } = "auto";
        }

        private static Config cfg = new Config();
        private static string CfgPath => Path.Combine(TShock.SavePath, "MPBossStats.json");
        private static string PlayersPath => Path.Combine(TShock.SavePath, "MPBossStatsPlayers.json");

        private enum Lng { En, Es }
        private static string T(Lng l, string en, string es) => l == Lng.Es ? es : en;

        private static Dictionary<string, int> langPrefs = new Dictionary<string, int>();
        private static readonly object prefLock = new object();

        private static Lng ServerLang()
        {
            string c = (cfg.Language ?? "auto").Trim().ToLowerInvariant();
            if (c == "es" || c == "spanish" || c == "español") return Lng.Es;
            if (c == "en" || c == "english") return Lng.En;
            try
            {
                string name = Terraria.Localization.LanguageManager.Instance.ActiveCulture.Name ?? "";
                if (name.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return Lng.Es;
            }
            catch { }
            return Lng.En;
        }

        private static string KeyOf(TSPlayer p)
            => p.Account != null ? "acc:" + p.Account.Name : $"{p.Name}:{p.IP}";

        private static Lng LangOf(TSPlayer p)
        {
            if (p == null || !p.RealPlayer) return ServerLang();
            int pref;
            lock (prefLock)
                pref = langPrefs.TryGetValue(KeyOf(p), out int v) ? v : 0;
            if (pref == 1) return Lng.En;
            if (pref == 2) return Lng.Es;
            return ServerLang();
        }

        private class PStats
        {
            public string Name;
            public long DamageDealt;
            public long DamageTaken;
            public int Deaths;
            public long TicksAlive;
            public int LastLife = -1;
            public bool WasDead;
        }

        private struct Strike
        {
            public int Player;
            public int Damage;
            public long Tick;
        }

        private class ReportLine
        {
            public string Text;
            public byte R, G, B;
        }

        private static readonly Dictionary<string, PStats> Stats = new Dictionary<string, PStats>();
        private static readonly List<Strike>[] Pending = new List<Strike>[Main.maxNPCs + 1];
        private static readonly int[] BaseLife = new int[Main.maxNPCs + 1];
        private static readonly int[] BaseType = new int[Main.maxNPCs + 1];
        private static readonly bool[] Killed = new bool[Main.maxNPCs + 1];

        private static long indirectDebuffs;

        private static readonly HashSet<int> DotBuffs = new HashSet<int>
        {
            20,
            24,
            39,
            44,
            70,
            153,
            189,
            323,
            324
        };
        private static readonly HashSet<string> BossNames = new HashSet<string>();

        private static bool fightActive;
        private static long fightTicks;
        private static int noBossTicks;
        private static long tick;

        private static Dictionary<Lng, List<ReportLine>> lastReport;

        public override void Initialize()
        {
            LoadConfig();
            LoadPrefs();
            for (int i = 0; i < BaseLife.Length; i++) BaseLife[i] = -1;

            ServerApi.Hooks.GameInitialize.Register(this, OnGameInitialize);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
            GetDataHandlers.NPCStrike += OnNpcStrike;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnGameInitialize);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.NpcKilled.Deregister(this, OnNpcKilled);
                GetDataHandlers.NPCStrike -= OnNpcStrike;
            }
            base.Dispose(disposing);
        }

        private void OnGameInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("", BossStatsCmd, "bossstats", "bs")
            {
                HelpText = "/bossstats <on|off|show|reset>"
            });
            Commands.ChatCommands.Add(new Command("", SetLanguage, "bosslang")
            {
                HelpText = "/bosslang en|es|auto"
            });
        }

        private static void LoadConfig()
        {
            try
            {
                if (File.Exists(CfgPath))
                    cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(CfgPath)) ?? new Config();
                else
                    SaveConfig();
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{Tag}] Error reading config / Error leyendo config: {ex.Message}");
                cfg = new Config();
            }
        }

        private static void SaveConfig()
        {
            try
            {
                File.WriteAllText(CfgPath,
                    JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{Tag}] Error saving config / Error guardando config: {ex.Message}");
            }
        }

        private static void LoadPrefs()
        {
            try
            {
                if (File.Exists(PlayersPath))
                    langPrefs = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(PlayersPath))
                                ?? new Dictionary<string, int>();
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{Tag}] Error reading player settings / Error leyendo preferencias: {ex.Message}");
                langPrefs = new Dictionary<string, int>();
            }
        }

        private static void SavePrefs()
        {
            try
            {
                lock (prefLock)
                    File.WriteAllText(PlayersPath,
                        JsonSerializer.Serialize(langPrefs, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{Tag}] Error saving player settings / Error guardando preferencias: {ex.Message}");
            }
        }

        private static void BossStatsCmd(CommandArgs args)
        {
            Lng L = LangOf(args.Player);
            string sub = args.Parameters.Count > 0 ? args.Parameters[0].ToLowerInvariant() : "status";
            bool canToggle = args.Player.HasPermission("bossstats.toggle");

            switch (sub)
            {
                case "on":
                case "off":
                    if (!canToggle)
                    {
                        args.Player.SendErrorMessage(T(L,
                            "You don't have permission (bossstats.toggle).",
                            "No tienes permiso (bossstats.toggle)."));
                        return;
                    }
                    cfg.Enabled = sub == "on";
                    SaveConfig();
                    if (!cfg.Enabled) ResetAll();
                    Broadcast(
                        l => cfg.Enabled
                            ? T(l, $"[{Tag}] Boss statistics ENABLED.", $"[{Tag}] Estadísticas de jefes ACTIVADAS.")
                            : T(l, $"[{Tag}] Boss statistics DISABLED.", $"[{Tag}] Estadísticas de jefes DESACTIVADAS."),
                        cfg.Enabled ? (byte)102 : (byte)255, cfg.Enabled ? (byte)255 : (byte)102, 102);
                    break;

                case "reset":
                    if (!canToggle)
                    {
                        args.Player.SendErrorMessage(T(L,
                            "You don't have permission (bossstats.toggle).",
                            "No tienes permiso (bossstats.toggle)."));
                        return;
                    }
                    ResetAll();
                    args.Player.SendSuccessMessage(T(L,
                        $"[{Tag}] Statistics reset.",
                        $"[{Tag}] Estadísticas reiniciadas."));
                    break;

                case "show":
                    if (!cfg.Enabled)
                    {
                        args.Player.SendErrorMessage(T(L,
                            $"[{Tag}] It is disabled.",
                            $"[{Tag}] Está desactivado."));
                        return;
                    }
                    if (fightActive)
                        SendReport(args.Player, BuildReports(true));
                    else if (lastReport != null)
                        SendReport(args.Player, lastReport);
                    else
                        args.Player.SendInfoMessage(T(L,
                            $"[{Tag}] No fight has been recorded yet.",
                            $"[{Tag}] Aún no hay ninguna pelea registrada."));
                    break;

                default:
                    args.Player.SendInfoMessage($"[{Tag}] " + T(L, "Status: ", "Estado: ") + (cfg.Enabled ? "ON" : "OFF")
                        + (fightActive ? T(L, " (fight in progress)", " (pelea en curso)") : ""));
                    args.Player.SendInfoMessage(T(L,
                        "Usage: /bossstats <on|off|show|reset>  |  /bosslang en|es|auto",
                        "Uso: /bossstats <on|off|show|reset>  |  /bosslang en|es|auto"));
                    break;
            }
        }

        private static void SetLanguage(CommandArgs args)
        {
            TSPlayer p = args.Player;
            Lng cur = LangOf(p);

            if (args.Parameters.Count < 1)
            {
                p.SendInfoMessage(T(cur,
                    "Current language: English. Use /bosslang en|es|auto",
                    "Idioma actual: Español. Usa /bosslang en|es|auto"));
                return;
            }

            int pref;
            switch (args.Parameters[0].ToLowerInvariant())
            {
                case "en": case "english": case "ingles": case "inglés": pref = 1; break;
                case "es": case "spanish": case "español": case "espanol": pref = 2; break;
                case "auto": pref = 0; break;
                default:
                    p.SendErrorMessage(T(cur,
                        $"{args.Parameters[0]} is not a valid language. Use /bosslang en|es|auto",
                        $"{args.Parameters[0]} no es un idioma válido. Usa /bosslang en|es|auto"));
                    return;
            }

            if (!p.RealPlayer)
            {
                p.SendInfoMessage(T(cur,
                    "The console uses the server language (\"Language\" in MPBossStats.json).",
                    "La consola usa el idioma del servidor (\"Language\" en MPBossStats.json)."));
                return;
            }

            lock (prefLock)
                langPrefs[KeyOf(p)] = pref;
            SavePrefs();

            Lng now = LangOf(p);
            p.SendSuccessMessage(T(now, "Language set to English", "Idioma cambiado a Español") + (pref == 0 ? " (auto)" : ""));
        }

        private static void Broadcast(Func<Lng, string> text, byte r, byte g, byte b)
        {
            foreach (TSPlayer p in TShock.Players)
            {
                if (p == null || !p.Active) continue;
                p.SendMessage(text(LangOf(p)), r, g, b);
            }
            TSPlayer.Server.SendMessage(text(ServerLang()), r, g, b);
        }

        private static bool IsBossAlive()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && !n.dontCountMe && (n.boss || n.type == 13 || n.type == 245))
                    return true;
            }
            return false;
        }

        private static readonly HashSet<int> BossTypes = new HashSet<int>
        {
            267,
            393, 394,
            13, 14, 15,
            134, 135, 136,
            36,
            128, 129, 130, 131,
            246, 247, 248, 249,
            396, 397
        };

        private static bool IsBossNpc(NPC n) => n.boss || BossTypes.Contains(n.type);

        private static void OnNpcKilled(NpcKilledEventArgs args)
        {
            if (args?.npc == null) return;
            int id = args.npc.whoAmI;
            if (id >= 0 && id < Killed.Length) Killed[id] = true;
        }

        private static void OnNpcStrike(object sender, GetDataHandlers.NPCStrikeEventArgs e)
        {
            if (!cfg.Enabled || e.Handled || e.Player == null) return;
            int id = e.ID;
            if (id < 0 || id >= Main.maxNPCs) return;

            int target = Main.npc[id].realLife >= 0 && Main.npc[id].realLife < Main.maxNPCs
                ? Main.npc[id].realLife : id;
            if (!Main.npc[target].active || !IsBossNpc(Main.npc[target])) return;

            if (Pending[target] == null) Pending[target] = new List<Strike>();
            Pending[target].Add(new Strike { Player = e.Player.Index, Damage = Math.Max(1, (int)e.Damage), Tick = tick });
        }

        private static void OnUpdate(EventArgs args)
        {
            tick++;
            if (!cfg.Enabled) return;

            bool bossAlive = IsBossAlive();

            if (bossAlive)
            {
                noBossTicks = 0;
                if (!fightActive)
                {
                    ResetAll();
                    fightActive = true;
                }
            }
            else if (fightActive)
            {
                noBossTicks++;
            }

            if (fightActive)
            {
                fightTicks++;
                TrackNpcDamage();
                TrackPlayers();

                if (!bossAlive && noBossTicks >= cfg.EndGraceTicks)
                    EndFight();
            }
            else
            {
                for (int i = 0; i < Pending.Length; i++) Pending[i]?.Clear();
            }
        }

        private static PStats Get(TSPlayer p)
        {
            if (!Stats.TryGetValue(p.Name, out PStats s))
            {
                s = new PStats { Name = p.Name };
                Stats[p.Name] = s;
            }
            return s;
        }

        private static void TrackNpcDamage()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                List<Strike> pend = Pending[i];

                pend?.RemoveAll(s => tick - s.Tick > 6);

                bool alive = n.active && IsBossNpc(n);

                if (alive)
                {
                    if (!BossNames.Contains(n.FullName) && (n.boss || n.type == 13))
                        BossNames.Add(n.FullName);
                }

                if (BaseLife[i] >= 0 && (!alive || BaseType[i] != n.type))
                {
                    bool sameNpc = BaseType[i] == n.type;
                    bool killed = Killed[i] || (sameNpc && !n.active && n.life <= 0);

                    int rem = 0;
                    if (killed) rem = BaseLife[i];
                    else if (sameNpc && n.life >= 0 && n.life < BaseLife[i]) rem = BaseLife[i] - n.life;

                    if (rem > 0)
                    {
                        if (pend != null && pend.Count > 0) Attribute(pend, rem);
                        else AttributeIndirect(sameNpc ? n : null, rem);
                    }

                    Killed[i] = false;
                    BaseLife[i] = -1;
                    if (!alive) continue;
                }

                if (!alive) continue;

                if (BaseLife[i] < 0)
                {
                    BaseLife[i] = n.life;
                    BaseType[i] = n.type;
                    continue;
                }

                int diff = BaseLife[i] - n.life;
                BaseLife[i] = n.life;
                if (diff > 0 && pend != null && pend.Count > 0)
                    Attribute(pend, diff);
                else if (diff > 0)
                    AttributeIndirect(n, diff);
            }
        }

        private static void AttributeIndirect(NPC n, int damage)
        {
            if (n == null || n.buffType == null) return;
            for (int k = 0; k < n.buffType.Length; k++)
            {
                if (n.buffTime[k] > 0 && DotBuffs.Contains(n.buffType[k]))
                {
                    indirectDebuffs += damage;
                    return;
                }
            }
        }

        private static void Attribute(List<Strike> pend, int totalDamage)
        {
            long raw = 0;
            foreach (var s in pend) raw += s.Damage;
            if (raw <= 0) { pend.Clear(); return; }

            long assigned = 0;
            for (int k = 0; k < pend.Count; k++)
            {
                Strike s = pend[k];
                long part = k == pend.Count - 1
                    ? totalDamage - assigned
                    : (long)totalDamage * s.Damage / raw;
                assigned += part;

                TSPlayer p = (s.Player >= 0 && s.Player < TShock.Players.Length) ? TShock.Players[s.Player] : null;
                if (p != null && p.Active && p.TPlayer != null)
                    Get(p).DamageDealt += part;
            }
            pend.Clear();
        }

        private static void TrackPlayers()
        {
            foreach (TSPlayer p in TShock.Players)
            {
                if (p == null || !p.Active || p.TPlayer == null) continue;
                PStats s = Get(p);
                Player tp = p.TPlayer;

                if (!tp.dead)
                {
                    s.TicksAlive++;
                    if (s.LastLife >= 0 && tp.statLife < s.LastLife)
                        s.DamageTaken += s.LastLife - tp.statLife;
                    s.LastLife = tp.statLife;
                    s.WasDead = false;
                }
                else
                {
                    if (!s.WasDead)
                    {
                        if (s.LastLife > 0) s.DamageTaken += s.LastLife;
                        s.Deaths++;
                        s.WasDead = true;
                    }
                    s.LastLife = -1;
                }
            }
        }

        private static void OnLeave(LeaveEventArgs args)
        {
            var p = TShock.Players[args.Who];
            if (p != null && Stats.TryGetValue(p.Name, out PStats s))
            {
                s.LastLife = -1;
                s.WasDead = false;
            }
        }

        private static void EndFight()
        {
            fightActive = false;
            long totalDmg = Stats.Values.Sum(x => x.DamageDealt);
            if (totalDmg + indirectDebuffs > 0)
            {
                lastReport = BuildReports(false);
                foreach (TSPlayer p in TShock.Players)
                {
                    if (p == null || !p.Active) continue;
                    SendReport(p, lastReport);
                }
                SendReport(TSPlayer.Server, lastReport);
            }
            Stats.Clear();
            BossNames.Clear();
            indirectDebuffs = 0;
            for (int i = 0; i < BaseLife.Length; i++) { BaseLife[i] = -1; Killed[i] = false; Pending[i]?.Clear(); }
            fightTicks = 0;
            noBossTicks = 0;
        }

        private static void ResetAll()
        {
            Stats.Clear();
            BossNames.Clear();
            indirectDebuffs = 0;
            fightActive = false;
            fightTicks = 0;
            noBossTicks = 0;
            for (int i = 0; i < BaseLife.Length; i++) { BaseLife[i] = -1; Killed[i] = false; Pending[i]?.Clear(); }
        }

        private static string FormatTime(double seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.Hours > 0) return $"{t.Hours}h {t.Minutes}m {t.Seconds}s";
            if (t.Minutes > 0) return $"{t.Minutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }

        private static Dictionary<Lng, List<ReportLine>> BuildReports(bool live)
        {
            return new Dictionary<Lng, List<ReportLine>>
            {
                { Lng.En, BuildReport(live, Lng.En) },
                { Lng.Es, BuildReport(live, Lng.Es) }
            };
        }

        private static List<ReportLine> BuildReport(bool live, Lng L)
        {
            var lines = new List<ReportLine>();

            var list = Stats.Values.Where(s => s.DamageDealt > 0 || s.DamageTaken > 0 || s.Deaths > 0)
                                   .OrderByDescending(s => s.DamageDealt).ToList();
            long playersTotal = list.Sum(s => s.DamageDealt);
            long indirect = indirectDebuffs;
            long total = playersTotal + indirect;

            string bosses = BossNames.Count > 0 ? string.Join(", ", BossNames) : T(L, "Boss", "Jefe");
            string liveTag = live ? T(L, " (live)", " (en curso)") : "";
            lines.Add(new ReportLine
            {
                Text = $"═══ Boss Stats{liveTag}: {bosses} — {FormatTime(fightTicks / 60.0)} ═══",
                R = 255, G = 215, B = 90
            });

            int rank = 1;
            foreach (var s in list)
            {
                double secs = s.TicksAlive / 60.0;
                long dps = secs >= 1 ? (long)(s.DamageDealt / secs) : 0;
                double pct = total > 0 ? 100.0 * s.DamageDealt / total : 0;

                string text = T(L,
                    $"#{rank} {s.Name}: Damage {s.DamageDealt:N0} ({pct:0.#}%) | DPS {dps:N0} | Taken {s.DamageTaken:N0} | Deaths {s.Deaths} | Alive {FormatTime(secs)}",
                    $"#{rank} {s.Name}: Daño {s.DamageDealt:N0} ({pct:0.#}%) | DPS {dps:N0} | Recibido {s.DamageTaken:N0} | Muertes {s.Deaths} | Vivo {FormatTime(secs)}");

                if (rank == 1) lines.Add(new ReportLine { Text = text, R = 102, G = 255, B = 114 });
                else lines.Add(new ReportLine { Text = text, R = 200, G = 255, B = 205 });
                rank++;
            }

            if (indirect > 0)
            {
                double pd = total > 0 ? 100.0 * indirectDebuffs / total : 0;
                lines.Add(new ReportLine
                {
                    Text = T(L,
                        $"Indirect damage: Debuffs {indirectDebuffs:N0} ({pd:0.#}%)",
                        $"Daño indirecto: Debuffs {indirectDebuffs:N0} ({pd:0.#}%)"),
                    R = 255, G = 176, B = 102
                });
            }

            lines.Add(new ReportLine
            {
                Text = T(L, $"Total damage: {total:N0}", $"Daño total: {total:N0}"),
                R = 165, G = 198, B = 255
            });
            return lines;
        }

        private static void SendReport(TSPlayer target, Dictionary<Lng, List<ReportLine>> reports)
        {
            foreach (ReportLine l in reports[LangOf(target)])
                target.SendMessage(l.Text, l.R, l.G, l.B);
        }
    }
}