using BepInEx;
using CardDataDumper.Component;
using ClassesManagerReborn.Util;
using ModdingUtils.Utils;
using Photon.Pun;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CardDataDumper.Config;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using UnboundLib.Cards;
using UnboundLib.Utils;
using UnityEngine;
using WillsWackyManagers.Utils;
using Color = UnityEngine.Color;

namespace CardDataDumper.Dumper
{
    public class DumpHandler : MonoBehaviour
    {
        public static DumpHandler Instance { get; private set; }

        private string dumpPath;
        private string jsonsPath;
        private string cardsPath;
        private string iconsPath;

        private GameObject camObj;
        private GameObject lightCamObj;

        private Camera camera;
        private Camera lightCamera;

        private Resolution lastResolution;
        private Optionshandler.FullScreenOption lastFullScreen;

        public const string BaseURL = "https://rounds.thunderstore.io/package/";
        public List<string> ProsessedCards = new();
        public Dictionary<string,string> modUrls = new();

        private string CardData = "{\"Cards\": [";
        private string StatsData = "{\"Stats\": [";
        private string ThemeData = "{\"Theme\": [";
        private string MapsData = "{\"Maps\": [";

        private static List<CardInfo> allCardsList = new();
        private static List<CardInfo> allCards
        {
            get
            {
                if (allCardsList.Count != 0) return allCardsList;

                var list = new List<CardInfo>();
                list.AddRange((ObservableCollection<CardInfo>) typeof(CardManager).GetField("activeCards", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
                list.AddRange((List<CardInfo>) typeof(CardManager).GetField("inactiveCards", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
                list.Sort((card1, card2) => string.CompareOrdinal(card1.gameObject.name, card2.gameObject.name));
                allCardsList = list;

                return allCardsList;
            }
        }

        private Stopwatch stopwatch;

        private void Awake()
        {
            Instance = this;
        }

        public void StartRender()
        {
            PrepareRender();

            stopwatch = Stopwatch.StartNew();
            
            SetupDumpPath();
            SetupDirectories();
            SetupCameras();

            modUrls.Add("Vanilla", "https://landfall.se/rounds");

            CardDataDumper.Instance.StartCoroutine(GetCardData());
            GetThemes();
            GetMapData();
        }

        private void PrepareRender()
        {
            Resolution resolution = new()
            {
                height = ConfigHandler.Instance.renderHeight + 80,
                width = ConfigHandler.Instance.renderWidth + 80,
                refreshRate = 65
            };

            lastResolution = Optionshandler.resolution;
            lastFullScreen = Optionshandler.fullScreen;

            Optionshandler.instance.SetFullScreen(Optionshandler.FullScreenOption.Windowed);
            Optionshandler.instance.SetResolution(resolution);
            MainMenuHandler.instance.gameObject.SetActive(false);
        }

        private void FinishRender()
        {
            stopwatch.Stop();
            UnityEngine.Debug.Log($"Execution Time: {stopwatch.ElapsedMilliseconds}ms");
            Optionshandler.instance.SetFullScreen(lastFullScreen);
            Optionshandler.instance.SetResolution(lastResolution);
            lightCamObj.GetComponent<SFRenderer>().enabled = true;
            MainMenuHandler.instance.gameObject.SetActive(true);
        }

        private void SetupDumpPath()
        {
            // Finding the directory to dump everything, in this case the rounds thunderstore general folder
            var pluginsPath = Directory.GetParent(Paths.PluginPath)?.FullName;
            if (pluginsPath == null) return;
            var profilePath = Directory.GetParent(pluginsPath)?.FullName;
            if (profilePath == null) return;
            var profilesPath = Directory.GetParent(profilePath)?.FullName;
            if (profilesPath == null) return;
            var gamePath = Directory.GetParent(profilesPath)?.FullName;
            if (gamePath == null) return;

            // If you want to use the rounds game folder then uncomment this and comment the lines on top
            // var gamePath = Paths.GameRootPath;

            dumpPath = Path.Combine(gamePath, "dump");
            if (!Directory.Exists(dumpPath))
                Directory.CreateDirectory(dumpPath);
        }

        private void SetupDirectories()
        {
            jsonsPath = Path.Combine(dumpPath, "jsons");
            if (!Directory.Exists(jsonsPath))
                Directory.CreateDirectory(jsonsPath);
            cardsPath = Path.Combine(dumpPath, "cards");
            if (!Directory.Exists(cardsPath))
                Directory.CreateDirectory(cardsPath);
            iconsPath = Path.Combine(dumpPath, "icons");
            if (!Directory.Exists(iconsPath))
                Directory.CreateDirectory(iconsPath);
        }

        private void SetupCameras()
        {
            camObj = MainMenuHandler.instance.gameObject.transform.parent.parent.GetComponentInChildren<MainCam>().gameObject;
            camera = camObj.GetComponent<Camera>();

            lightCamObj = camObj.transform.parent.Find("Lighting/LightCamera").gameObject;
            lightCamera = lightCamObj.GetComponent<Camera>();

            lightCamObj.GetComponent<SFRenderer>().enabled = false;
        }

        private void GetMapData()
        {
            foreach(string map in LevelManager.levels.Keys)
            {
                MapsData += $"{{\"ID\":\"{LevelManager.GetVisualName(map)}\",\"Name\":\"{LevelManager.GetVisualName(LevelManager.levels[map].name)}\",\"Mod\":\"{LevelManager.levels[map].category}\"}},";
            }
            MapsData = MapsData.Substring(0, MapsData.Length - 1) + "]}";
            File.WriteAllText(Path.Combine(jsonsPath, "maps.json"), MapsData);
        }

        private IEnumerator GetCardArt(CardInfo cardInfo, Guid guid, bool transparent = false)
        {
            var camPosition = camObj.transform.position;
            GameObject cardObject = PhotonNetwork.PrefabPool.Instantiate(cardInfo.gameObject.name, new Vector3(camPosition.x, camPosition.y - 2f, camPosition.z + 10), camera.transform.rotation);
            cardObject.SetActive(true);
            
            var backObject = FindObjectInChildren(cardObject, "Back");
            var backCanvas = backObject.AddComponent<Canvas>();
            backCanvas.enabled = false;
            
            foreach (CurveAnimation curveAnimation in cardObject.gameObject.GetComponentsInChildren<CurveAnimation>(true))
            {
                foreach (var curveAnimationAnimation in curveAnimation.animations)
                {
                    curveAnimationAnimation.speed = 1000;
                }
            }
            foreach (GeneralParticleSystem generalParticleSystem in cardObject.gameObject.GetComponentsInChildren<GeneralParticleSystem>())
            {
                generalParticleSystem.enabled = false;
            }

            var scaleShake = cardObject.gameObject.GetComponentInChildren<ScaleShake>();
            scaleShake.enabled = false;
            var setScaleToZero = cardObject.gameObject.GetComponentInChildren<SetScaleToZero>();
            setScaleToZero.enabled = false;

            cardObject.gameObject.GetComponentInChildren<CardVisuals>().firstValueToSet = true;
            var canvas = cardObject.GetComponentInChildren<Canvas>();
            canvas.transform.localScale *= 2f; 

            CardAnimationHandler cardAnimationHandler = null;
            if (cardInfo.cardArt != null)
            {
                var artObject = FindObjectInChildren(cardObject.gameObject, "Art");
                if (artObject != null)
                {
                    cardAnimationHandler = cardObject.AddComponent<CardAnimationHandler>();
                    cardAnimationHandler.ToggleAnimation(false);
                }
            }

            // Wait for the card to appear presentable
            for(int _ = 0; _< 5; _++) yield return null;

            var images = new List<byte[]>();
            for (float i = 0; i < ConfigHandler.Instance.renderFrames; i+=1)
            {
                yield return TakeScreenshot(cardAnimationHandler, Mathf.InverseLerp(0, ConfigHandler.Instance.renderFrames, i), images);
            }
            CreateGif(cardInfo, images);
  
            Destroy(cardObject);
        }

        private void CreateGif(CardInfo cardInfo, List<byte[]> images)
        {
            new Thread(() => 
            {
                // Create empty image.
                var gif = Image.Load(images[0], new PngDecoder());
                // gif = gif.Clone(x => x.Crop(100, 100));

                // Set animation loop repeat count to 5.
                var gifMetaData = gif.Metadata.GetGifMetadata();
                gifMetaData.RepeatCount = 0;

                // Set the delay until the next image is displayed.
                GifFrameMetadata metadata = gif.Frames.RootFrame.Metadata.GetGifMetadata();
                metadata.FrameDelay = 1;
                if (images.Count > 0)
                {
                    for (int i = 1; i < images.Count; i++)
                    {
                        // Create a color image, which will be added to the gif.
                        var image = Image.Load(images[i], new PngDecoder());
                        // image = image.Clone(x => x.Crop(100, 100));

                        // Set the delay until the next image is displayed.
                        metadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
                        metadata.FrameDelay = 1;

                        // Add the color image to the gif.
                        gif.Frames.AddFrame(image.Frames.RootFrame);
                    }
                }

                gif.SaveAsGif(Path.Combine(cardsPath, $"{cardInfo.gameObject.name}.gif"));
            }).Start();
        }

        private IEnumerator TakeScreenshot(CardAnimationHandler cardAnimationHandler, float time, ICollection<byte[]> images, bool transparent = false)
        {
            if (cardAnimationHandler != null)
            {
                if (time != 0)
                {
                    cardAnimationHandler.ToggleAnimation(true);
                    cardAnimationHandler.SetAnimationPoint(time);
                }
                else
                {
                    yield break;
                }
            }
            else
            {
                if (time != 0)
                {
                    // We go back if the card has no art and it took the first screenshot
                    yield break;
                }
            }

            // Wait for the card to change frame
            for(int _ = 0; _< 4; _++) yield return null;

            var scrTexture = transparent ? new Texture2D(ConfigHandler.Instance.renderWidth, ConfigHandler.Instance.renderHeight, TextureFormat.ARGB32, false) : new Texture2D(ConfigHandler.Instance.renderWidth, ConfigHandler.Instance.renderHeight, TextureFormat.RGB24, false);
            RenderTexture scrRenderTexture = new(scrTexture.width, scrTexture.height, 24);
            RenderTexture camRenderTexture = camera.targetTexture;
 
            camera.targetTexture = scrRenderTexture;
            camera.Render();
            camera.targetTexture = camRenderTexture;
 
            RenderTexture.active = scrRenderTexture;
            scrTexture.ReadPixels(new Rect(0, 0, scrTexture.width, scrTexture.height), 0, 0);
            scrTexture.Apply();

            Texture2D srcLightTexture = new(ConfigHandler.Instance.renderWidth, ConfigHandler.Instance.renderHeight, TextureFormat.RGB24, false); 
            RenderTexture srcLightRenderTexture = new(srcLightTexture.width, srcLightTexture.height, 24);
            RenderTexture camLightRenderTexture = lightCamera.targetTexture;
 
            lightCamera.targetTexture = srcLightRenderTexture;
            lightCamera.Render();
            lightCamera.targetTexture = camLightRenderTexture;
 
            RenderTexture.active = srcLightRenderTexture;
            srcLightTexture.ReadPixels(new Rect(0, 0, srcLightTexture.width, srcLightTexture.height), 0, 0);
            srcLightTexture.Apply();
            
            // Combine the two screenshots if alpha is zero on screenshot 1
            var pixels = scrTexture.GetPixels(0, 0, scrTexture.width, scrTexture.height);
            var pixels1 = srcLightTexture.GetPixels(0, 0, srcLightTexture.width, srcLightTexture.height);
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
            scrTexture.SetPixels(pixels);

            var bytes = scrTexture.EncodeToPNG();
            images.Add(bytes);
        }

        private void GetThemes()
        {
            CardChoice.instance.cardThemes.ToList().ForEach(theme => { ThemeData += $"{{\"Name\":\"{theme.themeType}\",\"Color\":\"{FormatColor(theme.targetColor)}\"}},"; });
            ThemeData = ThemeData.Substring(0, ThemeData.Length - 1);
            ThemeData += "]}";
            File.WriteAllText(Path.Combine(jsonsPath, "themes.json"), ThemeData);
        }

        private string FormatColor(Color color)
        {
            string r = $"00{(int)(color.r * 256):x}";
            string g = $"00{(int)(color.g * 256):x}";
            string b = $"00{(int)(color.b * 256):x}";
            return r.Substring(r.Length - 2) + g.Substring(g.Length - 2) + b.Substring(b.Length - 2);
        }

        private string GetCardSource(CardInfo card)
        {
            try
            {
                var pluginInfos = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.ToArray();

                foreach (PluginInfo info in pluginInfos)
                {
                    Assembly mod = Assembly.LoadFile(info.Location);

                    if (card.gameObject.GetComponent<CustomCard>().GetType().Assembly.GetName().ToString() !=
                        mod.GetName().ToString()) continue;
                    string local = info.Location;
                    while (local.Last() != '\\')
                    {
                        local = local.Substring(0, local.Length - 1);
                    }

                    File.WriteAllBytes(Path.Combine(iconsPath, $"{info.Metadata.Name}.png"),
                        File.ReadAllBytes(local + "icon.png"));

                    if (modUrls.ContainsKey(info.Metadata.Name)) return info.Metadata.Name;
                    local = local.Substring(0, local.Length - 1);
                    string author = "";
                    string package = "";
                    while (local.Last() != '-')
                    {
                        package = local.Last() + package;
                        local = local.Substring(0, local.Length - 1);
                    }

                    local = local.Substring(0, local.Length - 1);
                    while (local.Last() != '\\')
                    {
                        author = local.Last() + author;
                        local = local.Substring(0, local.Length - 1);
                    }

                    modUrls.Add(info.Metadata.Name, $"{BaseURL}{author}/{package}");
                    UnityEngine.Debug.Log($"{BaseURL}{author}/{package}");
                    return info.Metadata.Name;
                }
            }
            catch (Exception)
            {
                // UnityEngine.Debug.Log(e);
            }
            return "Vanilla";
        }

        private string GetCardClass(CardInfo card)
        {
            CustomCard card1 = card.GetComponent<CustomCard>();
            card1?.Callback();
            ClassNameMono mono = card.gameObject.GetComponent<ClassNameMono>();
            return mono == null ? "None" : mono.className;
        }

        private IEnumerator GetCardData()
        {
            var hiddenCards = ((List<CardInfo>) typeof(Cards).GetField("hiddenCards", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Cards.instance)).ToList();
            int count = allCards.Count + hiddenCards.Count;
            int curnet = 0;

            foreach (CardInfo card in allCards)
            {
                if (card.gameObject.name.Contains("JACK")) 
                    continue;

                UnityEngine.Debug.Log($"{++curnet}/{count}");
                if (!ProsessedCards.Contains(card.gameObject.name)) 
                {
                    Guid guid = GetDeterministicGuid(card.gameObject.name);
                    yield return GetCardArt(card, guid);

                    var cardSource = GetCardSource(card);
                    UnityEngine.Debug.Log(cardSource);
                    CardData += $"{{\"ID\":\"{card.gameObject.name}\",\"art\":\"{guid}\",\"Name\":\"{card.cardName}\",\"Rarity\":\"{card.rarity}\",\"Theme\":\"{card.colorTheme}\",\"Description\":\"{(card.cardDestription == null ? "" : card.cardDestription.Replace("\"", "\\\""))}\",\"IsCurse\":{card.categories.Contains(CurseManager.instance.curseCategory).ToString().ToLower()},\"Mod\":\"{cardSource}\",\"Multiple\":{card.allowMultiple.ToString().ToLower()},\"Class\":\"{GetCardClass(card)}\",\"Hidden\":{false.ToString().ToLower()}}},";
                    UnityEngine.Debug.Log($"({card.gameObject.name}):\n{card.cardName}({card.rarity},Theme:{card.colorTheme})\n{card.cardDestription}\n{GetCardStats(card)}\n\n\n");
                    ProsessedCards.Add(card.gameObject.name);
                }
                else { UnityEngine.Debug.Log("Error Duplict Card"); }

                yield return null;
            }

            foreach (CardInfo card in hiddenCards)
            {
                UnityEngine.Debug.Log($"{++curnet}/{count}");
                if (!ProsessedCards.Contains(card.gameObject.name))
                {
                    Guid guid = GetDeterministicGuid(card.gameObject.name);
                    yield return GetCardArt(card, guid);

                    var cardSource = GetCardSource(card);
                    UnityEngine.Debug.Log(cardSource);
                    CardData += $"{{\"ID\":\"{card.gameObject.name}\",\"art\":\"{guid}\",\"Name\":\"{card.cardName}\",\"Rarity\":\"{card.rarity}\",\"Theme\":\"{card.colorTheme}\",\"Description\":\"{(card.cardDestription == null ? "" : card.cardDestription.Replace("\"", "\\\""))}\",\"IsCurse\":{card.categories.Contains(CurseManager.instance.curseCategory).ToString().ToLower()},\"Mod\":\"{cardSource}\",\"Multiple\":{card.allowMultiple.ToString().ToLower()},\"Class\":\"{GetCardClass(card)}\",\"Hidden\":{true.ToString().ToLower()}}},";
                    UnityEngine.Debug.Log($"({card.gameObject.name}):\n{card.cardName}({card.rarity},Theme:{card.colorTheme})\n{card.cardDestription}\n{GetCardStats(card)}\n\n\n");
                    ProsessedCards.Add(card.gameObject.name);
                }
                else { UnityEngine.Debug.Log("Error Duplict Card"); }

