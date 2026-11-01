using UnityEngine;

// Keeps a zone name label facing the camera and readable from far away (drop height).
public class AgniBillboard : MonoBehaviour
{
    public float minWorldHeight = 7f;     // label never smaller than this (metres)
    public float screenFraction = 0.035f;  // target height as a fraction of the view
    public float baseHeight = 4f;          // label height at scale 1 (metres)
    public static Camera Override;          // used by the proof renderer
    bool shown = true;

    void LateUpdate() { Face(Override != null ? Override : Camera.main); }

    public void Face(Camera cam)
    {
        if (cam == null) return;
        Vector3 d = transform.position - cam.transform.position;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        float dist = d.magnitude;
        // declutter: when flying low only nearby names show; from drop height every name shows
        float camAlt = cam.transform.position.y;
        float maxDist = cam.orthographic ? 1e6f : Mathf.Lerp(650f, 6000f, Mathf.InverseLerp(40f, 450f, camAlt));
        bool show = dist < maxDist;
        if (show != shown) { foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = show; shown = show; }
        float want = cam.orthographic ? cam.orthographicSize * 2f * screenFraction
                                      : 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * screenFraction;
        float h = Mathf.Max(minWorldHeight, want);
        transform.localScale = Vector3.one * (h / baseHeight);
    }
}
