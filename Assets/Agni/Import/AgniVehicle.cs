// Agni Royale — IMPORT v1: drivable vehicles (the 7 Blender vehicles). SERVER-AUTHORITATIVE like the fighters:
// the server runs the Rigidbody + WheelColliders (or the boat's buoyancy) from the driver's input; NetworkTransform carries the pose,
// clients keep the body kinematic and only animate wheels/steering/motor. Physics numbers are the vehicle team's spec (README_VEHICLES.md):
// real masses, low centre of mass, damped suspension, surface grip, anti-roll bars, downforce, torque curve, handbrake drift, light air control.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public enum VKind : byte { Jeep = 0, Bike = 1, Buggy = 2, Pickup = 3, Rickshaw = 4, Monster = 5, Boat = 6 }

    public class VSpec
    {
        public string asset, display; public float mass, comDrop, wheelMass, susp, spring, damper, target, forceApp, torque, brakeF, brakeR, handbrake, steerLo, steerHi, topKmh, antiRoll, drag, angDrag, downforce, gripMul = 1f;
        public bool fourWD; public int seats;
        public static readonly VSpec[] All =
        {
            new VSpec { asset = "ShereJeep", display = "Shera Jeep", mass = 1500, comDrop = 0.12f, fourWD = true, wheelMass = 25, susp = 0.25f, spring = 35000, damper = 3500, target = 0.5f, forceApp = 0.15f, torque = 550, brakeF = 2600, brakeR = 2600, handbrake = 4000, steerLo = 34, steerHi = 8, topKmh = 110, antiRoll = 5500, drag = 0.05f, angDrag = 0.6f, downforce = 0.8f, seats = 4 },
            new VSpec { asset = "PhatphatiBike", display = "Phatphati", mass = 265, comDrop = 0.05f, wheelMass = 12, susp = 0.12f, spring = 22000, damper = 2000, target = 0.4f, forceApp = 0f, torque = 260, brakeF = 900, brakeR = 500, handbrake = 0, steerLo = 30, steerHi = 6, topKmh = 130, antiRoll = 0, drag = 0.04f, angDrag = 1.5f, downforce = 0.3f, seats = 2 },
            new VSpec { asset = "ToofanBuggy", display = "Toofan Buggy", mass = 700, comDrop = 0.15f, wheelMass = 18, susp = 0.30f, spring = 26000, damper = 3000, target = 0.5f, forceApp = 0.1f, torque = 650, brakeF = 2000, brakeR = 2000, handbrake = 3500, steerLo = 36, steerHi = 10, topKmh = 120, antiRoll = 7000, drag = 0.04f, angDrag = 0.6f, downforce = 1.2f, seats = 2 },
            new VSpec { asset = "ChhotaHaathiPickup", display = "Chhota Haathi", mass = 1800, comDrop = 0.10f, wheelMass = 18, susp = 0.20f, spring = 42000, damper = 4500, target = 0.5f, forceApp = 0.15f, torque = 600, brakeF = 3000, brakeR = 3000, handbrake = 4500, steerLo = 32, steerHi = 7, topKmh = 100, antiRoll = 6500, drag = 0.07f, angDrag = 0.7f, downforce = 0.8f, seats = 4 },
            new VSpec { asset = "AutoRickshaw", display = "Auto-Rickshaw", mass = 350, comDrop = 0.08f, wheelMass = 8, susp = 0.12f, spring = 16000, damper = 1600, target = 0.45f, forceApp = 0.2f, torque = 180, brakeF = 900, brakeR = 900, handbrake = 1500, steerLo = 40, steerHi = 12, topKmh = 75, antiRoll = 3000, drag = 0.08f, angDrag = 0.8f, downforce = 0.4f, seats = 4 },
            new VSpec { asset = "DhoomMonsterTruck", display = "Dhoom Monster", mass = 3200, comDrop = 0.35f, fourWD = true, wheelMass = 90, susp = 0.55f, spring = 60000, damper = 7000, target = 0.5f, forceApp = 0.3f, torque = 1400, brakeF = 5000, brakeR = 5000, handbrake = 6000, steerLo = 30, steerHi = 8, topKmh = 90, antiRoll = 9000, drag = 0.06f, angDrag = 0.8f, downforce = 1.5f, gripMul = 1.15f, seats = 4 },
            new VSpec { asset = "NaavSpeedboat", display = "Naav", mass = 600, topKmh = 75, drag = 0.6f, angDrag = 1.5f, seats = 4 },
        };
        public static VSpec Of(VKind k) => All[(int)k];
    }

    /// colour variants (per vehicle) + the shared skin atlas set (Resources/AgniImport/vehicle_skins.json)
    public static class AgniVehicleSkins
    {
        [System.Serializable] public class Skin { public string id, name, tex, rarity, em, ec; public float sm, me, ei; }
        [System.Serializable] class File { public Skin[] skins; }
        static Skin[] skins; static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        public static readonly string[][] Variants =
        {
            new[] { "MirchiRed", "SkyBlue", "DesertSand", "OliveGreen" }, new[] { "ShahiBlack", "MaroonGold", "ForestGreen" }, new[] { "ToofanOrange", "NeonLime", "SkyBlue" },
            new[] { "HaathiWhite", "MandiYellow", "TruckBlue" }, new[] { "HaraPeela", "KaalaPeela", "ElectricBlue" }, new[] { "DhoomRed", "NightBlack", "ToxicGreen" }, new[] { "KayalWhite", "SunsetRed", "LagoonTeal" },
        };
        public const ushort SkinBase = 100;
        public static Skin[] Skins { get { if (skins == null) { var t = Resources.Load<TextAsset>("AgniImport/vehicle_skins"); skins = t != null ? JsonUtility.FromJson<File>(t.text).skins : new Skin[0]; } return skins; } }
        public static int Count(VKind k) => Variants[(int)k].Length + Skins.Length;
        /// skin code: 0..9 = colour variant, 100+i = shared skin i
        public static ushort CodeAt(VKind k, int i) { int nv = Variants[(int)k].Length; return (ushort)(i < nv ? i : SkinBase + (i - nv)); }
        public static int IndexOf(VKind k, ushort code) { int nv = Variants[(int)k].Length; return code >= SkinBase ? nv + code - SkinBase : code; }
        public static string Name(VKind k, ushort code) { if (code >= SkinBase) { int i = code - SkinBase; return i < Skins.Length ? Skins[i].name : "?"; } var v = Variants[(int)k]; return code < v.Length ? v[code] : v[0]; }
        public static string Rarity(VKind k, ushort code) => code >= SkinBase && code - SkinBase < Skins.Length ? Skins[code - SkinBase].rarity : "default";
        static Texture2D Tex(string path) { if (string.IsNullOrEmpty(path)) return null; if (!cache.TryGetValue(path, out var t)) { t = Resources.Load<Texture2D>(path); cache[path] = t; } return t; }
        public static void Apply(Material m, VKind k, ushort code)
        {
            if (m == null) return;
            if (code >= SkinBase && code - SkinBase < Skins.Length)
            {
                var s = Skins[code - SkinBase]; var t = Tex("AgniImport/VehicleSkins/" + s.tex); if (t != null) m.SetTexture("_BaseMap", t);
                m.SetFloat("_Smoothness", s.sm); m.SetFloat("_Metallic", s.me);
                var e = Tex(string.IsNullOrEmpty(s.em) ? null : "AgniImport/VehicleSkins/" + s.em);
                if (e != null && ColorUtility.TryParseHtmlString(s.ec, out var ec)) { m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", e); m.SetColor("_EmissionColor", ec * Mathf.Min(s.ei, 1.6f)); }
                else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
            }
            else
            {
                var v = Variants[(int)k]; var t = Tex("AgniImport/VehicleTex/T_" + VSpec.Of(k).asset + "_Body_" + v[Mathf.Clamp(code, 0, v.Length - 1)]);
                if (t != null) m.SetTexture("_BaseMap", t); m.SetFloat("_Smoothness", 0.6f); m.SetFloat("_Metallic", 0.1f); m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
            }
        }
        /// this phone's pick per vehicle (garage in the lobby); sent to the server when you take the driver's seat
        public static ushort Pref(VKind k) => (ushort)PlayerPrefs.GetInt("agni.vskin." + k, 0);
        public static void SetPref(VKind k, ushort code) { PlayerPrefs.SetInt("agni.vskin." + k, code); PlayerPrefs.Save(); }
    }

    public class AgniVehicle : NetworkBehaviour
    {
        public static readonly List<AgniVehicle> All = new List<AgniVehicle>();
        public const int VehicleLayer = 9, WaterLayer = 4;
        public VKind kind;
        public Transform[] seats, exits, wheelVis, buoys; public WheelCollider[] wcs; public bool[] steerW, driveW, rearW;
        public Transform steering, motorPivot, prop, com; public Renderer[] bodyRenderers; public Light[] lights;

        public NetworkVariable<ushort> Skin = new NetworkVariable<ushort>(0);
        public NetworkVariable<sbyte> SteerNet = new NetworkVariable<sbyte>(0);
        public NetworkVariable<byte> ThrottleNet = new NetworkVariable<byte>(0);
        public NetworkVariable<float> Hp = new NetworkVariable<float>(1000f);

        [System.NonSerialized] public AgniPlayer[] occupants;
        public VSpec Spec => VSpec.Of(kind);
        public bool IsBoat => kind == VKind.Boat;
        public AgniPlayer Driver => occupants != null && occupants.Length > 0 ? occupants[0] : null;
        public float SpeedKmh { get; private set; }
        Rigidbody rb; Material bodyMat; float steerNow, airT, flipT, hbBlend; Vector3 lastPos; float[] spin; Vector3[] wheelRest; Quaternion[] wheelRot; Quaternion steerRest, motorRest;

        void Awake()
        {
            rb = GetComponent<Rigidbody>(); occupants = new AgniPlayer[Mathf.Max(1, seats != null ? seats.Length : 1)];
            spin = new float[wheelVis != null ? wheelVis.Length : 0]; wheelRest = new Vector3[spin.Length]; wheelRot = new Quaternion[spin.Length];
            for (int i = 0; i < spin.Length; i++) if (wheelVis[i] != null) { wheelRest[i] = wheelVis[i].localPosition; wheelRot[i] = wheelVis[i].localRotation; }
            if (steering != null) steerRest = steering.localRotation; if (motorPivot != null) motorRest = motorPivot.localRotation;
            Physics.IgnoreLayerCollision(VehicleLayer, WaterLayer, true); // cars sink into rivers, boats float by buoyancy, nobody drives on the sea plane
        }
        public override void OnNetworkSpawn()
        {
            All.Add(this);
            var s = Spec;
            if (IsServer)
            {
                rb.isKinematic = false; rb.mass = s.mass; rb.linearDamping = s.drag; rb.angularDamping = s.angDrag; rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rb.solverIterations = 12; rb.solverVelocityIterations = 4;
                if (com != null) rb.centerOfMass = transform.InverseTransformPoint(com.position) + Vector3.down * s.comDrop;
                if (wcs != null && wcs.Length > 0) { int q = Agni.UI.AgniMahal.Quality; wcs[0].ConfigureVehicleSubsteps(5f, q <= 0 ? 8 : 12, q <= 0 ? 10 : 15); }
            }
            else
            {
                rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
                if (wcs != null) foreach (var w in wcs) if (w != null) w.enabled = false;
            }
            ApplySkin(Skin.Value); Skin.OnValueChanged += (a, b) => ApplySkin(b);
            lastPos = transform.position;
        }
        public override void OnNetworkDespawn() { All.Remove(this); }
        void ApplySkin(ushort code)
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0) return;
            if (bodyMat == null) { bodyMat = new Material(bodyRenderers[0].sharedMaterial); bodyMat.name = "Body_" + kind; foreach (var r in bodyRenderers) if (r != null) r.sharedMaterial = bodyMat; }
            AgniVehicleSkins.Apply(bodyMat, kind, code);
        }

        // ================= seats (server)
        public int FreeSeat(bool wantDriver) { if (wantDriver && occupants[0] == null) return 0; for (int i = 0; i < occupants.Length; i++) if (occupants[i] == null) return i; return -1; }
        public int SeatOf(AgniPlayer p) { for (int i = 0; i < occupants.Length; i++) if (occupants[i] == p) return i; return -1; }
        public Vector3 SeatPos(int i) => seats != null && i >= 0 && i < seats.Length && seats[i] != null ? seats[i].position : transform.position + Vector3.up;
        public static AgniVehicle Nearest(Vector3 p, float range)
        {
            AgniVehicle best = null; float bd = range;
            foreach (var v in All) { if (v == null) continue; float d = v.DistanceTo(p); if (d < bd) { bd = d; best = v; } }
            return best;
        }
        public float DistanceTo(Vector3 p)
        {
            float d = Vector3.Distance(p, transform.position + transform.up * 0.8f) - 1.2f;
            if (seats != null) foreach (var s in seats) if (s != null) d = Mathf.Min(d, Vector3.Distance(p, s.position) - 0.4f);
            return Mathf.Max(0, d);
        }

        // ================= physics (server)
        static float SurfaceGrip(WheelHit h, out float roll)
        {
            roll = 0f; var c = h.collider;
            if (c is TerrainCollider)
            {
                if (h.point.y < 0.4f || Agni.Import.AgniIndia.Layer(h.point.x, h.point.z, "sand") > 0.5f) { roll = 0.10f; return 0.55f; } // sand / beach / river bed: bogs down
                if (Agni.Import.AgniIndia.Layer(h.point.x, h.point.z, "rock") > 0.5f) { roll = 0.02f; return 0.85f; }                      // rock
                if (Agni.Import.AgniIndia.Layer(h.point.x, h.point.z, "dry") > 0.5f) { roll = 0.02f; return 0.80f; }                       // dry dirt
                roll = 0.04f; return 0.70f;                                                                                                // grass
            }
            return 1.0f; // landmark concrete, village floors, bridges
        }
        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;
            var s = Spec; var drv = Driver; float dt = Time.fixedDeltaTime;
            Vector2 mv = Vector2.zero; bool hb = true;
            if (drv != null && drv.Alive) { mv = Vector2.ClampMagnitude(drv.input.move, 1f); hb = drv.input.Has(AgniInput.Sprint); }
            var m = AgniMatch.Instance; if (m != null && m.Phase.Value == (byte)MatchPhase.Ended) { mv = Vector2.zero; hb = true; }
            float fwdSpeed = Vector3.Dot(rb.linearVelocity, transform.forward); SpeedKmh = rb.linearVelocity.magnitude * 3.6f;
            if (IsBoat) { BoatPhysics(mv, dt); return; }
            if (wcs == null || wcs.Length == 0) return;
            // steering: lerp max steer by speed, rate limited 90 deg/s
            float k = Mathf.Clamp01(SpeedKmh / s.topKmh);
            float maxSteer = Mathf.Lerp(s.steerLo, s.steerHi, k);
            steerNow = Mathf.MoveTowards(steerNow, mv.x * maxSteer, 90f * dt);
            // throttle / brake: stick forward = gas, back = brake then reverse
            float throttle = 0, brake = 0;
            if (mv.y > 0.05f) { if (fwdSpeed < -1f) brake = mv.y; else throttle = mv.y; }
            else if (mv.y < -0.05f) { if (fwdSpeed > 1f) brake = -mv.y; else throttle = mv.y * 0.5f; }
            float curve = k < 0.3f ? 1f : Mathf.Lerp(1f, 0.35f, (k - 0.3f) / 0.7f);
            if (SpeedKmh > s.topKmh && throttle * fwdSpeed > 0) throttle = 0;
            bool drowned = com != null && com.position.y < Agni.Import.AgniIsland.SeaY - 0.4f; if (drowned) throttle = 0;
            int driven = 0; for (int i = 0; i < wcs.Length; i++) if (driveW[i]) driven++;
            hbBlend = Mathf.MoveTowards(hbBlend, hb ? 1f : 0f, dt / 0.4f);
            int grounded = 0;
            for (int i = 0; i < wcs.Length; i++)
            {
                var w = wcs[i]; if (w == null) continue;
                if (steerW[i]) w.steerAngle = steerNow;
                w.motorTorque = driveW[i] ? throttle * s.torque * curve : 0f;
                float bt = brake * (rearW[i] ? s.brakeR : s.brakeF);
                if (rearW[i] && hbBlend > 0.01f) bt = Mathf.Max(bt, s.handbrake * hbBlend);
                if (drv == null) bt = Mathf.Max(bt, s.brakeF * 0.6f); // parked
                w.brakeTorque = bt;
                float roll = 0; float grip = 1f;
                if (w.GetGroundHit(out var hit)) { grounded++; grip = SurfaceGrip(hit, out roll); rb.AddForceAtPosition(-rb.linearVelocity * roll * rb.mass * 0.25f / wcs.Length, hit.point); }
                grip *= s.gripMul; float side = grip * (grip < 1f ? 0.89f : 1f);
                if (kind == VKind.Bike && grip < 1f) side *= 0.9f;
                if (rearW[i]) { side *= Mathf.Lerp(1f, 0.45f, hbBlend); if (kind == VKind.Buggy && throttle > 0.8f && SpeedKmh > 40f) side *= 0.8f; }
                var ff = w.forwardFriction; ff.stiffness = grip * (rearW[i] ? Mathf.Lerp(1f, 0.8f, hbBlend) : 1f); w.forwardFriction = ff;
                var sf = w.sidewaysFriction; sf.stiffness = side; w.sidewaysFriction = sf;
            }
            // anti-roll bars (pairs: 0/1 front, 2/3 rear) — the vehicle team's snippet
            if (s.antiRoll > 0 && wcs.Length >= 4) { AntiRoll(wcs[0], wcs[1], s.antiRoll); AntiRoll(wcs[2], wcs[3], s.antiRoll); }
            else if (s.antiRoll > 0 && wcs.Length == 3) AntiRoll(wcs[1], wcs[2], s.antiRoll);
            // downforce
            float v = rb.linearVelocity.magnitude; rb.AddForce(-transform.up * s.downforce * v * v);
            // air control
            if (grounded == 0) { airT += dt; if (airT > 0.2f) { float tq = (kind == VKind.Bike ? 0.2f : 0.35f) * rb.mass; rb.AddRelativeTorque(new Vector3(mv.y * tq, 0, -mv.x * tq)); rb.angularDamping = 1.5f; } }
            else { airT = 0; rb.angularDamping = s.angDrag; }
            // bike: balance controller (PD on lean), wheelie on launch
            if (kind == VKind.Bike)
            {
                float lean = Vector3.SignedAngle(Vector3.ProjectOnPlane(Vector3.up, transform.forward), transform.up, transform.forward);
                float target = -mv.x * Mathf.Lerp(0, 35, SpeedKmh / 60f); if (SpeedKmh < 3f) target = 0;
                float rollRate = Vector3.Dot(rb.angularVelocity, transform.forward) * Mathf.Rad2Deg;
                float kp = 900f * rb.mass / 265f, kd = 120f * rb.mass / 265f;
                rb.AddTorque(transform.forward * (kp * (target - lean) - kd * rollRate) * Mathf.Deg2Rad * 10f);
                if (throttle > 0.9f && SpeedKmh < 12f) { float pitch = Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), transform.forward, transform.right); if (pitch > -15f) rb.AddRelativeTorque(Vector3.right * -rb.mass * 0.6f); }
            }
            // roll-over: upside down and stopped for 3 s -> flip back
            if (Vector3.Dot(transform.up, Vector3.up) < 0.2f && SpeedKmh < 2f) { flipT += dt; if (flipT > 3f) { flipT = 0; transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0); transform.position += Vector3.up * 1.2f; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } }
            else flipT = 0;
            var sn = (sbyte)Mathf.Clamp(Mathf.RoundToInt(steerNow), -60, 60); if (SteerNet.Value != sn) SteerNet.Value = sn;
            var tn = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(throttle) * 10), 0, 10); if (ThrottleNet.Value != tn) ThrottleNet.Value = tn;
        }
        void AntiRoll(WheelCollider l, WheelCollider r, float anti)
        {
            float tl = 1f, tr = 1f;
            bool gl = l.GetGroundHit(out var hl), gr = r.GetGroundHit(out var hr);
            if (gl) tl = (-l.transform.InverseTransformPoint(hl.point).y - l.radius) / l.suspensionDistance;
            if (gr) tr = (-r.transform.InverseTransformPoint(hr.point).y - r.radius) / r.suspensionDistance;
            float f = (tl - tr) * anti;
            if (gl) rb.AddForceAtPosition(l.transform.up * -f, l.transform.position);
            if (gr) rb.AddForceAtPosition(r.transform.up * f, r.transform.position);
        }
        void BoatPhysics(Vector2 mv, float dt)
        {
            float k = 6500f, c = 600f; int wet = 0;
            if (buoys != null) foreach (var b in buoys)
                {
                    if (b == null) continue; var p = b.position; float depth = Agni.Import.AgniIsland.SeaY - p.y;
                    if (depth > 0) { wet++; var pv = rb.GetPointVelocity(p); rb.AddForceAtPosition(Vector3.up * (k * Mathf.Min(depth, 0.6f) - c * pv.y), p); }
                }
            // beached? (hull over ground above water)
            bool beached = Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var gh, 1.6f, ~(1 << VehicleLayer | 1 << WaterLayer), QueryTriggerInteraction.Ignore) && gh.point.y > Agni.Import.AgniIsland.SeaY - 0.15f;
            // water drag: sideways 4x forward
            if (wet > 0)
            {
                var lv = transform.InverseTransformDirection(rb.linearVelocity);
                rb.AddForce(transform.TransformDirection(new Vector3(-lv.x * 4f, 0, -lv.z) * rb.mass * 0.35f * wet / 5f));
                steerNow = Mathf.MoveTowards(steerNow, -mv.x * 30f, 90f * dt);
                float thrust = (mv.y >= 0 ? mv.y : mv.y * 0.4f) * 4200f * (beached ? 0.1f : 1f);
                var dir = Quaternion.AngleAxis(steerNow, transform.up) * transform.forward; dir.y = 0;
                if (prop != null && prop.position.y < Agni.Import.AgniIsland.SeaY + 0.35f) rb.AddForceAtPosition(dir.normalized * thrust, prop.position);
                if (SpeedKmh > Spec.topKmh) rb.AddForce(-rb.linearVelocity.normalized * 3000f);
                // keep it from capsizing
                var up = Vector3.Cross(transform.up, Vector3.up); rb.AddTorque(up * rb.mass * 12f);
            }
            if (beached) { rb.linearDamping = 3f; } else rb.linearDamping = Spec.drag;
            var sn = (sbyte)Mathf.Clamp(Mathf.RoundToInt(steerNow), -60, 60); if (SteerNet.Value != sn) SteerNet.Value = sn;
            var tn = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(mv.y) * 10), 0, 10); if (ThrottleNet.Value != tn) ThrottleNet.Value = tn;
        }

        // ================= visuals (every peer)
        void LateUpdate()
        {
            if (!IsSpawned) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var vel = (transform.position - lastPos) / dt; lastPos = transform.position;
            float fwd = Vector3.Dot(vel, transform.forward); if (!IsServer) SpeedKmh = vel.magnitude * 3.6f;
            float steer = IsServer ? steerNow : SteerNet.Value;
            if (steering != null) steering.localRotation = steerRest * Quaternion.AngleAxis(kind == VKind.Bike || kind == VKind.Rickshaw ? steer : -steer * 4f, kind == VKind.Bike || kind == VKind.Rickshaw ? Vector3.up : Vector3.forward);
            if (motorPivot != null) motorPivot.localRotation = motorRest * Quaternion.AngleAxis(-steer, Vector3.up);
            if (wheelVis == null) return;
            for (int i = 0; i < wheelVis.Length; i++)
            {
                var t = wheelVis[i]; if (t == null) continue;
                var w = wcs != null && i < wcs.Length ? wcs[i] : null;
                if (IsServer && w != null && w.enabled) { w.GetWorldPose(out var p, out var q); t.SetPositionAndRotation(p, q); continue; }
                // client: spin from speed, steer from the synced angle, drop to the ground under the hub
                float r = w != null ? w.radius : 0.35f; spin[i] += fwd / r * Mathf.Rad2Deg * dt;
                bool st = steerW != null && i < steerW.Length && steerW[i];
                var basePos = transform.TransformPoint(wheelRest[i]); float susp = w != null ? w.suspensionDistance : 0.2f;
                float drop = 0f; if (Physics.Raycast(basePos + transform.up * 0.3f, -transform.up, out var h, 0.3f + r + susp, ~(1 << VehicleLayer | 1 << WaterLayer), QueryTriggerInteraction.Ignore)) drop = Mathf.Clamp(h.distance - 0.3f - r, -0.05f, susp); else drop = susp * 0.5f;
                t.position = basePos - transform.up * drop;
                t.rotation = transform.rotation * Quaternion.AngleAxis(st ? steer : 0, Vector3.up) * Quaternion.AngleAxis(spin[i], Vector3.right) * wheelRot[i];
            }
        }
    }

    /// server: places the vehicles at match start (near villages and landmarks; boats on Kayal canal and the river)
    public static class AgniVehicles
    {
        public static void ServerSpawnAll(int seed = 0)
        {
            var rng = new System.Random(seed == 0 ? System.Environment.TickCount : seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            VKind[] pool = { VKind.Jeep, VKind.Jeep, VKind.Bike, VKind.Bike, VKind.Buggy, VKind.Pickup, VKind.Rickshaw, VKind.Rickshaw };
            int n = 0;
            // two per village (one on each side), one per landmark, the monster truck in the mill yard
            foreach (var v in AgniVillages.Villages)
                for (int k = 0; k < 2; k++)
                {
                    float a = R(0, Mathf.PI * 2); var c = new Vector3(v.c.x, 0, v.c.y); float rr = v.r + R(6, 14);
                    if (TrySpawn(pool[rng.Next(pool.Length)], c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rr, R(0, 360), rng)) n++;
                }
            foreach (var s in Agni.Import.AgniLandmarks.Table)
            {
                if (s.name == "KayalVillage" || s.name == "VindhyaShikhar") continue;
                var outward = new Vector3(s.pos.x, 0, s.pos.z).normalized; var c = new Vector3(s.pos.x, 0, s.pos.z) - outward * (s.footR + 8f);
                if (TrySpawn(s.name == "BambaiMill" ? VKind.Monster : pool[rng.Next(pool.Length)], c, R(0, 360), rng)) n++;
            }
            // Vindhya: a jeep at the hill foot facing up the slope
            { var vs = Agni.Import.AgniLandmarks.Table.Find(x => x.name == "VindhyaShikhar"); if (vs != null && TrySpawn(VKind.Jeep, new Vector3(vs.pos.x, 0, vs.pos.z) + (Vector3.zero - new Vector3(vs.pos.x, 0, vs.pos.z)).normalized * (vs.footR + 4f), 0, rng)) n++; }
            // boats: Kayal canal x2, the river near Ghanta Ghar x2
            int boats = 0;
            var ky = Agni.Import.AgniLandmarks.Table.Find(t => t.name == "KayalVillage");
            if (ky != null) { var rot = Quaternion.Euler(0, ky.yaw, 0); SpawnBoat(ky.pos + rot * new Vector3(52f, 0, -1.5f), ky.yaw + 90f); SpawnBoat(ky.pos + rot * new Vector3(-58f, 0, -1.5f), ky.yaw - 90f); boats += 2; }
            var gg = Agni.Import.AgniLandmarks.Table.Find(t => t.name == "GhantaGharSquare");
            if (gg != null) { var rot = Quaternion.Euler(0, gg.yaw, 0); SpawnBoat(gg.pos + rot * new Vector3(-52f, 0, 60f), gg.yaw); SpawnBoat(gg.pos + rot * new Vector3(-52f, 0, -70f), gg.yaw + 180f); boats += 2; }
            var ml = Agni.Import.AgniLandmarks.Table.Find(t => t.name == "BambaiMill");
            if (ml != null) for (float d = 80; d < 400; d += 20) { var p = ml.pos + Vector3.left * d; if (Agni.Import.AgniIndia.H(p.x, p.z) < -1.5f) { SpawnBoat(p, 0f); boats++; break; } }
            Debug.Log("[AgniVehicles] spawned " + n + " land vehicles + " + boats + " boats");
        }
        static bool TrySpawn(VKind k, Vector3 at, float yaw, System.Random rng)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                var p = at + new Vector3((float)rng.NextDouble() * 10 - 5, 0, (float)rng.NextDouble() * 10 - 5);
                if (Agni.Import.AgniIsland.IsWaterOrBank(p.x, p.z, 3f)) continue;
                if (!Physics.Raycast(new Vector3(p.x, 260f, p.z), Vector3.down, out var h, 340f, ~(1 << AgniVehicle.VehicleLayer | 1 << AgniVehicle.WaterLayer), QueryTriggerInteraction.Ignore)) continue;
                if (h.normal.y < 0.92f || h.point.y > Agni.Import.AgniIndia.H(p.x, p.z) + 1.2f) continue; // flat open ground only (not on roofs)
                var rot = Quaternion.Euler(0, yaw, 0);
                if (Physics.CheckBox(h.point + Vector3.up * 1.3f, new Vector3(1.4f, 1.0f, 2.8f), rot, ~(1 << AgniVehicle.WaterLayer), QueryTriggerInteraction.Ignore)) continue;
                Spawn(k, h.point + Vector3.up * 0.15f, rot); return true;
            }
            return false;
        }
        static void SpawnBoat(Vector3 at, float yaw) { at.y = Agni.Import.AgniIsland.SeaY - 0.05f; Spawn(VKind.Boat, at, Quaternion.Euler(0, yaw, 0)); }
        public static AgniVehicle Spawn(VKind k, Vector3 pos, Quaternion rot, ushort skin = 0)
        {
            var pf = Resources.Load<GameObject>("AgniNet/Vehicles/" + VSpec.Of(k).asset); if (pf == null) { Debug.LogWarning("[AgniVehicles] missing prefab " + k); return null; }
            var go = Object.Instantiate(pf, pos, rot); var v = go.GetComponent<AgniVehicle>(); v.Skin.Value = skin;
            go.GetComponent<NetworkObject>().Spawn(true); return v;
        }
    }
}
