using System;
using System.IO;
using System.Text.RegularExpressions;
using KSP.UI.Screens;
using System.Collections.Generic;
using UnityEngine;


namespace LoadingScreenManager
{

    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    class Settings : MonoBehaviour
    {
        const float MAX_FADETIME = 10f;
        const float MAX_DISPLAYTIME = 10f;
        const float MAX_TIPTIME = 10f;


        private const int WIDTH = 400;
        private const int HEIGHT = 600;

        private const int DIALOGWIDTH = 400;
        private const int DIALOGHEIGHT = 100;

        public static string TEXTURE_DIR = "LoadingScreenManager/" + "Textures/";

        private static Texture2D LSM_button_img = new Texture2D(38, 38, TextureFormat.ARGB32, false);        
        public static ApplicationLauncherButton LSM_Button = null;
        bool visible = false;
        private Rect infoBounds = new Rect(Screen.width / 2 - WIDTH / 2, Screen.height / 2 - HEIGHT / 2, WIDTH, HEIGHT);

        private Rect dialogBounds = new Rect(Screen.width / 2 - DIALOGWIDTH / 2, Screen.height / 2 - DIALOGHEIGHT / 2, DIALOGWIDTH, DIALOGHEIGHT);

        Config cfg;

       

        FileSelection fsDialog = null;
        Vector2 scrollPos;
        int dialogEntry = -1;
        bool fileMaskWin = false;
        string fileMask;
        bool deleteFlag;

        // Vars here to avoid garbage collection

        LoadingScreenManager.ImageFolder deleteID;
        LoadingScreenManager.ImageFolder imageFolder;

        GUIStyle buttonStyle;
        GUIStyle toggleStyle;

        GUIContent content;
        Vector2 size;

        public void Start()
        {
            if (!GameDatabase.Instance.IsReady() || ApplicationLauncher.Instance == null)
                return;
            cfg = new Config();
            if (cfg._neverShowAgain)
                return;


            if (LSM_Button == null)
            {
                LSM_Button = ApplicationLauncher.Instance.AddModApplication(GUIToggleToolbar, GUIToggleToolbar,
                        null, null,
                        null, null,
                        ApplicationLauncher.AppScenes.MAINMENU,
                        GameDatabase.Instance.GetTexture(TEXTURE_DIR + "LoadingScreenManager-38", false));
            }
        }
        private void FixedUpdate()
        {
            if (GameDatabase.Instance.IsReady() && cfg == null)
                Start();

        }
        private void OnDestroy()
        {
            if (LSM_Button != null)
            {
                Debug.Log("OnDestroy, destroying button");
                LSM_Button.enabled = false;
                ApplicationLauncher.Instance.RemoveModApplication(LSM_Button);
                LSM_Button = null;
            }
        }

        public void GUIToggleToolbar(bool saveConfig)
        {
            visible = !visible;
            if (!visible && saveConfig)
                cfg.SaveConfig();
        }

        public void GUIToggleToolbar()
        {
            GUIToggleToolbar(true);
        }

        public void OnGUI()
        {
            //GUI.skin = HighLogic.Skin;
            //GUI.skin.
            //GUI.skin
            //     GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 1f);


            GUI.color = Color.grey;
            GUIStyle window = new GUIStyle(HighLogic.Skin.window);
            //window.normal.background.SetPixels( new[] { new Color(0.5f, 0.5f, 0.5f, 1f) });
            window.active.background = window.normal.background;

            Texture2D tex = window.normal.background; //.CreateReadable();

            //#if DEBUG
            //                Log.Debug("WindowOpacity set to " + value);
            //                tex.SaveToDisk("unmodified_window_bkg.png");
            //#endif

            var pixels = tex.GetPixels32();

            for (int i = 0; i < pixels.Length; ++i)
                pixels[i].a = 255;

            tex.SetPixels32(pixels); tex.Apply();
            //#if DEBUG
            //                tex.SaveToDisk("usermodified_window_bkg.png");
            //#endif
            // one of these apparently fixes the right thing
           // window.onActive.background =
           // window.onFocused.background =
           // window.onNormal.background =
            //window.onHover.background =
            window.active.background =
            window.focused.background =
            //window.hover.background =
            window.normal.background = tex;

            if (visible)
            {
                if (!fileMaskWin)
                {
                    this.infoBounds = GUILayout.Window(GetInstanceID() + 1, infoBounds, InfoWindow, "Loading Screen Manager", window);
                    if (infoBounds.height < HEIGHT)
                        infoBounds.height = HEIGHT;
                }
                else
                    this.dialogBounds = GUILayout.Window(GetInstanceID() + 1, dialogBounds, DialogWindow, "Loading Screen Manager", window);
            }
        }

        string StripRootPath(string str)
        {
            string root = System.IO.Path.GetFullPath(KSPUtil.ApplicationRootPath);

            if (str.IndexOf(root) == 0)
            {
                return str.Remove(0, root.Length);
            }
            return str;
        }

        void closeFSDialog()
        {
            fsDialog.Close();
            Destroy(fsDialog);
            fsDialog = null;
            dialogEntry = -1;
        }

        void DialogWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Enter File Mask: ");
            fileMask = GUILayout.TextField(fileMask);
            GUILayout.EndHorizontal();
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("OK"))
            {
                var imageFolder = cfg._imageFolders[dialogEntry];
                imageFolder.fileMasks = fileMask;
                cfg._imageFolders[dialogEntry] = imageFolder;
                dialogEntry = -1;
                fileMaskWin = false;
            }
            if (GUILayout.Button("Cancel"))
            {
                dialogEntry = -1;
                fileMaskWin = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        //
        // Following function from the following web page:
        //      http://stackoverflow.com/questions/1764204/how-to-display-abbreviated-path-names-in-net
        //
        /// <summary>
        /// Shortens a file path to the specified length
        /// </summary>
        /// <param name="path">The file path to shorten</param>
        /// <param name="maxLength">The max length of the output path (including the ellipsis if inserted)</param>
        /// <returns>The path with some of the middle directory paths replaced with an ellipsis (or the entire path if it is already shorter than maxLength)</returns>
        /// <remarks>
        /// Shortens the path by removing some of the "middle directories" in the path and inserting an ellipsis. If the filename and root path (drive letter or UNC server name)     in itself exceeds the maxLength, the filename will be cut to fit.
        /// UNC-paths and relative paths are also supported.
        /// The inserted ellipsis is not a true ellipsis char, but a string of three dots.
        /// </remarks>
        /// <example>
        /// ShortenPath(@"c:\websites\myproject\www_myproj\App_Data\themegafile.txt", 50)
        /// Result: "c:\websites\myproject\...\App_Data\themegafile.txt"
        /// 
        /// ShortenPath(@"c:\websites\myproject\www_myproj\App_Data\theextremelylongfilename_morelength.txt", 30)
        /// Result: "c:\...gfilename_morelength.txt"
        /// 
        /// ShortenPath(@"\\myserver\theshare\myproject\www_myproj\App_Data\theextremelylongfilename_morelength.txt", 30)
        /// Result: "\\myserver\...e_morelength.txt"
        /// 
        /// ShortenPath(@"\\myserver\theshare\myproject\www_myproj\App_Data\themegafile.txt", 50)
        /// Result: "\\myserver\theshare\...\App_Data\themegafile.txt"
        /// 
        /// ShortenPath(@"\\192.168.1.178\theshare\myproject\www_myproj\App_Data\themegafile.txt", 50)
        /// Result: "\\192.168.1.178\theshare\...\themegafile.txt"
        /// 
        /// ShortenPath(@"\theshare\myproject\www_myproj\App_Data\", 30)
        /// Result: "\theshare\...\App_Data\"
        /// 
        /// ShortenPath(@"\theshare\myproject\www_myproj\App_Data\themegafile.txt", 35)
        /// Result: "\theshare\...\themegafile.txt"
        /// </example>
        public static string ShortenPath(string path, int maxLength)
        {
            string ellipsisChars = "...";
            char dirSeperatorChar = Path.DirectorySeparatorChar;
            string directorySeperator = dirSeperatorChar.ToString();

            //simple guards
            if (path.Length <= maxLength)
            {
                return path;
            }
            int ellipsisLength = ellipsisChars.Length;
            if (maxLength <= ellipsisLength)
            {
                return ellipsisChars;
            }


            //alternate between taking a section from the start (firstPart) or the path and the end (lastPart)
            bool isFirstPartsTurn = true; //drive letter has first priority, so start with that and see what else there is room for

            //vars for accumulating the first and last parts of the final shortened path
            string firstPart = "";
            string lastPart = "";
            //keeping track of how many first/last parts have already been added to the shortened path
            int firstPartsUsed = 0;
            int lastPartsUsed = 0;

            string[] pathParts = path.Split(dirSeperatorChar);
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (isFirstPartsTurn)
                {
                    string partToAdd = pathParts[firstPartsUsed] + directorySeperator;
                    if ((firstPart.Length + lastPart.Length + partToAdd.Length + ellipsisLength) > maxLength)
                    {
                        break;
                    }
                    firstPart = firstPart + partToAdd;
                    if (partToAdd == directorySeperator)
                    {
                        //this is most likely the first part of and UNC or relative path 
                        //do not switch to lastpart, as these are not "true" directory seperators
                        //otherwise "\\myserver\theshare\outproject\www_project\file.txt" becomes "\\...\www_project\file.txt" instead of the intended "\\myserver\...\file.txt")
                    }
                    else
                    {
                        isFirstPartsTurn = false;
                    }
                    firstPartsUsed++;
                }
                else
                {
                    int index = pathParts.Length - lastPartsUsed - 1; //-1 because of length vs. zero-based indexing
                    string partToAdd = directorySeperator + pathParts[index];
                    if ((firstPart.Length + lastPart.Length + partToAdd.Length + ellipsisLength) > maxLength)
                    {
                        break;
                    }
                    lastPart = partToAdd + lastPart;
                    if (partToAdd == directorySeperator)
                    {
                        //this is most likely the last part of a relative path (e.g. "\websites\myproject\www_myproj\App_Data\")
                        //do not proceed to processing firstPart yet
                    }
                    else
                    {
                        isFirstPartsTurn = true;
                    }
                    lastPartsUsed++;
                }
            }

            if (lastPart == "")
            {
                //the filename (and root path) in itself was longer than maxLength, shorten it
                lastPart = pathParts[pathParts.Length - 1];//"pathParts[pathParts.Length -1]" is the equivalent of "Path.GetFileName(pathToShorten)"
                lastPart = lastPart.Substring(lastPart.Length + ellipsisLength + firstPart.Length - maxLength, maxLength - ellipsisLength - firstPart.Length);
            }

            return firstPart + ellipsisChars + lastPart;
        }

