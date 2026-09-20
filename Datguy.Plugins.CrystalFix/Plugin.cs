using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Pilz.Reflection.PluginSystem.Attributes;

public static class Plugin {
    private static Harmony harmony;
    // Enables storing of vertex colors for later use.
    // Only necessary because G_TEXTURE_GEN uses normals.
    private class VertexColor {
        public byte R;
        public byte G;
        public byte B;
        public byte A;
    }

    private static readonly Dictionary<object, VertexColor>
        crystalVertexColors = new Dictionary<object, VertexColor>();

    [LoadMethod]
    public static void Init() {
        Type writerType = typeof(SM64Lib.Model.Conversion.Fast3DWriting.Fast3DWriter);

        harmony = new Harmony("datguy.plugins.crystalfix");

        // Patches BuildVertexStructure to store vertex colors for crystal materials.

        MethodInfo buildVertexStructure =
            writerType.GetMethod(
                "<ProcessObject3DModel>g__buildVertexStructure|52_2",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        harmony.Patch(
            buildVertexStructure,
            transpiler: new HarmonyMethod(
                typeof(Plugin),
                nameof(BuildVertexStructureTranspiler)
            ),
            postfix: new HarmonyMethod(
                typeof(Plugin),
                nameof(BuildVertexStructurePostfix)));

        // Patches ImpTriCmds to add the neccessary F3D commands.

        MethodInfo impTriCmds =
            writerType.GetMethod(
                "ImpTriCmds",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        harmony.Patch(
            impTriCmds,
            transpiler: new HarmonyMethod(
                typeof(Plugin),
                nameof(ImpTriCmdsTranspiler))
        );
    }

    // Captures the vertex color of crystal objects

    private static IEnumerable<CodeInstruction>
        BuildVertexStructureTranspiler(IEnumerable<CodeInstruction> instructions) {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

        // Retreive the material.
        FieldInfo matField = null;

        foreach (CodeInstruction code in codes) {
            if (code.opcode == OpCodes.Ldfld &&
                code.operand is FieldInfo field &&
                field.Name == "mat")
            {
                matField = field;
                break;
            }
        }

        if (matField == null) {
            throw new Exception(
                "Could not find the 'mat' field in buildVertexStructure.");
        }

        // Finds the vertex alpha (needed to build a proper combiner).
        MethodInfo getAlpha = null;

        foreach (CodeInstruction code in codes) {
            if (code.opcode != OpCodes.Callvirt) continue;

            MethodInfo method = code.operand as MethodInfo;

            if (method == null) continue;

            if (method.Name != "get_A") continue;

            if (method.DeclaringType != null &&
                method.DeclaringType.Name == "VertexColor")
            {
                getAlpha = method;
                break;
            }
        }

        MethodInfo transparencyHelper =
            typeof(Plugin).GetMethod(
                nameof(SetVertexColorTransparency),
                BindingFlags.Static |
                BindingFlags.NonPublic);

        for (int i = 0; i < codes.Count - 1; i++) {
            if (codes[i].opcode != OpCodes.Ldarg_2)
                continue;

            if (codes[i + 1].opcode != OpCodes.Brfalse_S &&
                codes[i + 1].opcode != OpCodes.Brfalse)
            {
                continue;
            }

            codes.InsertRange(
                i + 2,
                new[]
                {
                    // Load the display-class argument.
                    new CodeInstruction(
                        OpCodes.Ldarg_S,
                        (byte)5),

                    // Load displayClass.mat.
                    new CodeInstruction(
                        OpCodes.Ldfld,
                        matField),

                    // Load vertcol.
                    new CodeInstruction(
                        OpCodes.Ldarg_2),

                    // Load vertcol.A.
                    new CodeInstruction(
                        OpCodes.Callvirt,
                        getAlpha),

                    // Call the helper.
                    new CodeInstruction(
                        OpCodes.Call,
                        transparencyHelper)
                });

            break;
        }

        return codes;
    }
    private static void BuildVertexStructurePostfix(object[] __args) {
        object final = __args[0];
        object vertcol = __args[2];

        if (final == null || vertcol == null) return;

        VertexColor color = new VertexColor {
            R = GetByteProperty(vertcol, "R"),
            G = GetByteProperty(vertcol, "G"),
            B = GetByteProperty(vertcol, "B"),
            A = GetByteProperty(vertcol, "A")
        };

        crystalVertexColors[final] = color;
    }

    // ================================================================
    // 2. Tell ImpF3D which FvGroup is currently being emitted
    // ================================================================

    private static IEnumerable<CodeInstruction>
        ImpTriCmdsTranspiler(IEnumerable<CodeInstruction> instructions) {
        List<CodeInstruction> codes =
            new List<CodeInstruction>(instructions);

        MethodInfo helper =
            typeof(Plugin).GetMethod(
                nameof(AddCrystalVertexColors),
                BindingFlags.Static |
                BindingFlags.NonPublic);

        bool found = false;

        for (int i = 0; i < codes.Count; i++) {
            // Look for the string literal used by the G_VTX command.
            if (codes[i].opcode != OpCodes.Ldstr)
                continue;

            string text = codes[i].operand as string;

            if (text == null ||
                !text.StartsWith("04 ", StringComparison.Ordinal))
                continue;

            // Find the ImpF3D(string) call associated with that string.
            for (int j = i + 1; j < codes.Count; j++) {
                if (codes[j].opcode != OpCodes.Call &&
                    codes[j].opcode != OpCodes.Callvirt)
                    continue;

                MethodInfo method = codes[j].operand as MethodInfo;

                if (method == null || method.Name != "ImpF3D") continue;

                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters.Length != 1 ||
                    parameters[0].ParameterType != typeof(string))
                    continue;

                // A vertex was found, now we insert the necessary commands.

                codes.InsertRange(
                    j + 1,
                    new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Call, helper)
                    });

                found = true;
                break;
            }

            if (found)
                break;
        }

