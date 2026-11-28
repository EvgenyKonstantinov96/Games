using UnityEngine;

namespace ZombieSurvivalRun
{
    sealed class ZSRSupportDrone : MonoBehaviour
    {
        const float SwivelLimit = 65f;
        const float TrackingDegreesPerSecond = 220f;
        readonly Transform[] rotors = new Transform[4];
        Transform gimbal, muzzle;
        GameObject flash;
        float age, flashTime;
        Vector3 velocity;
        Quaternion aimRotation = Quaternion.identity;

        public Vector3 Muzzle => muzzle.position;

        public static ZSRSupportDrone Launch(Transform parent, Vector3 heroPosition)
        {
            var root = new GameObject("Air support quadcopter");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(heroPosition.x, 1.2f, -.45f);
            var drone = root.AddComponent<ZSRSupportDrone>();
            drone.Construct();
            return drone;
        }

        void Construct()
        {
            ZSRVisuals.MeshPart("combat_drone", "Composite flight shell", transform,
                Vector3.zero, Vector3.one, ZSRCombatArt.Surface);
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var pivot = ZSRVisuals.Pivot("Rotor mount", transform, new Vector3(.45f * x, .108f, .38f * z));
                ZSRVisuals.MeshPart("combat_rotor", "Two-blade rotor", pivot, Vector3.zero, Vector3.one, ZSRCombatArt.Surface);
                rotors[index++] = pivot;
            }
            ZSRVisuals.Box("Rear status strip", transform, new Vector3(0, .017f, -.259f),
                new Vector3(.19f, .032f, .021f), ZSRVisuals.BlueGlow);
            for (int side = -1; side <= 1; side += 2)
                ZSRVisuals.Sphere("Flight position lamp", transform, new Vector3(side * .45f, .07f, -.438f),
                    new Vector3(.058f, .038f, .026f), side < 0 ? ZSRVisuals.BlueGlow : ZSRVisuals.OrangeGlow);
            ZSRVisuals.Sphere("Camera lens", transform, new Vector3(0, -.06f, .303f),
                new Vector3(.092f, .074f, .026f), ZSRVisuals.BlueGlow);
            gimbal = ZSRVisuals.Pivot("Stabilized turret", transform, new Vector3(0, -.128f, .23f));
            ZSRVisuals.Box("Gun housing", gimbal, new Vector3(0, 0, .115f),
                new Vector3(.121f, .057f, .31f), ZSRVisuals.Gunmetal);
            muzzle = ZSRVisuals.Pivot("Drone muzzle", gimbal, new Vector3(0, 0, .28f));
            flash = ZSRVisuals.Sphere("Drone muzzle flash", muzzle, Vector3.zero,
                new Vector3(.078f, .071f, .15f), ZSRVisuals.BlueGlow);
            flash.SetActive(false);
        }

        public void Tick(float heroX, Vector3 aim, float dt)
        {
            age += dt;
            float lane = heroX * .80f - .65f;
            var desired = new Vector3(lane, 2.31f + Mathf.Sin(age * 3.3f) * .14f, 1.48f + Mathf.Sin(age * 2.1f) * .22f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, .24f, 9f, dt);
            transform.rotation = Quaternion.Euler(4f, Mathf.Clamp(velocity.x * 2f, -12f, 12f),
                Mathf.Clamp(-velocity.x * 3.8f, -14f, 14f));
            for (int i = 0; i < rotors.Length; i++)
                rotors[i].Rotate(0f, (i == 0 || i == 3 ? 1f : -1f) * 2700f * dt, 0f, Space.Self);
            Vector3 direction = aim - gimbal.position;
            if (direction.sqrMagnitude > .001f)
            {
                var desiredAim = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), SwivelLimit);
                aimRotation = Quaternion.RotateTowards(aimRotation, desiredAim, TrackingDegreesPerSecond * dt);
                gimbal.rotation = aimRotation;
            }
            if (flashTime > 0f)
            {
                flashTime -= dt;
                if (flashTime <= 0f) flash.SetActive(false);
            }
        }

        public bool CanReach(Vector3 point) => Vector3.Angle(transform.forward, point - gimbal.position) <= SwivelLimit;

        public bool IsOnTarget(Vector3 point) => Vector3.Angle(gimbal.forward, point - Muzzle) <= 4f;

        public void Fire()
        {
            flashTime = .06f;
            flash.SetActive(true);
        }
    }
}
