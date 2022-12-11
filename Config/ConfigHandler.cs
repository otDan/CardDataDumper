using System;
using BepInEx.Configuration;
using CardDataDumper.Dumper;
using UnityEngine;

namespace CardDataDumper.Config
{
    public class ConfigHandler : MonoBehaviour
    {
        public static ConfigHandler Instance { get; private set; }

        public static ConfigEntry<int> renderWidthConfig;
        public static ConfigEntry<int> renderHeightConfig;
        public static ConfigEntry<int> renderFramesConfig;

        public int renderWidth = 350;
        public int renderHeight = 500;
        public int renderFrames = 24;

        private void Awake()
        {
            Instance = this;

            gameObject.AddComponent<ConfigMenu>();
            gameObject.AddComponent<DumpHandler>();
        }

        private void SetupConfig()
        {
            // OwnEnabledConfig = Config.Bind(CompatibilityModName, "OwnEnabled", true, "Own Marker Enabled");
            // OwnHeightConfig = Config.Bind(CompatibilityModName, "OwnHeight", 0.55f, "Own Marker Height");
            // OwnWidthConfig = Config.Bind(CompatibilityModName, "OwnWidth", 0.55f, "Own Marker Width");
            // OwnBloomConfig = Config.Bind(CompatibilityModName, "OwnBloom", 3f, "Own Marker Bloom");
            // ConfigController.OwnMarkerTypeConfig = Config.Bind(CompatibilityModName, "OwnMarkerType", 1, "Own Marker Type");
        }
    }
}
