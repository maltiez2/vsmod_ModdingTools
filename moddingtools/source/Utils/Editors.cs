using ImGuiNET;
using System.Numerics;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using VSImGui;

namespace ModdingTools;

public static partial class Editors
{
    public static bool InitStyles(ICoreAPI api)
    {
        ImGuiModSystem? system = api.ModLoader.GetModSystem<ImGuiModSystem>();
        Style? defaultStyle = system?.DefaultStyle;
        if (defaultStyle == null) return false;

        return true;
    }

    public static void NatFloatEditor(int id, string name, ref NatFloat value, int nameSize = 150)
    {
        ImGui.PushItemWidth(80);
        ImGui.Text($"{name}: "); ImGui.SameLine(nameSize);
        ImGui.Text("Avg ="); ImGui.SameLine(nameSize + 50);
        ImGui.InputFloat($"##avg{name}{id}", ref value.avg); ImGui.SameLine(nameSize + 150);
        ImGui.Text("Var ="); ImGui.SameLine(nameSize + 200);
        ImGui.InputFloat($"##var{name}{id}", ref value.var);
        ImGui.PopItemWidth();
    }
    public static void NatFloatVecEditor(int id, string name, ref NatFloat[] vector)
    {
        Vector3 average = new(vector[0].avg, vector[1].avg, vector[2].avg);
        Vector3 variance = new(vector[0].var, vector[1].var, vector[2].var);
        ImGui.Text($"{name}");
        ImGui.Text("average:  "); ImGui.SameLine();
        ImGui.InputFloat3($"##average{name}{id}", ref average, "%.2f");
        ImGui.Text("variance: "); ImGui.SameLine();
        ImGui.InputFloat3($"##variance{name}{id}", ref variance);
        vector[0].avg = average.X;
        vector[1].avg = average.Y;
        vector[2].avg = average.Z;
        vector[0].var = variance.X;
        vector[1].var = variance.Y;
        vector[2].var = variance.Z;
    }

    private static readonly string[] _transformFunction = new[]
    {
        "IDENTICAL",
        "LINEAR",
        "LINEARNULLIFY",
        "LINEARREDUCE",
        "LINEARINCREASE",
        "QUADRATIC",
        "INVERSELINEAR",
        "ROOT",
        "SINUS",
        "CLAMPEDPOSITIVESINUS",
        "COSINUS",
        "SMOOTHSTEP"
    };
    public static void EvolvingNatFloatEditor(int id, string label, ref EvolvingNatFloat value)
    {
        ImGui.Text($"{label}: ");
        ImGui.SameLine();
        int currentModel = (int)value.Transform;
        float currentFactor = value.Factor;
        ImGui.PushItemWidth(400);
        ImGui.Combo($"##avg{label}{id}", ref currentModel, _transformFunction, 12, 12);
        ImGui.PushItemWidth(200);
        ImGui.DragFloat($"##var{label}{id}", ref currentFactor);
        ImGui.PopItemWidth();
        ImGui.PopItemWidth();

        EnumTransformFunction newTransform = (EnumTransformFunction)currentModel;

        value = new(newTransform, currentFactor);
    }
}