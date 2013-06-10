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

                    App.Popups.ShowSaveDeck(this, "http://www.scrollsguide.com/deckbuilder/?d=123", "");
                }
                if (LobbyMenu.drawButton(positioner3.getButtonRect(4f), "Sync Deck"))
                {
                    log.WriteLine("click :)");
                }
            }
            else if (info.TargetMethod().Equals("ShowSaveDeck"))
            {
                if (isImport)
                {
                    Type popupType = typeof(Popups);

                    FieldInfo description = popupType.GetField("description", BindingFlags.NonPublic | BindingFlags.Instance);
                    description.SetValue(App.Popups, "Insert the link to your deck:");

                    FieldInfo header = popupType.GetField("header", BindingFlags.NonPublic | BindingFlags.Instance);
                    header.SetValue(App.Popups, "Import deck");

                    FieldInfo okText = popupType.GetField("okText", BindingFlags.NonPublic | BindingFlags.Instance);
                    okText.SetValue(App.Popups, "Import");
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

                loadFromWeb("143");
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
                    loadFromWeb(m.Groups[1].Value);
                }
                else
                {
                    log.WriteLine("No regex");
                }

                Type deckBuilderType = typeof(DeckBuilder2);
                FieldInfo initedInfo = deckBuilderType.GetField("inited", BindingFlags.NonPublic | BindingFlags.Instance);

                inited = (bool)initedInfo.GetValue(deckBuilder);
                if (inited)
                {
                    log.WriteLine("inited");

                    FieldInfo deckListInfo = deckBuilderType.GetField("allCardsDict", BindingFlags.NonPublic | BindingFlags.Instance);
                    allCardsDict = (Dictionary<long, Card>)deckListInfo.GetValue(deckBuilder);
                   
                    
                    MethodInfo mo = deckBuilderType.GetMethod("createDeckCard", BindingFlags.NonPublic | BindingFlags.Instance);
                    DeckCard dc =(DeckCard) mo.Invoke(deckBuilder, new object[] { (Card)allCardsDict[5907178], (Vector3)Vector3.zero });

                    MethodInfo mi = deckBuilderType.GetMethod("clearBoard", BindingFlags.Instance);
                    mi.Invoke(deckBuilder, null);
                }
                else
                {
                    log.WriteLine("not inited");
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

        }
    }
}
