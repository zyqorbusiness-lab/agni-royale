// AgniFace.cs — Agni Royale anime face controller (Unity 6, URP). Our own code.
// Drives the face blendshapes of a hero's LOD0 SkinnedMeshRenderer with layers:
//   base idle (auto-blink, eye saccades, micro smile)  <  emote face track  <  expression preset  <  one-shot reactions
//   + lip-sync from an AudioSource's amplitude, + look-at (Eye_Look_* blendshapes, optional Eye_L/Eye_R bones).
// Face tracks are the JSON files in characters_face/_anim (Face_<Emote>.json, 30 fps), hero-independent:
// channels bind to blendshapes by NAME, so the same tracks drive all 10 heroes.
// Graphics tiers: LOW = blink only (cheapest), NORMAL/HIGH/VERY HIGH = everything.
using System.Collections.Generic;
using UnityEngine;

public class AgniFace : MonoBehaviour
{
    public enum Tier { Low, Normal, High, VeryHigh }

    [Header("Face mesh (LOD0) and optional eye bones")]
    public SkinnedMeshRenderer face;
    public Transform eyeL, eyeR;            // Eye_L / Eye_R (children of mixamorig:Head)
    public Transform head;                  // mixamorig:Head (for look-at space)
    [Header("Quality")]
    public Tier tier = Tier.High;
    [Header("Tracks (TextAssets from characters_face/_anim)")]
    public TextAsset idleTrack;             // Face_Idle_Blink.json
    public TextAsset talkTrack;             // Face_Talk.json (used when no audio is given)
    public List<TextAsset> emoteTracks = new List<TextAsset>();   // Face_<Emote>.json
    public List<TextAsset> reactionTracks = new List<TextAsset>(); // Face_React_Hit/Kill/Win.json
    [Header("Look-at")]
    public Transform lookTarget;
    [Range(0, 1)] public float lookWeight = 1f;
    public float maxEyeBoneAngle = 8f;      // keep small: the eye is a recessed plane behind the lids
    public bool useEyeBones = false;        // blendshape look-at is the default (works on every tier)
    [Header("Lip-sync")]
    public AudioSource voice;
    public float lipGain = 18f;
    [Header("Smoothing")]
    public float damping = 0.06f;           // seconds; blinks are never damped

    // ---------------------------------------------------------------- data
    class Track
    {
        public string name; public int fps = 30; public int frames; public bool loop;
        public Dictionary<string, float[]> ch = new Dictionary<string, float[]>();
        public float Duration => frames / (float)fps;
        public float Sample(string c, float t)
        {
            if (!ch.TryGetValue(c, out var v)) return 0f;
            float f = t * fps;
            if (loop) { f %= frames; if (f < 0) f += frames; }
            else f = Mathf.Clamp(f, 0, frames - 1);
            int i = (int)f; float a = f - i; int j = loop ? (i + 1) % frames : Mathf.Min(i + 1, frames - 1);
            return Mathf.Lerp(v[i], v[j], a);
        }
    }

