using Vintagestory.API.Client;
using Vintagestory.API.Common;
using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;

namespace ModdingTools
{
    public class ToolsManager
    {
        private bool mOpenPopup = false;
        private Dictionary<string, ParticleEditor> mParticleEditors = new();
        private GuiDialog dialog;

        private ICoreClientAPI mClientApi;

        public ToolsManager(ICoreClientAPI api)
        {
            mClientApi = api;

            api.Input.RegisterHotKey("toolsmanagerpopup", "Modding tools: context menu", GlKeys.R, HotkeyType.DevTool, false, false, false);
            api.Input.SetHotKeyHandler("toolsmanagerpopup", EnablePopup);

            api.Input.RegisterHotKey("moddingtoolgui", "Modding tools: cursor lock/unlock", GlKeys.R, HotkeyType.DevTool, false, false, true);
            api.Input.SetHotKeyHandler("moddingtoolgui", ToggleCursorLock);

            dialog = new VanillaGuiDialog(api);
        }

        public void RenderTools()
        {
            if (mOpenPopup)
            {
                ImGui.OpenPopup("Modding tools");
                mOpenPopup = false;
            }

            ShowPopup();

            foreach ((string code, var editor) in mParticleEditors)
            {
                bool open = true;

                ImGui.SetNextWindowSize(new Vector2(300, 500), ImGuiCond.FirstUseEver);
                ImGui.Begin($"Particles editor: {code}", ref open);
                editor.RenderWindow();
                ImGui.End();

                if (!open)
                {
                    mParticleEditors.Remove(code);
                }
            }
        }

        private bool ToggleCursorLock(KeyCombination keyCombination)
        {
            if (dialog?.IsOpened() == true)
            {
                dialog.TryClose();
            }
            else
            {
                dialog?.TryOpen();
            }

            return true;
        }
        private bool EnablePopup(KeyCombination keyCombination)
        {
            mOpenPopup = true;
            return false;
        }
        private void ShowPopup()
        {
            if (!ImGui.BeginPopup("Modding tools")) return;

            if (ImGui.Selectable("Particle editor"))
            {
                OpenParticleEditor();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        private void OpenParticleEditor()
        {
            Block? block = mClientApi?.World?.Player?.CurrentBlockSelection?.Block;
            if (block == null) return;
            string code = block.Code.GetName();
            if (mParticleEditors.ContainsKey(code)) return;

            mParticleEditors.Add(code, new(mClientApi, block));
        }
    }
}
