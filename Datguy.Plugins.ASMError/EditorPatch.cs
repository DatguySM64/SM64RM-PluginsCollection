using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DevComponents.DotNetBar.Controls;
using HarmonyLib;

[HarmonyPatch]
public static class AsmWindowPatch {
    // somehow broke my code so this is the only fix
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("SM64_ROM_Manager.AsmToHexConverter");

        if (type == null)
            throw new Exception("Could not find SM64_ROM_Manager.AsmToHexConverter");

        return AccessTools.Constructor(type, Type.EmptyTypes);
    }

    [HarmonyPostfix]
    public static void Postfix(object __instance) {
        // Adds the log into the assembler window.
        // TODO: Make the split between the input and the output clearer.
        var form = __instance as Form;

        if (form == null) return;

        if (form.Controls
            .Cast<Control>()
            .Any(c => c.Name == "ASMErrorLogSplit"))
            return;

        int originalWidth = form.ClientSize.Width;
        int originalHeight = form.ClientSize.Height;

        Control[] originalControls = form.Controls.Cast<Control>().ToArray();

        form.ClientSize = new Size(originalWidth * 2, originalHeight);

        var split = new SplitContainer {
            Name = "ASMErrorLogSplit",
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 4
        };
        split.Panel1.BackColor = form.BackColor;
        split.Panel2.BackColor = form.BackColor;

        form.Controls.Clear();

        foreach (Control control in originalControls) {
            split.Panel1.Controls.Add(control);
        }

        var output = new TextBoxX {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Name = "ASMErrorLogOutput",
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.0f)
        };

        split.Panel2.Controls.Add(output);

        form.Controls.Add(split);

        split.SplitterDistance = originalWidth;

        Plugin.SetOutputBox(output);
    }
}
