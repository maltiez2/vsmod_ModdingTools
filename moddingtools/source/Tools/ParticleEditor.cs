using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using VSImGui;


namespace ModdingTools;

public class ParticleEditor
{
    public ParticleEditor(ICoreClientAPI api, Block block)
    {
        _clientApi = api;
        _selectedBlock = block;
        if (block != null && block.ParticleProperties != null)
        {
            List<AdvancedParticleProperties> backup = new();
            foreach (AdvancedParticleProperties property in block.ParticleProperties)
            {
                backup.Add(property.Clone());
            }

            _backup = backup.ToArray();
            _editors = _backup.Select(effect => new ParticlePropertiesEditor(effect, api)).ToList();
            _id = block.GetHashCode();
        }
        else
        {
            _backup = System.Array.Empty<AdvancedParticleProperties>();
            _editors = new();
            _id = 0;
        }
    }

    public void Draw()
    {
        if (_selectedBlock == null) return;

        EffectSelector();
        ImGui.SeparatorText("Advanced Particle Properties");
        DrawParticleEditor();
    }

    private readonly ICoreClientAPI _clientApi;
    private readonly Block _selectedBlock;
    private readonly AdvancedParticleProperties[] _backup;
    private readonly List<ParticlePropertiesEditor> _editors;
    private readonly int _id;
    private int _current;
    private bool _confirmRemoval;
    private bool _confirmRestore;

    private string TitleGeneral(string text) => $"{text}##{_id}";
    private bool ValidateCurrentIndex()
    {
        if (_current >= _editors.Count) _current = _editors.Count - 1;
        if (_current < 0) _current = 0;
        return _editors.Count != 0;
    }

    private void EffectSelector()
    {
        int size = _editors.Count;

        if (_confirmRemoval || _confirmRestore) ImGui.BeginDisabled();
        if (ImGui.Button(TitleGeneral("Add")))
        {
            AddEffect();
        }
        ImGui.SameLine();

        if (size > 0 && ImGui.Button(TitleGeneral("Duplicate")))
        {
            DuplicateEffect();
        }
        if (size > 0) ImGui.SameLine();
        if (_confirmRemoval || _confirmRestore) ImGui.EndDisabled();

        if (_confirmRestore) ImGui.BeginDisabled();
        if (size > 0 && Widgets.ButtonWithConfirmation(TitleGeneral("Remove"), ref _confirmRemoval))
        {
            RemoveEffect();
            _confirmRemoval = false;
            size = _editors.Count;
        }
        if (!_confirmRemoval && size > 0) ImGui.SameLine();
        if (_confirmRestore) ImGui.EndDisabled();

        if (!_confirmRemoval && size > 0 && Widgets.ButtonWithConfirmation(TitleGeneral("Restore all"), ref _confirmRestore))
        {
            RestoreAll();
            _confirmRestore = false;
            size = _editors.Count;
        }

        if (size == 0) return;

        ImGui.PushItemWidth(100);
        ImGui.InputInt(TitleGeneral("##selector_1"), ref _current);
        if (_current >= size) _current = size - 1;
        ImGui.SameLine();
        ImGui.PopItemWidth();

        ImGui.PushItemWidth(300);
        ImGui.SliderInt(TitleGeneral("Effect##selector_2"), ref _current, 0, size - 1);
        ImGui.PopItemWidth();
    }
    private void AddEffect()
    {
        AdvancedParticleProperties effect = new();
        _editors.Add(new(effect, _clientApi));
        if (_selectedBlock.ParticleProperties == null)
        {
            _selectedBlock.ParticleProperties = new AdvancedParticleProperties[] { effect };
            return;
        }
        _selectedBlock.ParticleProperties = _selectedBlock.ParticleProperties.Append(effect).ToArray();
    }
    private void RemoveEffect()
    {
        if (!ValidateCurrentIndex()) return;
        _editors.RemoveAt(_current);
        _selectedBlock.ParticleProperties = _selectedBlock.ParticleProperties.RemoveEntry(_current).ToArray();
        ValidateCurrentIndex();
    }
    private void DuplicateEffect()
    {
        if (!ValidateCurrentIndex()) return;
        ParticlePropertiesEditor effect = _editors[_current];
        ParticlePropertiesEditor duplicated = effect.Duplicate();
        _editors.Add(duplicated);
        _selectedBlock.ParticleProperties = _selectedBlock.ParticleProperties.Append(duplicated.Properties).ToArray();
    }
    private void RestoreAll()
    {
        _selectedBlock.ParticleProperties = _backup.Select(x => x.Clone()).ToArray();
        _editors.Clear();
        foreach (AdvancedParticleProperties? effect in _selectedBlock.ParticleProperties)
        {
            _editors.Add(new(effect, _clientApi));
        }
        ValidateCurrentIndex();
    }

