using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using BarRaider.SdTools;
using H.ProxyFactory;
using Newtonsoft.Json.Linq;
using SBOutputController.Shared;

namespace SBController.Plugin
{
    [PluginActionId("com.cheerpipe.sboutputchanger")]
    public class PluginAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings();
                return instance;
            }
        }

        #region Private Members

        private readonly PluginSettings _settings;

        #endregion
        public PluginAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this._settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this._settings = payload.Settings.ToObject<PluginSettings>();
            }

            Connection.OnApplicationDidLaunch += Connection_OnApplicationDidLaunch;
            Connection.OnApplicationDidTerminate += Connection_OnApplicationDidTerminate;
            Connection.OnDeviceDidConnect += Connection_OnDeviceDidConnect;
            Connection.OnDeviceDidDisconnect += Connection_OnDeviceDidDisconnect;
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            Connection.OnPropertyInspectorDidDisappear += Connection_OnPropertyInspectorDidDisappear;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            Connection.OnTitleParametersDidChange += Connection_OnTitleParametersDidChange;

            InitializeSbService().Wait();
        }

        private static ISbControllerService _sbControllerService;

        private void Connection_OnTitleParametersDidChange(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.TitleParametersDidChange> e)
        {
        }

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
        }

        private void Connection_OnPropertyInspectorDidDisappear(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidDisappear> e)
        {
        }

        private void Connection_OnPropertyInspectorDidAppear(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
        {
        }

        private void Connection_OnDeviceDidDisconnect(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.DeviceDidDisconnect> e)
        {
        }

        private void Connection_OnDeviceDidConnect(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.DeviceDidConnect> e)
        {
           
        }

        private void Connection_OnApplicationDidTerminate(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.ApplicationDidTerminate> e)
        {

        }

        private void Connection_OnApplicationDidLaunch(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.ApplicationDidLaunch> e)
        {
            InitializeSbService().Wait();
        }

        public override void Dispose()
        {

            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _factory.DisposeAsync();
            Logger.Instance.LogMessage(TracingLevel.INFO, @"Killing SBOutputController.Server");
            foreach (var process in Process.GetProcessesByName("SBOutputController.Server"))
            {
                process.Kill();
            }
            Connection.OnApplicationDidLaunch -= Connection_OnApplicationDidLaunch;
            Connection.OnApplicationDidTerminate -= Connection_OnApplicationDidTerminate;
            Connection.OnDeviceDidConnect -= Connection_OnDeviceDidConnect;
            Connection.OnDeviceDidDisconnect -= Connection_OnDeviceDidDisconnect;
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            Connection.OnPropertyInspectorDidDisappear -= Connection_OnPropertyInspectorDidDisappear;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor called");
        }

        private readonly PipeProxyFactory _factory = new();
        private readonly Timer _refreshTimer = new Timer();

        public bool Initialized => _sbControllerService != null && _refreshTimer.Enabled;
        public async Task InitializeSbService()
        {
            if (Initialized)
                return;

            string serverFilePath = "server\\SBOutputController.Server.exe";
            if (!File.Exists(serverFilePath))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Server executable can't be found at {serverFilePath}");
                return;
            }

            Process.Start(serverFilePath);
            Logger.Instance.LogMessage(TracingLevel.INFO, $"SBOutputController.Server started");

            try
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, @"Creating ISBControllerService");
                await _factory.InitializeAsync("SBOutputController");
                _sbControllerService =
                    await _factory.CreateInstanceAsync<SbControllerService, ISbControllerService>();
                _sbControllerService.OutputChanged += _sbControllerService_OutputChanged;
                Logger.Instance.LogMessage(TracingLevel.INFO,
                    _sbControllerService != null
                        ? "ISBControllerService created"
                        : "ISBControllerService not created but without exceptions");
            }
            catch (Exception e)
            {
                _sbControllerService = null;
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error while creating ISBControllerService. Exception: {e}");
            }

            _refreshTimer.Interval = 1000;
            _refreshTimer.Elapsed += async (_, _) => { await UpdateIcon(); };
            _refreshTimer.Start();

            await UpdateIcon();
        }

        private async void _sbControllerService_OutputChanged(object sender, OutputModeChangedEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Audio output changed. Updating icon");
            await UpdateIcon();
        }

        public override async void KeyPressed(KeyPayload payload)
        {
            //await InitializeSbService();
            if (_sbControllerService.GetCurrentOutputMode() == DeviceOutputModes.Headphones)
                _sbControllerService.SetSpeakers();
            else
                _sbControllerService.SetHeadphones();
            await UpdateIcon();
        }

        private DeviceOutputModes _currentMode;
        private async Task UpdateIcon()
        {
            //await InitializeSbService();
            DeviceOutputModes newMode = _sbControllerService.GetCurrentOutputMode();

            if (_currentMode == newMode)
                return;

            var iconName = newMode == DeviceOutputModes.Headphones ? "Headphones" : "Speakers";
            string imagesPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            using (Image outputIcon = Image.FromFile(
                       $@"{imagesPath}\Images\{iconName}.png"))
            {
                await Connection.SetImageAsync(outputIcon);
            }
            _currentMode = newMode;
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick() { }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(_settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void SaveSettings()
        {
            Connection.SetSettingsAsync(JObject.FromObject(_settings));
        }

        #endregion
    }
}