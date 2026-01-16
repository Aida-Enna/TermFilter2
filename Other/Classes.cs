using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TermFilter2
{
    public class TermFilter2Collection : List<TermFilter2Entry>
    {
        public ulong OwnerID { get; set; }
    }

    public class TermFilter2Entry
    {
        public string TermToFilter { get; set; }
        public List<XivChatType> EnabledChannels { get; set; } = new List<XivChatType>();
        public List<string> EnabledPlayers { get; set; } = new List<string>();
        public bool HideMessage { get; set; }
        public bool ReplaceWordInMessage { get; set; }
        public List<string> ReplaceMessageTerms { get; set; } = new List<string>();
    }
}
