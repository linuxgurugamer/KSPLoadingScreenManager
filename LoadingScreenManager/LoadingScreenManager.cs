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
using System.Reflection;


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
        internal static AssemblyLoader.LoadedAssembly TheChosenOne = null;
        //static bool first = true;


        #region Nested Structs

        /// <summary>
        ///     Holds the configuration for an image folder definition.
        /// </summary>
        public struct ImageFolder
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

        private string Version; 

        #endregion

        #region Configuration Fields

        private readonly Random _random = new Random();
  #endregion

        Config cfg = new Config();

        #region Public Methods

        /// <summary>
        ///     Initialization code - called by Unity.
        /// </summary>
        /// <remarks>
        ///     All core logic is called from here since we want to make our changes before everything gets running...
        /// </remarks>
        [UsedImplicitly]

        void Awake()
        {
#if false
            AssemblyLoader.LoadedAssembly[] list = AssemblyLoader.loadedAssemblies.Where(a => a.name == "LoadingScreenManager").ToArray();
            TheChosenOne = list.FirstOrDefault(a => a.assembly.GetName().Version == list.Select(i => i.assembly.GetName().Version).Max());
            if (first && Assembly.GetExecutingAssembly() == TheChosenOne.assembly)
            {
                Version = TheChosenOne.assembly.GetName().Version.ToString();
                Log.Info(Version);
                first = false;
                DontDestroyOnLoad(this);
            }
            else
            {
                DestroyImmediate(this);
            }
#endif

            // Following makes sure only the newest version of the DLL is run, assuming there are more than one.
            // Copied from Module Manager
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            IEnumerable<AssemblyLoader.LoadedAssembly> eligible = from a in AssemblyLoader.loadedAssemblies
                                                                  let ass = a.assembly
                                                                  where ass.GetName().Name == currentAssembly.GetName().Name
                                                                  orderby ass.GetName().Version descending, a.path ascending
                                                                  select a;

            // Elect the newest loaded version of LSM to set up the screens.
            // If there is a newer version loaded then don't do anything
            // If there is a same version but earlier in the list, don't do anything either.
            if (eligible.First().assembly != currentAssembly)
            {
                Log.Info("version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location +
                    " lost the election");
                DestroyImmediate(this);
                return;
            }
            string candidates = "";
            foreach (AssemblyLoader.LoadedAssembly a in eligible)
            {
                if (currentAssembly.Location != a.path)
                    candidates += "Version " + a.assembly.GetName().Version + " " + a.path + " " + "\n";
            }
            if (candidates.Length > 0)
            {
                Log.Info("version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location +
                    " won the election against\n" + candidates);
            }

            Version = currentAssembly.GetName().Version.ToString();

        }
        public void Start()
        {
            Log.Info("LoadingScreenManager {0} activated", Version);

            //this.LoadConfig();
            if (cfg._debugLogging) this.WriteStartupDebuggingInfo();
            this.ModifyLoadingScreens();

            Log.Info("LoadingScreenManager {0} finished", Version);
        }
        void Update()
        {
            if (HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                Log.Info("LoadingScreenManager {0} destroyed", Version);
                DestroyImmediate(this);
            }
        }
#endregion

