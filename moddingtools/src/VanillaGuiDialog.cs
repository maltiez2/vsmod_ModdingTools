using Vintagestory.API.Client;

namespace ModdingTools
{
    public class VanillaGuiDialog : GuiDialog
    {
        public override string ToggleKeyCombinationCode => "moddingtoolgui";
        public override bool PrefersUngrabbedMouse => false;

        public VanillaGuiDialog(ICoreClientAPI capi) : base(capi)
        {
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterTop);

            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 250, 20);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(textBounds);

            SingleComposer = capi.Gui.CreateCompo("moddingToolsGui", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Modding tools: cursor unlock", OnTitleBarCloseClicked)
                .Compose();
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
