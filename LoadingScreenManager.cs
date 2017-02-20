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
using Random = System.Random;

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

        private const string Version = "v1.01";
        private const string ConfigFileName = "LoadingScreenManager.cfg";
        private const string LogMessageFormat = "[LSM] {0}";

        private const string DefaultPath = "Screenshots/";
        private const string DefaultFileMasks = "*.png;*.jpg";

        #endregion

        #region Configuration Fields

        private readonly Random _random = new Random();
        private readonly List<ImageFolder> _imageFolders = new List<ImageFolder>();

        // -- Configuration fields and their default values.  (If no default below, uses C# default for type.) --

        private bool _debugLogging;
        private bool _dumpTips;
        private bool _dumpScreens;

        private int _totalSlides = 20;
        private bool _includeOriginalScreens = true;
        private bool _forceSlideshowWithNoImageFiles;

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
        public void Start()
        {
            this.WriteLog("LoadingScreenManager {0} activated", Version);

            this.LoadConfig();
            if (this._debugLogging) this.WriteStartupDebuggingInfo();
            this.ModifyLoadingScreens();

            this.WriteLog("LoadingScreenManager {0} finished", Version);
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     The core of the program that replaces the default LoadingScreen information with our customizations.
        /// </summary>
        private void ModifyLoadingScreens()
        {
            // We are going to rebuild the loaders and screens, so first grab the existing ones.

            // The logic is built to cope if mods and/or a future version change the screen types and/or ordering.  Originally
            // I thought this would be YAGNI but it turns out some mods add loaders and not screens so in fact it is very
            // needed, and the first implementation did not quite go far enough!

            // I don't know why anything would clear these, but just in case...
            if (LoadingScreen.Instance.loaders == null)
            {
                this.WriteLog("** WARNING - Loaders not defined.");
                LoadingScreen.Instance.loaders = new List<LoadingSystem>();
            }
            if (LoadingScreen.Instance.Screens == null)
            {
                this.WriteLog("** WARNING - Screens not defined.");
                LoadingScreen.Instance.Screens = new List<LoadingScreen.LoadingScreenState>();
            }

            var gameDatabaseIndex = -1;
            LoadingSystem gameDatabaseLoader = null;
            LoadingScreen.LoadingScreenState gameDatabaseScreen = null;
            LoadingScreen.LoadingScreenState firstModifiableScreen = null;

            // It's possible something could have removed all loaders (in which case we'll just add ours later).
            if (LoadingScreen.Instance.loaders.Count > 0)
            {
                // The GameDatabase loader and its accompanying screen is the one that shows the SQUAD logo.  Normally it's 1st,
                // but we find it dynamically in case some mod behaved badly and moved it.
                // *** IMPORTANT NOTE ***
                // Under KSP forum rules, mods may not modify the in-games logos, so please don't submit anything that makes
                // changes to these.  All we are doing here is saving them so we can add them back later.
                gameDatabaseIndex = LoadingScreen.Instance.loaders.FindIndex(l => l is GameDatabase);
                if (gameDatabaseIndex >= 0)
                {
                    if (gameDatabaseIndex != 0) this.WriteLog("** WARNING - Game Database Loader is not where expected.");
                    gameDatabaseLoader = LoadingScreen.Instance.loaders[gameDatabaseIndex];
                    gameDatabaseScreen = LoadingScreen.Instance.Screens[gameDatabaseIndex];
                }
                else this.WriteLog("** WARNING - Game Database Loader is not present.");

                // The "normal" loading screen after the logo is supposed to be second, but a mod could have inserted one.
                // So the first screen that isn't the logo is the one we can modify.
                // Note that we just assume this screen has the "original" tips and screens.  Could look for PartLoader to find
                // which index it is, but we don't need to be that strict.

                // Unless the *only* loader is the GameDatabase then there has to be a modifiable screen, and the only time it
                // can't be at index 0 is if the GameDatabase is (which is actually the norm).
                if (LoadingScreen.Instance.loaders.Count > 1 || gameDatabaseIndex < 0)
                {
                    var firstModifiableIndex = gameDatabaseIndex == 0 ? 1 : 0;
                    firstModifiableScreen = LoadingScreen.Instance.Screens[firstModifiableIndex];
                }
                if (firstModifiableScreen == null) this.WriteLog("** WARNING - No existing modifiable loaders.");
            }
            else this.WriteLog("** WARNING - No existing loaders found.");

            var filenames = this.GetImageFilenames();

            var customTips = this.LoadCustomTips();
            if (this._includeOriginalTips && firstModifiableScreen != null) customTips.AddRange(firstModifiableScreen.tips);
            var tips = customTips.ToArray();

            if (this._includeOriginalScreens && firstModifiableScreen != null)
            {
                for (var i = 0; i < firstModifiableScreen.screens.Length; i++)
                {
                    // Identify original by indexes, with a * in front so we know they can't be filenames.
                    filenames.Add($"*{i}");
                }
            }

            if (this._forceSlideshowWithNoImageFiles || filenames.Count > 0)
            {
                var newScreens = new List<LoadingScreen.LoadingScreenState>(this._totalSlides + 1);

                while (newScreens.Count < this._totalSlides && filenames.Count > 0)
                {
                    var filenameIndex = this._random.Next(filenames.Count);
                    var filename = filenames[filenameIndex];
                    filenames.RemoveAt(filenameIndex);

                    Texture2D texture;
                    // A * in front of a filename denotes an original image (see above).
                    if (filename[0] == '*')
                    {
                        this.WriteDebugLog("Using original image:  Index {0}", filename);
                        Debug.Assert(firstModifiableScreen != null);
                        // ReSharper doesn't recognize Unity's Assert() and I don't want to bother with external annotations...
                        // ReSharper disable once PossibleNullReferenceException
                        texture = firstModifiableScreen.screens[int.Parse(filename.Substring(1))];
                    }
                    else
                    {
                        this.WriteDebugLog("Loading image file:  {0}", filename);
                        using (var fileStream = new FileStream(filename, FileMode.Open))
                        {
                            this.WriteDebugLog("... File opened");
                            var bytes = new byte[fileStream.Length];
                            fileStream.Read(bytes, 0, (int) fileStream.Length);
                            this.WriteDebugLog("... File read");

                            // This is right out of the Unity documentation for Texture2D.LoadImage().
                            texture = new Texture2D(2, 2);
                            if (!texture.LoadImage(bytes))
                            {
                                this.WriteLog("!!! ERROR - Image file cannot be loaded: {0}", filename);
                                continue;
                            }
                        }
                    }

                    // All normal loading screens (except the logo of course) get rebuilt to use the timings and images.
                    // The default on the main screen is 40 minutes, so if we didn't do this the slideshow wouldn't work!
                    var screen = new LoadingScreen.LoadingScreenState
                    {
                        displayTime = this._displayTime,
                        fadeInTime = this._fadeInTime,
                        fadeOutTime = this._fadeOutTime,
                        tipTime = this._tipTime,
                        screens = new[] { texture },
                        tips = tips
                    };
                    newScreens.Add(screen);
                }

                // Existing loaders are saved to be added at the end.  See below for why.
                var existingLoaders = new List<LoadingSystem>(this._totalSlides + 1);
                // ReSharper disable once LoopCanBeConvertedToQuery
                for (var i = 0; i < LoadingScreen.Instance.loaders.Count; i++)
                {
                    if (i != gameDatabaseIndex) existingLoaders.Add(LoadingScreen.Instance.loaders[i]);
                }
                var totalDummyLoaders = newScreens.Count - existingLoaders.Count;

                var newLoaders = new List<LoadingSystem>(this._totalSlides + 1);
                for (var i = 0; i < totalDummyLoaders; i++)
                {
                    newLoaders.Add(new DummyLoader());
                }

                // Add the logo stuff to the start.  Note if some mod had moved it, this will get undone.
                if (gameDatabaseLoader != null)
                {
                    newLoaders.Insert(0, gameDatabaseLoader);
                    newScreens.Insert(0, gameDatabaseScreen);
                }

                // Add existing loaders at the end.
                // The reason we put our loaders first is that, for some reason I haven't been able to figure out, they will
                // disrupt the progress bar partway through, but at least if they're at the end the progress bar will end up
                // going to 100% eventually.  If we just tacked them onto the end the progress bar would finish at like 20%,
                // which would look bad to the user.
                newLoaders.AddRange(existingLoaders);

                LoadingScreen.Instance.loaders = newLoaders;
                LoadingScreen.Instance.Screens = newScreens;

                // Raw counts for debugging.
                this.WriteDebugLog("{0} loaders set{1}", newLoaders.Count, 
                    gameDatabaseIndex >= 0 ? " (including GameDatabase)" : "");
                this.WriteDebugLog("{0} raw screens set{1}", newScreens.Count, 
                    gameDatabaseIndex >= 0 ? " (including logo)" : "");

                // Slide count.  The logo is not counted as a slide.
                var slideCount = gameDatabaseIndex >= 0 ? newScreens.Count - 1 : newScreens.Count;
                this.WriteLog("{0} slides set", slideCount);
                if (slideCount < this._totalSlides)
                {
                    this.WriteLog("** WARNING:  Not enough images available ({0}) to meet requested number of slides ({1}).",
                        slideCount, this._totalSlides);
                }
            }
        }

        [NotNull]
        private List<string> GetImageFilenames()
        {
            var filenames = new List<string>();
            var dataPath = Path.GetDirectoryName(Application.dataPath) ?? "";

            this.WriteLog("Finding image files...");

            foreach (var imageFolder in this._imageFolders)
            {
                var path = Path.Combine(dataPath, imageFolder.path).Replace('\\', '/');
                var searchOption = imageFolder.ignoreSubfolders ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;

                try
                {
                    // Unity always uses a forward slash in paths so these must be changed on backslash OSs (e.g. Windows).
                    // Can only use one mask at a time so we have to do multiple GetFiles() calls.
                    // All this does is split up the filemasks and then making the call for each of them with the appropriate
                    // file masks.  SelectMany just lets us do it all at once without having to use a loop.
                    filenames.AddRange(imageFolder.fileMasks.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .SelectMany(fm => Directory.GetFiles(path, fm.Trim(), searchOption) ?? new string[0])
                        .Select(fn => fn.Replace('\\', '/')));
                    this.WriteLog("... Added image path:  {0}", path);
                }
                catch (IOException ex)
                {
                    this.WriteLog("ERROR: Unable to access folder '{0}':  {1}", path, ex.Message);
                }
            }

            if (this._debugLogging)
            {
                foreach (var filename in filenames)
                {
                    this.WriteDebugLog("... Image file: {0}", filename);

                }
            }

            this.WriteLog("{0} image files found", filenames.Count);

            return filenames;
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
                        customTips.AddRange(streamReader.ReadToEnd()
                            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .Where(t => t.Length > 0 && t[0] != '#'));
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
            else this.WriteLog("... No file found, using default configuration");

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
            configNode.RemoveValue("slidesToAdd");
            configNode.RemoveValue("runWithNoScreenshots");

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
                    this.WriteDebugLog(">> >> Existing screen texture names:");
                    foreach (var screenTexture in screen.screens)
                    {
                        this.WriteDebugLog("{0}", screenTexture.name);
                    }
                }
                if (this._dumpTips)
                {
                    this.WriteDebugLog(">> >> Existing screen tips:");
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
