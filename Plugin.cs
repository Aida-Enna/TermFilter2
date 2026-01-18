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
using System.Text.RegularExpressions;
using TermFilter2.Windows;
using Veda;
using static System.Net.Mime.MediaTypeNames;
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
        private MainWindow MainWindow { get; init; }

        public Random RNG = new();

        public Plugin(IDalamudPluginInterface pluginInterface, IChatGui chat, ICommandManager commands, IClientState clientState, IPlayerState playerState)
        {
            // Get or create a configuration object
            PluginConfig = (Configuration)PluginInterface.GetPluginConfig() ?? new Configuration();
            PluginConfig.Initialize(PluginInterface);

            if (!PluginConfig.Terms.ContainsKey(PlayerState.ContentId))
            {
                PluginConfig.Terms.Add(PlayerState.ContentId, new TermFilter2Collection());
                PluginConfig.Save();
            }

            MainWindow = new MainWindow(this);

            WindowSystem.AddWindow(MainWindow);

            PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi += MainWindow.Toggle;

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
                /* TO IMPLEMENT:
                 * Players
                 * Replace/Replace Terms
                 */

                if (sender.Payloads.Count == 0) { return; }
                if (sender.Payloads.Count == 1 && ((TextPayload)sender.Payloads.First()).Text == PlayerState.CharacterName) { return; }

                //Payload UIPayload = sender.Payloads.Where(x => x.Type == PayloadType.UIForeground).First();
                //uint UIColor = ((UIForegroundPayload)UIPayload).UIColor.Value.RowId;
                //Chat.Print(UIColor.ToString());

                //If it in any of the enabled channels
                if (PluginConfig.Terms[PlayerState.ContentId].Any(a => a.EnabledChannels.Any(b => type == b)))
                {
                    string MessageString = message.TextValue;

                    string PlayerName = ((PlayerPayload)sender.Payloads.Where(x => x.Type == PayloadType.Player).First()).PlayerName;
                    string PlayerWorld = ((PlayerPayload)sender.Payloads.Where(x => x.Type == PayloadType.Player).First()).World.Value.Name.ExtractText();
                    string PlayerCombined = string.Concat(PlayerName.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c))) + "@" + PlayerWorld;

                    //If any of the terms are in the sentence
                    if (PluginConfig.Terms[PlayerState.ContentId].Any(x => MessageString.Contains(x.TermToFilter, StringComparison.OrdinalIgnoreCase)))
                    {
                        //Get the TermFilter
                        TermFilter2Entry? TermEntry = PluginConfig.Terms[PlayerState.ContentId].Find(x => MessageString.Contains(x.TermToFilter, StringComparison.OrdinalIgnoreCase));
                        int TermFilterNumber = PluginConfig.Terms[PlayerState.ContentId].FindIndex(x => MessageString.Contains(x.TermToFilter, StringComparison.OrdinalIgnoreCase)) + 1;

                        if (TermEntry == null) { return; }

                        //Check to see if the player is in the player list, or if it's "All"
                        if (TermEntry.EnabledPlayers.Any(x => x == PlayerCombined) || (TermEntry.EnabledPlayers.Count == 1 && TermEntry.EnabledPlayers.FirstOrDefault() == "All"))
                        {
                            //If hide the message is enabled
                            if (TermEntry.HideMessage)
                            {
                                PluginLog.Debug("Hid message from \"" + PlayerName + "\": \"" + MessageString.Replace(TermEntry.TermToFilter.ToLower(), "->" + TermEntry.TermToFilter.ToUpper() + "<-") + "\"");
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
                                        if (TermEntry.ReplaceWordInMessage)
                                        {
                                            string ReplacementWord = TermEntry.ReplaceMessageTerms[RNG.Next(0, TermEntry.ReplaceMessageTerms.Count())];
                                            int WordIndex = MessageString.IndexOf(TermEntry.TermToFilter, StringComparison.OrdinalIgnoreCase);

                                            string MessageStringBefore = MessageString.Substring(0, WordIndex);
                                            string MessageStringWord = MessageString.Substring(WordIndex, TermEntry.TermToFilter.Length);
                                            string MessageStringAfter = MessageString.Substring(WordIndex + TermEntry.TermToFilter.Length);

                                            //string[] MessageParts = MessageString.Split(TermEntry.TermToFilter,StringSplitOptions.None);

                                            //string MessageStringBefore = MessageParts[0];
                                            //string MessageStringAfter = MessageParts[1];
                                            NewPayloads.Add(new TextPayload(MessageStringBefore));
                                            //NewPayloads.Add(new UIForegroundPayload(17));
                                            NewPayloads.Add(new UIGlowPayload(8));
                                            NewPayloads.Add(new TextPayload(MatchCase(ReplacementWord, MessageStringWord)));
                                            NewPayloads.Add(new UIGlowPayload(0));
                                            //NewPayloads.Add(new UIForegroundPayload(0));
                                            NewPayloads.Add(new TextPayload(MessageStringAfter));
                                            //NewPayloads.Add(new TextPayload("[Filtered] Message could not be displayed due to Term Filter " + TermFilterNumber + "."));
                                        }
                                        else
                                        {
                                            NewPayloads.Add(new UIForegroundPayload(17));
                                            //NewPayloads.Add(new UIGlowPayload(17));
                                            NewPayloads.Add(new TextPayload("[Filtered] Message could not be displayed due to Term Filter " + TermFilterNumber + "."));
                                            //NewPayloads.Add(new UIGlowPayload(0));
                                            NewPayloads.Add(new UIForegroundPayload(0));
                                        }
                                        PluginLog.Debug("Filtered message from \"" + PlayerName + "\": \"" + Regex.Replace(MessageString, TermEntry.TermToFilter, "->" + TermEntry.TermToFilter + "<-") + "\"", StringComparison.OrdinalIgnoreCase);
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

        [Command("/tf2")]
        [HelpMessage("Shows the Term Filter 2 configuration window")]
        public void ToggleMain(string command, string args)
        {
            MainWindow.Toggle();
        }

        string MatchCase(string replacement, string original)
        {
            if (string.IsNullOrEmpty(original))
                return replacement;

            if (original.All(char.IsUpper))
                return replacement.ToUpper();

            if (original.All(char.IsLower))
                return replacement.ToLower();

            // Title Case
            if (char.IsUpper(original[0]) && original.Skip(1).All(char.IsLower)) { return char.ToUpper(replacement[0]) + replacement[1..].ToLower(); }

            // Mixed case
            var result = new char[replacement.Length];
            bool lastWasUpper = char.IsUpper(original[^1]);

            for (int i = 0; i < replacement.Length; i++)
            {
                bool upper = i < original.Length ? char.IsUpper(original[i]) : lastWasUpper;
                result[i] = upper ? char.ToUpper(replacement[i]) : char.ToLower(replacement[i]);
            }

            return new string(result);
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            PluginInterface.SavePluginConfig(PluginConfig);

            PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi -= MainWindow.Toggle;

            WindowSystem.RemoveAllWindows();

            MainWindow.Dispose();

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