    private void DrawParticleEditor()
    {
        if (!ValidateCurrentIndex()) return;

        _editors[_current].Draw();
    }
}

public class ParticlePropertiesEditor
{
    public ParticlePropertiesEditor(AdvancedParticleProperties properties, ICoreAPI api)
    {
        _api = api;
        _properties = properties;
        _id = _properties.GetHashCode();
        WriteToBytes(out _backup, properties);
    }

    public AdvancedParticleProperties Properties => _properties;

    public bool Draw()
    {
        ControlButtons();

        if (ImGui.BeginTabBar(Title(""), ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem(Title("Colors")))
            {
                ImGui.BeginChild(Title(""));
                Colors();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Title("Behavior")))
            {
                ImGui.BeginChild(Title(""));
                Behavior();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Title("Flags")))
            {
                ImGui.BeginChild(Title(""));
                Flags();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        bool modified = _modified;
        _modified = false;
        return modified;
    }
    public void Restore()
    {
        RestoreFromBytes(_backup);
    }
    public ParticlePropertiesEditor Duplicate() => new(_properties.Clone(), _api);

    #region Private
    private readonly AdvancedParticleProperties _properties;
    private readonly byte[] _backup;
    private readonly ICoreAPI _api;
    private readonly int _id;
    private bool _confirming;
    private bool _modified;
    private bool _velocityEvolve;
    private EvolvingNatFloat[]? _velocityEvolveValue;

    private string Title(string text) => $"{text}##{_id}";
    private void RestoreFromBytes(byte[] data)
    {
        using (MemoryStream memoryStream = new(data))
        {
            using BinaryReader reader = new(memoryStream);
            _properties.FromBytes(reader, _api.World);
        }
        _properties.Init(_api);
    }
    private void WriteToBytes(out byte[] data, AdvancedParticleProperties properties)
    {
        using MemoryStream memoryStream = new();
        using BinaryWriter writer = new(memoryStream);
        properties.ToBytes(writer);
        data = memoryStream.ToArray();
    }

    #region Tabs
    private void ControlButtons()
    {
        if (Widgets.ButtonWithConfirmation(Title("Restore"), ref _confirming))
        {
            Restore();
            _modified = true;
        }
        if (!_confirming)
        {
            ImGui.SameLine();
            Export(ImGui.Button(Title("Export")));
        }
    }
    private void Colors()
    {
        ColorByBlock();
        HsvaColor();
        HsvaVariance();
        ImGui.SeparatorText("Colors evolve behavior");
        ColorEvolving();
    }
    private void Behavior()
    {
        ImGui.SeparatorText("Spawn properties");
        SpawnProperties();
        ImGui.SeparatorText("Motion properties");
        MotionProperties();
        ImGui.SeparatorText("Velocity properties");
        VelocityProperties();
        VelocityEvolve();
        ImGui.SeparatorText("Size properties");
        SizeProperties();
    }
    private void Flags()
    {
        ImGui.SeparatorText("Boolean properties");
        BooleanProperties();
        ImGui.SeparatorText("Vertex flags");
        FlagProperties();
    }
    #endregion

