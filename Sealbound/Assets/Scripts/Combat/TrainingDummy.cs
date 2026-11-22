using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>
    /// A practice target that flashes, wobbles and shows damage, falls when beaten and stands back up.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour
    {
        public Transform wobblePivot;
        public Renderer[] flashRenderers;
        public float respawnDelay = 2f;
        public string label = "Training Dummy";
        public float labelHeight = 2.6f;

        Health _health;
        Material[] _materials;
        Color[] _baseColors;
        float _flash;
        Vector3 _tiltVelocity;   // spring state, degrees
        Vector3 _tilt;
        float _deadTimer;
        float _lastHitTime = -10f;

        void Awake()
        {
            _health = GetComponent<Health>();
            _health.Damaged += OnDamaged;
            _health.Died += () => _deadTimer = respawnDelay;
        }

        void Start()
        {
            _health.ResetHealth(); // pick up a max health set after creation
            if (flashRenderers == null) flashRenderers = GetComponentsInChildren<Renderer>();
            _materials = new Material[flashRenderers.Length];
            _baseColors = new Color[flashRenderers.Length];
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                _materials[i] = flashRenderers[i].material; // per-dummy copy
                _baseColors[i] = _materials[i].color;
            }
        }

        void OnDamaged(DamageInfo info, float dealt)
        {
            _flash = 1f;
            _lastHitTime = Time.time;
            Vector3 local = transform.InverseTransformDirection(info.Direction);
            float kick = Mathf.Lerp(250f, 700f, info.Impact) * (_health.damageMultiplier < 1f ? 0.4f : 1f);
            _tiltVelocity += new Vector3(local.z, 0f, -local.x) * kick;
            DamageNumbers.Show(info.Point + Vector3.up * 0.5f, dealt, info.Impact > 0.6f, _health.damageMultiplier < 1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (_health.IsDead)
            {
                // Topple, wait, then stand back up.
                _tilt = Vector3.Lerp(_tilt, new Vector3(80f, 0f, 0f), 1f - Mathf.Exp(-8f * dt));
                _deadTimer -= dt;
                if (_deadTimer <= 0f) _health.ResetHealth();
            }
            else
            {
                // Damped spring back to upright.
                Vector3 accel = -_tilt * 180f - _tiltVelocity * 9f;
                _tiltVelocity += accel * dt;
                _tilt += _tiltVelocity * dt;
                _tilt = Vector3.ClampMagnitude(_tilt, 35f);

                // Slowly heal if left alone, so it is always ready to practise on.
                if (Time.time - _lastHitTime > 3f && _health.Current < _health.maxHealth)
                    _health.ResetHealth();
            }

            if (wobblePivot != null) wobblePivot.localRotation = Quaternion.Euler(_tilt);

            _flash = Mathf.MoveTowards(_flash, 0f, dt * 6f);
            if (_materials != null)
                for (int i = 0; i < _materials.Length; i++)
                    _materials[i].color = Color.Lerp(_baseColors[i], Color.white, _flash);
        }

        void OnGUI()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 top = transform.position + Vector3.up * labelHeight;
            Vector3 sp = cam.WorldToScreenPoint(top);
            if (sp.z < 0f || sp.z > 30f) return;
            float w = 90f, h = 8f;
            float x = sp.x - w / 2f, y = Screen.height - sp.y;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, h + 2), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.25f, 0.2f);
            GUI.DrawTexture(new Rect(x, y, w * (_health.Current / _health.maxHealth), h), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(x - 30f, y - 22f, w + 60f, 20f), label);
        }
    }
}