                yield return null;
            }

            CardData = CardData.Substring(0, CardData.Length - 1);
            CardData += "]}";
            CardData = CardData.Replace("\n", "\\n");
            StatsData = StatsData.Substring(0, StatsData.Length - 1);
            StatsData += "]}";
            StatsData = StatsData.Replace("\n", "\\n");
            File.WriteAllText(Path.Combine(jsonsPath, "cards.json"), CardData);
            File.WriteAllText(Path.Combine(jsonsPath, "stats.json"), StatsData);
            string ModData = "{\"Mods\":[";
            modUrls.Keys.ToList().ForEach(mod => {
                ModData += $"{{\"Mod\":\"{mod}\",\"Url\":\"{modUrls[mod]}\"}},";
            });
            ModData = ModData.Substring(0,ModData.Length - 1) + "]}";
            File.WriteAllText(Path.Combine(jsonsPath, "mods.json"), ModData);

            FinishRender();
        }

        private static Guid GetDeterministicGuid(string input)
        {
            //use MD5 hash to get a 16-byte hash of the string: 
            MD5CryptoServiceProvider provider = new();
            byte[] inputBytes = Encoding.Default.GetBytes(input);
            byte[] hashBytes = provider.ComputeHash(inputBytes); 

            //generate a guid from the hash: 
            Guid hashGuid = new(hashBytes); 

            return hashGuid;
        } 

        private string GetCardStats(CardInfo card)
        {
            var cardStats = card.cardStats;
            string value = "";
            for (int i = 0; i < cardStats.Length; i++) 
            {
                CardInfoStat stat = cardStats[i];
                StatsData += $"{{\"Idex\":{i},\"Amount\":\"{(stat.amount == null ? "" : stat.amount.Replace("\"", "\\\""))}\",\"Stat\":\"{(stat.stat == null ? "" : stat.stat.Replace("\"", "\\\""))}\",\"Card\":\"{card.gameObject.name}\"}},";
                value += $"({(stat.positive?"Pos":"Neg")}){stat.amount} {stat.stat}\n";
            }

            return value;
        }

        private static GameObject FindObjectInChildren(GameObject gameObject, string gameObjectName)
        {
            var children = gameObject.GetComponentsInChildren<Transform>(true);
            return (from item in children where item.name == gameObjectName select item.gameObject).FirstOrDefault();
        }
    }
}