    #region Editors
    private void ColorByBlock()
    {
        bool ColorByBlock = _properties.ColorByBlock;
        ImGui.Checkbox($"Color by block##{_id}", ref ColorByBlock);
        if (_properties.ColorByBlock != ColorByBlock) _modified = true;
        _properties.ColorByBlock = ColorByBlock;
    }
    private void HsvaColor()
    {
        float hue = _properties.HsvaColor[0].avg;
        float saturation = _properties.HsvaColor[1].avg;
        float value = _properties.HsvaColor[2].avg;
        float alpha = _properties.HsvaColor[3].avg;

        System.Numerics.Vector4 color = new(hue / 255, saturation / 255, value / 255, alpha / 255);
        System.Numerics.Vector4 colorBackup = color;
        ImGui.ColorPicker4($"Color##{_id}", ref color, ImGuiColorEditFlags.InputHSV | ImGuiColorEditFlags.DisplayHSV);

        _properties.HsvaColor[0].avg = color.X * 255f;
        _properties.HsvaColor[1].avg = color.Y * 255f;
        _properties.HsvaColor[2].avg = color.Z * 255f;
        _properties.HsvaColor[3].avg = color.W * 255f;

        if (color != colorBackup) _modified = true;
    }
    private void HsvaVariance()
    {
        float hue = _properties.HsvaColor[0].var;
        float saturation = _properties.HsvaColor[1].var;
        float value = _properties.HsvaColor[2].var;
        float alpha = _properties.HsvaColor[3].var;

        System.Numerics.Vector4 color = new(hue, saturation, value, alpha);
        System.Numerics.Vector4 colorBackup = color;
        ImGui.InputFloat4($"Variance HSVA##{_id}", ref color, "%.0f");

        _properties.HsvaColor[0].var = color.X;
        _properties.HsvaColor[1].var = color.Y;
        _properties.HsvaColor[2].var = color.Z;
        _properties.HsvaColor[3].var = color.W;

        if (color != colorBackup) _modified = true;
    }
    private void ColorEvolving()
    {
        EvolvingNatFloat? opacity = _properties.OpacityEvolve;
        PrticlePropertiesEditors.EvolvingNatFloatEditorNullable(_id, "Opacity", ref opacity);
        if (_properties.OpacityEvolve != opacity) _modified = true;
        _properties.OpacityEvolve = opacity;

        EvolvingNatFloat? red = _properties.RedEvolve;
        PrticlePropertiesEditors.EvolvingNatFloatEditorNullable(_id, "Red", ref red);
        if (_properties.RedEvolve != red) _modified = true;
        _properties.RedEvolve = red;

        EvolvingNatFloat? green = _properties.GreenEvolve;
        PrticlePropertiesEditors.EvolvingNatFloatEditorNullable(_id, "Green", ref green);
        if (_properties.GreenEvolve != green) _modified = true;
        _properties.GreenEvolve = green;

        EvolvingNatFloat? blue = _properties.BlueEvolve;
        PrticlePropertiesEditors.EvolvingNatFloatEditorNullable(_id, "Blue", ref blue);
        if (_properties.BlueEvolve != blue) _modified = true;
        _properties.BlueEvolve = blue;
    }

    private void SpawnProperties()
    {
        NatFloat lifeLength = _properties.LifeLength;
        PrticlePropertiesEditors.NatFloatEditor(_id, "Life length", ref lifeLength);
        _properties.LifeLength = lifeLength;

        NatFloat quantity = _properties.Quantity;
        PrticlePropertiesEditors.NatFloatEditor(_id, "Quantity", ref quantity);
        _properties.Quantity = quantity;

        NatFloat SecondarySpawnInterval = _properties.SecondarySpawnInterval;
        PrticlePropertiesEditors.NatFloatEditor(_id, "Secondary spawn interval", ref SecondarySpawnInterval, 250);
        _properties.SecondarySpawnInterval = SecondarySpawnInterval;

        NatFloat[] PosOffset = _properties.PosOffset;
        PrticlePropertiesEditors.NatFloatVecEditor(_id, "Position offset", ref PosOffset);
        _properties.PosOffset = PosOffset;
    }
    private void MotionProperties()
    {
        NatFloat gravity = _properties.GravityEffect;
        PrticlePropertiesEditors.NatFloatEditor(_id, "Gravity", ref gravity);
        _properties.GravityEffect = gravity;

        float WindAffectednes = _properties.WindAffectednes;
        ImGui.InputFloat($"Wind affectednes##{_id}", ref WindAffectednes);
        _properties.WindAffectednes = WindAffectednes;

        float Bounciness = _properties.Bounciness;
        ImGui.InputFloat($"Bounciness##{_id}", ref Bounciness);
        _properties.Bounciness = Bounciness;
    }

