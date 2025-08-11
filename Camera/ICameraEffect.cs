using System;
namespace BIG_DADDY_MOD.Patches {
    public abstract class CameraEffect {
        public abstract string Name { get; }
        public virtual bool IsAnimated => false;
        public abstract void Apply(CameraEffectSystem.CameraEffectController controller);
        public virtual void Update(CameraEffectSystem.CameraEffectController controller, float animationTime) {}
    }
}
