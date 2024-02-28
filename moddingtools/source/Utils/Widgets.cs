using ImGuiNET;
using Vintagestory.API.Common;
using VSImGui;

namespace ModdingTools;

public static partial class Widgets
{
    public static Style? RedButton { get; set; }
    public static Style? GreenButton { get; set; }

    public static bool InitStyles(ICoreAPI api)
    {
        ImGuiModSystem? system = api.ModLoader.GetModSystem<ImGuiModSystem>();
        Style? defaultStyle = system?.DefaultStyle;
        if (defaultStyle == null) return false;

        RedButton = new(defaultStyle)
        {
            ColorButton = new(0.6f, 0.4f, 0.4f, 1.0f),
            ColorButtonHovered = new(1.0f, 0.5f, 0.5f, 1.0f),
        };
        GreenButton = new(defaultStyle)
        {
            ColorButton = new(0.4f, 0.6f, 0.4f, 1.0f),
            ColorButtonHovered = new(0.5f, 1.0f, 0.5f, 1.0f),
        };

        return true;
    }

    public static bool ButtonWithConfirmation(string title, ref bool confirming, string confirm = "Confirm", string cancel = "Cancel")
    {
        if (RedButton == null || GreenButton == null)
        {
            return ImGui.Button(title);
        }

        if (confirming) ImGui.BeginDisabled();
        if (ImGui.Button(title))
        {
            confirming = true;
        }
        if (confirming) ImGui.EndDisabled();

        if (!confirming) return false;

        ImGui.SameLine();
        ImGui.Text(" : ");
        ImGui.SameLine();

        using (new StyleApplier(GreenButton))
        {
            if (ImGui.Button($"{confirm}##{title}"))
            {
                confirming = false;
                return true;
            }
        }

        ImGui.SameLine();

        using (new StyleApplier(RedButton))
        {
            if (ImGui.Button($"{cancel}##{title}"))
            {
                confirming = false;
                return false;
            }
        }

        return false;
    }
}