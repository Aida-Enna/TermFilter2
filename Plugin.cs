using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using TermFilter2.Windows;
using Veda;
using TextPayload = Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload;

namespace TermFilter2
{
    public class Plugin : IDalamudPlugin
    {
        public unsafe string Name => "TermFilter2";

        [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; }
        [PluginService] public static IChatGui Chat { get; set; }
        [PluginService] public static IPluginLog PluginLog { get; set; }
        [PluginService] public static IGameGui GameGui { get; set; }
        [PluginService] public static IClientState ClientState { get; set; } = null!;
        [PluginService] public static IContextMenu ContextMenu { get; private set; } = null!;
        [PluginService] public static IDataManager DataManager { get; private set; } = null!;
        [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;

        private PluginCommandManager<Plugin> commandManager;
        public static Configuration PluginConfig { get; set; }

        public readonly WindowSystem WindowSystem = new("TermFilter2");
        private ConfigWindow ConfigWindow { get; init; }

        //public static ChangeNicknameWindow ChangeNicknameWindow { get; set; }
        private MainWindow MainWindow { get; init; }

        public Plugin(IDalamudPluginInterface pluginInterface, IChatGui chat, ICommandManager commands, IClientState clientState, IPlayerState playerState)
        {
            // Get or create a configuration object
            PluginConfig = (Configuration)PluginInterface.GetPluginConfig() ?? new Configuration();
            PluginConfig.Initialize(PluginInterface);

            if (!PluginConfig.Terms.ContainsKey(PlayerState.ContentId))
            {
                PluginConfig.Terms.Add(PlayerState.ContentId, new TermFilter2Collection());
                PluginConfig.Terms[PlayerState.ContentId].Add(new TermFilter2Entry
                {
                    EnabledChannels = new List<XivChatType>
                    {
                        XivChatType.Ls2
                    },
                    HideMessage = false,
                    TermToFilter = "Howling"
                });
                PluginConfig.Save();
            }

            ConfigWindow = new ConfigWindow(this);
            //ChangeNicknameWindow = new ChangeNicknameWindow(this);
            MainWindow = new MainWindow(this);

            WindowSystem.AddWindow(ConfigWindow);
            //WindowSystem.AddWindow(ChangeNicknameWindow);
            WindowSystem.AddWindow(MainWindow);

            PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi += MainWindow.Toggle;
            PluginInterface.UiBuilder.OpenConfigUi += ConfigWindow.Toggle;

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);

            Chat.ChatMessage += ChatMessage;
            ClientState.Login += Login;
        }

        private void Login()
        {
            if (!PluginConfig.Terms.ContainsKey(PlayerState.ContentId))
            {
                PluginConfig.Terms.Add(PlayerState.ContentId, new TermFilter2Collection());
                PluginConfig.Save();
            }
        }

        private void ChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            try
            {
                if (!ClientState.IsLoggedIn) { return; }

                if (message.Payloads.Count == 0) { return; }

                //if (type == XivChatType.Ls2)
                if (PluginConfig.Terms[PlayerState.ContentId].Any(a => a.EnabledChannels.Any(b => type == b)))
                {
                    string Message = message.TextValue;
                    string PlayerName = string.Concat(sender.TextValue.ToString().Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)));
                    //string PlayerName = ((PlayerPayload)sender.Payloads.Where(x => x.Type == PayloadType.Player).First()).PlayerName;
                    //string PlayerWorld = ((PlayerPayload)sender.Payloads.Where(x => x.Type == PayloadType.Player).First()).World.Value.Name.ExtractText();

                    if (PluginConfig.Terms[PlayerState.ContentId].Any(x => Message.Contains(x.TermToFilter, StringComparison.OrdinalIgnoreCase)))
                    {
                        //Get the TermFilter
                        TermFilter2Entry? TermEntry = PluginConfig.Terms[PlayerState.ContentId].Find(x => Message.Contains(x.TermToFilter, StringComparison.OrdinalIgnoreCase));
                        int TermFilterNumber = PluginConfig.Terms[PlayerState.ContentId].FindIndex(x => Message.Contains(x.TermToFilter, StringComparison.OrdinalIgnoreCase)) + 1;

                        if (TermEntry == null) { return; }
                        if (TermEntry.HideMessage)
                        {
                            isHandled = true;
                            return;
                        }
                        else
                        {
                            var builder = new SeStringBuilder();
                            var NewPayloads = new List<Payload>();

                            //bool ClearedPlayerPayloadAlready = false;
                            //List<Payload> ReplacementPayload = new List<Payload>();
                            foreach (TextPayload payload in message.Payloads)
                            {
                                if (payload is TextPayload) // If it's a text payload, should be the message
                                {
                                    NewPayloads.Add(new UIForegroundPayload(17));
                                    NewPayloads.Add(new TextPayload("[Filtered] Message could not be displayed due to Term Filter " + TermFilterNumber + "."));
                                    PluginLog.Debug("Filtered message from \"" + PlayerName + "\": \"" + payload.Text.Replace(TermEntry.TermToFilter.ToLower(), "->" + TermEntry.TermToFilter.ToUpper() + "<-") + "\"");
                                }
                                else
                                {
                                    NewPayloads.Add(payload);
                                }
                            }

                            message.Payloads.Clear();
                            message.Payloads.AddRange(NewPayloads);
                            //Thread.Sleep(1);
                        }
                    }
                }

//#if DEBUG
//                int count = 0;
//                Plugin.PluginLog.Debug("==PAYLOAD START==");
//                foreach (Payload PLoad in message.Payloads)
//                {
//                    Plugin.PluginLog.Debug("[" + count + "] " + PLoad.ToString());
//                    count++;
//                }
//                Plugin.PluginLog.Debug("==PAYLOAD END==");
//#endif
            }
            catch (Exception f)
            {
                PluginLog.Error(f.ToString());
            }
        }

        //public static void FixNicknameEntries()
        //{
        //    List<NicknameEntry> EntriesToRemove = new();
        //    foreach (NicknameEntry Entry in PluginConfig.Nicknames[PlayerState.ContentId])
        //    {
        //        if (string.IsNullOrWhiteSpace(Entry.Nickname))
        //        {
        //            EntriesToRemove.Add(Entry);
        //        }
        //    }
        //    foreach (NicknameEntry EntryToRemove in EntriesToRemove)
        //    {
        //        PluginConfig.Nicknames[PlayerState.ContentId].Remove(EntryToRemove);
        //        PluginConfig.Nicknames[Plugin.PlayerState.ContentId].Sort((a, b) => string.Compare(a.PlayerWorld, b.PlayerWorld, StringComparison.Ordinal));
        //        PluginConfig.Save();
        //    }
        //}

        [Command("/termfilter2")]
        [Aliases("/tf2", "/filters")]
        [HelpMessage("Shows the main window")]
        public void ToggleMain(string command, string args)
        {
            MainWindow.Toggle();
        }

        [Command("/termfilter2config")]
        [Aliases("/tf2config", "/tf2c")]
        [HelpMessage("Shows the config menu")]
        public void ToggleConfig(string command, string args)
        {
            ConfigWindow.Toggle();
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            PluginInterface.SavePluginConfig(PluginConfig);

            PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi -= MainWindow.Toggle;
            PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;

            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();
            //ChangeNicknameWindow.Dispose();

            Chat.ChatMessage -= ChatMessage;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}