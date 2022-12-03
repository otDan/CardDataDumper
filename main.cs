using BepInEx;
using ClassesManagerReborn.Util;
using HarmonyLib;
using ModdingUtils.Utils;
using RarityLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using UnboundLib;
using UnboundLib.Cards;
using UnboundLib.Utils;
using UnityEngine;
using WillsWackyManagers.Utils;
using static UnityEngine.Random;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;

namespace CardDataDumper
{
    // Declares our Mod to Bepin
    [BepInPlugin(ModId, ModName, Version)]
    // The game our Mod Is associated with
    [BepInProcess("Rounds.exe")]
    public class main : BaseUnityPlugin
    {
        private const string ModId = "com.Root.Dump";
        private const string ModName = "Card Data Dumper";
        public const string Version = "0.0.0";
        public const string Databse = "Cards.sqlite";
        public const string BaseURL = "https://rounds.thunderstore.io/package/";
        public List<string> ProsessedCards = new List<string>();
        public Dictionary<string,string> modurls = new Dictionary<string,string>();
        //SQLiteConnection m_dbConnection;
        string CardData = "{\"Cards\": [";
        string StatsData = "{\"Stats\": [";
        string ThemeData = "{\"Theme\": [";
        string MapsData = "{\"Maps\": [";
        internal static List<CardInfo> allCards
        {
            get
            {
                List<CardInfo> list = new List<CardInfo>();
                list.AddRange((ObservableCollection<CardInfo>)typeof(CardManager).GetField("activeCards", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null));
                list.AddRange((List<CardInfo>)typeof(CardManager).GetField("inactiveCards", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null));
                list.Sort((CardInfo x, CardInfo y) => string.CompareOrdinal(x.gameObject.name, y.gameObject.name));
                return list;
            }
        }
        void Start()
        {
            modurls.Add("Vanilla", "https://landfall.se/rounds");
            Unbound.Instance.ExecuteAfterFrames(100, delegate {
                /*getModData();/**/
                StartCoroutine(GetCardData());
                DoThemes();/**/
                getMapData();
            });
        }
        public void getModData()
        {
            List<CardInfo> cards = ((List<CardInfo>)typeof(ModdingUtils.Utils.Cards).GetField("hiddenCards", BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ModdingUtils.Utils.Cards.instance)).ToList();
            cards.AddRange(allCards);
            cards.ForEach(c => GetCardSorce(c));
            UnityEngine.Debug.Log("Writing");
            string ModData = "{\"Mods\":[";
            modurls.Keys.ToList().ForEach(mod => {
                ModData += $"{{\"Mod\":\"{mod}\",\"Url\":\"{modurls[mod]}\"}},";
            });
            ModData = ModData.Substring(0, ModData.Length - 1) + "]}";
            File.WriteAllText("./jsons/Mods.Json", ModData);
            UnityEngine.Debug.Log("done");
        }

        public void getMapData()
        {
            foreach(string map in LevelManager.levels.Keys)
            {
                MapsData += $"{{\"ID\":\"{LevelManager.GetVisualName(map)}\",\"Name\":\"{LevelManager.GetVisualName(LevelManager.levels[map].name)}\",\"Mod\":\"{LevelManager.levels[map].category}\"}},";
            }
            MapsData = MapsData.Substring(0, MapsData.Length - 1) + "]}";
            File.WriteAllText("./jsons/Maps.Json", MapsData);
        }

        public IEnumerator GetCardArt(string card,Guid g)
        {
            var camObj = MainMenuHandler.instance.gameObject.transform.parent.parent.GetComponentInChildren<MainCam>().gameObject;
            var camera = camObj.GetComponent<Camera>();
            var lighObj = camObj.transform.parent.Find("Lighting/LightCamera").gameObject;
            var lightCam = lighObj.GetComponent<Camera>();
            Destroy(lighObj.GetComponent<SFRenderer>());
            MainMenuHandler.instance.gameObject.SetActive(false);
            GameObject cardObject = PhotonNetwork.PrefabPool.Instantiate(card,
                new Vector3(camObj.transform.position.x, camObj.transform.position.y-2f, camObj.transform.position.z+10),
                camera.transform.rotation);
            cardObject.transform.localScale = Vector3.one * 2;
            cardObject.SetActive(true);
            cardObject.GetComponentInChildren<CardVisuals>().firstValueToSet = true;
            Destroy(FindObjectInChildren(cardObject, "UI_ParticleSystem"));
            for(int _ = 0; _< 60; _++) yield return null;
            const int resWidth = 350;
            const int resHeight = 500;
            var rt = new RenderTexture(resWidth, resHeight, 24);
            camera.targetTexture = rt;
            var screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            camera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);


            camera.targetTexture = null;
            RenderTexture.active = null;
            // Destroy render texture to avoid null errors
            Destroy(rt);

            // Get camera to take picture from

