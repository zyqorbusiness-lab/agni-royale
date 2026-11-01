using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

// Map viewer camera for the Agni Royale blockout (no gameplay).
// Phone: drag on the LEFT side = fly (like a joystick), drag on the RIGHT side = look around,
// buttons on the right: UP / DOWN (hold), NEXT ZONE, DROP VIEW.
// PC/editor: WASD fly, Q/E down/up, right mouse = look, Shift = fast.
public class AgniFlyCam : MonoBehaviour
{
    public Vector3[] zonePoints;
    public string[] zoneNames;
    public float lookSpeed = 0.12f;
    public float maxHeight = 1100f;
    float yaw, pitch;
    int zoneIdx = -1;
    int leftId = -1, rightId = -1;
    Vector2 leftStart;
    Vector3 dropPos; Quaternion dropRot;
    Rect rUp, rDown, rNext, rDrop;
    string nearName = "";
    GUIStyle btn, title;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Vector3 e = transform.eulerAngles; yaw = e.y; pitch = e.x > 180 ? e.x - 360 : e.x;
        dropPos = transform.position; dropRot = transform.rotation;
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    void Layout()
    {
        float s = Screen.height / 7f, m = s * 0.15f;
        float x = Screen.width - s * 1.6f - m;
        rUp = new Rect(x, m, s * 1.6f, s);
        rDown = new Rect(x, m * 2 + s, s * 1.6f, s);
        rNext = new Rect(x, Screen.height - (s + m) * 2, s * 1.6f, s);
        rDrop = new Rect(x, Screen.height - (s + m), s * 1.6f, s);
    }
    static Vector2 GuiPos(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);

    void Update()
    {
        Layout();
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        Vector2 move = Vector2.zero, look = Vector2.zero; float rise = 0; bool fast = false;

        foreach (var t in ETouch.activeTouches)
        {
            Vector2 g = GuiPos(t.screenPosition);
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                Vector2 gs = GuiPos(t.startScreenPosition);
                if (rNext.Contains(gs)) { NextZone(); continue; }
                if (rDrop.Contains(gs)) { DropView(); continue; }
            }
            if (rUp.Contains(g)) { rise += 1; continue; }
            if (rDown.Contains(g)) { rise -= 1; continue; }
            bool ended = t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled;
            if (t.touchId == leftId)
            {
                Vector2 d = (t.screenPosition - leftStart) / (Screen.height * 0.18f);
                move = Vector2.ClampMagnitude(d, 1f);
                if (ended) leftId = -1;
            }
            else if (t.touchId == rightId)
            {
                look += t.delta;
                if (ended) rightId = -1;
            }
            else if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                if (t.screenPosition.x < Screen.width * 0.45f && leftId < 0) { leftId = t.touchId; leftStart = t.screenPosition; }
                else if (rightId < 0) rightId = t.touchId;
            }
        }

        var kb = Keyboard.current; var ms = Mouse.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) move.y += 1; if (kb.sKey.isPressed) move.y -= 1;
            if (kb.dKey.isPressed) move.x += 1; if (kb.aKey.isPressed) move.x -= 1;
            if (kb.eKey.isPressed) rise += 1; if (kb.qKey.isPressed) rise -= 1;
            fast = kb.leftShiftKey.isPressed;
            if (kb.nKey.wasPressedThisFrame) NextZone();
            if (kb.rKey.wasPressedThisFrame) DropView();
        }
        if (ms != null && ms.rightButton.isPressed) look += ms.delta.ReadValue();

        float ls = lookSpeed * PlayerPrefs.GetFloat("agni.sens", 1f); // Settings ▸ Sensitivity
        yaw += look.x * ls; pitch = Mathf.Clamp(pitch - look.y * ls, -85f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        Vector3 p = transform.position;
        float ground = Ground(p);
        float alt = Mathf.Max(1f, p.y - ground);
        float speed = Mathf.Lerp(18f, 260f, Mathf.InverseLerp(5f, 700f, alt)) * (fast ? 3f : 1f);
        Vector3 fwd = transform.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = transform.right; right.y = 0; right.Normalize();
        p += (fwd * move.y + right * move.x) * speed * dt + Vector3.up * rise * Mathf.Max(25f, speed * 0.7f) * dt;
        p.x = Mathf.Clamp(p.x, -600f, 4700f); p.z = Mathf.Clamp(p.z, -600f, 4700f);
        ground = Ground(p);
        p.y = Mathf.Clamp(p.y, Mathf.Max(ground, 0f) + 1.8f, maxHeight);
        transform.position = p;

        // nearest zone name for the HUD
        if (zonePoints != null && zonePoints.Length > 0 && Time.frameCount % 10 == 0)
        {
            float best = float.MaxValue; int bi = 0;
            for (int i = 0; i < zonePoints.Length; i++)
            {
                Vector2 a = new Vector2(zonePoints[i].x - p.x, zonePoints[i].z - p.z);
                if (a.sqrMagnitude < best) { best = a.sqrMagnitude; bi = i; }
            }
            nearName = Mathf.Sqrt(best) < 220f ? zoneNames[bi] : "";
        }
    }

    float Ground(Vector3 p)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 o = t.transform.position, s = t.terrainData.size;
            if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z)
                return t.SampleHeight(p) + o.y;
        }
        return 0f;
    }

    public void NextZone()
    {
        if (zonePoints == null || zonePoints.Length == 0) return;
        zoneIdx = (zoneIdx + 1) % zonePoints.Length;
        Vector3 z = zonePoints[zoneIdx];
        transform.position = z + new Vector3(0, 70f, -150f);
        yaw = 0; pitch = 24f;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
    }
    public void DropView()
    {
        transform.position = dropPos; transform.rotation = dropRot;
        Vector3 e = dropRot.eulerAngles; yaw = e.y; pitch = e.x > 180 ? e.x - 360 : e.x;
    }

    void OnGUI()
    {
        if (btn == null)
        {
            btn = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
        }
        int fs = Mathf.Max(14, Screen.height / 28);
        btn.fontSize = fs; title.fontSize = Mathf.RoundToInt(fs * 1.3f);
        GUI.color = new Color(1, 1, 1, 0.85f);
        GUI.Box(rUp, "UP", btn); GUI.Box(rDown, "DOWN", btn); GUI.Box(rNext, "NEXT ZONE", btn); GUI.Box(rDrop, "DROP VIEW", btn);
        GUI.color = Color.white;
        string head = "AGNI ROYALE  ·  map blockout" + (nearName.Length > 0 ? "\n" + nearName : "");
        GUI.Label(new Rect(Screen.width * 0.2f + 2, 12, Screen.width * 0.6f, fs * 4), "<color=#000000aa>" + head + "</color>", new GUIStyle(title) { richText = true });
        GUI.Label(new Rect(Screen.width * 0.2f, 10, Screen.width * 0.6f, fs * 4), head, title);
        var small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(fs * 0.7f) };
        GUI.Label(new Rect(12, Screen.height - fs * 2.2f, Screen.width * 0.5f, fs * 2), "Left side: drag to fly   ·   Right side: drag to look", small);
    }
}
