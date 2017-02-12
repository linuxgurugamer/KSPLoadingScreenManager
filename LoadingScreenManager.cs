// ***************************************************************************
// LoadingScreenManager mod for Kerbal Space Program
// Copyright (C) 2017 @paulprogart
// Released under CC-BY-NC-SA licence - see https://creativecommons.org/licenses/by-nc-sa/4.0/
//
// NOTE: This mod is written using C# 6.0 syntax so it requires Visual Studio 2015 or compatible.
// NOTE: This mod contains ReSharper annotation attributes (which luckily UnityEngine exposes, so it saves providing a file).
// ***************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace LoadingScreenManager
{
    /// <summary>
    ///     LoadingScreenManager mod for Kerbal Space Program
    /// </summary>
    /// <remarks>
    ///     Allows users to add their own loading images and show them as a slideshow, plus some other related stuff.
    /// </remarks>
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    [UsedImplicitly]
    public class LoadingScreenManager : MonoBehaviour
    {
        #region Nested Structs

        /// <summary>
        ///     Holds the configuration for an image folder definition.
        /// </summary>
        private struct ImageFolder
        {
            /// <summary>
            ///     Path relative to the KSP installation folder, including trailing / (slash).
            /// </summary>
            public string path;
            /// <summary>
            ///     File masks (e.g. *.jpg).  Multiples are allowed if separated by ; (semicolon).
            /// </summary>
            public string fileMasks;
            /// <summary>
            ///     If true, searches only the given path and not any subfolders within it.
            /// </summary>
            public bool ignoreSubfolders;
        }

        #endregion

        #region Nested Classses

        /// <summary>
        ///     Dummy loader that instantly reports itself complete.
        /// </summary>
        /// <remarks>
        ///     Best I can tell, KSP needs one loader per screen it shows, hence this class.
        /// </remarks>
        private sealed class DummyLoader : LoadingSystem
        {
            public override bool IsReady()
            {
                return true;
            }

            public override float ProgressFraction()
            {
                return 1f;
            }
        }

        #endregion

        #region Constants

        private const string Version = "v0.91 [PRERELEASE]";
        private const string ConfigFileName = "LoadingScreenManager.cfg";
        private const string LogMessageFormat = "[LSM] {0}";

        private const string DefaultPath = "Screenshots/";
        private const string DefaultFileMasks = "*.png;*.jpg";

        #endregion

        #region Configuration Fields

        private readonly List<ImageFolder> _imageFolders = new List<ImageFolder>();

        // -- Configuration fields and their default values.  (If no default below, uses C# default for type.) --

        // TODO: Debug Logging is ENABLED for prerelease... normally will want this false.
        private bool _debugLogging = true;
        private bool _dumpTips;
        private bool _dumpScreens;

        private int _slidesToAdd = 50;
        private bool _includeOriginalScreens = true;
        private bool _runWithNoScreenshots;

        // These are all in seconds.
        private float _displayTime = 5.0f;
        private float _fadeInTime = 0.5f;
        private float _fadeOutTime = 0.5f;
        private float _tipTime = 5.0f;

        private string _tipsFile = "";
        private bool _includeOriginalTips = true;

        #endregion

        #region Public Methods

        /// <summary>
        ///     Initialization code - called by Unity.
        /// </summary>
        /// <remarks>
        ///     All core logic is called from here since we want to make our changes before everything gets running...
        /// </remarks>
        [UsedImplicitly]
        public void Awake()
        {
            this.WriteLog("LoadingScreenManager {0} activated", Version);
            if (this._debugLogging) this.WriteStartupDebuggingInfo();

            this.LoadConfig();

            this.ModifyLoadingScreen();

            this.WriteLog("LoadingScreenManager {0} finished", Version);
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     The core of the program that replaces the default LoadingScreen information with our customizations.
        /// </summary>
        private void ModifyLoadingScreen()
        {
            // We are going to rebuild the loaders and screens, so first grab the existing ones.
            // TODO: ID logo & normal dynamically in case other mods or future versions change the count/order...

            // The first loader/screen that shows the SQUAD logo.
            // *** IMPORTANT NOTE ***
            // Under KSP forum rules, mods may not modify the in-games logos, so please don't submit anything that makes
            // changes to these.  All we are doing here is saving them so we can add them back later.
            var logoLoader = LoadingScreen.Instance.loaders[0];
            var logoScreen = LoadingScreen.Instance.Screens[0];

            // The second screen is the one we want to tweak.  Note we don't need to do anything to the loader.
            var normalLoadScreen = LoadingScreen.Instance.Screens[1];

            var screenshotTextures = this.LoadImageTextures();
            var customTips = this.LoadCustomTips();
            if (this._includeOriginalTips) customTips.AddRange(normalLoadScreen.tips);
            normalLoadScreen.tips = customTips.ToArray();

            // TODO: Rename runWithNoScreenshots to something more relevant, since the tips stuff now always runs
            if (this._runWithNoScreenshots || screenshotTextures.Count > 0)
            {
                if (this._includeOriginalScreens) screenshotTextures.AddRange(normalLoadScreen.screens);
                var screenTextures = screenshotTextures.ToArray();

                // Change the normal loading screen to use the timings and images.
                // The default is to leave this screen up for 40 minutes, so if we didn't change it the slideshow wouldn't work!
                normalLoadScreen.displayTime = this._displayTime;
                normalLoadScreen.fadeInTime = this._fadeInTime;
                normalLoadScreen.fadeOutTime = this._fadeOutTime;
                normalLoadScreen.tipTime = this._tipTime;
                normalLoadScreen.screens = screenTextures;

                var newLoaders =
                    new List<LoadingSystem>(this._slidesToAdd + LoadingScreen.Instance.Screens.Count) { logoLoader };
                var newScreens =
                    new List<LoadingScreen.LoadingScreenState>(this._slidesToAdd + LoadingScreen.Instance.loaders.Count)
                    {
                        logoScreen
                    };

                for (var i = 0; i < this._slidesToAdd; i++)
                {
                    newLoaders.Add(new DummyLoader());
                    newScreens.Add(new LoadingScreen.LoadingScreenState
                    {
                        displayTime = this._displayTime,
                        fadeInTime = this._fadeInTime,
                        fadeOutTime = this._fadeOutTime,
                        tipTime = this._tipTime,
                        screens = screenTextures,
                        tips = normalLoadScreen.tips
                    });
                }

                // Copy 
                newLoaders.AddRange(LoadingScreen.Instance.loaders.Skip(1));
                newScreens.AddRange(LoadingScreen.Instance.Screens.Skip(1));

                LoadingScreen.Instance.loaders = newLoaders;
                LoadingScreen.Instance.Screens = newScreens;
                this.WriteDebugLog("{0} loading screens set (including existing)", newScreens.Count);
            }
        }

        /// <summary>
        ///     Loads the images from disk.
        /// </summary>
        /// <returns>List of Unity textures - will be empty on error or if no files exist.</returns>
        [NotNull]
        private List<Texture2D> LoadImageTextures()
        {
            var textures = new List<Texture2D>();
            foreach (var imageFolder in this._imageFolders)
            {
                var searchOption = imageFolder.ignoreSubfolders ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;

                // Unity always uses a forward slash in paths so these must be changed on backslash OSs (e.g. Windows).
                var path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", imageFolder.path).Replace('\\', '/');

                // TODO: Clean up logging for multi-folder.
                this.WriteLog("Image path:  {0}", path);

                // Can only use one mask at a time so we have to iterate.
                foreach (var filenames in imageFolder.fileMasks
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(fm => Directory.GetFiles(path, fm.Trim(), searchOption) ?? new string[0]))
                {
                    this.WriteLog("{0} screenshot files found", filenames.Length);

                    foreach (var filename in filenames.Select(fn => fn.Replace('\\', '/')))
                    {
                        this.WriteDebugLog("Loading screenshot file:  {0}", filename);
                        using (var fileStream = new FileStream(filename, FileMode.Open))
                        {
                            this.WriteDebugLog("... File opened");
                            var bytes = new byte[fileStream.Length];
                            fileStream.Read(bytes, 0, (int) fileStream.Length);
                            this.WriteDebugLog("... File read");

                            // This is right out of the Unity documentation for Texture2D.LoadImage().
                            var texture = new Texture2D(2, 2);
                            if (texture.LoadImage(bytes)) textures.Add(texture);
                            else this.WriteLog("!!! ERROR - Screenshot cannot be loaded: {0}", filename);
                        }
                    }
                }
            }

            return textures;
        }

        [NotNull]
        private List<string> LoadCustomTips()
        {
            var customTips = new List<string>();
            if (!string.IsNullOrEmpty(this._tipsFile))
            {
                var tipsFile = this._tipsFile.Replace('\\', '/');
                this.WriteLog("Custom tips file:  {0}", tipsFile);
                try
                {
                    using (var streamReader = new StreamReader(this._tipsFile))
                    {
                        customTips.AddRange(streamReader.ReadToEnd().Split(new[] { Environment.NewLine },
                            StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));
                    }
                }
                catch (FileNotFoundException)
                {
                    this.WriteLog("!!! WARNING - Tips file not found - not using custom tips");
                }
                catch (DirectoryNotFoundException)
                {
                    this.WriteLog("!!! WARNING - Tips file directory not found - not using custom tips");
                }
                catch (IOException)
                {
                    this.WriteLog("!!! WARNING - Tips file I/O problem - not using custom tips");
                }
                this.WriteLog("{0} custom tips loaded", customTips.Count);
            }
            else this.WriteLog("No custom tips file provided - not using custom tips");
            return customTips;
        }

        /// <summary>
        ///     Loads the configuration file and sets up default values.
        /// </summary>
        private void LoadConfig()
        {
            // Use the PlugInData path that KSP has earmarked for this DLL.
            var assembly = AssemblyLoader.loadedAssemblies.GetByAssembly(this.GetType().Assembly);
            var configFilePath = Path.Combine(assembly.dataPath, ConfigFileName).Replace('\\', '/');
            this.WriteLog("Loading configuration file:  {0}", configFilePath);

            // These hold settings from the prerelease that have changed in name or structure.
            var screenshotFolder = DefaultPath;

            // Setup a ConfigNode with the desired defaults (which were set in the field declarations).
            var configNode = new ConfigNode(); //("LoadingScreenManager");
            configNode.AddValue("debugLogging", this._debugLogging);
            configNode.AddValue("dumpScreens", this._dumpScreens);
            configNode.AddValue("dumpTips", this._dumpTips);
            configNode.AddValue("screenshotFolder", screenshotFolder);
            configNode.AddValue("slidesToAdd", this._slidesToAdd);
            configNode.AddValue("includeOriginalScreens", this._includeOriginalScreens);
            configNode.AddValue("runWithNoScreenshots", this._runWithNoScreenshots);
            configNode.AddValue("displayTime", this._displayTime);
            configNode.AddValue("fadeInTime", this._fadeInTime);
            configNode.AddValue("fadeOutTime", this._fadeOutTime);
            configNode.AddValue("tipTime", this._tipTime);
            configNode.AddValue("tipsFile", this._tipsFile);
            configNode.AddValue("includeOriginalTips", this._includeOriginalTips);

            this.WriteDebugLog("... Loading file");

            // Try to read the ConfigNode from the .cfg file.  If we can, merge it with our created ConfigNode.
            // The advantage of doing it this way is that at the end we always end up with a full config file that can be saved,
            // which is handy for version upgrades or if the user has deleted settings.
            var loadedConfigNode = ConfigNode.Load(configFilePath);
            if (loadedConfigNode != null)
            {
                this.WriteDebugLog("... File loaded");
                loadedConfigNode.CopyTo(configNode, true);
                // CopyTo doesn't seem to deal with multiple nodes so it'll only copy one.  Thus, we have to copy them manually.
                configNode.RemoveNodes("FOLDER");
                var loadedFolderConfigNodes = loadedConfigNode.GetNodes("FOLDER") ?? new ConfigNode[0];
                foreach (var loadedFolderConfigNode in loadedFolderConfigNodes)
                {
                    configNode.AddNode(loadedFolderConfigNode);
                }
            }
            else this.WriteDebugLog("... No file found, using default configuration");

            // Now pull out values.  
            configNode.TryGetValue("debugLogging", ref this._debugLogging);
            configNode.TryGetValue("dumpScreens", ref this._dumpScreens);
            configNode.TryGetValue("dumpTips", ref this._dumpTips);
            configNode.TryGetValue("screenshotFolder", ref screenshotFolder);
            configNode.TryGetValue("slidesToAdd", ref this._slidesToAdd);
            configNode.TryGetValue("includeOriginalScreens", ref this._includeOriginalScreens);
            configNode.TryGetValue("runWithNoScreenshots", ref this._runWithNoScreenshots);
            configNode.TryGetValue("displayTime", ref this._displayTime);
            configNode.TryGetValue("fadeInTime", ref this._fadeInTime);
            configNode.TryGetValue("fadeOutTime", ref this._fadeOutTime);
            configNode.TryGetValue("tipTime", ref this._tipTime);
            configNode.TryGetValue("tipsFile", ref this._tipsFile);
            configNode.TryGetValue("includeOriginalTips", ref this._includeOriginalTips);

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

            // Translate the folder config nodes into a more convenient form.
            foreach (var folderConfigNode in folderConfigNodes)
            {
                var imageFolder = new ImageFolder();
                folderConfigNode.TryGetValue("path", ref imageFolder.path);
                folderConfigNode.TryGetValue("fileMasks", ref imageFolder.fileMasks);
                folderConfigNode.TryGetValue("ignoreSubfolders", ref imageFolder.ignoreSubfolders);
                this._imageFolders.Add(imageFolder);
            }

            // Remove legacy settings.
            configNode.RemoveValue("screenshotFolder");

            // Done, now save the modified configuration back to disk.
            this.WriteDebugLog("... Saving configuration file:\n{0}", configNode);
            var directoryName = Path.GetDirectoryName(configFilePath);
            if (directoryName != null) Directory.CreateDirectory(directoryName);
            configNode.Save(configFilePath);

            this.WriteDebugLog("... Done");
        }

        /// <summary>
        ///     Dumps a bunch of debug info to the log.
        /// </summary>
        /// <remarks>
        ///     This just avoids cluttering up the main logic method with a bunch of debug stuff.  Note this method also supports
        ///     the dumpScreens and dumpTips settings.
        /// </remarks>
        private void WriteStartupDebuggingInfo()
        {
            // See what loaders are setup.
            this.WriteDebugLog("{0} Loaders detected", LoadingScreen.Instance.loaders.Count);
            for (var i = 0; i < LoadingScreen.Instance.loaders.Count; i++)
            {
                var loader = LoadingScreen.Instance.loaders[i];
                this.WriteDebugLog(">> Loader #{0}: {1} - '{2}'", i + 1, loader.name, loader.ProgressTitle());
            }

            // See what screens are provided.
            this.WriteDebugLog("{0} Loading screens detected", LoadingScreen.Instance.Screens.Count);
            for (var i = 0; i < LoadingScreen.Instance.Screens.Count; i++)
            {
                var screen = LoadingScreen.Instance.Screens[i];
                // Screens don't have names but their textures do, so we just use the index and dump the timings.
                this.WriteDebugLog(">> Screen #{0}: dt={1} fi={2} fo={3} tt={4} ", i + 1, screen.displayTime,
                    screen.fadeInTime, screen.fadeOutTime, screen.tipTime);

                // Screen textures and tips are custom to each screen.
                if (this._dumpScreens)
                {
                    this.WriteDebugLog(">> >> Screen Texture Names:");
                    foreach (var screenTexture in screen.screens)
                    {
                        this.WriteDebugLog("{0}", screenTexture.name);
                    }
                }
                if (this._dumpTips)
                {
                    this.WriteDebugLog(">> >> Screen Tips:");
                    foreach (var tip in screen.tips)
                    {
                        this.WriteDebugLog("{0}", tip);
                    }
                }
            }
        }

        /// <summary>
        ///     Convenience method to write to the log using an addon-specific prefix.
        /// </summary>
        /// <param name="format">As per <see cref="string.Format(string, object[])" /></param>
        /// <param name="args">As per <see cref="string.Format(string, object[])" /></param>
        [StringFormatMethod("format")]
        private void WriteLog(string format, params object[] args)
        {
            Debug.LogFormat(this, string.Format(LogMessageFormat, format), args);
        }

        /// <summary>
        ///     Convenience method to write debug messages to the log using an addon-specific prefix.
        /// </summary>
        /// <param name="format">As per <see cref="string.Format(string, object[])" /></param>
        /// <param name="args">As per <see cref="string.Format(string, object[])" /></param>
        [StringFormatMethod("format")]
        private void WriteDebugLog(string format, params object[] args)
        {
            if (this._debugLogging) Debug.LogFormat(this, string.Format(LogMessageFormat, format), args);
        }

        #endregion
    }
}
