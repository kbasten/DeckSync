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
    public class DeckSync : BaseMod, IOkStringCancelCallback, IOkCallback
    {

        private DeckBuilder2 deckBuilder = null;

        public static bool loaded = false;

        private bool inited = false;
        private Dictionary<long, Card> allCardsDict = null;

        private Type deckBuilderType = typeof(DeckBuilder2);

		private GUISkin buttonSkin = (GUISkin)Resources.Load("_GUISkins/Lobby");

		private float currentTableCardZ;

		private bool isImporting = false;

        public override void AfterInvoke(InvocationInfo info, ref object returnValue)
        {
            if (info.targetMethod.Equals("OnGUI"))
            {
				if (deckBuilder == null)
				{
					deckBuilder = (DeckBuilder2)info.target;
				}

				GUI.skin = buttonSkin;
                GUIPositioner positioner3 = App.LobbyMenu.getSubMenuPositioner(1f, 5);
                GUI.skin = buttonSkin;
                if (LobbyMenu.drawButton(positioner3.getButtonRect(3f), "Import Deck"))
				{
					App.Popups.ShowOk(this, "impintro", "Warning", "Using the deck import feature may crash your game on rare occasions, use with caution!", "Ok");
                }
            }
        }

        public override bool BeforeInvoke(InvocationInfo info, out object returnValue)
        {
            returnValue = null;

            return isImporting;
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version)
        {
			try
			{
				return new MethodDefinition[] {
						scrollsTypes["DeckBuilder2"].Methods.GetMethod("OnGUI")[0] // to draw gui buttons on the deckbuilder screen
				};
			}
			catch
			{
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
                patterns.Add(new RegexPattern("^(http://)?www\\.scrollsguide\\.com/deckbuilder/\\?d=([0-9]+)$", 2));
                patterns.Add(new RegexPattern("^(http://)?www\\.scrollsguide\\.com/deckbuilder/?#([0-9]+)$", 2)); // not sure whether the / after deckbuilder is required...
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

			WebClientTimeOut wc = new WebClientTimeOut();
			/*
			wc.DownloadStringCompleted += (sender, e) =>
			{
				processDeck(e.Result);
			};
			 * */
			wc.TimeOut = 5000;
			Console.WriteLine("Loading deck " + deckId);
			String r = wc.DownloadString("http://a.scrollsguide.com/deck/load?id=" + deckId);
			processDeck(r);
        }

		private void processDeck(String result)
		{
			JsonReader reader = new JsonReader();

			Console.WriteLine("Deck result: " + result);



			try
			{
				ApiDeckMessage adm = (ApiDeckMessage)reader.Read(result, System.Type.GetType("ApiDeckMessage"));

				if (adm.msg.Equals("success"))
				{
					if (adm.data.deleted == 1)
					{
						App.Popups.ShowOk(null, "fail", "Import failed", "That deck is deleted.", "Ok");
					}
					else
					{
						DeckCardsMessage dcm = new DeckCardsMessage();
						dcm.metadata = null;
						dcm.valid = false;
						dcm.deck = adm.data.name;

						List<long> done = new List<long>();
						List<Card> toPlace = new List<Card>();
						foreach (KeyValuePair<long, Card> singleScroll in allCardsDict)
						{
							for (int i = 0; i < adm.data.scrolls.Length; i++)
							{
								DeckScroll d = adm.data.scrolls[i];

								if (singleScroll.Value.getCardType().id == d.id && d.c > 0 && !done.Contains(singleScroll.Key)) // this scroll needs to be added to the deck still
								{
									d.c--;
									done.Add(singleScroll.Key);
									Card c = new Card();
									c.id = singleScroll.Key;
									toPlace.Add(c);
									Console.WriteLine("Added to toPlaceOnBoard " + singleScroll.Key + " " + adm.data.name);
								}
							}
						}

						dcm.cards = toPlace.ToArray<Card>();

						try
						{
							Console.WriteLine("now importing...");
							MethodInfo mo = deckBuilderType.GetMethod("handleMessage", BindingFlags.Public | BindingFlags.Instance, null, new Type[]{typeof(DeckCardsMessage)}, null);
							mo.Invoke(deckBuilder, new object[] { (DeckCardsMessage)dcm });
						}
						catch (Exception w)
						{
							Console.WriteLine(w);
						}
						
						 /*
						this.currentTableCardZ = 800f;
						List<long> toPlaceOnBoard = new List<long>();
						DepletingMultiMapQuery<long, Vector3> positions = new DepletingMultiMapQuery<long, Vector3>();
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
									positions.Add(singleScroll.Key, new Vector3(0.5f, 0.5f, getNextZ()));
								}
							}
						}

						if (toPlaceOnBoard.Count > 0 && deckBuilder != null)
						{
							try
							{
								Console.WriteLine("now importing...");
								MethodInfo mo = deckBuilderType.GetMethod("loadDeck", BindingFlags.NonPublic | BindingFlags.Instance);
								mo.Invoke(deckBuilder, new object[] { (string)adm.data.name, (List<long>)toPlaceOnBoard, null });
							}
							catch (Exception w)
							{
								Console.WriteLine(w);
							}
						}
						else
						{
							Console.WriteLine("Null or 0");
						}
						 */
					}
				}
				else
				{
					App.Popups.ShowOk(null, "fail", "Import failed", "That deck does not exist.", "Ok");
				}
			}
			catch  // just... general fail
			{
				App.Popups.ShowOk(null, "fail", "Import failed", "Failed to import deck, please try again later.", "Ok");
			}
		}
		private float getNextZ()
		{
			return (this.currentTableCardZ -= 0.05f);
		}

		public void PopupOk(string popupType) // this is the first popup with the warning
		{
			MethodInfo mo = deckBuilderType.GetMethod("clearTable", BindingFlags.Public | BindingFlags.Instance);
			mo.Invoke(deckBuilder, new object[] { });
			App.Popups.ShowTextInput(this, "", "", "impdeck", "Import deck", "Insert the link to your deck:", "Import");
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

	internal class DepletingMultiMapQuery<K, T>
	{
		private Dictionary<K, Item<K, T>> d;

		public DepletingMultiMapQuery()
		{
			this.d = new Dictionary<K, Item<K, T>>();
		}

		public void Add(K id, T obj)
		{
			if (!this.d.ContainsKey(id))
			{
				this.d.Add(id, new Item<K, T>());
			}
			this.d[id].list.Add(obj);
		}

		public T getNext(K id)
		{
			return this.d[id].getNext();
		}

		public bool hasNext(K id)
		{
			Item<K, T> item = null;
			return (this.d.TryGetValue(id, out item) && item.hasNext());
		}

		private class Item<M, N>
		{
			public int index;
			public List<T> list;

			public Item()
			{
				this.index = -1;
				this.list = new List<T>();
			}

			public T getNext()
			{
				return this.list[++this.index];
			}

			public bool hasNext()
			{
				return (this.index < (this.list.Count - 1));
			}
		}
	}
}