    public static readonly Dictionary<string, Dictionary<string, float>> Presets = new Dictionary<string, Dictionary<string, float>>
    {
        {"Neutral", new Dictionary<string,float>{}},
        {"Soft_Smile", new Dictionary<string,float>{{"Smile",0.5f}, {"Brow_Up",0.08f}}},
        {"Serene", new Dictionary<string,float>{{"Smile",0.4f}, {"Eyes_Shy",0.28f}, {"Brow_Sad",0.12f}}},
        {"Happy", new Dictionary<string,float>{{"Eyes_Happy",1f}, {"Smile",1f}, {"Brow_Up",0.3f}}},
        {"Laugh", new Dictionary<string,float>{{"Eyes_Happy",1f}, {"Smile_Big",1f}, {"A",0.35f}, {"Brow_Up",0.4f}, {"Cheek_Blush",1.0f}}},
        {"Big_Smile", new Dictionary<string,float>{{"Smile_Big",0.85f}, {"Eyes_Happy",0.35f}, {"Brow_Up",0.3f}, {"Cheek_Blush",0.3f}}},
        {"Cheer", new Dictionary<string,float>{{"Smile_Big",0.7f}, {"A",0.6f}, {"Eyes_Happy",0.6f}, {"Brow_Up",0.7f}}},
        {"Wink_Smile", new Dictionary<string,float>{{"Blink_L",1f}, {"Smile",1f}, {"Brow_Up_L",0.4f}}},
        {"Kiss", new Dictionary<string,float>{{"Kiss_Pout",1f}, {"Blink_R",1f}, {"Brow_Up",0.25f}, {"Cheek_Blush",0.9f}}},
        {"Wink", new Dictionary<string,float>{{"Blink_L",1f}, {"Smile_Smirk_L",0.6f}, {"Brow_Up_L",0.3f}}},
        {"A_viseme", new Dictionary<string,float>{{"A",1f}}},
        {"Pout", new Dictionary<string,float>{{"Kiss_Pout",0.8f}, {"Brow_Up",0.2f}, {"Eyes_Shy",0.2f}}},
        {"Shy_Blush", new Dictionary<string,float>{{"Shy_Smile",1f}, {"Eyes_Shy",1f}, {"Brow_Sad",0.45f}, {"Cheek_Blush",1f}}},
        {"Confident_Smirk", new Dictionary<string,float>{{"Smile_Smirk_L",1f}, {"Eyes_Angry",0.6f}, {"Brow_Up_L",1.0f}, {"Eye_Look_Right",0.3f}}},
        {"Cool", new Dictionary<string,float>{{"Smile_Smirk_L",0.6f}, {"Eyes_Shy",0.35f}, {"Brow_Up_L",0.3f}}},
        {"Sultry_Calm", new Dictionary<string,float>{{"Smile",0.35f}, {"Eyes_Shy",0.45f}, {"Brow_Up",0.15f}}},
        {"Proud", new Dictionary<string,float>{{"Smile",0.8f}, {"Brow_Up",0.35f}, {"Eyes_Happy",0.25f}}},
        {"Focus", new Dictionary<string,float>{{"Brow_Angry",0.35f}, {"Eyes_Angry",0.25f}, {"Smile",0.15f}}},
        {"Effort", new Dictionary<string,float>{{"Angry_Teeth",0.6f}, {"Eyes_Angry",0.6f}, {"Brow_Angry",0.7f}}},
        {"Angry", new Dictionary<string,float>{{"Eyes_Angry",1f}, {"Brow_Angry",1f}, {"Angry_Teeth",1f}}},
        {"Sad", new Dictionary<string,float>{{"Eyes_Sad",1f}, {"Brow_Sad",1f}, {"Frown",1f}, {"Eye_Look_Down",0.3f}}},
        {"Cry", new Dictionary<string,float>{{"Eyes_Sad",0.8f}, {"Brow_Sad",1f}, {"Frown",0.8f}, {"Tear",1f}, {"Eyes_Shy",0.3f}}},
        {"Surprised", new Dictionary<string,float>{{"Eyes_Wide",1f}, {"Brow_Up",1f}, {"Mouth_O",1f}}},
        {"Ooh", new Dictionary<string,float>{{"Mouth_O",0.6f}, {"Brow_Up",0.5f}, {"Eyes_Wide",0.3f}}},
        {"Playful_Tongue", new Dictionary<string,float>{{"Tongue_Out",1f}, {"Blink_L",1f}, {"Brow_Up_L",0.4f}, {"Smile",0.3f}}},
        {"Robot", new Dictionary<string,float>{{"Eyes_Wide",0.25f}}},
        {"Eyes_Closed_Smile", new Dictionary<string,float>{{"Eyes_Happy",0.15f}, {"Blink",0.9f}, {"Smile",0.55f}}},
    };

    static readonly string[] Channels = {
        "Blink","Blink_L","Blink_R","Eyes_Happy","Eyes_Wide","Eyes_Sad","Eyes_Angry","Eyes_Shy","Eye_Look_Up","Eye_Look_Down","Eye_Look_Left","Eye_Look_Right",
        "Brow_Up","Brow_Angry","Brow_Sad","Brow_Up_L","Smile","Smile_Big","Smile_Smirk_L","Smile_Smirk_R","Frown","Mouth_O","Kiss_Pout","Angry_Teeth","Shy_Smile","Tongue_Out",
        "A","I","U","E","O","Cheek_Blush","Tear" };

