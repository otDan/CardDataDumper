using BepInEx;
using CardDataDumper.Config;
using CardDataDumper.Dumper;
using HarmonyLib;

namespace CardDataDumper
{
    [BepInDependency("com.willis.rounds.unbound")]
    [BepInPlugin(ModId, CompatibilityModName, Version)]
    [BepInProcess("Rounds.exe")]
    public class CardDataDumper : BaseUnityPlugin
    {
        private const string ModId = "com.Root.Dump";
        private string ModName = System.Text.RegularExpressions.Regex.Replace(CompatibilityModName, "[A-Z]", " $0");
        public const string Version = "1.0.0";
        public const string ModInitials = "";
        private const string CompatibilityModName = "CardDataDumper";
        public static CardDataDumper Instance { get; private set; }

        internal void Awake()
        {
            Instance = this;

            var harmony = new Harmony(ModId);
            harmony.PatchAll();

            gameObject.AddComponent<ConfigHandler>();
        }
    }
}
