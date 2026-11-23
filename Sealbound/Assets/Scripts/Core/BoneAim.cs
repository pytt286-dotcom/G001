using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>Small helpers for bending a character's bones on top of its animation.</summary>
    public static class BoneAim
    {
        /// <summary>
        /// Turns <paramref name="bone"/> so that <paramref name="end"/> lies in <paramref name="direction"/> from it.
        /// weight 0 leaves the animation alone, 1 points fully. Call in LateUpdate, after the animation has run.
        /// </summary>
        public static void Aim(Transform bone, Transform end, Vector3 direction, float weight)
        {
            if (bone == null || end == null || weight <= 0f) return;
            Vector3 current = end.position - bone.position;
            if (current.sqrMagnitude < 1e-8f || direction.sqrMagnitude < 1e-8f) return;
            Quaternion q = Quaternion.FromToRotation(current, direction);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, q, Mathf.Clamp01(weight)) * bone.rotation;
        }

        /// <summary>Points a whole arm (upper arm, then forearm) along a direction.</summary>
        public static void AimArm(Transform upper, Transform lower, Transform end, Vector3 direction, float weight)
        {
            Aim(upper, end, direction, weight);
            Aim(lower, end, direction, weight);
        }

        /// <summary>Bounds of a model's parts in its own (unscaled) space.</summary>
        public static Bounds LocalMeshBounds(Transform root)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }
    }
}
