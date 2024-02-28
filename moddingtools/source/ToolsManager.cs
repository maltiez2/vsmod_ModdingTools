using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using VSImGui;
using VSImGui.src.ImGui;

namespace ModdingTools;

public interface IModdingTool
{
    VSDialogStatus Draw(float deltaSeconds);
}

public delegate IModdingTool? ToolProducerDelegate(Selection selection);
public delegate string? SelectionOptionDelegate(Selection selection);

public class SelectionMenuToolsManager : IModdingTool
{
    public SelectionMenuToolsManager(ICoreClientAPI api)
    {
        _api = api;
        _imGuiSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
    }

    public int Register(ToolProducerDelegate toolDelegate, SelectionOptionDelegate selectionDelegate)
    {
        int id = ++_delegatesCounter;
        _toolDelegates.Add(id, toolDelegate);
        _selectionDelegates.Add(id, selectionDelegate);
        return id;
    }
    public void Unregister(int id)
    {
        _toolDelegates.Remove(id);
        _selectionDelegates.Remove(id);
    }
    public VSDialogStatus Draw(float deltaSeconds)
    {
        VSDialogStatus status = VSDialogStatus.Closed;

        if (_openPopup)
        {
            ImGui.OpenPopup("Modding tools");
            _openPopup = false;
            _imGuiSystem.Show();
            status = VSDialogStatus.GrabMouse;
        }

        foreach (IModdingTool tool in _activeTools)
        {
            VSDialogStatus toolStatus = tool.Draw(deltaSeconds);
            switch (toolStatus)
            {
                case VSDialogStatus.Closed:
                    _activeTools.Remove(tool);
                    break;
                case VSDialogStatus.GrabMouse:
                    status = VSDialogStatus.GrabMouse;
                    break;
                case VSDialogStatus.DontGrabMouse:
                    if (status == VSDialogStatus.Closed) status = VSDialogStatus.DontGrabMouse;
                    break;
            }
        }

        return status;
    }

    private readonly ICoreClientAPI _api;
    private readonly ImGuiModSystem _imGuiSystem;

    private readonly Dictionary<int, ToolProducerDelegate> _toolDelegates = new();
    private readonly Dictionary<int, SelectionOptionDelegate> _selectionDelegates = new();
    private int _delegatesCounter = 0;

    private readonly List<IModdingTool> _activeTools = new();
    private bool _openPopup = false;

}

public class ToolsManager
{
    private bool _openPopup = false;
    private readonly Dictionary<string, ParticleEditor> _particleEditors = new();
    private readonly ImGuiModSystem _imGuiSystem;
    private readonly ICoreClientAPI _clientApi;

    public ToolsManager(ICoreClientAPI api)
    {
        _clientApi = api;
        _imGuiSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();

        api.Input.RegisterHotKey("toolsmanagerpopup", "Modding tools: context menu", GlKeys.R, HotkeyType.DevTool, false, false, false);
        api.Input.SetHotKeyHandler("toolsmanagerpopup", EnablePopup);
    }

    public VSDialogStatus RenderTools(float deltaSeconds)
    {
        bool opened = false;

        if (_openPopup)
        {
            ImGui.OpenPopup("Modding tools");
            _openPopup = false;
            _imGuiSystem.Show();
        }

        ShowPopup();

        foreach ((string code, ParticleEditor? editor) in _particleEditors)
        {
            bool open = true;

            ImGui.SetNextWindowSize(new Vector2(300, 500), ImGuiCond.FirstUseEver);
            ImGui.Begin($"Particles editor: {code}", ref open);
            editor.Draw();
            ImGui.End();

            if (open) opened = true;

            if (!open)
            {
                _particleEditors.Remove(code);
            }
        }

        return opened ? VSDialogStatus.GrabMouse : VSDialogStatus.Closed;
    }

    private bool EnablePopup(KeyCombination keyCombination)
    {
        _selection.Deselect();
        _openPopup = true;
        return false;
    }

    private readonly Selection _selection = new();
    private void ShowPopup()
    {
        if (!ImGui.BeginPopup("Modding tools"))
        {
            _selection.Deselect();
            return;
        }

        ImGui.SeparatorText("Modding tools");

        _selection.Select(_clientApi);

        if (
            _selection.SlotSelection == null &&
            _selection.BlockSelection != null &&
            ImGui.Selectable("Edit particle effects")
            )
        {
            OpenParticleEditor(_selection.BlockSelection);
            ImGui.CloseCurrentPopup();
        }

        if (
            _selection.SlotSelection?.Itemstack?.Block is Block blockInSlot &&
            ImGui.Selectable("Edit particle effects")
            )
        {
            OpenParticleEditor(blockInSlot);
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }
    private void OpenParticleEditor(Block block)
    {
        string code = block.Code.GetName();
        if (_particleEditors.ContainsKey(code)) return;

        _particleEditors.Add(code, new(_clientApi, block));
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

            /*Console.WriteLine("*** Selection ***");
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
            Console.WriteLine($"    block entity: {BlockEntitySelection?.Block?.ShapeInventory?.Base}");*/
        }
    }
    public void Deselect()
    {
        Selected = false;
    }
}
