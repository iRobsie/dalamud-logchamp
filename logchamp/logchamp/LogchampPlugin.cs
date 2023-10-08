using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace logchamp
{
    public class LogchampPlugin : IDalamudPlugin
    {
        public string Name => "LogChamp";
        private const string commandName = "/logs";
        private static bool drawConfiguration;

        private Configuration configuration;
        private Configuration.Timeframe configResetLogs;

        // Define the variables in your class
        private string ActDirectory = "%appdata%"; // Initialize to an empty string
        private string IinactDirectory = "%appdata%"; // Initialize to an empty string

        [PluginService] private DalamudPluginInterface PluginInterface { get; set; } = null!;
        [PluginService] private ICommandManager CommandManager { get; set; } = null!;
        [PluginService] private IChatGui chatGui { get; set; } = null!; // Add this line to declare chatGui

        private bool cleanedOnStartup;

        public LogchampPlugin([RequiredVersion("1.0")] DalamudPluginInterface dalamudPluginInterface, [RequiredVersion("1.0")] IChatGui chatGui, [RequiredVersion("1.0")] ICommandManager commandManager)
        {
            this.chatGui = chatGui;

            configuration = (Configuration)dalamudPluginInterface.GetPluginConfig() ?? new Configuration();
            configuration.Initialize(dalamudPluginInterface);

            LoadConfiguration();

            dalamudPluginInterface.UiBuilder.Draw += DrawConfiguration;
            dalamudPluginInterface.UiBuilder.OpenConfigUi += OpenConfig;

            chatGui.ChatMessage += OnChatMessage;

            commandManager.AddHandler(commandName, new CommandInfo(CleanupCommand)
            {
                HelpMessage = "opens the configuration",
                ShowInHelp = true
            });
        }

        private void OnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
        {
            if (type == XivChatType.Notice && !cleanedOnStartup)
            {
                Task.Run(() => DeleteLogs(configResetLogs, ActDirectory, IinactDirectory));
                cleanedOnStartup = true;
            }
        }

        private void CleanupCommand(string command, string args)
        {
            OpenConfig();

            Task.Run(() => DeleteLogs(configuration.DeleteAfterTimeframeAct, ActDirectory, IinactDirectory));
        }

        private async Task DeleteLogs(Configuration.Timeframe timeframe, string actDirectory, string iinactDirectory)
        {
            var logsDirectoryInfo = new DirectoryInfo(actDirectory);

            if (!logsDirectoryInfo.Exists)
            {
                chatGui.Print($"{Name}: couldn't find directory, please check the configuration -> /logs");
                return;
            }

            var deucalionDirectoryInfo = new DirectoryInfo(iinactDirectory);
            var initialSize = await Task.Run(() => logsDirectoryInfo.GetTotalSize("*.log") + deucalionDirectoryInfo.GetTotalSize("*.log"));
            var filesToDelete = logsDirectoryInfo.GetFilesOlderThan(timeframe).ToList();
            filesToDelete.AddRange(deucalionDirectoryInfo.GetFilesOlderThan(timeframe).ToList());

            if (filesToDelete.Count == 0)
                return;

            foreach (var file in filesToDelete)
            {
                try
                {
                    if (file.Exists && !file.IsReadOnly)
                        file.Delete();
                }
                catch (Exception exception)
                {
                    chatGui.Print($"{Name}: error deleting {file.Name}\n{exception}");
                }
            }

            logsDirectoryInfo = new DirectoryInfo(actDirectory);
            deucalionDirectoryInfo = new DirectoryInfo(iinactDirectory);
            var newSize = await Task.Run(() => logsDirectoryInfo.GetTotalSize("*.log") + deucalionDirectoryInfo.GetTotalSize("*.log"));

            chatGui.Print($"{Name}: deleted {filesToDelete.Count} log(s) older than {timeframe.ToName()} with a total size of {(initialSize - newSize).FormatFileSize()}");
        }

        private void DrawConfiguration()
        {
            if (!drawConfiguration)
                return;

            ImGui.Begin($"{Name} Configuration", ref drawConfiguration);

            ImGui.InputText("Act Directory", ref ActDirectory, 256);
            ImGui.InputText("Iinact Directory", ref IinactDirectory, 256);
            SaveConfiguration();



            bool isActTimeframeOpen = false;
            bool isIinactTimeframeOpen = false;




            var actDirectoryInfo = new DirectoryInfo(ActDirectory);
            var iinactDirectoryInfo = new DirectoryInfo(IinactDirectory);

            if (actDirectoryInfo.Exists)
            {
                var sevenAct = actDirectoryInfo.GetFilesOlderThan(configResetLogs).ToList();
                var thirtyAct = actDirectoryInfo.GetFilesOlderThan(configResetLogs).ToList();
                var ninetyAct = actDirectoryInfo.GetFilesOlderThan(configResetLogs).ToList();

                ImGui.TextDisabled($"Logs older than 7 days in Act Directory: {sevenAct.Count} files - {sevenAct.Sum(file => file.Length).FormatFileSize()}");
                ImGui.TextDisabled($"Logs older than 30 days in Act Directory: {thirtyAct.Count} files - {thirtyAct.Sum(file => file.Length).FormatFileSize()}");
                ImGui.TextDisabled($"Logs older than 90 days in Act Directory: {ninetyAct.Count} files - {ninetyAct.Sum(file => file.Length).FormatFileSize()}");
            }
            else
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Act directory doesn't exist");








            ImGui.Text("Delete logs for Act Directory after");
            ImGui.SameLine();
            Enum timeframeRef = configResetLogs;
            if (DrawEnumCombo(ref timeframeRef))
            {
                configResetLogs = (Configuration.Timeframe)timeframeRef;
                SaveConfiguration();
            }








            ImGui.End();
        }



        private void SaveConfiguration()
        {
            configuration.ActDirectory = ActDirectory;
            configuration.IinactDirectory = IinactDirectory;
            configuration.DeleteAfterTimeframeAct = configResetLogs;
            configuration.DeleteAfterTimeframeIinact = configResetLogs;
            PluginInterface.SavePluginConfig(configuration);
        }

        private void LoadConfiguration()
        {
            ActDirectory = configuration.ActDirectory;
            IinactDirectory = configuration.IinactDirectory;
            configResetLogs = configuration.DeleteAfterTimeframeAct;
            configResetLogs = configuration.DeleteAfterTimeframeIinact;
        }

        private static bool DrawEnumCombo(ref Enum comboEnum)
        {
            var names = Enum.GetNames(comboEnum.GetType());
            var values = Enum.GetValues(comboEnum.GetType());
            var index = Array.IndexOf(values, comboEnum);

            if (ImGui.Combo("##combo", ref index, names, names.Length))
            {
                comboEnum = (Enum)values.GetValue(index);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= DrawConfiguration;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
            chatGui.ChatMessage -= OnChatMessage;

            CommandManager.RemoveHandler(commandName);
        }

        private void OpenConfig()
        {
            drawConfiguration = !drawConfiguration;
        }
    }
}
