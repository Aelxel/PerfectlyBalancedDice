#nullable enable
using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Game.Gui;
using Dalamud.Game.ClientState;
using Dalamud.Game;
using BetterPlaytime.Attributes;
using BetterPlaytime.Data;
using BetterPlaytime.Gui;
using BetterPlaytime.Logic;
using CheapLoc;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Game.Gui.Dtr;
using XivCommon;
using System.Security.Cryptography;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using XivCommon.Functions;

namespace BetterPlaytime
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] public static DataManager Data { get; private set; } = null!;
        [PluginService] public static ChatGui Chat { get; private set; } = null!;
        [PluginService] public static Framework Framework { get; private set; } = null!;
        
        public string Name => "Perfectly Fair Dice";

        public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        public Configuration Configuration { get; set; }
        private Localization Localization = new();
        private PluginUI PluginUi { get; init; }
        private TimeManager TimeManager { get; init; }
        private ServerBar ServerBar { get; init; }
        private ClientState clientState;
        private static XivCommonBase xivCommon = null!;
        private bool pluginSendCommand = false;
        public string fullResult = "";
        
        private readonly PluginCommandManager<Plugin> commandManager;
        
        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commands,
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] DtrBar dtrBar)
        {
            PluginInterface = pluginInterface;
            this.clientState = clientState;
            
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);
            
            TimeManager = new TimeManager(this);
            PluginUi = new PluginUI(this, TimeManager);
            ServerBar = new ServerBar(this, TimeManager, dtrBar);
            
            commandManager = new PluginCommandManager<Plugin>(this, commands);
            
            Localization.SetupWithLangCode(PluginInterface.UiLanguage);
            
            
            Chat.ChatMessage += OnChatMessage;
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            PluginInterface.LanguageChanged += Localization.SetupWithLangCode;

            xivCommon = new XivCommonBase();
            
        }
        
        [Command("/PRoll")]
        [Aliases("/ExperimentalRoll")]
        [HelpMessage("rolls in chat\nArguments:\nconfig - Opens config")]
        public void PluginCommand(string command, string args)
        {
            switch (args)
            {
                case "config":
                    PluginUi.SettingsVisible = true;
                    break;
                case "ui":
                    PluginUi.PlaytimeTracker.Visible = true;
                    break;
                default:
                    RollCommand();
                    break;
            }
        }

        public void RollCommand()
        {
            #region roll
            ReloadConfig();

            double randomValue = 0;
            Random rnd = new Random();
            randomValue = rnd.NextDouble();

            float[] result = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
            float[] weights = { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };

            float adjustment = 1.3f;
            float exp = 1;
            if (Configuration.BiasValue >= 50.0f)
            {

                exp = Configuration.BiasValue / 50.0f;
                exp *= adjustment;
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] = MathF.Pow((i + 1), exp) - MathF.Pow(i, exp);
                    Chat.Print(weights[i].ToString());
                }
            }
            else
            {
                exp = (100.0f - Configuration.BiasValue) / 50.0f;
                exp *= adjustment;

                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] = MathF.Pow(weights.Length - i, exp) - MathF.Pow(weights.Length - i - 1.0f, exp);
                    Chat.Print(weights[i].ToString());
                }
            }

            float tally = 0.0f;
            foreach (float weight in weights)
            {
                //Chat.Print(weight.ToString()); 
                tally += weight;
            }
           // Chat.Print("weights tallied" + tally.ToString());
            randomValue = rnd.Next(((int)tally)) + rnd.NextDouble(); //random 135.42
            //Chat.Print(randomValue.ToString() + "random value");

            int itr = 0; // itmes looped

            float searchVal = 0f; //sesarch val
            float searchSum = weights[0];

            while (searchVal < randomValue)
            {
                searchVal += 0.01f;
                if (searchVal > searchSum)
                {
                    searchSum += weights[itr];
                    itr++;
                }
            }

          //  Chat.Print("roll got" + itr.ToString());

            #endregion
            string rollString = "Has rolled " + result[itr];
            fullResult = rollString;
            // send playtime command after user uses btime command
            PluginLog.Debug($"Requesting playtime from server.");
            xivCommon.Functions.Chat.SendMessage(rollString);
            pluginSendCommand = true;
           // Chat.Print("Roll Success");
            Chat.Print(rollString);
        }

        public void SendRoll()
        {
           // RollCommand();
            var playerName = GetLocalPlayerName();
            if (playerName == string.Empty) return;

            ReloadConfig();
            Chat.Print("Test Success");
            Chat.Print($"{playerName}: {fullResult}");
            PluginLog.Information($"{playerName}: {fullResult}");
      
        }
       
        private void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            // 57 = sysmsg
            var xivChatType = (ushort) type;
            if (xivChatType != 57) return;

            var m = Reg.Match(message.ToString(), clientState.ClientLanguage);
            if (!m.Success) return;
            
            var playerName = GetLocalPlayerName();
            if (playerName == string.Empty) return;

            PluginLog.Debug($"Extracted Player Name: {playerName}.");

            ReloadConfig();

            if (pluginSendCommand)
            {
                // plugin requested this message, so don't show it in chat
                pluginSendCommand = false;
                handled = true;

                // continue /btime command
                SendRoll();
            }
        }

        public string GetLocalPlayerName()
        {
            var local = clientState.LocalPlayer;
            if (local == null || local.HomeWorld.GameData?.Name == null)
            {
                return string.Empty;
            }
            return $"{local.Name}\uE05D{local.HomeWorld.GameData.Name}";
        }
        
        public void ReloadConfig()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);
        }
        
        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            PluginInterface.LanguageChanged -= Localization.SetupWithLangCode;
            
            Chat.ChatMessage -= OnChatMessage;
           // Framework.Update -= TimeManager.AutoSaveEvent;
            Framework.Update -= ServerBar.UpdateTracker;
            
          //  TimeManager.StopAutoSave();
            PluginUi.Dispose();
            commandManager.Dispose();
            ServerBar.Dispose();
            xivCommon.Dispose();
        }

        private void DrawUI()
        {
            PluginUi.Draw();
        }
        
        private void DrawConfigUI()
        {
            PluginUi.SettingsVisible = true;
        }
    }
}
