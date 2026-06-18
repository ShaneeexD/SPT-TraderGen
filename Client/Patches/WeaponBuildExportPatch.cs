using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using EFT.UI;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace TraderGen.Client.Patches
{
    internal static class WeaponBuildExportPatch
    {
        internal static ManualLogSource Log;
        private static GameObject _statusGo;

        internal static void Init(ManualLogSource log)
        {
            Log = log;
            _statusGo = new GameObject("TraderGen_StatusToast");
            UnityEngine.Object.DontDestroyOnLoad(_statusGo);
            _statusGo.AddComponent<StatusToastBehaviour>();
            Log?.LogInfo("[TraderGen] WeaponBuildExportPatch initialized.");
        }

        [HarmonyPatch(typeof(EditBuildScreen), "Awake")]
        internal static class EditBuildScreenAwakePatch
        {
            [HarmonyPostfix]
            private static void Postfix(EditBuildScreen __instance)
            {
                try
                {
                    var saveAsButtonField = typeof(EditBuildScreen).GetField("_saveAsBuildButton", BindingFlags.Instance | BindingFlags.NonPublic);
                    var sourceButton = saveAsButtonField?.GetValue(__instance) as Button;

                    if (sourceButton == null)
                    {
                        Log?.LogWarning("[TraderGen] _saveAsBuildButton not found on EditBuildScreen.");
                        return;
                    }

                    var contextButtons = sourceButton.transform.parent;
                    if (contextButtons == null)
                    {
                        Log?.LogWarning("[TraderGen] Could not find button parent transform.");
                        return;
                    }

                    var cloned = UnityEngine.Object.Instantiate(sourceButton.gameObject, contextButtons);
                    cloned.name = "ExportToTraderGen";

                    // EFT uses LocalizedText, not UnityEngine.UI.Text
                    var localizedText = cloned.GetComponentInChildren(Type.GetType("EFT.UI.LocalizedText, Assembly-CSharp"), true);
                    if (localizedText != null)
                    {
                        var locKeyProp = localizedText.GetType().GetProperty("LocalizationKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var formattedProp = localizedText.GetType().GetProperty("FormattedText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        locKeyProp?.SetValue(localizedText, "EXPORT TO TG");
                        formattedProp?.SetValue(localizedText, "EXPORT TO TG");
                    }

                    var btn = cloned.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => ExportBuild(__instance));

                    Log?.LogInfo("[TraderGen] Injected 'EXPORT TO TG' button into build screen.");
                }
                catch (Exception ex)
                {
                    Log?.LogError($"[TraderGen] Failed to inject button: {ex}");
                }
            }
        }

        static void ExportBuild(EditBuildScreen screen)
        {
            try
            {
                var itemField = typeof(EditBuildScreen).BaseType?.GetField("Item", BindingFlags.Instance | BindingFlags.NonPublic);
                var item = itemField?.GetValue(screen);

                if (item == null)
                {
                    Log?.LogWarning("[TraderGen] No item found on EditBuildScreen.");
                    ShowStatus("No build to export.");
                    return;
                }

                var json = WalkItemTree(item, item.GetType());
                if (!string.IsNullOrEmpty(json))
                {
                    GUIUtility.systemCopyBuffer = json;
                    ShowStatus("Build copied to clipboard!");
                    Log?.LogInfo("[TraderGen] Build exported to clipboard.");
                    Log?.LogDebug($"[TraderGen] JSON: {json}");
                }
                else
                {
                    ShowStatus("Could not read build data.");
                    Log?.LogWarning("[TraderGen] Failed to extract build data.");
                }
            }
            catch (Exception ex)
            {
                ShowStatus("Export failed. Check BepInEx log.");
                Log?.LogError($"[TraderGen] Export build failed: {ex}");
            }
        }

        static void ShowStatus(string message)
        {
            if (_statusGo == null) return;
            var toast = _statusGo.GetComponent<StatusToastBehaviour>();
            if (toast != null) toast.Show(message);
        }

        internal class StatusToastBehaviour : MonoBehaviour
        {
            private string _message = "";
            private float _timer;

            public void Show(string message)
            {
                _message = message;
                _timer = 4f;
            }

            void OnGUI()
            {
                if (_timer > 0)
                {
                    _timer -= Time.deltaTime;
                    GUI.Label(new Rect(20, Screen.height - 80, 400, 28), $"<color=#FFD700><b>{_message}</b></color>");
                }
            }
        }

        static object GetPropOrFieldValue(object obj, string name1, string name2)
        {
            var type = obj.GetType();
            var prop = type.GetProperty(name1, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? type.GetProperty(name2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return prop.GetValue(obj);
            var field = type.GetField(name1, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? type.GetField(name2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(obj);
            return null;
        }

        static string WalkItemTree(object item, Type itemType)
        {
            if (item == null) return null;
            try
            {
                var tplProp = itemType.GetProperty("TemplateId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (tplProp == null) return null;
                var tplId = tplProp.GetValue(item)?.ToString();
                if (string.IsNullOrEmpty(tplId) || tplId.Length != 24) return null;

                var root = new ExportNode { itemTpl = tplId };

                var slotsField = itemType.GetField("Slots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (slotsField != null)
                {
                    var slots = slotsField.GetValue(item) as IEnumerable;
                    if (slots != null)
                    {
                        foreach (var slot in slots.Cast<object>())
                        {
                            if (slot == null) continue;
                            var slotType = slot.GetType();

                            var slotId = GetPropOrFieldValue(slot, "ID", "Id")?.ToString();
                            if (string.IsNullOrEmpty(slotId)) continue;

                            var contained = GetPropOrFieldValue(slot, "ContainedItem", "Contained");
                            if (contained == null) continue;

                            var childJson = WalkItemTreeRecursive(contained, contained.GetType());
                            if (childJson != null)
                            {
                                childJson.slotId = slotId;
                                root.children.Add(childJson);
                            }
                        }
                    }
                }

                return JsonConvert.SerializeObject(root);
            }
            catch (Exception ex)
            {
                Log?.LogDebug($"[TraderGen] WalkItemTree failed: {ex.Message}");
                return null;
            }
        }

        static ExportNode WalkItemTreeRecursive(object item, Type itemType)
        {
            if (item == null) return null;
            var tplProp = itemType.GetProperty("TemplateId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (tplProp == null) return null;
            var tplId = tplProp.GetValue(item)?.ToString();
            if (string.IsNullOrEmpty(tplId) || tplId.Length != 24) return null;

            var node = new ExportNode { itemTpl = tplId };

            var slotsField = itemType.GetField("Slots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (slotsField != null)
            {
                var slots = slotsField.GetValue(item) as IEnumerable;
                if (slots != null)
                {
                    foreach (var slot in slots.Cast<object>())
                    {
                        if (slot == null) continue;
                        var slotType = slot.GetType();

                        var slotId = GetPropOrFieldValue(slot, "ID", "Id")?.ToString();
                        if (string.IsNullOrEmpty(slotId)) continue;

                        var contained = GetPropOrFieldValue(slot, "ContainedItem", "Contained");
                        if (contained == null) continue;

                        var childJson = WalkItemTreeRecursive(contained, contained.GetType());
                        if (childJson != null)
                        {
                            childJson.slotId = slotId;
                            node.children.Add(childJson);
                        }
                    }
                }
            }

            return node;
        }

        [Serializable]
        internal class ExportNode
        {
            public string itemTpl;
            public string slotId;
            public List<ExportNode> children = new List<ExportNode>();
        }
    }
}
