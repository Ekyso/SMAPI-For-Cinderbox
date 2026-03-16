using System;
using AndroidUtils = Android.Util;

namespace StardewModdingAPI.Mobile;

/// <summary>Simple logger that outputs to Android logcat.</summary>
public static class AndroidLogger
{
    private const string Tag = "SMAPI";

    public static void Log(object? msg)
    {
        if (msg == null)
            msg = "";

        AndroidUtils.Log.Debug(Tag, msg.ToString() ?? "");
    }

    public static void Log(string tag, object? msg)
    {
        if (msg == null)
            msg = "";

        AndroidUtils.Log.Debug(tag, msg.ToString() ?? "");
    }
}
