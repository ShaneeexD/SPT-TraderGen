using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using EFT.Communications;
using EFT.InventoryLogic;
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
        private static Item _contextMenuItem;

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

        [HarmonyPatch(typeof(EFT.UI.DragAndDrop.ItemView), "ShowContextMenu")]
        internal static class ItemViewShowContextMenuPatch
        {
            [HarmonyPrefix]
            static void Prefix(EFT.UI.DragAndDrop.ItemView __instance)
            {
                try
                {
                    var itemContext = __instance.ItemContext;
                    if (itemContext != null)
                    {
                        _contextMenuItem = itemContext.Item;
                    }
                }
                catch (Exception ex)
                {
                    Log?.LogDebug($"[TraderGen] ItemViewShowContextMenuPatch failed: {ex.Message}");
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
                    NotificationManagerClass.DisplayMessageNotification("Build copied to clipboard!", ENotificationDurationType.Default);
                    Log?.LogInfo("[TraderGen] Build exported to clipboard.");
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

                var gridsField = itemType.GetField("Grids", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (gridsField != null)
                {
                    var grids = gridsField.GetValue(item) as IEnumerable;
                    if (grids != null)
                    {
                        foreach (var grid in grids.Cast<object>())
                        {
                            if (grid == null) continue;
                            var gridId = GetPropOrFieldValue(grid, "ID", "Id")?.ToString();
                            if (string.IsNullOrEmpty(gridId)) continue;

                            var itemsProp = grid.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            var items = itemsProp?.GetValue(grid) as IEnumerable;
                            if (items == null) continue;

                            foreach (var gridItem in items.Cast<object>())
                            {
                                if (gridItem == null) continue;
                                var childJson = WalkItemTreeRecursive(gridItem, gridItem.GetType());
                                if (childJson != null)
                                {
                                    childJson.slotId = gridId;

                                    var currentAddress = gridItem.GetType().GetProperty("CurrentAddress", BindingFlags.Instance | BindingFlags.Public)?.GetValue(gridItem);
                                    if (currentAddress != null)
                                    {
                                        var locProp = currentAddress.GetType().GetProperty("LocationInGrid", BindingFlags.Instance | BindingFlags.Public);
                                        var loc = locProp?.GetValue(currentAddress);
                                        if (loc != null)
                                        {
                                            var locType = loc.GetType();
                                            var xField = locType.GetField("x");
                                            var yField = locType.GetField("y");
                                            var rField = locType.GetField("r");
                                            childJson.locX = xField != null ? (int)xField.GetValue(loc) : 0;
                                            childJson.locY = yField != null ? (int)yField.GetValue(loc) : 0;
                                            childJson.locR = rField != null ? Convert.ToInt32(rField.GetValue(loc)) : 0;
                                        }
                                    }

                                    root.children.Add(childJson);
                                }
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

            var gridsField = itemType.GetField("Grids", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (gridsField != null)
            {
                var grids = gridsField.GetValue(item) as IEnumerable;
                if (grids != null)
                {
                    foreach (var grid in grids.Cast<object>())
                    {
                        if (grid == null) continue;
                        var gridId = GetPropOrFieldValue(grid, "ID", "Id")?.ToString();
                        if (string.IsNullOrEmpty(gridId)) continue;

                        var itemsProp = grid.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var items = itemsProp?.GetValue(grid) as IEnumerable;
                        if (items == null) continue;

                        foreach (var gridItem in items.Cast<object>())
                        {
                            if (gridItem == null) continue;
                            var childJson = WalkItemTreeRecursive(gridItem, gridItem.GetType());
                            if (childJson != null)
                            {
                                childJson.slotId = gridId;

                                var currentAddress = gridItem.GetType().GetProperty("CurrentAddress", BindingFlags.Instance | BindingFlags.Public)?.GetValue(gridItem);
                                if (currentAddress != null)
                                {
                                    var locProp = currentAddress.GetType().GetProperty("LocationInGrid", BindingFlags.Instance | BindingFlags.Public);
                                    var loc = locProp?.GetValue(currentAddress);
                                    if (loc != null)
                                    {
                                        var locType = loc.GetType();
                                        var xField = locType.GetField("x");
                                        var yField = locType.GetField("y");
                                        var rField = locType.GetField("r");
                                        childJson.locX = xField != null ? (int)xField.GetValue(loc) : 0;
                                        childJson.locY = yField != null ? (int)yField.GetValue(loc) : 0;
                                        childJson.locR = rField != null ? Convert.ToInt32(rField.GetValue(loc)) : 0;
                                    }
                                }

                                node.children.Add(childJson);
                            }
                        }
                    }
                }
            }

            return node;
        }

        [HarmonyPatch(typeof(ItemUiContext), "ShowContextMenu")]
        internal static class ItemUiContextShowContextMenuPatch
        {
            [HarmonyPostfix]
            static void Postfix(ItemUiContext __instance)
            {
                try
                {
                    if (Plugin.EnableExportButton != null && !Plugin.EnableExportButton.Value)
                    {
                        return;
                    }

                    var item = _contextMenuItem;
                    if (item == null)
                    {
                        Log?.LogDebug("[TraderGen] ShowContextMenu postfix: no stored item.");
                        return;
                    }

                    if (!ShouldExportItem(item))
                    {
                        Log?.LogDebug($"[TraderGen] ShowContextMenu postfix: item type '{item.GetType().Name}' rejected.");
                        return;
                    }

                    var contextMenu = __instance.ContextMenu;
                    if (contextMenu == null)
                    {
                        Log?.LogWarning("[TraderGen] ShowContextMenu postfix: ContextMenu is null.");
                        return;
                    }

                    var ibcField = typeof(SimpleContextMenu).GetField("_interactionButtonsContainer", BindingFlags.Instance | BindingFlags.NonPublic);
                    var container = ibcField?.GetValue(contextMenu) as InteractionButtonsContainer;
                    if (container == null)
                    {
                        Log?.LogWarning("[TraderGen] ShowContextMenu postfix: InteractionButtonsContainer is null.");
                        return;
                    }

                    var buttonsContainerField = typeof(InteractionButtonsContainer).GetField("_buttonsContainer", BindingFlags.Instance | BindingFlags.NonPublic);
                    var buttonsContainer = buttonsContainerField?.GetValue(container) as Transform;
                    if (buttonsContainer == null)
                    {
                        Log?.LogWarning("[TraderGen] ShowContextMenu postfix: _buttonsContainer is null.");
                        return;
                    }
                    Action onClick = () => ExportItem(_contextMenuItem);

                    var existing = buttonsContainer.Find("ExportToTraderGen");
                    if (existing != null)
                    {
                        var existingButton = existing.GetComponent<SimpleContextMenuButton>();
                        if (existingButton != null)
                        {
                            var buttonField = typeof(SimpleContextMenuButton).GetField("_button", BindingFlags.Instance | BindingFlags.NonPublic);
                            var btn = buttonField?.GetValue(existingButton) as Button;
                            btn?.onClick.RemoveAllListeners();

                            existingButton.Show("EXPORT TO TG", "EXPORT TO TG", null, onClick, null, false, true);
                            existing.SetAsLastSibling();
                            Log?.LogDebug("[TraderGen] ShowContextMenu postfix: updated existing button.");
                            return;
                        }
                    }

                    var templateField = typeof(InteractionButtonsContainer).GetField("_buttonTemplate", BindingFlags.Instance | BindingFlags.NonPublic);
                    var template = templateField?.GetValue(container) as SimpleContextMenuButton;
                    if (template == null)
                    {
                        Log?.LogWarning("[TraderGen] ShowContextMenu postfix: _buttonTemplate is null.");
                        return;
                    }

                    var templateGo = template.GetType().GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public)?.GetValue(template) as GameObject;
                    if (templateGo == null)
                    {
                        Log?.LogWarning("[TraderGen] ShowContextMenu postfix: could not get template GameObject.");
                        return;
                    }

                    var clonedGo = UnityEngine.Object.Instantiate(templateGo, buttonsContainer);
                    clonedGo.name = "ExportToTraderGen";
                    var cloned = clonedGo.GetComponent<SimpleContextMenuButton>();
                    cloned?.Show("EXPORT TO TG", "EXPORT TO TG", null, onClick, null, false, true);
                    clonedGo.transform.SetAsLastSibling();

                    Log?.LogInfo("[TraderGen] Injected context menu export button via ShowContextMenu postfix.");
                }
                catch (Exception ex)
                {
                    Log?.LogError($"[TraderGen] ShowContextMenu postfix failed: {ex}");
                }
            }

            static bool ShouldExportItem(Item item)
            {
                var typeName = item.GetType().Name.ToLowerInvariant();
                return typeName.Contains("armor") || typeName.Contains("vest") || typeName.Contains("rig") || typeName.Contains("backpack") || typeName.Contains("weapon");
            }

            static void ExportItem(Item item)
            {
                try
                {
                    var json = WalkItemTree(item, item.GetType());
                    if (!string.IsNullOrEmpty(json))
                    {
                        GUIUtility.systemCopyBuffer = json;
                        ShowStatus("Item copied to clipboard!");
                        NotificationManagerClass.DisplayMessageNotification("Item copied to clipboard!", ENotificationDurationType.Default);
                        Log?.LogInfo("[TraderGen] Item exported to clipboard.");
                    }
                    else
                    {
                        ShowStatus("Could not read item data.");
                        Log?.LogWarning("[TraderGen] Failed to extract item data.");
                    }
                }
                catch (Exception ex)
                {
                    ShowStatus("Export failed.");
                    Log?.LogError($"[TraderGen] Export failed: {ex}");
                }
            }
        }

        [Serializable]
        internal class ExportNode
        {
            public string itemTpl;
            public string slotId;
            public int locX;
            public int locY;
            public int locR;
            public List<ExportNode> children = new List<ExportNode>();
        }
    }
}
