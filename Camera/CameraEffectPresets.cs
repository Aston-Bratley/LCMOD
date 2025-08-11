using CameraLibrary.Patches;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace CameraLibrary.Patches
{
    public static class CameraEffectPresets
    {
        public static void RegisterDefaultEffects()
        {
            var controller = CameraEffectSystem.localPlayerEffects;
            if (controller == null) return;
            controller.RegisterEffect("ChromaticAberration", new ChromaticAberrationEffect());
            controller.RegisterEffect("FilmGrain", new FilmGrainEffect());
            controller.RegisterEffect("WavyCamera", new WavyCameraEffect());
        }
    }

    public class ChromaticAberrationEffect : CameraEffect
    {
        public override string Name => "Chromatic Aberration";
        public override void Apply(CameraEffectSystem.CameraEffectController controller)
        {
            if (controller.ChromaticAberration != null)
            {
                controller.ChromaticAberration.intensity.Override(0.8f);
                controller.ChromaticAberration.active = true;
            }
        }
    }

    public class FilmGrainEffect : CameraEffect
    {
        public override string Name => "Film Grain";
        public override void Apply(CameraEffectSystem.CameraEffectController controller)
        {
            if (controller.FilmGrain != null)
            {
                controller.FilmGrain.intensity.Override(0.6f);
                controller.FilmGrain.response.Override(0.8f);
                controller.FilmGrain.active = true;
            }
        }
    }

    public class WavyCameraEffect : CameraEffect
    {
        public override string Name => "Wavy Camera";
        public override bool IsAnimated => true;
        public override void Apply(CameraEffectSystem.CameraEffectController controller)
        {
            // Initial setup if needed
        }

        public override void Update(CameraEffectSystem.CameraEffectController controller, float animationTime)
        {
            if (controller.Player.gameplayCamera != null)
            {
                float waveX = Mathf.Sin(animationTime * 2f) * 0.02f;
                float waveY = Mathf.Cos(animationTime * 1.5f) * 0.015f;
                controller.Player.gameplayCamera.transform.localPosition = controller.OriginalCameraPosition + new Vector3(waveX, waveY, 0f);
            }
        }
    }
}
