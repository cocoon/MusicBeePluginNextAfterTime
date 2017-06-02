using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();

        private bool debugging = false;

        private string ConfigFileName = "PluginSettingsNextAfterTime.cfg";

        private int configuredTime = 30;
        public int ConfiguredTime
        {
            get { return configuredTime; }
            set { configuredTime = value; }
        }

        private int currentTime = 0;
        public int CurrentTime
        {
            get { return currentTime; }
            set { currentTime = value; }
        }

        private System.Timers.Timer plgTimer;
        public System.Timers.Timer PlgTimer
        {
            get { return plgTimer; }
            set { plgTimer = value; }
        }

        public TextBox ConfiguredTimeTextBox;


        public Plugin.MB_SendNotificationDelegate SendNotificationHandler;


        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);

            this.SendNotificationHandler = mbApiInterface.MB_SendNotification;


            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "NextAfterTime";
            about.Description = "Automatically go to the next track in the playlist after X seconds.";
            about.Author = "cocoon@github";
            about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            about.Type = PluginType.General;
            about.VersionMajor = 1;  // your plugin version
            about.VersionMinor = 0;
            about.Revision = 1;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            about.ConfigurationPanelHeight = 50;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function
            return about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            // save any persistent settings in a sub-folder of this path
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            if (panelHandle != IntPtr.Zero)
            {
                Panel configPanel = (Panel)Panel.FromHandle(panelHandle);
                Label prompt = new Label();
                prompt.AutoSize = true;
                prompt.Location = new Point(0, 0);
                prompt.Text = "Time in seconds:";
                ConfiguredTimeTextBox = new TextBox();
                ConfiguredTimeTextBox.Bounds = new Rectangle(160, 0, 100, ConfiguredTimeTextBox.Height);
                ConfiguredTimeTextBox.Text = ConfiguredTime.ToString();

                configPanel.Controls.AddRange(new Control[] { prompt, ConfiguredTimeTextBox });
            }
            return false;
        }


        public void ReadSettings()
        {
            string dataPath = "";
            string FullConfigFilePath = "";

            try
            {
                // any persistent settings are in a sub-folder of this path
                dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
                FullConfigFilePath = System.IO.Path.Combine(dataPath, ConfigFileName);

            }
            catch (Exception ex)
            {
                MessageBox.Show("ReadSettings ERROR: " + ex.ToString());
            }

            int outPutInt = -1;
            int LineCounter = 0;

            try
            {
                if (File.Exists(FullConfigFilePath))
                {
                    using (FileStream fs = File.Open(FullConfigFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (BufferedStream bs = new BufferedStream(fs))
                    using (StreamReader sr = new StreamReader(bs))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null && LineCounter == 0)
                        {
                            if (line.Length <= 9)
                            {
                                Int32.TryParse(line, out outPutInt);
                            }
                            else
                            {
                               MessageBox.Show("Plugin ERROR ReadSettings: Settingsfile line contains too many characters!");
                            }
                            LineCounter++;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Plugin ERROR ReadSettings: Settingsfile does not exist: " + FullConfigFilePath);
                }

                if(outPutInt > 0)
                {
                    ConfiguredTime = outPutInt;
                    if (ConfiguredTimeTextBox != null) ConfiguredTimeTextBox.Text = ConfiguredTime.ToString();
                }
                else
                {
                    MessageBox.Show("Plugin ERROR ReadSettings: Settingsfile line contains no valid number!");
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Plugin ERROR ReadSettings: " + ex.ToString());
            }
            

        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            try
            {
                // save any persistent settings in a sub-folder of this path
                string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
                string FullConfigFilePath = System.IO.Path.Combine(dataPath, ConfigFileName);

                if (ConfiguredTimeTextBox != null)
                {
                    int outPutInt = -1;
                    Int32.TryParse(ConfiguredTimeTextBox.Text, out outPutInt);

                    if (outPutInt > 0)
                    {
                        ConfiguredTime = outPutInt;
                        if(debugging) MessageBox.Show("Plugin SaveSettings: ConfiguredTime: " + ConfiguredTime);
                        TimerSetup();
                    }
                    else
                    {
                        MessageBox.Show("Plugin ERROR SaveSettings: Settingsfile line contains no valid number!");
                    }
                }
                else
                {
                    MessageBox.Show("Plugin ERROR SaveSettings: ConfiguredTimeTextBox was null!");
                }

                if (debugging) MessageBox.Show("SaveSettings clicked, try to save to: " + FullConfigFilePath);


                try
                {
                    System.IO.File.WriteAllText(FullConfigFilePath, ConfiguredTime.ToString());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Plugin ERROR SaveSettings: " + ex.ToString());
                }

                if (SendNotificationHandler != null) SendNotificationHandler.Invoke(CallbackType.SettingsUpdated);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Plugin ERROR SaveSettings: " + ex.ToString());
            }

            
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
            if (PlgTimer != null) PlgTimer.Stop();
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
            try
            {
                string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
                string FullConfigFilePath = System.IO.Path.Combine(dataPath, ConfigFileName);
                if (File.Exists(FullConfigFilePath))
                {
                    File.Delete(FullConfigFilePath);
                }
            }
            catch (Exception ex)
            { }
        }


        public void TimerSetup()
        {
            if (PlgTimer != null)
            {
                PlgTimer.Stop();
                PlgTimer.Interval = ConfiguredTime * 1000; // in Miliseconds
                PlgTimer.Enabled = true;
            }
            else
            {
                PlgTimer = new System.Timers.Timer();
                PlgTimer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
                PlgTimer.Interval = ConfiguredTime * 1000; // in Miliseconds
                PlgTimer.Enabled = true;
            }

            if (SendNotificationHandler != null) SendNotificationHandler.Invoke(CallbackType.SettingsUpdated);

            if (PlgTimer != null && debugging) MessageBox.Show("Plugin TimerSetup() called with Interval: " + PlgTimer.Interval);
            mbApiInterface.MB_SetBackgroundTaskMessage("#### Plugin NextAfterTime: skip to next track after " + ConfiguredTime + " seconds.");
        }

        // Specify what you want to happen when the Elapsed event is raised.
        private void OnTimedEvent(object source, System.Timers.ElapsedEventArgs e)
        {
            if (mbApiInterface.Player_GetPlayState() == PlayState.Playing)
            {
                mbApiInterface.Player_PlayNextTrack();
            }
            else
            {
                PlgTimer.Stop();
            }
        }


        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:

                    //Load Settings from Settingsfile AFTER  mbApiInterface.Initialise(apiInterfacePtr);
                    try
                    {
                        ReadSettings();
                        TimerSetup();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Initialise ERROR ReadSettings: " + ex.ToString());
                    }

                    // perform startup initialisation
                    switch (mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                            if (debugging) MessageBox.Show("Plugin NotificationType.PluginStartup case PlayState.Playing");
                            break;
                        case PlayState.Paused:
                            // ...
                            if (PlgTimer != null) PlgTimer.Stop();
                            break;
                        case PlayState.Stopped:
                            // ...
                            if (PlgTimer != null) PlgTimer.Stop();
                            break;
                    }
                    break;
                case NotificationType.TrackChanged:
                    string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                    // ...
                    break;

                case NotificationType.PlayStateChanged:
                    // perform startup initialisation
                    switch (mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                            if (debugging) MessageBox.Show("Plugin NotificationType.PlayStateChanged case PlayState.Playing");
                            TimerSetup();
                            PlgTimer.Start();
                            break;
                        case PlayState.Paused:
                            // ...
                            if (PlgTimer != null) PlgTimer.Stop();
                            break;
                        case PlayState.Stopped:
                            // ...
                            if (PlgTimer != null) PlgTimer.Stop();
                            break;
                    }
                    break;
            }
        }

        // return an array of lyric or artwork provider names this plugin supports
        // the providers will be iterated through one by one and passed to the RetrieveLyrics/ RetrieveArtwork function in order set by the user in the MusicBee Tags(2) preferences screen until a match is found
        public string[] GetProviders()
        {
            return null;
        }

        // return lyrics for the requested artist/title from the requested provider
        // only required if PluginType = LyricsRetrieval
        // return null if no lyrics are found
        public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album, bool synchronisedPreferred, string provider)
        {
            return null;
        }

        // return Base64 string representation of the artwork binary data from the requested provider
        // only required if PluginType = ArtworkRetrieval
        // return null if no artwork is found
        public string RetrieveArtwork(string sourceFileUrl, string albumArtist, string album, string provider)
        {
            //Return Convert.ToBase64String(artworkBinaryData)
            return null;
        }
   }
}