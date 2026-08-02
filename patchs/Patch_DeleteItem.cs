using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_DeleteItem
    {
        private static ConfigEntry<KeyCode> DeleteKey = null!;

        // Inicializa a configuração do patch usando o sistema do ValheimQoLPlugin
        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            DeleteKey = plugin.config(
                "DeleteItem",
                "DeleteKey",
                KeyCode.Delete,
                "Sets the key that permanently deletes the item currently held by the inventory cursor. Example: Delete uses the keyboard Delete key. Equipped items cannot be deleted.",
                false
            );
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static void PlayerUpdate_Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            if (InventoryGui.instance == null) return;

            if (Input.GetKeyDown(DeleteKey.Value))
            {
                TryDeleteSelectedItem();
            }
        }

        private static void TryDeleteSelectedItem()
        {
            var invGui = InventoryGui.instance;

            // Campos privados acessados via Harmony
            var item = AccessTools.Field(typeof(InventoryGui), "m_dragItem").GetValue(invGui) as ItemDrop.ItemData;
            var inventory = AccessTools.Field(typeof(InventoryGui), "m_dragInventory").GetValue(invGui) as Inventory;
            var dragGo = AccessTools.Field(typeof(InventoryGui), "m_dragGo").GetValue(invGui) as GameObject;

            if (item == null || inventory == null)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "No item selected.");
                return;
            }

            // Evita deletar itens equipados
            if (item.m_equipped)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Cannot delete equipped item!");
                return;
            }

            // Remove o item do inventário
            inventory.RemoveItem(item);

            // Destroi o ícone visual do item (corrige bug do item "preso" na tela)
            if (dragGo != null)
            {
                Object.Destroy(dragGo);
            }

            // Limpa as referências internas do InventoryGui
            AccessTools.Field(typeof(InventoryGui), "m_dragGo").SetValue(invGui, null);
            AccessTools.Field(typeof(InventoryGui), "m_dragItem").SetValue(invGui, null);
            AccessTools.Field(typeof(InventoryGui), "m_dragInventory").SetValue(invGui, null);

            // Mensagem de confirmação
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"Deleted: {item.m_shared.m_name}");

            // Atualiza o inventário do jogador na interface
            Player? localPlayer = Player.m_localPlayer;
            if (localPlayer != null)
            {
                invGui.m_playerGrid.UpdateInventory(
                    localPlayer.GetInventory(),
                    localPlayer,
                    null);
            }
        }
    }
}