            var rt1 = new RenderTexture(resWidth, resHeight, 24);
            lightCam.targetTexture = rt1;
            var screenShot1 = new Texture2D(resWidth, resHeight, TextureFormat.ARGB32, false);
            lightCam.Render();
            RenderTexture.active = rt1;
            screenShot1.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            lightCam.targetTexture = null;
            RenderTexture.active = null;
            // Destroy render texture to avoid null errors
            Destroy(rt1);

            // Combine the two screenshots if alpha is zero on screenshot 1
            var pixels = screenShot.GetPixels(0, 0, screenShot.width, screenShot.height);
            var pixels1 = screenShot1.GetPixels(0, 0, screenShot.width, screenShot.height);
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a != 0)
                {
                    pixels[i].a = 1;
                }
                if (pixels1[i].a != 0)
                {
                    pixels1[i].a = 1;
                }

                if (pixels[i].a == 0)
                {
                    pixels[i] = pixels1[i];
                }
            }
            screenShot.SetPixels(pixels);

            var bytes = screenShot.EncodeToPNG();
            var dir = Directory.CreateDirectory(Path.Combine(Paths.ConfigPath, "Cards"));
            var filename = Path.Combine(dir.FullName, card + ".png");
            File.WriteAllBytes($"./cards/{g}.png", bytes);
            Destroy(cardObject);
            /*
            Destroy(camera);
            Destroy(cardObject);
            Destroy(screenShot);
            Destroy(rt);*/
        }
        public void DoThemes()
        {
            CardChoice.instance.cardThemes.ToList().ForEach(theme => {
                ThemeData += $"{{\"Name\":\"{theme.themeType}\",\"Color\":\"{FormatColor(theme.targetColor)}\"}},";
            });
            ThemeData = ThemeData.Substring(0, ThemeData.Length - 1);
            ThemeData += "]}";
            File.WriteAllText("./jsons/Themes.Json", ThemeData);
        }
        public string FormatColor(Color color)
        {
            string r = $"00{((int)(color.r * 256)):x}";
            string g = $"00{((int)(color.g * 256)):x}";
            string b = $"00{((int)(color.b * 256)):x}";
            return r.Substring(r.Length - 2) + g.Substring(g.Length - 2) + b.Substring(b.Length - 2);
        }
        public string GetCardSorce(CardInfo card)
        {
            try
            {
                PluginInfo[] pluginInfos = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.ToArray();
                foreach (PluginInfo info in pluginInfos)
                {
                    Assembly mod = Assembly.LoadFile(info.Location);
                    if (card.gameObject.GetComponent<CustomCard>().GetType().Assembly.GetName().ToString() == mod.GetName().ToString())
                    {
                        string local = info.Location;
                        while(local.Last() != '\\')
                        {
                            local = local.Substring(0, local.Length - 1);
                        }
                        File.WriteAllBytes($"./modicons/{info.Metadata.Name}.png",File.ReadAllBytes(local+"icon.png"));
                        if (!modurls.ContainsKey(info.Metadata.Name))
                        {
                            local = local.Substring(0, local.Length - 1);
                            string author = "";
                            string package = "";
                            while (local.Last() != '-')
                            {
                                package = local.Last().ToString() + package;
                                local = local.Substring(0, local.Length - 1);
                            }
                            local = local.Substring(0, local.Length - 1);
                            while (local.Last() != '\\')
                            {
                                author = local.Last().ToString() + author;
                                local = local.Substring(0, local.Length - 1);
                            }
                            modurls.Add(info.Metadata.Name, $"{BaseURL}{author}/{package}");
                            UnityEngine.Debug.Log($"{BaseURL}{author}/{package}");
                        }
                        return info.Metadata.Name;
                    }
                }
            }
            catch(Exception e) { UnityEngine.Debug.Log(e); }
            return "Vanilla";
        }

        public string GetCardClass(CardInfo card)
        {
            CustomCard card1 = card.GetComponent<CustomCard>();
            if (card1 != null)
                card1.Callback();
            ClassNameMono mono = card.gameObject.GetComponent<ClassNameMono>();
            if (mono == null)
                return "None";
            else
                return mono.className;
        }

        public IEnumerator GetCardData()
        {
            List<CardInfo> hiddenCards = ((List<CardInfo>)typeof(ModdingUtils.Utils.Cards).GetField("hiddenCards", BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ModdingUtils.Utils.Cards.instance)).ToList();
            int count = allCards.Count + hiddenCards.Count;
            int curnet = 0;
            foreach (CardInfo card in allCards)
            {
                UnityEngine.Debug.Log($"{++curnet}/{count}");
                if (!ProsessedCards.Contains(card.name))
                {
                    Guid g = Guid.NewGuid();
                    yield return GetCardArt(card.name, g);
                    UnityEngine.Debug.Log(GetCardSorce(card));
                    //new SQLiteCommand($"INSERT INTO Cards (ID,Name,Rarity,Theme,Descripion,IsCurse) VALUES ({card.name},{card.cardName},{card.colorTheme},{card.cardDestription},{card.categories.Contains(CurseManager.instance.curseCategory)})", m_dbConnection).ExecuteNonQuery();
                    CardData += $"{{\"ID\":\"{card.name}\",\"art\":\"{g}\",\"Name\":\"{card.cardName}\",\"Rarity\":\"{card.rarity}\",\"Theme\":\"{card.colorTheme}\",\"Description\":\"{(card.cardDestription == null ? "" : card.cardDestription.Replace("\"", "\\\""))}\",\"IsCurse\":{card.categories.Contains(CurseManager.instance.curseCategory).ToString().ToLower()},\"Mod\":\"{GetCardSorce(card)}\",\"Multiple\":{card.allowMultiple.ToString().ToLower()},\"Class\":\"{GetCardClass(card)}\",\"Hidden\":{false.ToString().ToLower()}}},";
                    UnityEngine.Debug.Log($"({card.name}):\n{card.cardName}({card.rarity},Theme:{card.colorTheme})\n{card.cardDestription}\n{GetCardStats(card)}\n\n\n");
                    ProsessedCards.Add(card.name);
                }
                else { UnityEngine.Debug.Log("Error Duplict Card"); }
            }
            foreach (CardInfo card in hiddenCards)
            {
                UnityEngine.Debug.Log($"{++curnet}/{count}");
                if (!ProsessedCards.Contains(card.name))
                {
                    Guid g = Guid.NewGuid();
                    yield return GetCardArt(card.name, g);
                    UnityEngine.Debug.Log(GetCardSorce(card));
                    //new SQLiteCommand($"INSERT INTO Cards (ID,Name,Rarity,Theme,Descripion,IsCurse) VALUES ({card.name},{card.cardName},{card.colorTheme},{card.cardDestription},{card.categories.Contains(CurseManager.instance.curseCategory)})", m_dbConnection).ExecuteNonQuery();
                    CardData += $"{{\"ID\":\"{card.name}\",\"art\":\"{g}\",\"Name\":\"{card.cardName}\",\"Rarity\":\"{card.rarity}\",\"Theme\":\"{card.colorTheme}\",\"Description\":\"{(card.cardDestription == null ? "" : card.cardDestription.Replace("\"", "\\\""))}\",\"IsCurse\":{card.categories.Contains(CurseManager.instance.curseCategory).ToString().ToLower()},\"Mod\":\"{GetCardSorce(card)}\",\"Multiple\":{card.allowMultiple.ToString().ToLower()},\"Class\":\"{GetCardClass(card)}\",\"Hidden\":{true.ToString().ToLower()}}},";
                    UnityEngine.Debug.Log($"({card.name}):\n{card.cardName}({card.rarity},Theme:{card.colorTheme})\n{card.cardDestription}\n{GetCardStats(card)}\n\n\n");
                    ProsessedCards.Add(card.name);
                }
                else { UnityEngine.Debug.Log("Error Duplict Card"); }
            }
            CardData = CardData.Substring(0, CardData.Length - 1);
            CardData += "]}";
            CardData = CardData.Replace("\n", "\\n");
            StatsData = StatsData.Substring(0, StatsData.Length - 1);
            StatsData += "]}";
            StatsData = StatsData.Replace("\n", "\\n");
            File.WriteAllText("./jsons/Cards.Json", CardData);
            File.WriteAllText("./jsons/Stats.Json", StatsData);
            string ModData = "{\"Mods\":[";
            modurls.Keys.ToList().ForEach(mod => {
                ModData += $"{{\"Mod\":\"{mod}\",\"Url\":\"{modurls[mod]}\"}},";
            });
            ModData = ModData.Substring(0,ModData.Length - 1) + "]}";
            File.WriteAllText("./jsons/Mods.Json", ModData);
        }
        public string GetCardStats(CardInfo card)
        {
            CardInfoStat[] cardStats = card.cardStats;
            string value = "";

            for (int i = 0; i < cardStats.Length; i++) 
            {
                CardInfoStat stat = cardStats[i];
                // new SQLiteCommand($"INSERT INTO Cards (Index,Amount,Stat,Card) VALUES ({i},{stat.amount},{stat.stat}{card.name})", m_dbConnection).ExecuteNonQuery();
                StatsData += $"{{\"Idex\":{i},\"Amount\":\"{(stat.amount == null ? "" : stat.amount.Replace("\"", "\\\""))}\",\"Stat\":\"{(stat.stat == null ? "" : stat.stat.Replace("\"", "\\\""))}\",\"Card\":\"{card.name}\"}},";
                value += $"({(stat.positive?"Pos":"Neg")}){stat.amount} {stat.stat}\n";
            }

            return value;
        }

        private static GameObject FindObjectInChildren(GameObject gameObject, string gameObjectName)
        {
            Transform[] children = gameObject.GetComponentsInChildren<Transform>(true);
            return (from item in children where item.name == gameObjectName select item.gameObject).FirstOrDefault();
        }
    }
}
