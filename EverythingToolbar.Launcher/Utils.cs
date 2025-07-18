﻿using EverythingToolbar.Helpers;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using NLog;
using Shell32;
using System;
using System.Diagnostics;
using System.IO;
using Wpf.Ui.Appearance;
using File = System.IO.File;

namespace EverythingToolbar.Launcher
{
    internal class Utils
    {
        private static readonly ILogger Logger = ToolbarLogger.GetLogger<Utils>();

        public static bool IsTaskbarPinned()
        {
            var taskBarPath = GetTaskbarShortcutPath();
            return File.Exists(taskBarPath);
        }

        public static string GetTaskbarShortcutPath()
        {
            const string relativeTaskBarPath = @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar";
            var taskBarPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), relativeTaskBarPath);

            if (Directory.Exists(taskBarPath))
            {
                try
                {
                    var lnkFiles = Directory.GetFiles(taskBarPath, "*.lnk");
                    var shell = new Shell();
                    var thisExecutableName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
                    foreach (var lnkFile in lnkFiles)
                    {
                        var folder = shell.NameSpace(Path.GetDirectoryName(lnkFile));
                        var folderItem = folder.ParseName(Path.GetFileName(lnkFile));
                        if (folderItem != null && folderItem.IsLink)
                        {
                            var link = (ShellLinkObject)folderItem.GetLink;
                            var linkFileName = Path.GetFileName(link.Path);

                            if (linkFileName == thisExecutableName)
                                return lnkFile;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to scan taskbar icon links. Using default path...");
                }
            }

            return Path.Combine(taskBarPath, "EverythingToolbar.lnk");
        }

        public static bool IsTaskbarCenterAligned()
        {
            if (ToolbarSettings.User.IsForceCenterAlignment)
                return true;

            if (Helpers.Utils.GetWindowsVersion() < Helpers.Utils.WindowsVersion.Windows11)
                return false;

            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
            {
                var taskbarAlignment = key?.GetValue("TaskbarAl");
                var leftAligned = taskbarAlignment != null && (int)taskbarAlignment == 0;
                return !leftAligned;
            }
        }

        public static bool GetAutostartState()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                return key?.GetValue("EverythingToolbar") != null;
            }
        }

        public static void SetAutostartState(bool enabled)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                try
                {
                    if (enabled)
                        key?.SetValue("EverythingToolbar", "\"" + Process.GetCurrentProcess().MainModule.FileName + "\"");
                    else
                        key?.DeleteValue("EverythingToolbar", false);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to set autostart state.");
                }
            }
        }

        public static void ChangeTaskbarPinIcon(string iconName, bool restartExplorer)
        {
            var taskbarShortcutPath = GetTaskbarShortcutPath();

            if (File.Exists(taskbarShortcutPath))
                File.Delete(taskbarShortcutPath);

            var shell = new WshShell();
            var shortcut = (IWshShortcut)shell.CreateShortcut(taskbarShortcutPath);
            shortcut.TargetPath = Process.GetCurrentProcess().MainModule.FileName;
            shortcut.IconLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, iconName);
            shortcut.Save();

            if (!restartExplorer)
                return;

            foreach (var process in Process.GetProcessesByName("explorer"))
                process.Kill();
        }

        public static string GetThemedAppIconName()
        {
            switch (SystemThemeManager.GetCachedSystemTheme())
            {
                case SystemTheme.Light:
                    return "Icons/Light.ico";
                case SystemTheme.Dark:
                    return "Icons/Dark.ico";
                default:
                    return "Icons/Medium.ico";
            }
        }
    }
}