using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Serilog.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TermFilter2.Windows
{
    public class MainWindow : Window, IDisposable
    {
        //private static ExcelSheet<World>? worldSheet = Plugin.DataManager.GetExcelSheet<World>();

        // We give this window a constant ID using ###.
        // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
        // and the window ID will always be "###XYZ counter window" for ImGui
        public MainWindow(Plugin plugin) : base("TermFilter2###NNMainWindow")
        {
            Flags = ImGuiWindowFlags.AlwaysAutoResize;
            SizeCondition = ImGuiCond.Always;
        }

        public void Dispose()
        { }

        public override void PreDraw()
        {
        }

        private bool ShowSupport;
        public string TermToAdd = "";
        public XivChatType EnableChannelToAdd;
        public List<XivChatType> EnabledChannelsToAdd = new List<XivChatType>();
        public List<string> EnabledPlayersToAdd = new List<string>();
        public bool HideMessageToAdd = false;
        public bool ReplaceWordInMessageToAdd = false;
        public string ReplaceWordToAdd = "";
        public List<string> ReplaceTermsToAdd = new List<string>();

        public static readonly IReadOnlyList<XivChatType> PlayerChatChannels = Enum.GetValues<XivChatType>().Where(v => v != XivChatType.Echo &&
        typeof(XivChatType).GetField(v.ToString())?.IsDefined(typeof(XivChatTypeInfoAttribute), false) == true).OrderBy(v => v.ToString()).ToList();

        private string CurrentError = "";

        //public List<XivChatType> PlayerChatChannels = new List<XivChatType>
        //{
        //      XivChatType.Alliance,
        //      XivChatType.CrossLinkShell1,
        //      XivChatType.CrossLinkShell2,
        //      XivChatType.CrossLinkShell3,
        //      XivChatType.CrossLinkShell4,
        //      XivChatType.CrossLinkShell5,
        //      XivChatType.CrossLinkShell6,
        //      XivChatType.CrossLinkShell7,
        //      XivChatType.CrossLinkShell8,
        //      XivChatType.CrossParty,
        //      XivChatType.CustomEmote,
        //      XivChatType.FreeCompany,
        //      XivChatType.Ls1,
        //      XivChatType.Ls2,
        //      XivChatType.Ls3,
        //      XivChatType.Ls4,
        //      XivChatType.Ls5,
        //      XivChatType.Ls6,
        //      XivChatType.Ls7,
        //      XivChatType.Ls8,
        //      XivChatType.NoviceNetwork,
        //      XivChatType.Party,
        //      XivChatType.PvPTeam,
        //      XivChatType.Say,
        //      XivChatType.Shout,
        //      XivChatType.TellIncoming,
        //      XivChatType.Yell
        //};

        public override void Draw()
        {
            List<TermFilter2Entry> ToRemove = new();
            if (string.IsNullOrWhiteSpace(Plugin.PlayerState.CharacterName) || Plugin.PlayerState is null || !Plugin.ClientState.IsLoggedIn) { return; }
            ImGui.Text(Plugin.PlayerState.CharacterName + "@" + Plugin.PlayerState.HomeWorld.Value.Name.ExtractText() + " has set the following term filters:");
            if (ImGui.BeginTable($"##TotalStatsTable", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Term");
                ImGui.TableSetupColumn("Term ID");
                ImGui.TableSetupColumn("Enabled Channels");
                ImGui.TableSetupColumn("Enabled Players (WIP)");
                ImGui.TableSetupColumn("Hide Message");
                ImGui.TableSetupColumn("Replace Message");
                ImGui.TableSetupColumn("Replace Terms");
                ImGui.TableSetupColumn("Modify Term");
                ImGui.TableHeadersRow();

                foreach (var (index, name) in Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Index())
                {
                    if (string.IsNullOrWhiteSpace(name.TermToFilter)) { continue; }
                    using var id = ImRaii.PushId(index);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name.TermToFilter);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted((index + 1).ToString());
                    ImGui.TableNextColumn();
                    List<string> FixedChannelNames = new List<string>();
                    List<XivChatType> EnabledChannelsShortened = new(name.EnabledChannels);
                    if (new HashSet<XivChatType>(name.EnabledChannels).SetEquals(PlayerChatChannels))
                    {
                        FixedChannelNames.Add("All Player Chat Channels");
                    }
                    else
                    {
                        foreach (var thing in EnabledChannelsShortened)
                        {
                            FixedChannelNames.Add(FixLSName(thing.GetInfoName()));
                        }
                    }
                    string EnabledChannels = string.Join(Environment.NewLine, FixedChannelNames);
                    ImGui.TextUnformatted(EnabledChannels);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("(Feature not yet available)");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name.HideMessage.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name.ReplaceWordInMessage.ToString());
                    ImGui.TableNextColumn();
                    string ReplaceMessageTerms = string.Join(Environment.NewLine, name.ReplaceMessageTerms);
                    ImGui.TextUnformatted(ReplaceMessageTerms);
                    ImGui.TableNextColumn();
                    if (ImGui.Button("Modify"))
                    {
                        TermToAdd = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].TermToFilter;
                        EnableChannelToAdd = XivChatType.None;
                        EnabledChannelsToAdd = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].EnabledChannels;
                        EnabledPlayersToAdd = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].EnabledPlayers;
                        HideMessageToAdd = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].HideMessage;
                        ReplaceWordInMessageToAdd = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].ReplaceWordInMessage;
                        ReplaceTermsToAdd.Clear();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Delete"))
                    {
                        ToRemove.Add(Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index]);
                    }
                }
                ImGui.EndTable();

                ImGui.Text("Term: ");
                ImGui.SameLine();
                ImGui.InputText("##Term", ref TermToAdd);
                ImGui.Separator();
                ImGui.Text("Channels: ");
                ImGui.SameLine();
                //ImGui.SetNextItemWidth(180);
                ChatTypeDropDown(" ",
                () => FixLSName(EnableChannelToAdd.ToString()),
                s => EnableChannelToAdd = Enum.Parse<XivChatType>(s),
                s => s == FixLSName(EnableChannelToAdd.ToString()),
                PlayerChatChannels.Select(c => c.ToString()).OrderBy(a => a).ToList());
                ImGui.SameLine();
                if (ImGui.Button("Add"))
                {
                    if (EnableChannelToAdd != XivChatType.None && !EnabledChannelsToAdd.Contains(EnableChannelToAdd)){ EnabledChannelsToAdd.Add(EnableChannelToAdd); }
                }
                ImGui.SameLine();
                if (ImGui.Button("Remove"))
                {
                    if (EnableChannelToAdd != XivChatType.None && EnabledChannelsToAdd.Contains(EnableChannelToAdd)) { EnabledChannelsToAdd.Remove(EnableChannelToAdd); }
                }
                ImGui.SameLine();
                if (ImGui.Button("Add all player chat channels"))
                {
                    EnabledChannelsToAdd.Clear();
                    EnabledChannelsToAdd.AddRange(PlayerChatChannels);
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear"))
                {
                    EnabledChannelsToAdd.Clear();
                }
                if (EnabledChannelsToAdd.Count > 0)
                {
                    ImGui.Text("Channels to add to the new term filter:");
                    List<string> FixedChannelNames = new List<string>();
                    foreach (var thing in EnabledChannelsToAdd)
                    {
                        FixedChannelNames.Add(FixLSName(thing.GetInfoName()));
                    }
                    ImGui.TextWrapped(string.Join(", ", FixedChannelNames.OrderBy(x => x)));
                }
                ImGui.Separator();
                ImGui.Text("Default behavior is to show \"Message could not be displayed due to Term Filter #(Term ID)\".\nIf you would like to override that behavior, you may do so here:");
                if (ImGui.Checkbox("Hide message", ref HideMessageToAdd))
                {
                    ReplaceWordInMessageToAdd = false;
                }
                ImGui.SameLine();
                if (ImGui.Checkbox("Replace word", ref ReplaceWordInMessageToAdd))
                {
                    HideMessageToAdd = false;
                }
                if (ReplaceWordInMessageToAdd)
                {
                    ImGui.Text("Word to replace the term:");
                    ImGui.SameLine();
                    ImGui.InputText("##WordToAdd", ref TermToAdd);
                }

                ImGui.Separator();
                if (ImGui.Button("Add term"))
                {
                    if (string.IsNullOrWhiteSpace(TermToAdd))
                    {
                        CurrentError = "You must enter a term to filter.";
                        return;
                    }
                    if (EnabledChannelsToAdd.Count == 0)
                    {
                        CurrentError = "You must add at least 1 channel to filter.";
                        return;
                    }
                    CurrentError = "";
                    //if (string.IsNullOrWhiteSpace(PlayerToAddWorld) || PlayerToAddWorld == "Not Selected")
                    //{
                    //    Plugin.Chat.Print("Invalid world, please check it again.");
                    //    return;
                    //}

                    TermFilter2Entry? CurrentTermFilter2Entry = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Find(x => x.TermToFilter.ToLower() == TermToAdd.ToLower());

                    TermFilter2Entry NewEntry = new TermFilter2Entry { TermToFilter = TermToAdd, EnabledChannels = EnabledChannelsToAdd, EnabledPlayers = EnabledPlayersToAdd, HideMessage = HideMessageToAdd, ReplaceWordInMessage = ReplaceWordInMessageToAdd, ReplaceMessageTerms = ReplaceTermsToAdd };
                    NewEntry.EnabledChannels = NewEntry.EnabledChannels.OrderBy(x => x.GetInfoName(), StringComparer.OrdinalIgnoreCase).ToList();
                    NewEntry.EnabledPlayers = NewEntry.EnabledPlayers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    NewEntry.ReplaceMessageTerms = NewEntry.ReplaceMessageTerms.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    if (CurrentTermFilter2Entry != null)
                    {
                        Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].FindIndex(x => x.TermToFilter.ToLower() == TermToAdd.ToLower())] = NewEntry;
                    }
                    else
                    {
                        Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Add(NewEntry);
                    }
                        
                    Plugin.PluginConfig.Save();
                    TermToAdd = "";
                    EnableChannelToAdd = XivChatType.None;
                    EnabledChannelsToAdd.Clear();
                    EnabledPlayersToAdd.Clear();
                    HideMessageToAdd = false;
                    ReplaceWordInMessageToAdd = false;
                    ReplaceTermsToAdd.Clear();
                }
                if (!string.IsNullOrWhiteSpace(CurrentError)) { ImGui.TextColored(new System.Numerics.Vector4(1f, 0f, 0f, 1f), CurrentError); }
            }

            foreach (var Item in ToRemove)
            {
                Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Remove(Item);
                Plugin.Chat.Print("[TF2] The term \"" + Item.TermToFilter + "\" has been removed from the filter.");

                List<TermFilter2Entry> EntriesToRemove = new();
                foreach (TermFilter2Entry Entry in Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId])
                {
                    if (string.IsNullOrWhiteSpace(Entry.TermToFilter))
                    {
                        EntriesToRemove.Add(Entry);
                    }
                }

                foreach (TermFilter2Entry EntryToRemove in EntriesToRemove)
                {
                    Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Remove(EntryToRemove);
                }

                Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Sort((a, b) => string.Compare(a.TermToFilter, b.TermToFilter, StringComparison.Ordinal));
                Plugin.PluginConfig.Save();
            }
        }

        public static string FixLSName(string OriginalLSName)
        {
            string NewLSName = OriginalLSName;
            if (NewLSName.StartsWith("Ls")) { NewLSName = NewLSName.Replace("Ls", "Linkshell"); }
            NewLSName = Regex.Replace(NewLSName, "(?<!^)([A-Z])", " $1");
            NewLSName = Regex.Replace(NewLSName, "(?<!^)(\\d)", " $1");
            NewLSName = NewLSName.Replace("N P C", "NPC");
            NewLSName = NewLSName.Replace("Pv P", "PvP");
            NewLSName = NewLSName.Replace("Cross Link Shell", "Cross-world Linkshell");
            return NewLSName;
        }

        //Lifted from the Orchestration plugin, thank you perchbird!
        //https://github.com/lmcintyre/OrchestrionPlugin/blob/main/Orchestrion/UI/Windows/SettingsWindow.cs
        private static void ChatTypeDropDown(string text,
        Func<string> get,
        Action<string> set,
        Func<string, bool> isSelected,
        List<string> items,
        Func<string, string> displayFunc = null,
        Action<bool> onChange = null)
        {
            var value = get();
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            using var combo = ImRaii.Combo(text, value);
            if (!combo.Success)
            {
                // ImGui.PopItemWidth();
                return;
            }
            foreach (var item in items)
            {
                var display = displayFunc != null ? displayFunc(FixLSName(item)) : FixLSName(item);
                if (ImGui.Selectable(display, isSelected(FixLSName(item))))
                {
                    set(item);
                    Plugin.PluginConfig.Save();
                }
            }
            if (get() != value)
                onChange?.Invoke(true);
            // ImGui.PopItemWidth();
        }
    }

    public static class XivChatTypeExtensions
    {
        public static string GetInfoName(this XivChatType type)
        {
            var field = typeof(XivChatType).GetField(type.ToString());
            var attr = field?.GetCustomAttribute<XivChatTypeInfoAttribute>();

            return attr?.FancyName ?? string.Empty;
        }
    }
}