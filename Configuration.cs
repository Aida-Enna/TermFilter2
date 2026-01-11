using Dalamud.Configuration;
using Dalamud.Plugin;
using System.Collections.Generic;

namespace TermFilter2
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }


        public Dictionary<ulong, TermFilterCollection> Terms { get; set; } = new Dictionary<ulong, TermFilterCollection>();

        private IDalamudPluginInterface pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