    int[] idx; float[] cur, vel, target;
    Dictionary<string, int> chIndex = new Dictionary<string, int>();
    Track idle, talk, emote, emotePrev, reaction;
    Dictionary<string, Track> tracks = new Dictionary<string, Track>();
    float emoteT, emoteW, emoteFade = 0.25f, emoteTarget; bool emoteOn;
    Dictionary<string, float> preset, presetPrev; float presetW, presetTarget, presetFade = 0.3f, presetHold = -1f, presetT;
    float reactT; float talkW, talkTarget, lipLevel; float[] audioBuf = new float[256];
    float nextBlink, blinkT = -1f; float idleT; Vector2 sacc, saccTarget; float nextSacc;
    Quaternion eyeLRest, eyeRRest;

    void Awake()
    {
        if (!face) face = GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = face ? face.sharedMesh : null;
        if (mesh == null || mesh.blendShapeCount == 0) { enabled = false; return; } // LOD1/LOD2 have no blendshapes
        idx = new int[Channels.Length]; cur = new float[Channels.Length]; vel = new float[Channels.Length]; target = new float[Channels.Length];
        for (int i = 0; i < Channels.Length; i++) { idx[i] = mesh.GetBlendShapeIndex(Channels[i]); chIndex[Channels[i]] = i; }
        idle = Load(idleTrack); talk = Load(talkTrack);
        foreach (var t in emoteTracks) { var k = Load(t); if (k != null) tracks[k.name] = k; }
        foreach (var t in reactionTracks) { var k = Load(t); if (k != null) tracks[k.name] = k; }
        if (eyeL) eyeLRest = eyeL.localRotation; if (eyeR) eyeRRest = eyeR.localRotation;
        nextBlink = Random.Range(1f, 3f);
    }

    // ---------------------------------------------------------------- public API
    /// Start the face track that matches an emote (same name as the body emote, e.g. "Flying_Kiss").
    public void PlayEmoteFace(string emoteName, float fade = 0.25f)
    {
        if (!tracks.TryGetValue(emoteName, out var t)) return;
        emotePrev = emote; emote = t; emoteT = 0; emoteFade = Mathf.Max(0.05f, fade); emoteTarget = 1; emoteOn = true;
    }
    public void StopEmoteFace(float fade = 0.3f) { emoteFade = Mathf.Max(0.05f, fade); emoteTarget = 0; }
    /// Hold an expression preset ("Happy", "Shy_Blush", "Angry"...). hold < 0 = until ClearExpression.
    public void SetExpression(string name, float fade = 0.3f, float hold = -1f)
    {
        if (!Presets.TryGetValue(name, out var p)) return;
        presetPrev = preset; preset = p; presetFade = Mathf.Max(0.05f, fade); presetTarget = 1; presetHold = hold; presetT = 0;
    }
    public void ClearExpression(float fade = 0.3f) { presetFade = Mathf.Max(0.05f, fade); presetTarget = 0; }
    /// One-shot reaction layered over everything: "React_Hit", "React_Kill", "React_Win".
    public void React(string name) { if (tracks.TryGetValue(name, out var t)) { reaction = t; reactT = 0; } }
    public void SetTalking(bool on) { talkTarget = on ? 1 : 0; }

    // ---------------------------------------------------------------- update
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        System.Array.Clear(target, 0, target.Length);
        bool full = tier != Tier.Low;

        // 1) base idle layer
        idleT += dt;
        if (full && idle != null) for (int i = 0; i < Channels.Length; i++) target[i] = idle.Sample(Channels[i], idleT);
        if (full) Saccades(dt);

