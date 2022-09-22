﻿namespace Avalonia.Extensions.Controls
{
    internal enum CharClass
    {
        CharClassUnknown,
        CharClassWhitespace,
        CharClassAlphaNumeric,
    }
    public enum MessageBoxButtons
    {
        Ok,
        OkNo
    }
    public enum ShowPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
    public enum ScollOrientation
    {
        Vertical,
        Horizontal
    }
    public enum ExpandStatus
    {
        Expanded,
        Collapsed
    }
    public enum PopupLength
    {
        Short = 2000,
        Default = 5000,
        Long = 8000
    }
    public enum OS
    {
        Linux,
        OSX,
        Windows,
        Android,
        Unknow
    }
}