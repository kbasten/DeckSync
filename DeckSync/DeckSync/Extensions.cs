using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public static class Extensions
{
    public static void ShowTextInput(this Popups popups, IOkStringCancelCallback callback, string loadedDeckName, string problems, string popupType, string header, string description, string okText)
    {
        popups.ShowSaveDeck(callback, loadedDeckName, problems);
        Type popupT = typeof(Popups);
        popupT.GetField("popupType", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(popups, popupType);
        popupT.GetField("header", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(popups, header);
        popupT.GetField("description", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(popups, description);
        popupT.GetField("okText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(popups, okText);
    }
}