    private void VelocityProperties()
    {
        NatFloat velocityX = _properties.Velocity[0];
        PrticlePropertiesEditors.NatFloatEditor(_id, "Velocity.X", ref velocityX);
        _properties.Velocity[0] = velocityX;

        NatFloat velocityY = _properties.Velocity[1];
        PrticlePropertiesEditors.NatFloatEditor(_id, "Velocity.Y", ref velocityY);
        _properties.Velocity[1] = velocityY;

        NatFloat velocityZ = _properties.Velocity[2];
        PrticlePropertiesEditors.NatFloatEditor(_id, "Velocity.Z", ref velocityZ);
        _properties.Velocity[2] = velocityZ;
    }
    private void VelocityEvolve()
    {
        ImGui.Checkbox(Title("Velocity evolving"), ref _velocityEvolve);

        if (!_velocityEvolve)
        {
            if (_velocityEvolveValue == null && _properties.VelocityEvolve != null)
            {
                _velocityEvolveValue = _properties.VelocityEvolve;
            }

            _properties.VelocityEvolve = null;
            return;
        }

        if (_properties.VelocityEvolve == null)
        {
            if (_velocityEvolveValue != null)
            {
                _properties.VelocityEvolve = _velocityEvolveValue;
            }
            else
            {
                _properties.VelocityEvolve = new EvolvingNatFloat[]
                {
                    new(),
                    new(),
                    new()
                };
            }

            _velocityEvolveValue = null;
        }

        if (_properties.VelocityEvolve != null)
        {
            EvolvingNatFloat velocityEvolveX = _properties.VelocityEvolve[0];
            PrticlePropertiesEditors.EvolvingNatFloatEditor(_id, "Velocity.X evolve", ref velocityEvolveX);
            _properties.VelocityEvolve[0] = velocityEvolveX;

            EvolvingNatFloat velocityEvolveY = _properties.VelocityEvolve[1];
            PrticlePropertiesEditors.EvolvingNatFloatEditor(_id, "Velocity.Y evolve", ref velocityEvolveY);
            _properties.VelocityEvolve[1] = velocityEvolveY;

            EvolvingNatFloat velocityEvolveZ = _properties.VelocityEvolve[2];
            PrticlePropertiesEditors.EvolvingNatFloatEditor(_id, "Velocity.Z evolve", ref velocityEvolveZ);
            _properties.VelocityEvolve[2] = velocityEvolveZ;
        }
    }
    private void SizeProperties()
    {
        NatFloat size = _properties.Size;
        PrticlePropertiesEditors.NatFloatEditor(_id, "Size", ref size);
        _properties.Size = size;

        EvolvingNatFloat? sizeEvolve = _properties.SizeEvolve;
        PrticlePropertiesEditors.EvolvingNatFloatEditorNullable(_id, "Size evolve", ref sizeEvolve);
        _properties.SizeEvolve = sizeEvolve;
    }

