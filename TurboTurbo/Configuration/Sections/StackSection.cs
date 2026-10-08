using System;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class StackSection
{
    public static Section Build(EngineSimulationHost host, Action onToggle)
    {
        var s = host.Profile.Stack;
        var model = host.Stack;
        var section = new Section("Exhaust stack") { OnToggle = onToggle };

        section.AddFloat("Stack fill time",
            "Time [s] of cold idling needed to fill an empty exhaust stack with unburned fuel.\n" +
            "Lower this to make the stack foul more quickly.",
            10f, 1800f, false, () => s.FillTime, v => { s.FillTime = v; s.Validate(); }, TweakGrade.Basic);
        section.AddFloat("Stack clear time",
            "Time [s] for a full exhaust stack to dry out at full exhaust temperature.\n" +
            "Lower this to make the stack clear more quickly once hot.",
            5f, 600f, false, () => s.ClearTime, v => { s.ClearTime = v; s.Validate(); }, TweakGrade.Basic);
        section.AddFloat("Stack wall tau",
            "Thermal lag [s] of the exhaust metal. Higher values make the stack react more slowly to " +
            "changes in exhaust temperature, so it takes longer to warm up and to dry out.",
            5f, 120f, false, () => s.TauWall, v => { s.TauWall = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Flow warm bias",
            "Speeds up exhaust wall warm-up with exhaust flow, so idling heats the stack more slowly than a hard pull.",
            0f, 1f, false, () => s.FlowWarmBias, v => { s.FlowWarmBias = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Capture cold temperature",
            "Exhaust wall temperature [K] at and below which unburned fuel fully condenses onto the stack.",
            250f, 900f, false, () => s.CaptureColdK, v => { s.CaptureColdK = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Capture hot temperature",
            "Exhaust wall temperature [K] at and above which unburned fuel no longer condenses.",
            250f, 900f, false, () => s.CaptureHotK, v => { s.CaptureHotK = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Clear start temperature",
            "Exhaust wall temperature [K] at which condensed fuel starts to boil back off.",
            250f, 1000f, false, () => s.ClearStartK, v => { s.ClearStartK = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Clear full temperature",
            "Exhaust wall temperature [K] at which condensed fuel boils off at its full rate.",
            250f, 1000f, false, () => s.ClearFullK, v => { s.ClearFullK = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Contact at idle",
            "Fraction of unburned fuel that contacts the stack wall at zero exhaust flow.",
            0f, 1f, false, () => s.ContactIdle, v => { s.ContactIdle = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Contact at full flow",
            "Fraction of unburned fuel that contacts the stack wall at full exhaust flow.",
            0f, 1f, false, () => s.ContactFullFlow, v => { s.ContactFullFlow = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Max deposit",
            "Cap on the fraction of unburned fuel that deposits, leaving a floor that always escapes as visible vapour.",
            0.01f, 1f, false, () => s.StickMax, v => { s.StickMax = v; s.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Reference unburned",
            "Unburned fuel rate used to calibrate the fill time. Should match a cold idle if the fill time is to be accurate.",
            0f, 0.05f, false, () => s.ReferenceUnburned, v => { s.ReferenceUnburned = v; s.Validate(); }, TweakGrade.Developer);

        section.AddProgressButton("Fill exhaust stack", "Immediately fill the exhaust stack to 100%, to test the effect.",
            () => model.Fill(), () => model.WetStack, TweakGrade.Basic);

        return section;
    }
}