#region Private Methods

        bool LoadImageFile(string filename, out Texture2D texture)
        {
            Log.Info("Loading image file:  {0}", filename);
            using (var fileStream = new FileStream(filename, FileMode.Open))
            {
                Log.Info("... File opened");
                var bytes = new byte[fileStream.Length];
                fileStream.Read(bytes, 0, (int)fileStream.Length);
                Log.Info("... File read");

                // This is right out of the Unity documentation for Texture2D.LoadImage().
                texture = new Texture2D(2, 2);
                if (!texture.LoadImage(bytes))
                {
                    Log.Info("!!! ERROR - Image file cannot be loaded: {0}", filename);
                    return false;
                }
            }
            return true;
        }
        void AddScreen(List<LoadingScreen.LoadingScreenState>  newScreens, Texture2D texture, string[] tips)
        {
            // All normal loading screens (except the logo of course) get rebuilt to use the timings and images.
            // The default on the main screen is 40 minutes, so if we didn't do this the slideshow wouldn't work!
            var screen = new LoadingScreen.LoadingScreenState
            {
                displayTime = cfg._displayTime,
                fadeInTime = cfg._fadeInTime,
                fadeOutTime = cfg._fadeOutTime,
                tipTime = cfg._tipTime,
                screens = new[] { texture },
                tips = tips
            };
            newScreens.Add(screen);
        }
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
                Log.Error("** WARNING - Loaders not defined.");
                LoadingScreen.Instance.loaders = new List<LoadingSystem>();
            }
            if (LoadingScreen.Instance.Screens == null)
            {
                Log.Error("** WARNING - Screens not defined.");
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
                    if (gameDatabaseIndex != 0) Log.Warning("** WARNING - Game Database Loader is not where expected.");
                    gameDatabaseLoader = LoadingScreen.Instance.loaders[gameDatabaseIndex];
                    gameDatabaseScreen = LoadingScreen.Instance.Screens[gameDatabaseIndex];
                }
                else Log.Error("** WARNING - Game Database Loader is not present.");

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
                if (firstModifiableScreen == null) Log.Warning("** WARNING - No existing modifiable loaders.");
            }
            else Log.Warning("** WARNING - No existing loaders found.");

            var imageFilenames = this.GetImageFilenames();

            var customTips = this.LoadCustomTips();
            if (cfg._includeOriginalTips && firstModifiableScreen != null) customTips.AddRange(firstModifiableScreen.tips);
            var tips = customTips.ToArray();

            if (cfg._includeOriginalScreens && firstModifiableScreen != null)
            {
                for (var i = 0; i < firstModifiableScreen.screens.Length; i++)
                {
                    // Identify original by indexes, with a * in front so we know they can't be filenames.
                    imageFilenames.Add($"*{i}");
                }
            }

            if (cfg._forceSlideshowWithNoImageFiles || imageFilenames.Count > 0)
            {
                var newScreens = new List<LoadingScreen.LoadingScreenState>(cfg._totalSlides + 1);
                Texture2D texture;
                string[] ltips = new string[1] ;
                
                // Add logo screens here
                for (int i = 0; i < cfg.logoScreens.Count; i++)
                {
                    var s = cfg.logoScreens[i];
                    if (File.Exists(s))
                    {
                        ltips[0] = cfg.logoTips[i];

                        if (LoadImageFile(s, out texture))
                            AddScreen(newScreens, texture, ltips);
                    }
                }

                while (newScreens.Count < cfg._totalSlides && imageFilenames.Count > 0)
                {
                    var filenameIndex = this._random.Next(imageFilenames.Count);
                    var filename = imageFilenames[filenameIndex];
                    imageFilenames.RemoveAt(filenameIndex);

                    
                    // A * in front of a filename denotes an original image (see above).
                    if (filename[0] == '*')
                    {
                        Log.Info("Using original image:  Index {0}", filename);
                        Debug.Assert(firstModifiableScreen != null);
                        // ReSharper doesn't recognize Unity's Assert() and I don't want to bother with external annotations...
                        // ReSharper disable once PossibleNullReferenceException
                        texture = (Texture2D)firstModifiableScreen.screens[int.Parse(filename.Substring(1))];
                    }
                    else
                    {
                        if (!LoadImageFile(filename, out texture))
                            continue;
                    }

                    AddScreen(newScreens, texture, tips);
 
                }

                // Existing loaders are saved to be added at the end.  See below for why.
                var existingLoaders = new List<LoadingSystem>(cfg._totalSlides + 1);
                // ReSharper disable once LoopCanBeConvertedToQuery
                for (var i = 0; i < LoadingScreen.Instance.loaders.Count; i++)
                {
                    if (i != gameDatabaseIndex) existingLoaders.Add(LoadingScreen.Instance.loaders[i]);
                }
                var totalDummyLoaders = newScreens.Count - existingLoaders.Count;

                var newLoaders = new List<LoadingSystem>(cfg._totalSlides + 1);
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
                Log.Info("{0} loaders set{1}", newLoaders.Count, 
                    gameDatabaseIndex >= 0 ? " (including GameDatabase)" : "");
                Log.Info("{0} raw screens set{1}", newScreens.Count, 
                    gameDatabaseIndex >= 0 ? " (including logo)" : "");

                // Slide count.  The logo is not counted as a slide.
                var slideCount = gameDatabaseIndex >= 0 ? newScreens.Count - 1 : newScreens.Count;
                Log.Info("{0} slides set", slideCount);
                if (slideCount < cfg._totalSlides)
                {
                    Log.Info("** WARNING:  Not enough images available ({0}) to meet requested number of slides ({1}).",
                        slideCount, cfg._totalSlides);
                }
            }
        }
        void GetFilenames(Dictionary<string, ImageFolder> list, ref List<string> filenames)
        {
            var dataPath = Path.GetDirectoryName(Application.dataPath) ?? "";
            foreach (var imageFolder in list)
            {
                Log.Info("imageFolder: " + imageFolder.Value.path);

                var path = Path.Combine(dataPath, imageFolder.Value.path).Replace('\\', '/');
                var searchOption = imageFolder.Value.ignoreSubfolders ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;

                try
                {
                    // Unity always uses a forward slash in paths so these must be changed on backslash OSs (e.g. Windows).
                    // Can only use one mask at a time so we have to do multiple GetFiles() calls.
                    // All this does is split up the filemasks and then making the call for each of them with the appropriate
                    // file masks.  SelectMany just lets us do it all at once without having to use a loop.

                    var l = imageFolder.Value.fileMasks.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .SelectMany(fm => Directory.GetFiles(path, fm.Trim(), searchOption) ?? new string[0])
                        .Select(fn => fn.Replace('\\', '/'));
                    if (l != null)
                        filenames.AddRange(l);
                    //                    filenames.AddRange(imageFolder.fileMasks.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    //                        .SelectMany(fm => Directory.GetFiles(path, fm.Trim(), searchOption) ?? new string[0])
                    //                        .Select(fn => fn.Replace('\\', '/'))
                    //                        );
                    Log.Info("... Added image path:  {0}", path);
                }
                catch (IOException ex)
                {
                    Log.Error("ERROR: Unable to access folder '{0}':  {1}", path, ex.Message);
                }
            }
        }
        [NotNull]
        private List<string> GetImageFilenames()
        {
            var filenames = new List<string>();
            var dataPath = Path.GetDirectoryName(Application.dataPath) ?? "";

            Log.Info("Finding image files...");
            Log.Info("cfg._imageFolders");
            GetFilenames(cfg._imageFolders, ref filenames);
            Log.Info("cfg._addonImageFolders");
            GetFilenames(cfg._addonImageFolders, ref filenames);

            if (cfg._debugLogging)
            {
                foreach (var filename in filenames)
                {
                    Log.Debug("... Image file: {0}", filename);

                }
            }

            Log.Info("{0} image files found", filenames.Count);

            return filenames;
        }

        [NotNull]
        private List<string> LoadCustomTips()
        {
            var customTips = new List<string>();

            if (!string.IsNullOrEmpty(cfg._tipsFile))
            {
                var tipsFile = cfg._tipsFile.Replace('\\', '/');
                Log.Info("Custom tips file:  {0}", tipsFile);
                try
                {
                    using (var streamReader = new StreamReader(cfg._tipsFile))
                    {
                        customTips.AddRange(streamReader.ReadToEnd()
                            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .Where(t => t.Length > 0 && t[0] != '#'));
                    }
                }
                catch (FileNotFoundException)
                {
                    Log.Warning("!!! WARNING - Tips file not found - not using custom tips");
                }
                catch (DirectoryNotFoundException)
                {
                    Log.Warning("!!! WARNING - Tips file directory not found - not using custom tips");
                }
                catch (IOException)
                {
                    Log.Warning("!!! WARNING - Tips file I/O problem - not using custom tips");
                }
                Log.Warning("{0} custom tips loaded", customTips.Count);
            }
            else Log.Warning("No custom tips file provided - not using custom tips");
            return customTips;
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
            Log.Debug("{0} Loaders detected", LoadingScreen.Instance.loaders.Count);
            for (var i = 0; i < LoadingScreen.Instance.loaders.Count; i++)
            {
                var loader = LoadingScreen.Instance.loaders[i];
                Log.Debug(">> Loader #{0}: {1} - '{2}'", i + 1, loader.name, loader.ProgressTitle());
            }

            // See what screens are provided.
            Log.Debug("{0} Loading screens detected", LoadingScreen.Instance.Screens.Count);
            for (var i = 0; i < LoadingScreen.Instance.Screens.Count; i++)
            {
                var screen = LoadingScreen.Instance.Screens[i];
                // Screens don't have names but their textures do, so we just use the index and dump the timings.
                Log.Debug(">> Screen #{0}: dt={1} fi={2} fo={3} tt={4} ", i + 1, screen.displayTime,
                    screen.fadeInTime, screen.fadeOutTime, screen.tipTime);

                // Screen textures and tips are custom to each screen.
                if (cfg._dumpScreens)
                {
                    Log.Debug(">> >> Existing screen texture names:");
                    foreach (var screenTexture in screen.screens)
                    {
                        Log.Debug("{0}", screenTexture.name);
                    }
                }
                if (cfg._dumpTips)
                {
                    Log.Debug(">> >> Existing screen tips:");
                    foreach (var tip in screen.tips)
                    {
                        Log.Debug("{0}", tip);
                    }
                }
            }
        }
#if false
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
            if (cfg._debugLogging) Debug.LogFormat(this, string.Format(LogMessageFormat, format), args);
        }
#endif
#endregion
    }
}