    private void BooleanProperties()
    {
        bool DieOnRainHeightmap = _properties.DieOnRainHeightmap;
        ImGui.Checkbox(Title("Die on rain height map"), ref DieOnRainHeightmap);
        _properties.DieOnRainHeightmap = DieOnRainHeightmap;

        bool RandomVelocityChange = _properties.RandomVelocityChange;
        ImGui.Checkbox(Title("Random velocity change"), ref RandomVelocityChange);
        _properties.RandomVelocityChange = RandomVelocityChange;

        bool DieInAir = _properties.DieInAir;
        ImGui.Checkbox(Title("Die in air"), ref DieInAir);
        _properties.DieInAir = DieInAir;

        bool DieInLiquid = _properties.DieInLiquid;
        ImGui.Checkbox(Title("Die in liquid"), ref DieInLiquid);
        _properties.DieInLiquid = DieInLiquid;

        bool SwimOnLiquid = _properties.SwimOnLiquid;
        ImGui.Checkbox(Title("Swim on liquid"), ref SwimOnLiquid);
        _properties.SwimOnLiquid = SwimOnLiquid;

        bool SelfPropelled = _properties.SelfPropelled;
        ImGui.Checkbox(Title("Self-propelled"), ref SelfPropelled);
        _properties.SelfPropelled = SelfPropelled;

        bool TerrainCollision = _properties.TerrainCollision;
        ImGui.Checkbox(Title("Terrain collision"), ref TerrainCollision);
        _properties.TerrainCollision = TerrainCollision;
    }
    private void FlagProperties()
    {
        VertexFlags flags = new(_properties.VertexFlags);

        int glowLevel = flags.GlowLevel;
        ImGui.SliderInt(Title("Glow level"), ref glowLevel, 0, 255);
        flags.GlowLevel = (byte)glowLevel;

        bool Reflective = flags.Reflective;
        ImGui.Checkbox(Title("Reflective"), ref Reflective);
        flags.Reflective = Reflective;

        int ZOffset = flags.ZOffset;
        ImGui.SliderInt(Title("Z offset"), ref ZOffset, 0, 255);
        flags.ZOffset = (byte)ZOffset;

        bool Lod0 = flags.Lod0;
        ImGui.Checkbox(Title("Lod0"), ref Lod0);
        flags.Lod0 = Lod0;

        EnumWindBitMode WindMode = flags.WindMode;
        PrticlePropertiesEditors.WindBitModeEditor(_id, "Wind mode", ref WindMode);
        flags.WindMode = WindMode;

        int WindData = flags.WindData;
        ImGui.SliderInt(Title("Wind data"), ref WindData, 0, 255);
        flags.WindData = (byte)WindData;

        int Normal = flags.Normal;
        ImGui.InputInt(Title("Normal"), ref Normal);
        flags.Normal = (short)GameMath.Clamp(Normal, short.MinValue, short.MaxValue);

        _properties.VertexFlags = flags.All;
    }
    #endregion

    #region Export
    private string _serialized = "";
    private string _csharpCode = "";
    private bool _outputOpened = false;
    private bool _exception = false;
    private string _message = "";
    private int _outputOption = 0;
    public void Export(bool open)
    {
        if (open)
        {
            ImGui.OpenPopup(Title("Output##moddingtools"));
            _csharpCode = SerializeToCode();
            _serialized = SerializeToJson();
            _outputOpened = true;
        }

        if (_outputOpened && ImGui.BeginPopupModal(Title("Output##moddingtools"), ref _outputOpened))
        {
            ImGui.RadioButton("JSON", ref _outputOption, 0); ImGui.SameLine();
            ImGui.RadioButton("C#", ref _outputOption, 1);

            ImGui.Separator();
            switch (_outputOption)
            {
                case 0:
                    JsonCode();
                    break;
                case 1:
                    CsharpCode();
                    break;
            }
            ImGui.EndPopup();
        }
    }

