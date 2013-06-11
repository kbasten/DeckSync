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
        private StreamWriter log;

        private bool isImport = false;

        private bool inited = false;
        private Dictionary<long, Card> allCardsDict = null;

        private Type deckBuilderType = typeof(DeckBuilder2);

        ~DeckSync()
        {
            closeLog();
        }

        private void closeLog()
        {
            log.Flush();
            log.Close();
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue)
        {
            if (info.TargetMethod().Equals("OnGUI"))
            {
                GUIPositioner positioner3 = App.LobbyMenu.getSubMenuPositioner(1f, 5);
                if (LobbyMenu.drawButton(positioner3.getButtonRect(3f), "Import Deck"))
                {
                    log.WriteLine("Importing");
                    isImport = true;

                    App.Popups.ShowSaveDeck(this, "http://www.scrollsguide.com/deckbuilder/?d=143", "");
                }
                if (LobbyMenu.drawButton(positioner3.getButtonRect(4f), "Sync Deck"))
                {
                    log.WriteLine("click :)");
                }
            }
            else if (info.TargetMethod().Equals("ShowSaveDeck"))
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
            if (info.TargetMethod().Equals("addListener"))
            {
                if (info.Arguments()[0] is DeckBuilder2)
                {
                    deckBuilder = (DeckBuilder2)info.Arguments()[0];
                    log.WriteLine("now added deckbuidler");
                }
            }
            returnValue = null;
            return false;
        }

        public override Mono.Cecil.MethodDefinition[] GetHooks(Mono.Cecil.TypeDefinitionCollection scrollsTypes, int version)
        {
            return new MethodDefinition[] {
                    scrollsTypes["Communicator"].Methods.GetMethod("addListener", new Type[]{typeof(ICommListener)}),
                    scrollsTypes["DeckBuilder2"].Methods.GetMethod("OnGUI")[0],
                    scrollsTypes["Popups"].Methods.GetMethod("ShowSaveDeck", new Type[]{ typeof(IOkStringCancelCallback), typeof(String), typeof(String)})
            };
        }

        public override string GetName()
        {
            return "DeckSync";
        }

        public override int GetVersion()
        {
            return 1;
        }

        public override void Init()
        {
            if (!DeckSync.loaded)
            {
                try
                {
                    log = File.CreateText("DeckSync.log");
                    log.AutoFlush = true;
                }
                catch (IOException e)
                {
                    Console.WriteLine(e);
                }
                DeckSync.loaded = true;
            }
        }

        public void PopupCancel(string popupType)
        {
            isImport = false;
        }

        public void PopupOk(string popupType, string choice)
        {
            if (popupType == "savedeck" && isImport)
            {
                log.WriteLine("now loading deck " + choice);

                Match m = Regex.Match(choice, "^http://www\\.scrollsguide\\.com/deckbuilder/\\?d=([0-9]+)$");
                if (m.Success)
                {
                    log.WriteLine("Regex: " + m.Groups[1].Value);

                    FieldInfo initedInfo = deckBuilderType.GetField("inited", BindingFlags.NonPublic | BindingFlags.Instance);

                    inited = (bool)initedInfo.GetValue(deckBuilder);
                    if (inited)
                    {
                        log.WriteLine("inited");

                        FieldInfo deckListInfo = deckBuilderType.GetField("allCardsDict", BindingFlags.NonPublic | BindingFlags.Instance);
                        allCardsDict = (Dictionary<long, Card>)deckListInfo.GetValue(deckBuilder);

                        loadFromWeb(m.Groups[1].Value);
                    }
                    else
                    {
                        log.WriteLine("not inited");
                    }
                }
                else
                {
                    log.WriteLine("No regex");
                }

            }
            else
            {
                if (!isImport)
                {
                    log.WriteLine("not isimport");
                }
                else
                {
                    log.WriteLine("isimport");
                }
            }
        }

        private void loadFromWeb(String deckId)
        {
            String deckJSON = new WebClient().DownloadString("http://a.scrollsguide.com/deck/load?id=" + deckId);

            log.WriteLine(deckJSON);

            JsonReader reader = new JsonReader();
            ApiDeckMessage adm = reader.Read(deckJSON, System.Type.GetType("ApiDeckMessage")) as ApiDeckMessage;

            log.WriteLine(adm.msg);

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
