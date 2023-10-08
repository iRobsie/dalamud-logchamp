using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace logchamp
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }
        public string ActDirectory { get; set; } = $@"C:\Users\{Environment.UserName}\AppData\Roaming\Advanced Combat Tracker\FFXIVLogs";
        public string DeucalionDirectory { get; set; } = Environment.ExpandEnvironmentVariables("%AppData%\\deucalion");
        public string IinactDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\IINACT";
        public enum Timeframe
        {
            Seven,
            Fourteen,
            Thirty,
            Sixty,
            Ninety
        }

        public Timeframe DeleteAfterTimeframeAct { get; set; } = Timeframe.Thirty;
        public Timeframe DeleteAfterTimeframeIinact { get; set; } = Timeframe.Thirty;

        private DalamudPluginInterface _pluginInterface;

        public void Initialize(DalamudPluginInterface pInterface)
        {
            _pluginInterface = pInterface;
        }

        public void Save()
        {
            _pluginInterface.SavePluginConfig(this);
        }
    }
}
