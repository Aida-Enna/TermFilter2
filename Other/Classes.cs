using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TermFilter2
{
    public class TermFilterCollection : List<TermFilterEntry>
    {
        public ulong OwnerID { get; set; }
    }

    public class TermFilterEntry
    {
        public string TermToFilter { get; set; }
        public List<XivChatType> EnabledChannels { get; set; }
    }
}
