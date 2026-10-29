using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuitarPlatformer.EditorTools
{
    internal sealed class BuilderAssets
    {
        public Font Font;
        public Material Board;
        public Material Fret;
        public Material Nut;
        public Material Dot;
        public Material NeonPink;
        public Material NeonCyan;
        public Material Pick;
        public Material PickEye;
        public Material PickHighlight;
        public Material GhostPick;
        public Material GhostEye;
        public Material Ring;
        public Material Spark;
        public Material Grid;
        public Material Trail;
        public Material Beacon;
        public Material[] NotePads;
        public Material[] PadRims;
        public VolumeProfile Profile;

        public static BuilderAssets Build()
        {
            var a = new BuilderAssets();
            a.Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (a.Font == null) a.Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (a.Font == null) Debug.LogError("Guitar Platformer: no built-in font found.");

            Texture2D grid = BuilderUtil.CreateGridTexture();
            Texture2D dot = BuilderUtil.CreateSoftDotTexture();

            a.Board = BuilderUtil.Lit("Board", new Color(0.115f, 0.045f, 0.085f), 0.52f);
            a.Fret = BuilderUtil.Lit("FretWire", new Color(0.18f, 0.22f, 0.3f), 0.92f, 1f,
                new Color(0.02f, 0.12f, 0.2f));
            a.Nut = BuilderUtil.Lit("Nut", new Color(0.92f, 0.86f, 0.68f), 0.38f, 0f,
                new Color(0.12f, 0.1f, 0.06f));
            a.Dot = BuilderUtil.Unlit("FretDot", new Color(2.2f, 2.3f, 2.5f, 1f));
            a.NeonPink = BuilderUtil.Unlit("NeonPink", new Color(2.5f, 0.18f, 1.5f, 1f));
            a.NeonCyan = BuilderUtil.Unlit("NeonCyan", new Color(0.1f, 1.8f, 2.5f, 1f));
            a.Pick = BuilderUtil.Lit("PickBody", new Color(0.86f, 0.95f, 1f), 0.36f, 0f,
                new Color(0.12f, 0.34f, 0.42f));
            a.PickEye = BuilderUtil.Lit("PickEye", new Color(0.008f, 0.012f, 0.03f), 0.65f);
            a.PickHighlight = BuilderUtil.Lit("PickHighlight", new Color(1f, 1f, 1f), 0.5f, 0f,
                new Color(0.4f, 0.4f, 0.4f));
            a.GhostPick = BuilderUtil.Unlit("GhostPick", new Color(0.75f, 0.92f, 1f, 0.62f),
                true, false, null, null, true);
            a.GhostEye = BuilderUtil.Unlit("GhostEye", new Color(0.015f, 0.02f, 0.045f, 0.9f),
                true, false, null, null, false);
            a.Ring = BuilderUtil.Unlit("Ripple", Color.white, true, false, null, null, true);
            a.Spark = BuilderUtil.Particle("Spark", dot, Color.white);
            a.Grid = BuilderUtil.Unlit("NeonGrid", Color.white, true, false, grid, new Vector2(14f, 14f), true);
            a.Trail = BuilderUtil.Particle("Trail", null, Color.white);
            a.Beacon = BuilderUtil.Unlit("TargetBeacon", new Color(0.2f, 1f, 0.92f, 0.6f), true, true,
                null, null, true);

            a.NotePads = new Material[12];
            a.PadRims = new Material[12];
            for (int i = 0; i < 12; i++)
            {
                Color c = GuitarTuning.PitchColors[i];
                Color baseColor = c * 0.08f;
                baseColor.a = 1f;
                Color emission = c * 0.5f;
                emission.a = 1f;
                a.NotePads[i] = BuilderUtil.Lit("Pad_" + i, baseColor, 0.55f, 0f, emission);
                a.PadRims[i] = BuilderUtil.Unlit("PadRim_" + i, c * 1.7f);
            }

            a.Profile = BuildProfile();
            AssetDatabase.SaveAssets();
            return a;
        }

        private static VolumeProfile BuildProfile()
        {
            string path = BuilderUtil.Profiles + "/NeonVolumeProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "NeonVolumeProfile";
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.name = "Bloom";
            bloom.active = true;
            bloom.intensity.Override(1.2f);
            bloom.threshold.Override(0.55f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(0.9f, 0.95f, 1f));
            AddSubAsset(profile, bloom);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.name = "Tonemapping";
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);
            AddSubAsset(profile, tonemapping);

            var vignette = profile.Add<Vignette>(true);
            vignette.name = "Vignette";
            vignette.active = true;
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);
            AddSubAsset(profile, vignette);

            var color = profile.Add<ColorAdjustments>(true);
            color.name = "ColorAdjustments";
            color.active = true;
            color.postExposure.Override(0.1f);
            color.contrast.Override(9f);
            color.saturation.Override(10f);
            color.colorFilter.Override(Color.white);
            AddSubAsset(profile, color);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void AddSubAsset(VolumeProfile profile, ScriptableObject component)
        {
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
    }
}
