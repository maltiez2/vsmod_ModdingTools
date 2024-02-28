using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ModdingTools;

public class ModdingToolsSystem : ModSystem
{
    private ToolsManager? mToolsManager;

    public override void StartClientSide(ICoreClientAPI api)
    {
        mToolsManager = new ToolsManager(api);
        api.ModLoader.GetModSystem<VSImGui.ImGuiModSystem>().Draw += mToolsManager.RenderTools;
        Widgets.InitButtonStyles(api);
    }
}
