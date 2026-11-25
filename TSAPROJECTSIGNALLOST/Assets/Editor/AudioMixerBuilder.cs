using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace SignalLost.EditorTools
{
    /// <summary>
    /// Creates Assets/Audio/SL_Mixer.mixer with Master > Music / SFX groups and exposed volume parameters
    /// (MasterVolume, MusicVolume, SfxVolume). Unity has no public API for authoring mixers, so this uses
    /// the editor's own AudioMixerController through reflection. If that ever fails, AudioManager simply
    /// falls back to per-source volume, so the game still works.
    /// </summary>
    public static class AudioMixerBuilder
    {
        public const string MixerPath = "Assets/Audio/SL_Mixer.mixer";

        private static readonly string[] Exposed = { "MasterVolume", "MusicVolume", "SfxVolume" };

        public static AudioMixer Build()
        {
            PlaceholderModelBuilder.EnsureFolder("Assets/Audio");
            AudioMixer existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null && IsComplete(existing)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(MixerPath);

            try
            {
                Assembly editor = typeof(Editor).Assembly;
                Type ctrlType = editor.GetType("UnityEditor.Audio.AudioMixerController");
                Type paramType = editor.GetType("UnityEditor.Audio.ExposedAudioParameter");
                const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                MethodInfo create = ctrlType.GetMethod("CreateMixerControllerAtPath", All);
                object ctrl = create.Invoke(null, new object[] { MixerPath });
                object master = ctrlType.GetProperty("masterGroup", All).GetValue(ctrl);

                MethodInfo createGroup = null;
                foreach (MethodInfo m in ctrlType.GetMethods(All))
                {
                    if (m.Name == "CreateNewGroup" && m.GetParameters().Length >= 1) createGroup = m;
                }
                MethodInfo addChild = ctrlType.GetMethod("AddChildToParent", All);
                object music = InvokeCreateGroup(createGroup, ctrl, "Music");
                addChild.Invoke(ctrl, new[] { music, master });
                object sfx = InvokeCreateGroup(createGroup, ctrl, "SFX");
                addChild.Invoke(ctrl, new[] { sfx, master });

                MethodInfo addExposed = ctrlType.GetMethod("AddExposedParameter", All);
                Expose(addExposed, paramType, ctrl, master, "MasterVolume");
                Expose(addExposed, paramType, ctrl, music, "MusicVolume");
                Expose(addExposed, paramType, ctrl, sfx, "SfxVolume");

                MethodInfo onChanged = ctrlType.GetMethod("OnSubAssetChanged", All);
                if (onChanged != null && onChanged.GetParameters().Length == 0) onChanged.Invoke(ctrl, null);
                EditorUtility.SetDirty((UnityEngine.Object)ctrl);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(MixerPath);
                AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                Debug.Log("[Signal Lost] Audio mixer built at " + MixerPath + (IsComplete(mixer) ? "" : " (incomplete; using source volumes)"));
                return mixer;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Signal Lost] Could not author the AudioMixer (" + e.GetBaseException().Message + "). Audio will use per-source volume instead.");
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
        }

        private static object InvokeCreateGroup(MethodInfo createGroup, object ctrl, string name)
        {
            ParameterInfo[] ps = createGroup.GetParameters();
            object[] args = new object[ps.Length];
            args[0] = name;
            for (int i = 1; i < ps.Length; i++) args[i] = ps[i].ParameterType == typeof(bool) ? (object)false : null;
            return createGroup.Invoke(ctrl, args);
        }

        private static void Expose(MethodInfo addExposed, Type paramType, object ctrl, object group, string name)
        {
            const BindingFlags Inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            object guid = group.GetType().GetMethod("GetGUIDForVolume", Inst).Invoke(group, null);
            Type argType = addExposed.GetParameters()[0].ParameterType;
            if (argType.IsAssignableFrom(paramType))
            {
                // Older editors: AddExposedParameter(ExposedAudioParameter).
                object p = Activator.CreateInstance(paramType);
                paramType.GetField("guid", Inst).SetValue(p, guid);
                paramType.GetField("name", Inst).SetValue(p, name);
                addExposed.Invoke(ctrl, new[] { p });
                return;
            }

            // 2021+: AddExposedParameter(AudioParameterPath), the same call the mixer window's "Expose" menu makes.
            Type pathType = argType.Assembly.GetType("UnityEditor.Audio.AudioGroupParameterPath");
            object path = Activator.CreateInstance(pathType, Inst, null, new[] { group, guid }, null);
            addExposed.Invoke(ctrl, new[] { path });

            // Unity names new parameters "MyExposedParam N"; rename ours by GUID.
            PropertyInfo prop = ctrl.GetType().GetProperty("exposedParameters", Inst);
            Array arr = (Array)prop.GetValue(ctrl);
            for (int i = 0; i < arr.Length; i++)
            {
                object p = arr.GetValue(i);
                if (!p.GetType().GetField("guid", Inst).GetValue(p).Equals(guid)) continue;
                p.GetType().GetField("name", Inst).SetValue(p, name);
                arr.SetValue(p, i);
            }
            prop.SetValue(ctrl, arr);
        }

        private static bool IsComplete(AudioMixer m)
        {
            if (m == null) return false;
            if (m.FindMatchingGroups("Music").Length == 0 || m.FindMatchingGroups("SFX").Length == 0) return false;
            foreach (string p in Exposed)
            {
                if (!m.GetFloat(p, out _)) return false;
            }
            return true;
        }
    }
}
