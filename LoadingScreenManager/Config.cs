using System;
using System.IO;
using KSP.UI.Screens;
using System.Collections.Generic;
using UnityEngine;
using JetBrains.Annotations;


namespace LoadingScreenManager
{
    public class Config
    {
        public const string DefaultPath = "Screenshots/";
        private const string ConfigFilePath = "GameData/LoadingScreenManager/PluginData/";
        private const string ConfigFileName = "LoadingScreenManager.cfg";
        private const string LogMessageFormat = "[LSM] {0}";
        public const string DefaultFileMasks = "*.png;*.jpg";

        public string dirSeperator = "\\";
        public string altSeperator = "/";

        #region Configuration Fields
        // -- Configuration fields and their default values.  (If no default below, uses C# default for type.) --

        public bool _debugLogging;
        public bool _dumpTips;
        public bool _dumpScreens;

        public int _totalSlides = 20;
        public bool _includeOriginalScreens = true;
        public bool _forceSlideshowWithNoImageFiles;

        // These are all in seconds.
        public float _displayTime = 5.0f;
        public float _fadeInTime = 0.5f;
        public float _fadeOutTime = 0.5f;
        public float _tipTime = 5.0f;

        public string _tipsFile = "";
        public bool _includeOriginalTips = true;

        public bool _neverShowAgain = false;

        public string _logoScreen = "";
        public List<string> logoScreens = new List<string>();
        public string _logoTip = "";
        public List<string> logoTips = new List<string>();

        #endregion

        public readonly List<LoadingScreenManager.ImageFolder> _imageFolders = new List<LoadingScreenManager.ImageFolder>();

        public Config()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer)
            {
                dirSeperator = "/";
                altSeperator = "\\";
            }

            LoadConfig();
        }
#if false
        /// <summary>
        ///     Convenience method to write debug messages to the log using an addon-specific prefix.
        /// </summary>
        /// <param name="format">As per <see cref="string.Format(string, object[])" /></param>
        /// <param name="args">As per <see cref="string.Format(string, object[])" /></param>
        [StringFormatMethod("format")]
        private void WriteDebugLog(string format, params object[] args)
        {
            if (this._debugLogging) Log.Debug(string.Format(LogMessageFormat, format), args);
        }
        /// <summary>
        ///     Convenience method to write to the log using an addon-specific prefix.
        /// </summary>
        /// <param name="format">As per <see cref="string.Format(string, object[])" /></param>
        /// <param name="args">As per <see cref="string.Format(string, object[])" /></param>
        [StringFormatMethod("format")]
        private void WriteLog(string format, params object[] args)
        {
            Debug.LogFormat(string.Format(LogMessageFormat, format), args);
        }
