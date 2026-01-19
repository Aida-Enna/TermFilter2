using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace TermFilter2.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private static ExcelSheet<World>? worldSheet = Plugin.DataManager.GetExcelSheet<World>();

        // We give this window a constant ID using ###.
        // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
        // and the window ID will always be "###XYZ counter window" for ImGui
        public unsafe MainWindow(Plugin plugin) : base("Term Filter 2###NNMainWindow")
        {
            //Flags = ImGuiWindowFlags.AlwaysAutoResize;
            SizeCondition = ImGuiCond.FirstUseEver;
            Size = new System.Numerics.Vector2((Device.Instance()->Width / 2), (Device.Instance()->Height / 2));
        }

        public override bool DrawConditions()
        {
            if (!Plugin.ClientState.IsLoggedIn)
            {
                return false;
            }
            return true;
        }

        public void Dispose()
        { }

        public override void PreDraw()
        {
        }

        private bool ShowSupport;
        public string TermToAdd = "";
        public bool SpecifyChannelEnabled = false;
        public XivChatType EnableChannelToAdd;
        public List<XivChatType> EnabledChannelsToAdd = new List<XivChatType>();
        public List<string> EnabledPlayersToAdd = new List<string>();
        public string PlayerToAddName = "";
        public string PlayerToAddWorld = "";
        public bool HideMessageToAdd = false;
        public bool ReplaceWordInMessageToAdd = false;
        public string ReplaceWordToAdd = "";
        public List<string> ReplaceWordsToAdd = new List<string>();
        public bool SpecifyPlayersEnabled = false;

        public static readonly IReadOnlyList<XivChatType> PlayerChatChannels = Enum.GetValues<XivChatType>().Where(v => v != XivChatType.Echo &&
        typeof(XivChatType).GetField(v.ToString())?.IsDefined(typeof(XivChatTypeInfoAttribute), false) == true).OrderBy(v => v.ToString()).ToList();

        private string AddTermError = "";
        private string AddPlayerError = "";
        private string AddWordError = "";

        public unsafe override void Draw()
        {
            List<TermFilter2Entry> ToRemove = new();
            if (string.IsNullOrWhiteSpace(Plugin.PlayerState.CharacterName) || Plugin.PlayerState is null || !Plugin.ClientState.IsLoggedIn) 
            {
                ImGui.Text("This plugin will only work while logged in.");
                return; 
            }
            if (Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Count > 0)
            {
                ImGui.Text(Plugin.PlayerState.CharacterName + "@" + Plugin.PlayerState.HomeWorld.Value.Name.ExtractText() + " has set the following term filters:");
                if (ImGui.BeginTable($"##TermsTable", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY, new System.Numerics.Vector2((Device.Instance()->Width / 2), (Device.Instance()->Height / 2))))
                {
                    ImGui.TableSetupColumn("Term");
                    ImGui.TableSetupColumn("ID");
                    ImGui.TableSetupColumn("Channel(s)");
                    ImGui.TableSetupColumn("Player(s)");
                    ImGui.TableSetupColumn("Hide");
                    ImGui.TableSetupColumn("Replace");
                    ImGui.TableSetupColumn("Replace Term(s)");
                    ImGui.TableSetupColumn("Modify/Delete Term", ImGuiTableColumnFlags.None, 150);
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
                        string FilteredPlayers = string.Join(Environment.NewLine, name.EnabledPlayers);
                        ImGui.TextUnformatted(FilteredPlayers);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(name.HideMessage.ToString());
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(name.ReplaceWordInMessage.ToString());
                        ImGui.TableNextColumn();
                        List<string> FixedReplaceMessageTerms = new List<string>();
                        foreach (var thing in name.ReplaceMessageTerms)
                        {
                            FixedReplaceMessageTerms.Add("\"" + thing + "\"");
                        }
                        string ReplaceMessageTerms = string.Join(Environment.NewLine, FixedReplaceMessageTerms);
                        ImGui.TextUnformatted(ReplaceMessageTerms);
                        ImGui.TableNextColumn();
                        if (ImGui.Button("Modify"))
                        {
                            TermToAdd = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].TermToFilter;
                            EnableChannelToAdd = XivChatType.None;
                            if (!new HashSet<XivChatType>(name.EnabledChannels).SetEquals(PlayerChatChannels))
                            {
                                SpecifyChannelEnabled = true;
                            }
                            else
                            {
                                SpecifyChannelEnabled = false;
                            }
                            EnabledChannelsToAdd.Clear();
                            EnabledChannelsToAdd.AddRange(Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].EnabledChannels);
                            if (Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].EnabledPlayers.FirstOrDefault() != "All")
                            {
                                SpecifyPlayersEnabled = true;
                            }
                            else
                            {
                                SpecifyPlayersEnabled = false;
                            }
                            EnabledPlayersToAdd.Clear();
                            EnabledPlayersToAdd.AddRange(Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].EnabledPlayers);
                            PlayerToAddName = "";
                            PlayerToAddWorld = "Not Selected";
                            HideMessageToAdd = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].HideMessage;
                            ReplaceWordInMessageToAdd = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].ReplaceWordInMessage;
                            ReplaceWordsToAdd.Clear();
                            ReplaceWordsToAdd.AddRange(Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index].ReplaceMessageTerms);
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Delete"))
                        {
                            ToRemove.Add(Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId][index]);
                        }
                    }
                    ImGui.EndTable();
                }
            }
            else
            {
                ImGui.Text(Plugin.PlayerState.CharacterName + "@" + Plugin.PlayerState.HomeWorld.Value.Name.ExtractText() + " has not yet set any term filters.");
            }
            ImGui.Text("Term: ");
            ImGui.SameLine();
            ImGui.InputText("##Term", ref TermToAdd);
            ImGui.Separator();
            ImGui.Checkbox("Only filter specific channels", ref SpecifyChannelEnabled);
            if (SpecifyChannelEnabled)
            {
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
                    if (EnableChannelToAdd != XivChatType.None && !EnabledChannelsToAdd.Contains(EnableChannelToAdd)) { EnabledChannelsToAdd.Add(EnableChannelToAdd); }
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
                if (ImGui.Button("Clear###ClearEnabledChannels"))
                {
                    EnabledChannelsToAdd.Clear();
                }
                if (EnabledChannelsToAdd.Count > 0)
                {
                    ImGui.Text("Channels to add to the new term filter:");
                    if (new HashSet<XivChatType>(EnabledChannelsToAdd).SetEquals(PlayerChatChannels))
                    {
                        ImGui.TextWrapped("All Player Chat Channels");
                    }
                    else
                    {
                        List<string> FixedChannelNames = new List<string>();
                        foreach (var thing in EnabledChannelsToAdd)
                        {
                            FixedChannelNames.Add(FixLSName(thing.GetInfoName()));
                        }
                        ImGui.TextWrapped(string.Join(", ", FixedChannelNames.OrderBy(x => x)));
                    }
                }
                ImGui.Separator();
            }
            ImGui.Checkbox("Only filter specific players", ref SpecifyPlayersEnabled);
            if (SpecifyPlayersEnabled)
            {
                ImGui.Text("Name: ");
                ImGui.SameLine();
                ImGui.Indent(300);
                ImGui.Text("World: ");
                ImGui.Unindent(300);
                ImGui.InputText("##Name", ref PlayerToAddName);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(180);
                if (ImGui.BeginCombo("###World", string.IsNullOrWhiteSpace(PlayerToAddWorld) ? "Not Selected" : PlayerToAddWorld))
                {
                    foreach (World w in worldSheet.Where(w => w.IsPublic).OrderBy(x => x.Name.ToString()))
                    {
                        if (ImGui.Selectable(w.Name.ToString()))
                        {
                            PlayerToAddWorld = w.Name.ToString();
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                if (ImGui.Button("Add player"))
                {
                    if (string.IsNullOrWhiteSpace(PlayerToAddName) || PlayerToAddName.Count(c => c == ' ') != 1)
                    {
                        AddPlayerError = "Invalid name, please check it again.";
                        return;
                    }
                    else if (string.IsNullOrWhiteSpace(PlayerToAddWorld) || PlayerToAddWorld == "Not Selected")
                    {
                        AddPlayerError = "Invalid world, please check it again.";
                        return;
                    }
                    else
                    {
                        if (!EnabledPlayersToAdd.Contains(PlayerToAddName + "@" + PlayerToAddWorld))
                        {
                            //If we have all players filtered, clear it and add the new person
                            if (EnabledPlayersToAdd.Count() == 1 && EnabledPlayersToAdd.First() == "All") { EnabledPlayersToAdd.Clear(); }
                            EnabledPlayersToAdd.Add(PlayerToAddName + "@" + PlayerToAddWorld);
                        }
                        AddPlayerError = "";
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Remove player"))
                {
                    if (EnabledPlayersToAdd.Contains(PlayerToAddName + "@" + PlayerToAddWorld)) { EnabledPlayersToAdd.Remove(PlayerToAddName + "@" + PlayerToAddWorld); }
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear###ClearEnabledPlayers"))
                {
                    EnabledPlayersToAdd.Clear();
                }
                if (EnabledPlayersToAdd.Count > 0)
                {
                    ImGui.Text("Players to add to the new term filter:");
                    ImGui.TextWrapped(string.Join(", ", EnabledPlayersToAdd.OrderBy(x => x)));
                }
                if (!string.IsNullOrWhiteSpace(AddPlayerError)) { ImGui.TextColored(new System.Numerics.Vector4(1f, 0f, 0f, 1f), AddPlayerError); }
                ImGui.Separator();
            }
            ImGui.Text("Default behavior is to show \"Message could not be displayed due to Term Filter #(Term ID)\".\nIf you would like to override that behavior, you may do so here:");
            if (ImGui.Checkbox("Hide message", ref HideMessageToAdd))
            {
                ReplaceWordInMessageToAdd = false;
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Replace term with other word(s)", ref ReplaceWordInMessageToAdd))
            {
                HideMessageToAdd = false;
            }
            if (ReplaceWordInMessageToAdd)
            {
                ImGui.Text("Word(s) to replace the term:");
                ImGui.SameLine();
                ImGui.InputText("##WordToAdd", ref ReplaceWordToAdd);
                ImGui.SameLine();
                if (ImGui.Button("Add word(s)"))
                {
                    if (string.IsNullOrWhiteSpace(ReplaceWordToAdd))
                    {
                        AddWordError = "Invalid word, please check it again.";
                        return;
                    }
                    else
                    {
                        if (!ReplaceWordsToAdd.Contains(ReplaceWordToAdd)) { ReplaceWordsToAdd.Add(ReplaceWordToAdd); }
                        AddWordError = "";
                        ReplaceWordToAdd = "";
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Remove word(s)"))
                {
                    if (ReplaceWordsToAdd.Contains(ReplaceWordToAdd))
                    {
                        ReplaceWordsToAdd.Remove(ReplaceWordToAdd);
                        ReplaceWordToAdd = "";
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear###ClearReplaceWords"))
                {
                    Thread.Sleep(1);
                    ReplaceWordsToAdd.Clear();
                }
                if (ReplaceWordsToAdd.Count > 0)
                {
                    ImGui.Text("Words that will be randomly picked from to replace the term:");
                    List<string> FixedWordsToAdd = new List<string>();
                    foreach (var thing in ReplaceWordsToAdd)
                    {
                        FixedWordsToAdd.Add("\"" + thing + "\"");
                    }
                    ImGui.TextWrapped(string.Join(", ", FixedWordsToAdd.OrderBy(x => x)));
                }
                if (!string.IsNullOrWhiteSpace(AddWordError)) { ImGui.TextColored(new System.Numerics.Vector4(1f, 0f, 0f, 1f), AddWordError); }
            }

            ImGui.Separator();
            if (ImGui.Button("Add term (or modify term if it exists)"))
            {
                if (string.IsNullOrWhiteSpace(TermToAdd))
                {
                    AddTermError = "You must enter a term to filter.";
                    return;
                }
                if (SpecifyChannelEnabled && EnabledChannelsToAdd.Count == 0)
                {
                    AddTermError = "You must add at least 1 channel to filter if you have channel filters enabled.";
                    return;
                }
                if (SpecifyPlayersEnabled && EnabledPlayersToAdd.Count == 0)
                {
                    AddTermError = "You must add at least 1 player to filter if you have player filters enabled.";
                    return;
                }
                if (ReplaceWordInMessageToAdd && ReplaceWordsToAdd.Count == 0)
                {
                    AddTermError = "You must add at least 1 word to replace if you have replacing the word enabled.";
                    return;
                }
                AddTermError = "";
                //if (string.IsNullOrWhiteSpace(PlayerToAddWorld) || PlayerToAddWorld == "Not Selected")
                //{
                //    Plugin.Chat.Print("Invalid world, please check it again.");
                //    return;
                //}

                TermFilter2Entry? CurrentTermFilter2Entry = Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Find(x => x.TermToFilter.ToLower() == TermToAdd.ToLower());

                //If they don't want to filter by chat channel, add it to all of them
                if (!SpecifyChannelEnabled)
                {
                    EnabledChannelsToAdd.Clear();
                    EnabledChannelsToAdd.AddRange(PlayerChatChannels);
                }
                if (!SpecifyPlayersEnabled)
                {
                    EnabledPlayersToAdd.Clear();
                    EnabledPlayersToAdd.Add("All");
                }

                TermFilter2Entry NewEntry = new TermFilter2Entry { TermToFilter = TermToAdd, EnabledChannels = EnabledChannelsToAdd, EnabledPlayers = EnabledPlayersToAdd, HideMessage = HideMessageToAdd, ReplaceWordInMessage = ReplaceWordInMessageToAdd, ReplaceMessageTerms = ReplaceWordsToAdd };
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
                SpecifyChannelEnabled = false;
                EnableChannelToAdd = XivChatType.None;
                EnabledChannelsToAdd.Clear();
                SpecifyPlayersEnabled = false;
                EnabledPlayersToAdd.Clear();
                HideMessageToAdd = false;
                ReplaceWordInMessageToAdd = false;
                ReplaceWordsToAdd.Clear();
            }
            if (!string.IsNullOrWhiteSpace(AddTermError)) { ImGui.TextColored(new System.Numerics.Vector4(1f, 0f, 0f, 1f), AddTermError); }

            foreach (var Item in ToRemove)
            {
                Plugin.PluginConfig.Terms[Plugin.PlayerState.ContentId].Remove(Item);

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
            if (ImGui.Button("Want to help support my work?"))
            {
                ShowSupport = !ShowSupport;
            }
            if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Click me!"); }
            if (ShowSupport)
            {
                ImGui.Text("Here are the current ways you can support the work I do.\nEvery bit helps, thank you! Have a great day!");
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.19f, 0.52f, 0.27f, 1));
                if (ImGui.Button("Donate via Paypal"))
                {
                    Dalamud.Utility.Util.OpenLink("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=QXF8EL4737HWJ");
                }
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.95f, 0.39f, 0.32f, 1));
                if (ImGui.Button("Become a Patron"))
                {
                    Dalamud.Utility.Util.OpenLink("https://www.patreon.com/bePatron?u=5597973");
                }
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.25f, 0.67f, 0.87f, 1));
                if (ImGui.Button("Support me on Ko-Fi"))
                {
                    Dalamud.Utility.Util.OpenLink("https://ko-fi.com/Y8Y114PMT");
                }
                ImGui.PopStyleColor();
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