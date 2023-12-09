﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using ImGuiNET;
using Newtonsoft.Json;
using Vintagestory.API.MathTools;
using System.Collections.Generic;
using System.Numerics;

namespace ModdingTools
{
    public class ParticleEditor
    {
        private readonly ICoreClientAPI mClientApi;
        private readonly Block mSelectedBlock;
        private readonly AdvancedParticleProperties[] mBackup;

        public ParticleEditor(ICoreClientAPI api, Block block)
        {
            mClientApi = api;
            mSelectedBlock = block;
            if (block != null)
            {
                List<AdvancedParticleProperties> backup = new();
                foreach (AdvancedParticleProperties property in block.ParticleProperties)
                {
                    backup.Add(property.Clone());
                }
                
                mBackup = backup.ToArray();
            }
            else
            {
                mBackup = new AdvancedParticleProperties[] { };
            }
        }

        public void RenderWindow()
        {
            if (mSelectedBlock != null)
            {
                RestoreFromBackup();
                ProcessParticleEffects(mSelectedBlock);
            }
        }
        private void RestoreFromBackup()
        {
            if (ImGui.Button("Restore to defaults"))
            {
                for (int index = 0; index < mBackup.Length; index++)
                {
                    mSelectedBlock.ParticleProperties[index] = mBackup[index].Clone();
                }
            }
        }
        private void ProcessParticleEffects(Block block)
        {
            if (block.ParticleProperties == null) return;

            int id = 0;

            foreach (var particleProperties in block.ParticleProperties)
            {
                if (particleProperties == null) continue;

                id++;
                
                if (ImGui.CollapsingHeader($"Particle effect #{id}")) ShowParticleEffectGui(id, particleProperties);
            }
        }
        private void ShowParticleEffectGui(int id, AdvancedParticleProperties particleProperties)
        {
            ImGui.Indent();

            JsonOutput(id, particleProperties);
            ParticleModelEditor(id, particleProperties);
            ColorEditor(id, particleProperties);
            ColorEvolveEditor(id, particleProperties);
            BehaviorEditor(id, particleProperties);
            SizeEditor(id, particleProperties);
            VelocityEditor(id, particleProperties);
            BooleansEditor(id, particleProperties);
            FlagsEditor(id, particleProperties);

            ImGui.BeginDisabled();
            ImGui.CollapsingHeader($"Secondary particles:##{id}");
            ImGui.CollapsingHeader($"Death particles:##{id}");
            ImGui.EndDisabled();

            ImGui.Unindent();
        }