#endif
        string oldConfigFilePath
        {
            get
            {
                var assembly = AssemblyLoader.loadedAssemblies.GetByAssembly(this.GetType().Assembly);
                return Path.Combine(assembly.dataPath, ConfigFileName).Replace('\\', '/');
            }
        }
        string configFilePath
        {
            get
            {
                return Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, ConfigFilePath), ConfigFileName).Replace('\\', '/');               
            }
        }
        string ExtraCfgFilePath(string s)
        {
            return Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, ConfigFilePath), s).Replace('\\', '/');
        }
        /// <summary>
        ///     Loads the configuration file and sets up default values.
        /// </summary>
        private void LoadConfig()
        {
            // Use the PlugInData path that KSP has earmarked for this DLL.
            
            Log.Info("Loading configuration file:  {0}", configFilePath);

            // Settings from prerelease that's changed in structure.
            var screenshotFolder = DefaultPath;

            // Setup a ConfigNode with the desired defaults (which were set in the field declarations).
            var configNode = new ConfigNode();
            configNode.AddValue("debugLogging", this._debugLogging);
            configNode.AddValue("dumpScreens", this._dumpScreens);
            configNode.AddValue("dumpTips", this._dumpTips);
            configNode.AddValue("totalSlides", this._totalSlides);
            configNode.AddValue("includeOriginalScreens", this._includeOriginalScreens);
            configNode.AddValue("forceSlideshowWithNoImageFiles", this._forceSlideshowWithNoImageFiles);
            configNode.AddValue("displayTime", this._displayTime);
            configNode.AddValue("fadeInTime", this._fadeInTime);
            configNode.AddValue("fadeOutTime", this._fadeOutTime);
            configNode.AddValue("tipTime", this._tipTime);
            configNode.AddValue("tipsFile", this._tipsFile);
            configNode.AddValue("includeOriginalTips", this._includeOriginalTips);
            configNode.AddValue("neverShowAgain", this._neverShowAgain);

            Log.Info("... Loading file");

            // Try to read the ConfigNode from the .cfg file.  If we can, merge it with our created ConfigNode.
            // The advantage of doing it this way is that at the end we always end up with a full config file that can be saved,
            // which is handy for version upgrades or if the user has deleted settings.
            ConfigNode loadedConfigNode = ConfigNode.Load(oldConfigFilePath);
            
            if (loadedConfigNode != null)
            {
                Log.Info("... Old config loaded");
                try
                {
                    File.Delete(oldConfigFilePath);
                }
                catch { }
            }
            else
                loadedConfigNode = ConfigNode.Load(configFilePath);
            if (loadedConfigNode != null)
            {
                Log.Info("... File loaded");
                loadedConfigNode.CopyTo(configNode, true);
                // CopyTo doesn't seem to deal with multiple nodes so it'll only copy one.  Thus, we have to copy them manually.
                configNode.RemoveNodes("FOLDER");
                var loadedFolderConfigNodes = loadedConfigNode.GetNodes("FOLDER") ?? new ConfigNode[0];
                foreach (var loadedFolderConfigNode in loadedFolderConfigNodes)
                {
                    configNode.AddNode(loadedFolderConfigNode);
                }
            }
            else Log.Info("... No file found, using default configuration");

            // Get legacy settings in case of old version, they will be upgraded.
            configNode.TryGetValue("screenshotFolder", ref screenshotFolder);
            configNode.TryGetValue("slidesToAdd", ref this._totalSlides);
            configNode.TryGetValue("runWithNoScreenshots", ref this._forceSlideshowWithNoImageFiles);

            // Now pull out values.
            configNode.TryGetValue("debugLogging", ref this._debugLogging);
            configNode.TryGetValue("dumpScreens", ref this._dumpScreens);
            configNode.TryGetValue("dumpTips", ref this._dumpTips);
            configNode.TryGetValue("totalSlides", ref this._totalSlides);
            configNode.TryGetValue("includeOriginalScreens", ref this._includeOriginalScreens);
            configNode.TryGetValue("forceSlideshowWithNoImageFiles", ref this._forceSlideshowWithNoImageFiles);
            configNode.TryGetValue("displayTime", ref this._displayTime);
            configNode.TryGetValue("fadeInTime", ref this._fadeInTime);
            configNode.TryGetValue("fadeOutTime", ref this._fadeOutTime);
            configNode.TryGetValue("tipTime", ref this._tipTime);
            configNode.TryGetValue("tipsFile", ref this._tipsFile);
            this._tipsFile = this._tipsFile.Replace(altSeperator, dirSeperator);
            configNode.TryGetValue("includeOriginalTips", ref this._includeOriginalTips);
            configNode.TryGetValue("neverShowAgain", ref this._neverShowAgain);

            configNode.TryGetValue("logoScreen", ref this._logoScreen);
            if (this._logoScreen != null)
            {
                logoScreens.Add(this._logoScreen);
                configNode.TryGetValue("logoTip", ref this._logoTip);
                logoTips.Add(this._logoTip ?? " ");
            }
            var folderConfigNodes = configNode.GetNodes("FOLDER");

            // If no folders defined, add a subnode for a default folder.
            if (folderConfigNodes == null || folderConfigNodes.Length == 0)
            {
                var folderConfigNode = new ConfigNode("FOLDER");
                // Use the prerelease setting, which will have the normal default if it's not found.
                folderConfigNode.AddValue("path", screenshotFolder);
                folderConfigNode.AddValue("fileMasks", DefaultFileMasks);
                folderConfigNode.AddValue("ignoreSubfolders", default(bool));
                configNode.AddNode(folderConfigNode);

                folderConfigNodes = new[] { folderConfigNode };
            }

            // Now read all other config files in the same location, look for FOLDER nodes
            string[] cfgFiles = Directory.GetFiles(ConfigFilePath,  "*.cfg");
            foreach (var f in cfgFiles)
            {
                // don't reprocess the same config file again
                if (f.Replace('\\', '/') != configFilePath)
                {
                    Log.Info("extra files: " + f);
                    loadedConfigNode = ConfigNode.Load(ExtraCfgFilePath(f));
                    if (loadedConfigNode != null)
                    {
                        Log.Info("... File loaded: " + ExtraCfgFilePath(f));

                        // Need to save all the logo screens and logo tips into arrays

                        configNode.TryGetValue("logoScreen", ref this._logoScreen);
                        if (this._logoScreen != null)
                            logoScreens.Add(this._logoScreen);
                        configNode.TryGetValue("logoTip", ref this._logoTip);
                        if (this._logoTip == null && this._logoScreen != null)
                            this._logoTip = "";
                        if (this._logoTip != null)
                            logoTips.Add(this._logoTip);

                        var loadedFolderConfigNodes = loadedConfigNode.GetNodes("FOLDER") ?? null;
                        if (loadedFolderConfigNodes != null)
                            foreach (var loadedFolderConfigNode in loadedFolderConfigNodes)
                            {
                                configNode.AddNode(loadedFolderConfigNode);
                            }
                    }
                }
            }

            // Translate the folder config nodes into a more convenient form.
            foreach (var folderConfigNode in folderConfigNodes)
            {
                var imageFolder = new LoadingScreenManager.ImageFolder();
                folderConfigNode.TryGetValue("path", ref imageFolder.path);
                imageFolder.path = imageFolder.path.Replace(altSeperator, dirSeperator);
                folderConfigNode.TryGetValue("fileMasks", ref imageFolder.fileMasks);
                folderConfigNode.TryGetValue("ignoreSubfolders", ref imageFolder.ignoreSubfolders);
                if (Directory.Exists(imageFolder.path))
                {
                    this._imageFolders.Add(imageFolder);
                }
            }

            // Remove legacy settings.
            configNode.RemoveValue("screenshotFolder");
            configNode.RemoveValue("slidesToAdd");
            configNode.RemoveValue("runWithNoScreenshots");
            SaveConfig(configNode);
        }

        public void SaveConfig()
        {
            var configNode = new ConfigNode();
            configNode.AddValue("debugLogging", this._debugLogging);
            configNode.AddValue("dumpScreens", this._dumpScreens);
            configNode.AddValue("dumpTips", this._dumpTips);
            configNode.AddValue("totalSlides", this._totalSlides);
            configNode.AddValue("includeOriginalScreens", this._includeOriginalScreens);
            configNode.AddValue("forceSlideshowWithNoImageFiles", this._forceSlideshowWithNoImageFiles);
            configNode.AddValue("displayTime", this._displayTime);
            configNode.AddValue("fadeInTime", this._fadeInTime);
            configNode.AddValue("fadeOutTime", this._fadeOutTime);
            configNode.AddValue("tipTime", this._tipTime);
            configNode.AddValue("tipsFile", this._tipsFile.Replace("\\", "/"));
            configNode.AddValue("includeOriginalTips", this._includeOriginalTips);
            configNode.AddValue("neverShowAgain", this._neverShowAgain);

            configNode.AddValue("logoScreen", this._logoScreen);
            configNode.AddValue("logoTip", this._logoTip);


            foreach (var imageFolder in this._imageFolders)
            {
                var folderConfigNode = new ConfigNode("FOLDER");
                folderConfigNode.AddValue("path", imageFolder.path.Replace("\\","/"));
                folderConfigNode.AddValue("fileMasks", imageFolder.fileMasks);
                folderConfigNode.AddValue("ignoreSubfolders", imageFolder.ignoreSubfolders);
                configNode.AddNode(folderConfigNode);
                folderConfigNode = null;
            }
           
            SaveConfig(configNode);
        }


        public void SaveConfig(ConfigNode configNode)
        {
            // Done, now save the modified configuration back to disk.
            Log.Info("... Saving configuration file:\n{0}", configNode);
            var directoryName = Path.GetDirectoryName(configFilePath);
            if (directoryName != null) Directory.CreateDirectory(directoryName);
            configNode.Save(configFilePath);

            Log.Info("... Done");
        }
    }

}
