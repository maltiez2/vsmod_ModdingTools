using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ModdingTools
{
    public class ModdingToolsSystem : ModSystem
    {
        private bool mClientSide = false;
        private ICoreClientAPI? mClientApi;
        private ICoreServerAPI? mServerApi;
        private ToolsManager? mToolsManager;

        public override void StartServerSide(ICoreServerAPI api)
        {
            mServerApi = api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            mClientApi = api;
            mClientSide = true;
            mToolsManager = new ToolsManager(api);
            
            api.ModLoader.GetModSystem<VSImGui.VSImGuiModSystem>().SetUpImGuiWindows += mToolsManager.RenderTools;
        }
    }
}
