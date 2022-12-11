using CardDataDumper.Dumper;
using TMPro;
using UnboundLib;
using UnboundLib.Utils.UI;
using UnityEngine;

namespace CardDataDumper.Config
{
    internal class ConfigMenu : MonoBehaviour
    {
        public static ConfigMenu Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Unbound.RegisterMenu("CARD DATA DUMPER", () => { }, Menu, null, false);
        }

        private static void Menu(GameObject menu)
        {
            // void TypeChanged(float val)
            // {
            //     var oldValue = ConfigHandler.OwnMarkerTypeConfig.Value;
            //     ConfigHandler.OwnMarkerTypeConfig.Value = Mathf.RoundToInt(Mathf.Clamp(val, 1, types));
            //     ownMarkerType = ConfigHandler.OwnMarkerTypeConfig.Value - 1;
            //
            //     if (oldValue == ConfigHandler.OwnMarkerTypeConfig.Value) return;
            // }

            MenuHandler.CreateText("CARD DATA DUMPER", menu, out TextMeshProUGUI _);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 15);

            // MenuHandler.

            MenuHandler.CreateButton("START DUMP", menu, DumpHandler.Instance.StartRender);
        }
    }
}