        return codes;
    }

    // Helper functions

    private static byte GetByteProperty(object obj, string name) {
        PropertyInfo property =
            obj.GetType().GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        return (byte)property.GetValue(obj, null);
    }
    private static void AddCrystalVertexColors(object writer, object mat, object grp) {
        if (mat == null || grp == null) return;

        PropertyInfo crystalProperty =
            mat.GetType().GetProperty(
                "EnableCrystalEffect",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (crystalProperty == null) return;

        bool crystal = (bool)crystalProperty.GetValue(mat, null);

        if (!crystal) return;

        PropertyInfo vertexDataProperty =
            grp.GetType().GetProperty(
                "FinalVertexData",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (vertexDataProperty == null) return;

        IEnumerable vertices =
            vertexDataProperty.GetValue(grp, null)
            as IEnumerable;

        if (vertices == null) return;

        MethodInfo impF3D =
            writer.GetType().GetMethod(
                "ImpF3D",
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public,
                null,
                new Type[] { typeof(string) },
                null);

        int vertexIndex = 0;

        foreach (object vertex in vertices) {
            VertexColor color;

            if (crystalVertexColors.TryGetValue(
                vertex,
                out color))
            {
                // Tell the importer that this material has transparency and needs the appropriate combiner.
                if (color.A < 0xFF)
                {
                    SetMaterialTransparency(mat);
                }

                int offset =
                    vertexIndex * 40 + 0x10;

                // Inserts a G_MOVEWORD command after loading the vertex to set the vertex color.
                string command =
                    string.Format(
                        "BC {0:X2} {1:X2} 0C {2:X2} {3:X2} {4:X2} {5:X2}",
                        (offset >> 8) & 0xFF,
                        offset & 0xFF,
                        color.R,
                        color.G,
                        color.B,
                        color.A);

                impF3D.Invoke(writer, new object[] { command });
            }

            vertexIndex++;
        }
    }
    private static void SetMaterialTransparency(object mat) {
        Type matType = mat.GetType();

        PropertyInfo typeProperty =
            matType.GetProperty(
                "Type",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        PropertyInfo transparencyProperty =
            matType.GetProperty(
                "HasTransparency",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (typeProperty != null) {
            typeProperty.SetValue(
                mat,
                Enum.ToObject(
                    typeProperty.PropertyType,
                    3),
                null);
        }

        if (transparencyProperty != null) {
            transparencyProperty.SetValue(mat, true, null);
        }
    }
    private static void SetVertexColorTransparency(object mat, int alpha) {
        if (mat == null || alpha >= 0xFF) return;

        Type matType = mat.GetType();

        PropertyInfo typeProperty =
            matType.GetProperty(
                "Type",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        PropertyInfo transparencyProperty =
            matType.GetProperty(
                "HasTransparency",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (typeProperty != null) {
            // MaterialType.TextureTransparent = 3
            object transparentType =
                Enum.ToObject(
                    typeProperty.PropertyType,
                    3);

            typeProperty.SetValue(
                mat,
                transparentType,
                null);
        }

        if (transparencyProperty != null) {
            transparencyProperty.SetValue(
                mat,
                true,
                null);
        }
    }
}