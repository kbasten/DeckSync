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
    public class DeckSync : BaseMod, IOkStringCancelCallback
    {

        private DeckBuilder2 deckBuilder = null;

        public static bool loaded = false;

        private bool inited = false;
        private Dictionary<long, Card> allCardsDict = null;

        private Type deckBuilderType = typeof(DeckBuilder2);

        private GUISkin buttonSkin = (GUISkin)Resources.Load("_GUISkins/Lobby");

        public override void AfterInvoke(InvocationInfo info, ref object returnValue)
        {
            if (info.targetMethod.Equals("OnGUI"))
            {
				if (deckBuilder == null)
					deckBuilder = (DeckBuilder2)info.Target;

                GUIPositioner positioner3 = App.LobbyMenu.getSubMenuPositioner(1f, 5);
                GUI.skin = buttonSkin;
                if (LobbyMenu.drawButton(positioner3.getButtonRect(3f), "Import Deck"))
                {
                    App.Popups.ShowTextInput(this, "http://www.scrollsguide.com/deckbuilder/#143", "", "impdeck", "Import deck", "Insert the link to your deck:", "Import");
                }
            }
        }

        public override bool BeforeInvoke(InvocationInfo info, out object returnValue)
        {
            returnValue = null;
            return false;
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version)
        {
            try {	
				return new MethodDefinition[] {
                    scrollsTypes["DeckBuilder2"].Methods.GetMethod("OnGUI")[0]
            	};
			} catch {
				return new MethodDefinition[] { };
			}
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

        }

        public void PopupOk(string popupType, string choice)
        {
            if (popupType == "impdeck")
            {
                // try a few regex patterns until a match is found
                List<RegexPattern> patterns = new List<RegexPattern>();
                patterns.Add(new RegexPattern("^http://www\\.scrollsguide\\.com/deckbuilder/\\?d=([0-9]+)$", 1));
                patterns.Add(new RegexPattern("^http://www\\.scrollsguide\\.com/deckbuilder/?#([0-9]+)$", 1)); // not sure whether the / after deckbuilder is required...
                patterns.Add(new RegexPattern("^#?([0-9]+)$", 1));

                bool hasMatch = false;
                for (int i = 0; i < patterns.Count && !hasMatch; i++)
                {
                    RegexPattern pattern = patterns[i];

                    Match m = Regex.Match(choice, pattern.getPattern());
                    if (m.Success)
                    {
                        hasMatch = true;

                        FieldInfo initedInfo = deckBuilderType.GetField("inited", BindingFlags.NonPublic | BindingFlags.Instance);

                        inited = (bool)initedInfo.GetValue(deckBuilder);
                        if (inited)
                        {
                            FieldInfo deckListInfo = deckBuilderType.GetField("allCardsDict", BindingFlags.NonPublic | BindingFlags.Instance);
                            allCardsDict = (Dictionary<long, Card>)deckListInfo.GetValue(deckBuilder);

                            loadFromWeb(m.Groups[pattern.getMatchNum()].Value);
                        }
                    }
                }
                if (!hasMatch)
                {
                    App.Popups.ShowOk(null, "fail", "Invalid deck", "That is not a valid link to your deck.", "Ok");
                }
            }
        }

        private void loadFromWeb(String deckId)
        {
            String deckJSON = new WebClient().DownloadString("http://a.scrollsguide.com/deck/load?id=" + deckId);

            JsonReader reader = new JsonReader();

            try
            {
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
                        // mo.Invoke(deckBuilder, new object[]{(int)1});
                    }
                }
                else
                {
                    App.Popups.ShowOk(null, "fail", "Import failed", "That deck does not exist, or is deleted.", "Ok");
                }
            }
            catch  // just... general fail
            {
                App.Popups.ShowOk(null, "fail", "Import failed", "That deck does not exist, or is deleted.", "Ok");
            }
        }
    }

    internal class RegexPattern
    {
        private String pattern;
        private int matchNum;

        public RegexPattern(String pattern, int matchNum)
        {
            this.pattern = pattern;
            this.matchNum = matchNum;
        }

        public String getPattern()
        {
            return this.pattern;
        }
        public int getMatchNum()
        {
            return this.matchNum;
        }
    }
}
