using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ModdingTools;

public class ModdingToolsSystem : ModSystem
{
    public override void StartClientSide(ICoreClientAPI api)
    {
        ToolsManager toolsManager = new(api);
        api.ModLoader.GetModSystem<VSImGui.ImGuiModSystem>().Draw += toolsManager.RenderTools; // @TODO remove on dispose
        Widgets.InitStyles(api);
        Editors.InitStyles(api);
    }
}
