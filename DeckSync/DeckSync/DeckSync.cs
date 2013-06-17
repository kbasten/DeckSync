using Mono.Cecil;
using ScrollsModLoader.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;
using System.Text.RegularExpressions;
using System.Net;
using JsonFx.Json;
using UnityEngine;

namespace DeckSync
{
    public class DeckSync : BaseMod , IOkStringCancelCallback
    {

        private DeckBuilder2 deckBuilder = null;

        public static bool loaded = false;

        private bool isImport = false;

        private bool inited = false;
        private Dictionary<long, Card> allCardsDict = null;

        private Type deckBuilderType = typeof(DeckBuilder2);

        public override void AfterInvoke(InvocationInfo info, ref object returnValue)
        {
            if (info.targetMethod.Equals("OnGUI"))
            {
                GUIPositioner positioner3 = App.LobbyMenu.getSubMenuPositioner(1f, 5);
                if (LobbyMenu.drawButton(positioner3.getButtonRect(3f), "Import Deck"))
                {
                    isImport = true;

                    App.Popups.ShowSaveDeck(this, "http://www.scrollsguide.com/deckbuilder/?d=143", "");
                }
            }
            else if (info.targetMethod.Equals("ShowSaveDeck"))
            {
                Type popupType = typeof(Popups);
                if (isImport)
                {
                    FieldInfo description = popupType.GetField("description", BindingFlags.NonPublic | BindingFlags.Instance);
                    description.SetValue(App.Popups, "Insert the link to your deck:");

                    FieldInfo header = popupType.GetField("header", BindingFlags.NonPublic | BindingFlags.Instance);
                    header.SetValue(App.Popups, "Import deck");

                    FieldInfo okText = popupType.GetField("okText", BindingFlags.NonPublic | BindingFlags.Instance);
                    okText.SetValue(App.Popups, "Import");
                }
                else
                {
                    FieldInfo description = popupType.GetField("description", BindingFlags.NonPublic | BindingFlags.Instance);
                    description.SetValue(App.Popups, "Please name your deck");

                    FieldInfo header = popupType.GetField("header", BindingFlags.NonPublic | BindingFlags.Instance);
                    header.SetValue(App.Popups, "Save deck");

                    FieldInfo okText = popupType.GetField("okText", BindingFlags.NonPublic | BindingFlags.Instance);
                    okText.SetValue(App.Popups, "Save");

                }
            }
        }

        public override bool BeforeInvoke(InvocationInfo info, out object returnValue)
        {
            if (info.targetMethod.Equals("addListener"))
            {
                if (info.arguments[0] is DeckBuilder2)
                {
                    deckBuilder = (DeckBuilder2)info.arguments[0];
                }
            }
            returnValue = null;
            return false;
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version)
        {
            return new MethodDefinition[] {
                    scrollsTypes["Communicator"].Methods.GetMethod("addListener", new Type[]{typeof(ICommListener)}),
                    scrollsTypes["DeckBuilder2"].Methods.GetMethod("OnGUI")[0],
                    scrollsTypes["Popups"].Methods.GetMethod("ShowSaveDeck", new Type[]{ typeof(IOkStringCancelCallback), typeof(String), typeof(String)})
            };
        }

        public static string GetName()
        {
            return "DeckSync";
        }

        public static int GetVersion()
        {
            return 1;
        }

        public void PopupCancel(string popupType)
        {
            isImport = false;
        }

        public void PopupOk(string popupType, string choice)
        {
            if (popupType == "savedeck" && isImport)
            {
                Match m = Regex.Match(choice, "^http://www\\.scrollsguide\\.com/deckbuilder/\\?d=([0-9]+)$");
                if (m.Success)
                {
                    FieldInfo initedInfo = deckBuilderType.GetField("inited", BindingFlags.NonPublic | BindingFlags.Instance);

                    inited = (bool)initedInfo.GetValue(deckBuilder);
                    if (inited)
                    {
                        FieldInfo deckListInfo = deckBuilderType.GetField("allCardsDict", BindingFlags.NonPublic | BindingFlags.Instance);
                        allCardsDict = (Dictionary<long, Card>)deckListInfo.GetValue(deckBuilder);

                        loadFromWeb(m.Groups[1].Value);
                    }
                    else
                    {
                    }
                }
                else // no matches for regex
                {
                }

            }
            else
            {
                if (!isImport)
                {
                }
                else
                {
                }
            }
        }

        private void loadFromWeb(String deckId)
        {
            String deckJSON = new WebClient().DownloadString("http://a.scrollsguide.com/deck/load?id=" + deckId);

            JsonReader reader = new JsonReader();
            ApiDeckMessage adm = reader.Read(deckJSON, System.Type.GetType("ApiDeckMessage")) as ApiDeckMessage;

            if (adm.msg.Equals("success"))
            {
                List<long> toPlaceOnBoard = new List<long>();
                foreach (KeyValuePair<long, Card> singleScroll in allCardsDict)
                {
                    //log.WriteLine("Checking " + singleScroll.Value.getName() + " ...");
                    for (int i = 0; i < adm.data.scrolls.Length; i++)
                    {
                        DeckScroll d = adm.data.scrolls[i];
                        //log.WriteLine("Comparing " + d.id + " to " + singleScroll.Value.getCardType().id + " ...");
                        if (singleScroll.Value.getCardType().id == d.id && d.c > 0 && !toPlaceOnBoard.Contains(singleScroll.Key)) // this scroll needs to be added to the deck still
                        {
                            d.c--;
                            toPlaceOnBoard.Add(singleScroll.Key);
                            // log.WriteLine("Added to toPlaceOnBoard");
                        }
                    }
                }

                if (toPlaceOnBoard.Count > 0)
                {
                    MethodInfo mo = deckBuilderType.GetMethod("loadDeck", BindingFlags.NonPublic | BindingFlags.Instance);
                    mo.Invoke(deckBuilder, new object[] { (int)-1, (List<long>)toPlaceOnBoard, null });

                    // mo = deckBuilderType.GetMethod("alignTableCards", BindingFlags.NonPublic | BindingFlags.Instance);
                    //mo.Invoke(deckBuilder, new object[]{(int)1});
                }
            }
        }
    }
}