        // 2) emote layer (crossfade in/out)
        if (full && emote != null)
        {
            emoteT += dt; emoteW = Mathf.MoveTowards(emoteW, emoteTarget, dt / emoteFade);
            if (!emote.loop && emoteT > emote.Duration && emoteTarget > 0) StopEmoteFace(0.35f);
            float w = Smooth(emoteW);
            for (int i = 0; i < Channels.Length; i++) target[i] = Mathf.Lerp(target[i], emote.Sample(Channels[i], emoteT), w);
            if (emoteW <= 0 && emoteTarget <= 0) { emote = null; emoteOn = false; }
        }
        // 3) expression preset layer
        if (full && preset != null)
        {
            presetT += dt; if (presetHold >= 0 && presetT > presetHold && presetTarget > 0) ClearExpression(presetFade);
            presetW = Mathf.MoveTowards(presetW, presetTarget, dt / presetFade); float w = Smooth(presetW);
            for (int i = 0; i < Channels.Length; i++)
            {
                preset.TryGetValue(Channels[i], out float pv);
                float from = target[i];
                if (presetPrev != null && presetW < 1f && presetTarget > 0) { presetPrev.TryGetValue(Channels[i], out float pp); from = Mathf.Lerp(from, pp, 1f - w); }
                target[i] = Mathf.Lerp(from, pv, w);
            }
            if (presetW <= 0 && presetTarget <= 0) { preset = null; presetPrev = null; }
        }
        // 4) one-shot reactions (strongest wins per channel)
        if (full && reaction != null)
        {
            reactT += dt; float env = Mathf.Clamp01(reactT / 0.06f) * Mathf.Clamp01((reaction.Duration - reactT) / 0.2f);
            for (int i = 0; i < Channels.Length; i++) target[i] = Mathf.Max(target[i], reaction.Sample(Channels[i], reactT) * env);
            if (reactT > reaction.Duration) reaction = null;
        }
        // 5) lip-sync (audio amplitude -> A/E/O/U) or the Talk track
        if (full) LipSync(dt);
        // 6) auto-blink (all tiers): fast close 2f, hold 1f, open 4f; skipped while a layer holds the eyes closed
        float closed = Mathf.Max(target[C("Blink")], target[C("Eyes_Happy")]);
        float blink = AutoBlink(dt, closed > 0.35f);
        target[C("Blink")] = Mathf.Max(target[C("Blink")], blink);
        // 7) look-at
        if (full) LookAt();

