using System;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class ThermalSection
{
    public static Section Build(EngineSimulationHost host, Action onToggle)
    {
        var c = host.Profile.Combustion;
        var section = new Section("Thermal tracking") { OnToggle = onToggle };

        section.AddFloat("Cylinder tau",
            "Combustion to cylinder thermal lag [s]. How quickly cylinder temperature reacts to a change in engine power.\n" +
            "Lower this to make the cylinders respond more quickly to a change in engine power.",
            1f, 30f, false, () => c.TauCylinder, v => { c.TauCylinder = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block tau",
            "Cylinder to engine block thermal lag [s]. How quickly the cylinders feed heat into the engine block.\n" +
            "Lower this to make the engine block respond more quickly to a change in cylinder temperature.",
            1f, 360f, false, () => c.TauEngine, v => { c.TauEngine = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block cooldown (open)",
            "Block-to-ambient cooldown lag [s] with the thermostat open.\n" +
            "Maximum cooling rate when the engine block is being cooled at full capacity. Lower = more cooling.",
            30f, 1200f, false, () => c.TauCooldownOpen, v => { c.TauCooldownOpen = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block cooldown (closed)",
            "Block-to-ambient cooldown lag [s] with the thermostat shut.\n" +
            "Ambient cooling rate when the engine block is not being actively cooled. Lower = more cooling.",
            300f, 7200f, false, () => c.TauCooldownClosed, v => { c.TauCooldownClosed = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Cylinder gain",
            "Cylinder temperature rise above the block at full power [K].\n" +
            "Raise this to increase the temperature difference between cylinders and engine block at steady-state full power.",
            0f, 200f, false, () => c.CylinderGainK, v => { c.CylinderGainK = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Cold cylinder temperature",
            "Cylinder temperature [K] below which the full cold-temperature combustion penalty is fully applied.",
            250f, 400f, false, () => c.ColdWallFloorK, v => { c.ColdWallFloorK = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Warm cylinder temperature",
            "Cylinder temperature [K] above which the cold-temperature penalty has completely disappeared.",
            250f, 600f, false, () => c.WarmWallTargetK, v => { c.WarmWallTargetK = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Cold burn floor",
            "Maximum reachable combustion efficiency when the full cold-temperature penalty is applied.",
            0f, 1f, false, () => c.MinBurnFractionAtCold, v => { c.MinBurnFractionAtCold = v; c.Validate(); }, TweakGrade.Developer);

        return section;
    }
}