        private void InfoWindow(int id)
        {
            // in the following, to keep the dialog on screen, comment out the return and uncomment the GUI.enabled
            if (fsDialog != null && fsDialog.visible == true)
                return;
            //GUI.enabled = false;

            GUILayout.BeginVertical(GUILayout.Width(390));
            GUILayout.BeginHorizontal();
            cfg._debugLogging = GUILayout.Toggle(cfg._debugLogging, " Debug Logging");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            cfg._dumpScreens = GUILayout.Toggle(cfg._dumpScreens, " Dump Screens");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            cfg._dumpTips = GUILayout.Toggle(cfg._dumpTips, " Dump Tips");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            cfg._forceSlideshowWithNoImageFiles = GUILayout.Toggle(cfg._forceSlideshowWithNoImageFiles, " Force Slideshow With No Image Files");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Slides:  " + cfg._totalSlides.ToString(), GUILayout.Width(150));
            cfg._totalSlides = Mathf.RoundToInt(GUILayout.HorizontalSlider(cfg._totalSlides, 1, 100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Display Time:  " + cfg._displayTime.ToString("F2"), GUILayout.Width(150));
            cfg._displayTime = GUILayout.HorizontalSlider(cfg._displayTime, 1f, MAX_DISPLAYTIME);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();        
            GUILayout.Label("Fade in/out Time:  " + cfg._fadeInTime.ToString("F2"), GUILayout.Width(150));
            cfg._fadeInTime = GUILayout.HorizontalSlider(cfg._fadeInTime, 0f, MAX_FADETIME);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tooltip Time:  " + cfg._tipTime.ToString("F2"), GUILayout.Width(150));
            cfg._tipTime = GUILayout.HorizontalSlider(cfg._tipTime, 1f, MAX_TIPTIME);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            cfg._includeOriginalScreens = GUILayout.Toggle(cfg._includeOriginalScreens, " Include Original Screens");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            cfg._includeOriginalTips = GUILayout.Toggle(cfg._includeOriginalTips, " Include Original Tips");
            GUILayout.EndHorizontal();

            //GUILayout.EndVertical();
            //GUILayout.BeginVertical(GUILayout.Width(390));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tips File:"))
            {
                if (fsDialog == null)
                    fsDialog = gameObject.AddComponent<FileSelection>();

                if (cfg._tipsFile != "")
                    fsDialog.SetSelectedDirectory(Path.GetDirectoryName(cfg._tipsFile), false);
                
                dialogEntry = 0;
                fsDialog.startDialog();
            }

            if (fsDialog != null && fsDialog.done  &&
                fsDialog.SelectedDirectory != null && fsDialog.SelectedFile != null && dialogEntry == 0)
            {
                if (fsDialog.SelectedDirectory != "" || fsDialog.SelectedFile != "")
                {
                    cfg._tipsFile = StripRootPath(fsDialog.SelectedDirectory + cfg.dirSeperator + fsDialog.SelectedFile);
                }
                closeFSDialog();           
            }
            GUILayout.Label(cfg._tipsFile, GUI.skin.textField);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            if (GUILayout.Button("Image Folders (click to add)"))
            {
                if (fsDialog == null)
                    fsDialog = gameObject.AddComponent<FileSelection>();
                fsDialog.SetSelectedDirectory(Path.GetDirectoryName(Config.DefaultPath), true);
                dialogEntry = cfg._imageFolders.Count + 1;
                fsDialog.startDialog();
            }
            if (fsDialog != null && fsDialog.done  &&
                    fsDialog.SelectedDirectory != null && fsDialog.SelectedFile != null && dialogEntry == cfg._imageFolders.Count + 1)
            {
                if (fsDialog.SelectedDirectory != "" && fsDialog.SelectedFile == "")
                {
                    LoadingScreenManager.ImageFolder imgFolder = new LoadingScreenManager.ImageFolder();
                    imgFolder.path = StripRootPath(fsDialog.SelectedDirectory) + cfg.dirSeperator;
                    imgFolder.fileMasks = Config.DefaultFileMasks;
                    imgFolder.ignoreSubfolders = false;
                    cfg._imageFolders.Add(imgFolder);
                }
                closeFSDialog();
            }
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            
            deleteFlag = false;
            for (int i = 0; i < cfg._imageFolders.Count; i++)
            {
                imageFolder = cfg._imageFolders[i];

                GUILayout.BeginHorizontal();

                if (GUILayout.Button(ShortenPath(imageFolder.path,37)))
                {
                    if (fsDialog == null)
                        fsDialog = gameObject.AddComponent<FileSelection>();
                    fsDialog.SetSelectedDirectory(Path.GetDirectoryName(imageFolder.path), true);
                    dialogEntry = i + 1;
                    fsDialog.startDialog();
                }
                // following for tooltip
                if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    Vector2 s = GUI.skin.box.CalcSize(new GUIContent(imageFolder.path));
                    var rect = new Rect(Event.current.mousePosition.x + 10, Event.current.mousePosition.y + 10, s.x, s.y);

                    GUI.tooltip = imageFolder.path;
                    GUI.Box(rect, GUI.tooltip);
                }

                if (fsDialog != null && fsDialog.done == true &&
                    fsDialog.SelectedDirectory != null && fsDialog.SelectedFile != null && dialogEntry == i + 1)
                {
                    if (fsDialog.SelectedDirectory != "" && fsDialog.SelectedFile == "")
                    {
                        imageFolder.path = StripRootPath(fsDialog.SelectedDirectory) + cfg.dirSeperator;
                        cfg._imageFolders[i] = imageFolder;
                    }
                    fsDialog.Close();
                    Destroy(fsDialog);
                    fsDialog = null;
                    dialogEntry = -1;
                }

                 buttonStyle = new GUIStyle(GUI.skin.button);
                 toggleStyle = new GUIStyle(GUI.skin.toggle);

                content = new GUIContent("Masks");
                size = GUI.skin.box.CalcSize(content);
                if (GUILayout.Button("Masks", GUILayout.Width(size.x + buttonStyle.padding.left + buttonStyle.padding.right)))
                {
                    dialogEntry = i;
                    fileMaskWin = true;
                    fileMask = string.Copy(imageFolder.fileMasks);
                }
                // Following for tooltip
                if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    Vector2 s = GUI.skin.box.CalcSize(new GUIContent(imageFolder.fileMasks));
                    var rect = new Rect(Event.current.mousePosition.x + 10, Event.current.mousePosition.y + 10, s.x, s.y);

                    GUI.tooltip = imageFolder.fileMasks;
                    GUI.Box(rect, GUI.tooltip);
                }
                if (imageFolder.ignoreSubfolders)
                    buttonStyle.normal.textColor = Color.red;
                else
                    buttonStyle.normal.textColor = Color.green;

                content = new GUIContent("Sub");
                size = GUI.skin.box.CalcSize(content);
                if (GUILayout.Button("Sub", buttonStyle, GUILayout.Width(size.x + buttonStyle.padding.left + buttonStyle.padding.right)))
                {
                    imageFolder.ignoreSubfolders = !imageFolder.ignoreSubfolders;
                    cfg._imageFolders[i] = imageFolder;
                }

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    deleteID = imageFolder;
                    deleteFlag = true;
                }
                GUILayout.EndHorizontal();
            }
            if (deleteFlag)
            {
                cfg._imageFolders.Remove(deleteID);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            toggleStyle.normal.textColor = Color.yellow;
            cfg._neverShowAgain = GUILayout.Toggle(cfg._neverShowAgain, " Never show again on toolbar", toggleStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("OK"))
            {
                GUIToggleToolbar(true);
                if (cfg._neverShowAgain)
                {
                    OnDestroy();
                }
            }
            if (GUILayout.Button("Cancel"))
            {
                // reload config to discard all changes
                cfg = new Config();
                GUIToggleToolbar(false);

            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUI.DragWindow();
        }
    }

}