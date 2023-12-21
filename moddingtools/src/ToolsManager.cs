using ImGuiNET;
using ModdingTools.Render;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ModdingTools
{
    public class ToolsManager
    {
        private bool mOpenPopup = false;
        private Dictionary<string, ParticleEditor> mParticleEditors = new();
        private GuiDialog dialog;

        private Render.TestItemGuiRenderer mRenderer;

        private ICoreClientAPI mClientApi;

        public ToolsManager(ICoreClientAPI api)
        {
            mClientApi = api;

            api.Input.RegisterHotKey("toolsmanagerpopup", "Modding tools: context menu", GlKeys.R, HotkeyType.DevTool, false, false, false);
            api.Input.SetHotKeyHandler("toolsmanagerpopup", EnablePopup);

            api.Input.RegisterHotKey("moddingtoolgui", "Modding tools: cursor lock/unlock", GlKeys.R, HotkeyType.DevTool, false, false, true);
            api.Input.SetHotKeyHandler("moddingtoolgui", ToggleCursorLock);

            dialog = new VanillaGuiDialog(api);

            mShapeManager = new(api);

            mRenderer = new(api);
        }

        public void RenderTools()
        {
            if (mOpenPopup)
            {
                ImGui.OpenPopup("Modding tools");
                mOpenPopup = false;
            }

            ShowPopup();

            foreach ((string code, ParticleEditor? editor) in mParticleEditors)
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

            if (mRenderer.mSlot != null)
            {
                ImGui.Begin("Item model");

                if (RenderedTexture.Texture != -1)
                {
                    ImGui.GetWindowDrawList().AddImage(
                        RenderedTexture.Texture,
                        ImGui.GetCursorScreenPos(),
                        ImGui.GetCursorScreenPos() + new System.Numerics.Vector2(500, 500),
                        new System.Numerics.Vector2(0, 1),
                        new System.Numerics.Vector2(1, 0)
                    );
                }

                ImGui.Text($"textureId: {RenderedTexture.Texture}");

                for (int i = 0; i < 16; i += 4)
                {
                    Vector4 row = new(mRenderer.mModelMat[i + 0], mRenderer.mModelMat[i + 1], mRenderer.mModelMat[i + 2], mRenderer.mModelMat[i + 3]);
                    ImGui.SliderFloat4($"row {i}", ref row, -5, 5);
                    mRenderer.mModelMat[i + 0] = row.X;
                    mRenderer.mModelMat[i + 1] = row.Y;
                    mRenderer.mModelMat[i + 2] = row.Z;
                    mRenderer.mModelMat[i + 3] = row.W;
                }

                ImGui.End();
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
            mSelection.Deselect();
            mOpenPopup = true;
            return false;
        }

        private readonly Selection mSelection = new();
        private void ShowPopup()
        {
            if (!ImGui.BeginPopup("Modding tools"))
            {
                mSelection.Deselect();
                return;
            }

            ImGui.SeparatorText("Modding tools");

            mSelection.Select(mClientApi);

            if (
                mSelection.SlotSelection == null &&
                mSelection.BlockSelection != null &&
                ImGui.Selectable("Edit particle effects")
                )
            {
                OpenParticleEditor(mSelection.BlockSelection);
                ImGui.CloseCurrentPopup();
            }

            if (
                mSelection.SlotSelection?.Itemstack?.Block is Block blockInSlot &&
                ImGui.Selectable("Edit particle effects")
                )
            {
                OpenParticleEditor(blockInSlot);
                ImGui.CloseCurrentPopup();
            }

            if (mSelection.SlotSelection?.Itemstack?.Item?.Shape is CompositeShape itemShape && ImGui.Selectable("Edit item model"))
            {
                mRenderer.mSlot = mSelection.SlotSelection;

                mRenderer.mShape = new(mClientApi, mSelection.SlotSelection?.Itemstack?.Item?.Shape?.Base?.Path ?? "");

                ImGui.CloseCurrentPopup();
            }
            if (mSelection.SlotSelection?.Itemstack?.Block?.ShapeInventory is CompositeShape blockShape && ImGui.Selectable("Edit block model"))
            {
                OpenShapeEditor(blockShape);
                ImGui.CloseCurrentPopup();
            }
            if (mSelection.EntitySelection?.Properties?.Client?.Shape is CompositeShape entityShape && ImGui.Selectable("Edit entity model"))
            {
                OpenShapeEditor(entityShape);
                ImGui.CloseCurrentPopup();
            }

            if (mSelection.SlotSelection == null && mSelection.BlockSelection?.Shape is CompositeShape inWorldBlockShape && ImGui.Selectable("Edit block model"))
            {
                OpenShapeEditor(inWorldBlockShape);
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        private void OpenParticleEditor(Block block)
        {
            string code = block.Code.GetName();
            if (mParticleEditors.ContainsKey(code)) return;

            mParticleEditors.Add(code, new(mClientApi, block));
        }

        private ItemStack mStack;
        private string mTexture;
        private Matrixf mMVP = Matrixf.Create();
        private ImGuiShapeManager mShapeManager;
        private void OpenShapeEditor(CompositeShape shape)
        {

        }

        private void RowEditor(int offset)
        {
            Vector4 row_1 = new(mMVP.Values[offset + 0], mMVP.Values[offset + 1], mMVP.Values[offset + 2], mMVP.Values[offset + 3]);
            ImGui.SliderFloat4($"row: {offset} - {offset + 3}", ref row_1, -1, 1);
            mMVP.Values[offset + 0] = row_1.X;
            mMVP.Values[offset + 1] = row_1.Y;
            mMVP.Values[offset + 2] = row_1.Z;
            mMVP.Values[offset + 3] = row_1.W;
        }
    }

    public class Selection
    {
        public Block? BlockSelection { get; set; }
        public Entity? EntitySelection { get; set; }
        public ItemSlot? SlotSelection { get; set; }
        public BlockEntity? BlockEntitySelection { get; set; }
        public bool Selected { get; private set; }

        public void Select(ICoreClientAPI api)
        {
            if (!Selected)
            {
                Selected = true;
                BlockSelection = api?.World?.Player?.CurrentBlockSelection?.Block;
                EntitySelection = api?.World?.Player?.CurrentEntitySelection?.Entity;
                SlotSelection = api?.World?.Player?.InventoryManager?.CurrentHoveredSlot;
                BlockEntitySelection = BlockSelection?.GetBlockEntity<BlockEntity>(api?.World?.Player?.CurrentBlockSelection);

                Console.WriteLine("*** Selection ***");
                Console.WriteLine($"    block: {BlockSelection?.Code}");
                Console.WriteLine($"    entity: {EntitySelection?.Code}");
                Console.WriteLine($"    slot: {SlotSelection?.Itemstack?.Item?.Code ?? SlotSelection?.Itemstack?.Block?.Code}");
                Console.WriteLine($"    block entity: {BlockEntitySelection?.Block?.Code}");
                Console.WriteLine("Shapes:");
                Console.WriteLine($"    block: {BlockSelection?.Shape.Base}");
                Console.WriteLine($"    entity: {EntitySelection?.Properties?.Client?.Shape?.Base}");
                Console.WriteLine($"    slot: {SlotSelection?.Itemstack?.Item?.Shape?.Base ?? SlotSelection?.Itemstack?.Block?.Shape?.Base}");
                Console.WriteLine($"    block entity: {BlockEntitySelection?.Block?.Shape?.Base}");
                Console.WriteLine("ShapeInventory:");
                Console.WriteLine($"    block: {BlockSelection?.ShapeInventory?.Base}");
                Console.WriteLine($"    slot: {SlotSelection?.Itemstack?.Block?.ShapeInventory?.Base}");
                Console.WriteLine($"    block entity: {BlockEntitySelection?.Block?.ShapeInventory?.Base}");
            }
        }
        public void Deselect()
        {
            Selected = false;
        }
    }
}