        // apply with damping (blinks direct)
        for (int i = 0; i < Channels.Length; i++)
        {
            if (idx[i] < 0) continue;
            bool direct = i <= 2;
            cur[i] = direct ? target[i] : Mathf.SmoothDamp(cur[i], target[i], ref vel[i], damping);
            if (!full && i != 0) cur[i] = 0;
            face.SetBlendShapeWeight(idx[i], Mathf.Clamp01(cur[i]) * 100f);
        }
    }

    int C(string n) => chIndex[n];
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

    float AutoBlink(float dt, bool suppressed)
    {
        nextBlink -= dt;
        if (blinkT < 0 && nextBlink <= 0 && !suppressed) { blinkT = 0; nextBlink = Random.Range(2.2f, 4.8f); if (Random.value < 0.18f) nextBlink = 0.34f; }
        if (blinkT < 0) return 0;
        blinkT += dt; float f = blinkT * 30f; float v = f < 2 ? f / 2f : f < 3 ? 1f : f < 7 ? 1f - (f - 3) / 4f : 0f;
        if (f >= 7) blinkT = -1f;
        return v;
    }

    void Saccades(float dt)
    {
        nextSacc -= dt;
        if (nextSacc <= 0) { saccTarget = new Vector2(Random.Range(-0.16f, 0.16f), Random.Range(-0.08f, 0.1f)); nextSacc = Random.Range(0.7f, 1.9f); }
        sacc = Vector2.MoveTowards(sacc, saccTarget, dt / 0.1f * 0.3f);
        AddLook(sacc.x, sacc.y);
    }

    void AddLook(float x, float y)
    {
        if (x > 0) target[C("Eye_Look_Left")] += x; else target[C("Eye_Look_Right")] += -x;
        if (y > 0) target[C("Eye_Look_Up")] += y; else target[C("Eye_Look_Down")] += -y;
    }

    void LookAt()
    {
        if (!lookTarget || !head || lookWeight <= 0) { if (useEyeBones) ResetEyes(); return; }
        Vector3 d = head.InverseTransformDirection((lookTarget.position - head.position).normalized);
        // mixamo head: forward is +Z in the head's local space after FBX import (check once in the editor; flip signs if your import differs)
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) * Mathf.Rad2Deg;
        if (Mathf.Abs(yaw) > 70f) return; // target behind: the head should turn instead
        float x = Mathf.Clamp(yaw / 25f, -1, 1) * lookWeight, y = Mathf.Clamp(pitch / 18f, -1, 1) * lookWeight;
        AddLook(x, y);
        if (useEyeBones && eyeL && eyeR)
        {
            var q = Quaternion.Euler(-y * maxEyeBoneAngle, 0, -x * maxEyeBoneAngle);
            eyeL.localRotation = eyeLRest * q; eyeR.localRotation = eyeRRest * q;
        }
    }
    void ResetEyes() { if (eyeL) eyeL.localRotation = eyeLRest; if (eyeR) eyeR.localRotation = eyeRRest; }

    void LipSync(float dt)
    {
        bool hasAudio = voice && voice.isPlaying;
        talkW = Mathf.MoveTowards(talkW, (hasAudio || talkTarget > 0) ? 1 : 0, dt / 0.15f);
        if (talkW <= 0) return;
        if (hasAudio)
        {
            voice.GetOutputData(audioBuf, 0); float s = 0; for (int i = 0; i < audioBuf.Length; i++) s += audioBuf[i] * audioBuf[i];
            float rms = Mathf.Sqrt(s / audioBuf.Length); float lvl = Mathf.Clamp01(rms * lipGain);
            lipLevel = Mathf.Lerp(lipLevel, lvl, 1f - Mathf.Exp(-dt * 25f));
            // amplitude -> open vowels; a slow wobble picks the vowel shape so it is not one flat "A"
            float ph = Time.time * 7.3f; float a = lipLevel;
            target[C("A")] = Mathf.Max(target[C("A")], a * (0.55f + 0.45f * Mathf.Sin(ph)) * talkW);
            target[C("O")] = Mathf.Max(target[C("O")], a * 0.5f * Mathf.Max(0, Mathf.Sin(ph * 0.61f + 1f)) * talkW);
            target[C("E")] = Mathf.Max(target[C("E")], a * 0.5f * Mathf.Max(0, Mathf.Sin(ph * 1.37f + 2f)) * talkW);
            target[C("I")] = Mathf.Max(target[C("I")], a * 0.35f * Mathf.Max(0, Mathf.Sin(ph * 0.83f + 4f)) * talkW);
        }
        else if (talk != null)
        {
            string[] v = { "A", "I", "U", "E", "O" };
            foreach (var c in v) target[C(c)] = Mathf.Lerp(target[C(c)], talk.Sample(c, Time.time), talkW);
        }
    }

    // ---------------------------------------------------------------- tiny JSON reader for the face track format
    static Track Load(TextAsset ta)
    {
        if (ta == null) return null;
        var root = new MiniJson(ta.text).Parse() as Dictionary<string, object>;
        if (root == null) return null;
        var t = new Track { name = (string)root["name"], fps = System.Convert.ToInt32(root["fps"]), frames = System.Convert.ToInt32(root["frames"]), loop = (bool)root["loop"] };
        foreach (var kv in (Dictionary<string, object>)root["channels"])
        {
            var l = (List<object>)kv.Value; var a = new float[l.Count];
            for (int i = 0; i < a.Length; i++) a[i] = System.Convert.ToSingle(l[i]);
            t.ch[kv.Key] = a;
        }
        return t;
    }

    class MiniJson
    {
        readonly string s; int i;
        public MiniJson(string s) { this.s = s; }
        public object Parse() { Ws(); return Val(); }
        void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        object Val()
        {
            Ws(); char c = s[i];
            if (c == '{') { i++; var d = new Dictionary<string, object>(); Ws(); if (s[i] == '}') { i++; return d; }
                while (true) { Ws(); var k = Str(); Ws(); i++; d[k] = Val(); Ws(); if (s[i] == ',') { i++; continue; } i++; return d; } }
            if (c == '[') { i++; var l = new List<object>(); Ws(); if (s[i] == ']') { i++; return l; }
                while (true) { l.Add(Val()); Ws(); if (s[i] == ',') { i++; continue; } i++; return l; } }
            if (c == '"') return Str();
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            int j = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(j, i - j), System.Globalization.CultureInfo.InvariantCulture);
        }
        string Str() { i++; int j = i; while (s[i] != '"') i++; var r = s.Substring(j, i - j); i++; return r; }
    }
}