    private void CsharpCode()
    {
        Vector2 size = ImGui.GetWindowSize();
        size.X -= 12;
        size.Y -= 112;

        if (ImGui.Button("Copy##csharp") || CopyCombination())
        {
            ImGui.SetClipboardText(_csharpCode);
        }

        ImGui.InputTextMultiline("##outputcsharp", ref _csharpCode, (uint)_csharpCode.Length * 2, size, ImGuiInputTextFlags.ReadOnly);
    }
    private void JsonCode()
    {
        Vector2 size = ImGui.GetWindowSize();
        size.X -= 12;
        size.Y -= 112;

        if (ImGui.Button("Apply##json")) DeserializeFromJson(_serialized);
        ImGui.SameLine();
        if (ImGui.Button("Copy##json") || CopyCombination())
        {
            ImGui.SetClipboardText(_serialized);
        }
        ImGui.SameLine();
        if (ImGui.Button("Paste##json") || PasteCombination())
        {
            _serialized = ImGui.GetClipboardText();
        }

        if (_exception) ShowException(ref size);
        ImGui.InputTextMultiline("##outputjson", ref _serialized, (uint)_serialized.Length * 2, size);
    }
    private bool CopyCombination()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        return io.KeyCtrl && io.KeysDown[(int)ImGuiKey.C];
    }
    private bool PasteCombination()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        return io.KeyCtrl && io.KeysDown[(int)ImGuiKey.V];
    }
    private void ShowException(ref Vector2 size)
    {
        Vector2 errorSize = ImGui.GetWindowSize();
        errorSize.X -= 10;
        errorSize.Y = 100;
        size.Y -= 104;

        ImGui.BeginChild(Title($"Apply##exception"), errorSize, true);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + errorSize.X);
        ImGui.Text(_message);
        ImGui.PopTextWrapPos();
        ImGui.EndChild();
    }

    private string SerializeToCode()
    {
        return "<not supported yet>";
    }
    private string SerializeToJson()
    {
        JsonSerializerSettings settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        return JsonConvert.SerializeObject(_properties, Formatting.Indented, settings);
    }
    private void DeserializeFromJson(string json)
    {
        try
        {
            _exception = false;
            JObject token = JObject.Parse(json);
            JsonObject effectJson = new(token);
            AdvancedParticleProperties effect = effectJson.AsObject<AdvancedParticleProperties>();
            WriteToBytes(out byte[] data, effect);
            RestoreFromBytes(data);
        }
        catch (Exception exception)
        {
            _exception = true;
            _message = $"Error: {exception.Message}\n\nStack trace: {exception.StackTrace}";
        }
    }
    #endregion

    #endregion
}

public static partial class PrticlePropertiesEditors
{
    public static string[] ParticleModels = new[] { "Quad", "Cube" };
    public static void ParticleModelEditor(int id, AdvancedParticleProperties particleProperties)
    {
        int currentModel = (int)particleProperties.ParticleModel;

        ImGui.Combo($"Model##{id}", ref currentModel, ParticleModels, 2, 2);

        particleProperties.ParticleModel = (EnumParticleModel)currentModel;
    }

    public static string[] TransformFunction = new[]
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
    public static Dictionary<int, EvolvingNatFloat?> mPrevValues = new();
    public static void EvolvingNatFloatEditorNullable(int id, string label, ref EvolvingNatFloat? value)
    {
        bool enabled = value != null;

        ImGui.Checkbox($"{label}##{id}", ref enabled);

        if (!enabled)
        {
            if (value != null) mPrevValues[id] = value;
            value = null;
            return;
        }

        if (value == null && !mPrevValues.TryGetValue(id, out value))
        {
            value = new(EnumTransformFunction.LINEAR, 0);
        }

        int currentModel = (int)value.Transform;
        float currentFactor = value.Factor;
        ImGui.Combo($"##combo{label}{id}", ref currentModel, TransformFunction, 12, 12);
        ImGui.DragFloat($"##drag{label}{id}", ref currentFactor);

        EnumTransformFunction newTransform = (EnumTransformFunction)currentModel;

        value = new(newTransform, currentFactor);
    }
    public static void EvolvingNatFloatEditor(int id, string label, ref EvolvingNatFloat value)
    {
        ImGui.Text($"{label}: ");

        int currentModel = (int)value.Transform;
        float currentFactor = value.Factor;
        ImGui.Combo($"##avg{label}{id}", ref currentModel, TransformFunction, 12, 12);
        ImGui.DragFloat($"##var{label}{id}", ref currentFactor);

        EnumTransformFunction newTransform = (EnumTransformFunction)currentModel;

        value = new(newTransform, currentFactor);
    }

    public static string[] WindBitMode = new[]
    {
        "NoWind",
        "WeakWind",
        "NormalWind",
        "Leaves",
        "Bend",
        "TallBend",
        "Water",
        "ExtraWeakWind",
        "Fruit",
        "WeakWindNoBend",
        "WeakWindInverseBend",
        "WaterPlant"
    };
    public static void WindBitModeEditor(int id, string name, ref EnumWindBitMode value)
    {
        int intValue = (int)value;
        ImGui.Combo($"{name}##{id}", ref intValue, WindBitMode, WindBitMode.Length);
        value = (EnumWindBitMode)intValue;
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
}