        // MAIN EDITORS
        private void JsonOutput(int id, AdvancedParticleProperties particleProperties)
        {
            if (!ImGui.CollapsingHeader($"JSON:##{id}")) return;
            ImGui.Indent();
            JsonSerializerSettings settings = new();
            settings.NullValueHandling = NullValueHandling.Ignore;
            string output = JsonConvert.SerializeObject(particleProperties, Formatting.Indented, settings);
            ImGui.InputTextMultiline($"##{id}", ref output, (uint)output.Length, new(500, 300), ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AutoSelectAll);
            ImGui.Unindent();
        }
        private void ColorEditor(int id, AdvancedParticleProperties particleProperties)
        {
            if (!ImGui.CollapsingHeader($"Color:##{id}")) return;
            ImGui.Indent();

            HsvaEditor(id, particleProperties);
            HsvaVarianceEditor(id, particleProperties);

            bool ColorByBlock = particleProperties.ColorByBlock;
            ImGui.Checkbox($"Color by block##{id}", ref ColorByBlock);
            particleProperties.ColorByBlock = ColorByBlock;

            ImGui.Unindent();
        }
        private void ColorEvolveEditor(int id, AdvancedParticleProperties particleProperties)
        {
            if (!ImGui.CollapsingHeader($"Color evolve:##{id}")) return;
            ImGui.Indent();

            EvolvingNatFloat? opacity = particleProperties.OpacityEvolve;
            EvolvingNatFloatEditorNullable(id, "Opacity", ref opacity);
            particleProperties.OpacityEvolve = opacity;

            EvolvingNatFloat? red = particleProperties.RedEvolve;
            EvolvingNatFloatEditorNullable(id, "Red", ref red);
            particleProperties.RedEvolve = red;

            EvolvingNatFloat? green = particleProperties.GreenEvolve;
            EvolvingNatFloatEditorNullable(id, "Green", ref green);
            particleProperties.GreenEvolve = green;

            EvolvingNatFloat? blue = particleProperties.BlueEvolve;
            EvolvingNatFloatEditorNullable(id, "Blue", ref blue);
            particleProperties.BlueEvolve = blue;

            ImGui.Unindent();
        }
        private void NatFloatEditor(int id, string name, ref NatFloat value, int nameSize = 150)
        {
            ImGui.PushItemWidth(80);
            ImGui.Text($"{name}: "); ImGui.SameLine(nameSize);
            ImGui.Text("Avg ="); ImGui.SameLine(nameSize + 50);
            ImGui.InputFloat($"##avg{name}{id}", ref value.avg); ImGui.SameLine(nameSize + 150);
            ImGui.Text("Var ="); ImGui.SameLine(nameSize + 200);
            ImGui.InputFloat($"##var{name}{id}", ref value.var);
            ImGui.PopItemWidth();
        }
        private void BehaviorEditor(int id, AdvancedParticleProperties particleProperties)
        {
            if (!ImGui.CollapsingHeader($"Behavior:##{id}")) return;

            ImGui.Indent();

            NatFloat gravity = particleProperties.GravityEffect;
            NatFloatEditor(id, "Gravity", ref gravity);
            particleProperties.GravityEffect = gravity;

            NatFloat lifeLength = particleProperties.LifeLength;
            NatFloatEditor(id, "Life length", ref lifeLength);
            particleProperties.LifeLength = lifeLength;

            NatFloat quantity = particleProperties.Quantity;
            NatFloatEditor(id, "Quantity", ref quantity);
            particleProperties.Quantity = quantity;

            NatFloat SecondarySpawnInterval = particleProperties.SecondarySpawnInterval;
            NatFloatEditor(id, "Secondary spawn interval", ref SecondarySpawnInterval, 250);
            particleProperties.SecondarySpawnInterval = SecondarySpawnInterval;

            float WindAffectednes = particleProperties.WindAffectednes;
            ImGui.InputFloat($"Wind affectednes##{id}", ref WindAffectednes);
            particleProperties.WindAffectednes = WindAffectednes;

            float Bounciness = particleProperties.Bounciness;
            ImGui.InputFloat($"Bounciness##{id}", ref Bounciness);
            particleProperties.Bounciness = Bounciness;

            NatFloat[] PosOffset = particleProperties.PosOffset;
            NatFloatVecEditor(id, "Position offset", ref PosOffset);
            particleProperties.PosOffset = PosOffset;

            ImGui.Unindent();
        }
        private void SizeEditor(int id, AdvancedParticleProperties particleProperties)
        {
            if (!ImGui.CollapsingHeader($"Size:##{id}")) return;

            ImGui.Indent();

            NatFloat size = particleProperties.Size;
            NatFloatEditor(id, "Size", ref size);
            particleProperties.Size = size;

            EvolvingNatFloat? sizeEvolve = particleProperties.SizeEvolve;
            EvolvingNatFloatEditorNullable(id, "Size evolve", ref sizeEvolve);
            particleProperties.SizeEvolve = sizeEvolve;

            ImGui.Unindent();
        }
        private void VelocityEditor(int id, AdvancedParticleProperties particleProperties)
        {
            if (!ImGui.CollapsingHeader($"Velocity:##{id}")) return;

            ImGui.Indent();

            NatFloat velocityX = particleProperties.Velocity[0];
            NatFloatEditor(id, "Velocity.X", ref velocityX);
            particleProperties.Velocity[0] = velocityX;

            NatFloat velocityY = particleProperties.Velocity[1];
            NatFloatEditor(id, "Velocity.Y", ref velocityY);
            particleProperties.Velocity[1] = velocityY;

            NatFloat velocityZ = particleProperties.Velocity[2];
            NatFloatEditor(id, "Velocity.Z", ref velocityZ);
            particleProperties.Velocity[2] = velocityZ;

            if (particleProperties.VelocityEvolve == null && ImGui.Button("Add velocity evolve"))
            {
                particleProperties.VelocityEvolve = new EvolvingNatFloat[]
                {
                    new(),
                    new(),
                    new()
                };
            }

            if (particleProperties.VelocityEvolve != null)
            {
                EvolvingNatFloat velocityEvolveX = particleProperties.VelocityEvolve[0];
                EvolvingNatFloatEditor(id, "Velocity.X evolve", ref velocityEvolveX);
                particleProperties.VelocityEvolve[0] = velocityEvolveX;

                EvolvingNatFloat velocityEvolveY = particleProperties.VelocityEvolve[1];
                EvolvingNatFloatEditor(id, "Velocity.Y evolve", ref velocityEvolveY);
                particleProperties.VelocityEvolve[1] = velocityEvolveY;

                EvolvingNatFloat velocityEvolveZ = particleProperties.VelocityEvolve[2];
                EvolvingNatFloatEditor(id, "Velocity.Z evolve", ref velocityEvolveZ);
                particleProperties.VelocityEvolve[2] = velocityEvolveZ;
            }

            if (particleProperties.VelocityEvolve != null && ImGui.Button("Remove velocity evolve"))
            {
                particleProperties.VelocityEvolve = null;
            }

            ImGui.Unindent();
        }
        private void BooleansEditor(int id, AdvancedParticleProperties particleProperties)
        {
            if (!ImGui.CollapsingHeader($"Bool properties:##{id}")) return;

            ImGui.Indent();

            bool DieOnRainHeightmap = particleProperties.DieOnRainHeightmap;
            ImGui.Checkbox($"Die on rain height map##{id}", ref DieOnRainHeightmap);
            particleProperties.DieOnRainHeightmap = DieOnRainHeightmap;

            bool RandomVelocityChange = particleProperties.RandomVelocityChange;
            ImGui.Checkbox($"Random velocity change##{id}", ref RandomVelocityChange);
            particleProperties.RandomVelocityChange = RandomVelocityChange;

            bool DieInAir = particleProperties.DieInAir;
            ImGui.Checkbox($"Die in air##{id}", ref DieInAir);
            particleProperties.DieInAir = DieInAir;

            bool DieInLiquid = particleProperties.DieInLiquid;
            ImGui.Checkbox($"Die in liquid##{id}", ref DieInLiquid);
            particleProperties.DieInLiquid = DieInLiquid;

            bool SwimOnLiquid = particleProperties.SwimOnLiquid;
            ImGui.Checkbox($"Swim on liquid##{id}", ref SwimOnLiquid);
            particleProperties.SwimOnLiquid = SwimOnLiquid;

            bool SelfPropelled = particleProperties.SelfPropelled;
            ImGui.Checkbox($"Self-propelled##{id}", ref SelfPropelled);
            particleProperties.SelfPropelled = SelfPropelled;

            bool TerrainCollision = particleProperties.TerrainCollision;
            ImGui.Checkbox($"Terrain collision##{id}", ref TerrainCollision);
            particleProperties.TerrainCollision = TerrainCollision;

            ImGui.Unindent();
        }
        private void FlagsEditor(int id, AdvancedParticleProperties particleProperties)
        {
            if (!ImGui.CollapsingHeader($"Vertex flags:##{id}")) return;

            ImGui.Indent();

            VertexFlags flags = new(particleProperties.VertexFlags);

            int glowLevel = flags.GlowLevel;
            ImGui.SliderInt($"Glow level##{id}", ref glowLevel, 0, 255);
            flags.GlowLevel = (byte)glowLevel;

            bool Reflective = flags.Reflective;
            ImGui.Checkbox("Reflective", ref Reflective);
            flags.Reflective = Reflective;

            int ZOffset = flags.ZOffset;
            ImGui.SliderInt($"Z offset##{id}", ref ZOffset, 0, 255);
            flags.ZOffset = (byte)ZOffset;

            bool Lod0 = flags.Lod0;
            ImGui.Checkbox("Lod0", ref Lod0);
            flags.Lod0 = Lod0;

            EnumWindBitMode WindMode = flags.WindMode;
            WindBitModeEditor(id, "Wind mode", ref WindMode);
            flags.WindMode = WindMode;

            int WindData = flags.WindData;
            ImGui.SliderInt($"Wind data##{id}", ref WindData, 0, 255);
            flags.WindData = (byte)WindData;

            int Normal = flags.Normal;
            ImGui.InputInt($"Normal##{id}", ref Normal);
            flags.Normal = (short)GameMath.Clamp(Normal, short.MinValue, short.MaxValue);

            particleProperties.VertexFlags = flags.All;

            ImGui.Unindent();
        }

