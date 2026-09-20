using System.Windows.Forms;
using HarmonyLib;
using Pilz.Reflection.PluginSystem.Attributes;

public static class Plugin {
    private static Harmony harmony;
    private static TextBoxBase outputBox;
    [LoadMethod]
    public static void Init() {
        // If Harmony somehow already has an instance in this plugin already, don't create another one
        if (harmony == null) {
            harmony = new Harmony("datguy.plugins.asmerror");
            harmony.PatchAll();
        }
    }

    // Helper functions for modifying the output box
    public static void SetOutputBox(TextBoxBase box) {
        outputBox = box;
    }

    public static void ClearOutput() {
        if (outputBox == null)
            return;

        if (outputBox.InvokeRequired) {
            outputBox.Invoke(new System.Action(ClearOutput));
            return;
        }

        outputBox.Clear();
    }

    public static void AppendOutput(string text) {
        if (outputBox == null)
            return;

        if (outputBox.InvokeRequired) {
            outputBox.Invoke(new System.Action(() => AppendOutput(text)));
            return;
        }

        outputBox.AppendText(text);
    }
}