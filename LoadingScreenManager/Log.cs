using System;
using System.Collections;
using System.Diagnostics;

namespace LoadingScreenManager
{
    public static class Log
    {
        public enum LEVEL
        {
            OFF = 0,
            DEBUG = 1,
            ERROR = 2,
            WARNING = 3,
            INFO = 4,
            DETAIL = 5,
            TRACE = 6
        };

        public static LEVEL level = LEVEL.INFO;

        private static readonly String PREFIX = "LSM" + ": ";

        public static LEVEL GetLevel()
        {
            return level;
        }

        public static void SetLevel(LEVEL level)
        {
            Log.Info("log level " + level);
            Log.level = level;
        }

        public static LEVEL GetLogLevel()
        {
            return level;
        }

        private static bool IsLevel(LEVEL level)
        {
            return level == Log.level;
        }

        public static bool IsLogable(LEVEL level)
        {
            return level <= Log.level;
        }

        public static void Trace(String msg)
        {
            if (IsLogable(LEVEL.TRACE))
            {
                UnityEngine.Debug.Log(PREFIX + msg);
            }
        }

        public static void Detail(String msg)
        {
            if (IsLogable(LEVEL.DETAIL))
            {
                UnityEngine.Debug.Log(PREFIX + msg);
            }
        }

        [ConditionalAttribute("DEBUG")]
        public static void Info(String msg, params object[] args)
        {
            if (IsLogable(LEVEL.INFO))
            {
                UnityEngine.Debug.Log(string.Format(PREFIX + msg, args));
            }
        }

        public static void Debug(String msg, params object[] args)
        {
            if (IsLogable(LEVEL.DEBUG))
            {
                UnityEngine.Debug.Log(string.Format(PREFIX + msg, args));
            }
        }

        [ConditionalAttribute("DEBUG")]
        public static void Test(String msg)
        {
            //if (IsLogable(LEVEL.INFO))
            {
                UnityEngine.Debug.LogWarning(PREFIX + "TEST:" + msg);
            }
        }

        public static void Warning(String msg, params object[] args)
        {
            if (IsLogable(LEVEL.WARNING))
            {
                UnityEngine.Debug.LogWarning(string.Format(PREFIX + msg, args));
            }
        }

        public static void Error(String msg, params object[] args)
        {
            if (IsLogable(LEVEL.ERROR))
            {
                UnityEngine.Debug.LogError(string.Format(PREFIX + msg, args));
            }
        }

        public static void Exception(Exception e)
        {
            Log.Error("exception caught: " + e.GetType() + ": " + e.Message);
        }

    }
}