        // SUPPLEMENTARY EDITORS
        private void HsvaEditor(int id, AdvancedParticleProperties particleProperties)
        {
            float hue = particleProperties.HsvaColor[0].avg;
            float saturation = particleProperties.HsvaColor[1].avg;
            float value = particleProperties.HsvaColor[2].avg;
            float alpha = particleProperties.HsvaColor[3].avg;

            System.Numerics.Vector4 color = new(hue / 255, saturation / 255, value / 255, alpha / 255);
            ImGui.ColorPicker4($"Color##{id}", ref color, ImGuiColorEditFlags.InputHSV | ImGuiColorEditFlags.DisplayHSV);

            particleProperties.HsvaColor[0].avg = color.X * 255f;
            particleProperties.HsvaColor[1].avg = color.Y * 255f;
            particleProperties.HsvaColor[2].avg = color.Z * 255f;
            particleProperties.HsvaColor[3].avg = color.W * 255f;
        }
        private void HsvaVarianceEditor(int id, AdvancedParticleProperties particleProperties)
        {
            float hue = particleProperties.HsvaColor[0].var;
            float saturation = particleProperties.HsvaColor[1].var;
            float value = particleProperties.HsvaColor[2].var;
            float alpha = particleProperties.HsvaColor[3].var;

            System.Numerics.Vector4 color = new(hue, saturation, value, alpha);

            ImGui.InputFloat4($"Variance HSVA##{id}", ref color, "%.0f");

            particleProperties.HsvaColor[0].var = color.X;
            particleProperties.HsvaColor[1].var = color.Y;
            particleProperties.HsvaColor[2].var = color.Z;
            particleProperties.HsvaColor[3].var = color.W;
        }

        private static string[] ParticleModels = new[] { "Quad", "Cube" };
        private void ParticleModelEditor(int id, AdvancedParticleProperties particleProperties)
        {
            int currentModel = (int)particleProperties.ParticleModel;

            ImGui.Combo($"Model##{id}", ref currentModel, ParticleModels, 2, 2);

            particleProperties.ParticleModel = (EnumParticleModel)currentModel;
        }

        private static string[] TransformFunction = new[]
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
        private Dictionary<int, EvolvingNatFloat?> mPrevValues = new();
        private void EvolvingNatFloatEditorNullable(int id, string label, ref EvolvingNatFloat? value)
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
            ImGui.Combo($"##{label}{id}", ref currentModel, TransformFunction, 12, 12);
            ImGui.DragFloat($"##{label}{id}", ref currentFactor);

            EnumTransformFunction newTransform = (EnumTransformFunction)currentModel;

            value = new(newTransform, currentFactor);
        }
        private void EvolvingNatFloatEditor(int id, string label, ref EvolvingNatFloat value)
        {
            ImGui.Text($"{label}: ");

            int currentModel = (int)value.Transform;
            float currentFactor = value.Factor;
            ImGui.Combo($"##avg{label}{id}", ref currentModel, TransformFunction, 12, 12);
            ImGui.DragFloat($"##var{label}{id}", ref currentFactor);

            EnumTransformFunction newTransform = (EnumTransformFunction)currentModel;

            value = new(newTransform, currentFactor);
        }

        private static string[] WindBitMode = new[]
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
        private void WindBitModeEditor(int id, string name, ref EnumWindBitMode value)
        {
            int intValue = (int)value;
            ImGui.Combo($"{name}##{id}", ref intValue, WindBitMode, WindBitMode.Length);
            value = (EnumWindBitMode)intValue;
        }

        private void NatFloatVecEditor(int id, string name, ref NatFloat[] vector)
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
}
