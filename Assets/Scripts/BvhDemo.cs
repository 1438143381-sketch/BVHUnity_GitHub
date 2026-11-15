using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class BvhDemo : MonoBehaviour
{
    private BvhClip clip;
    private readonly List<Transform> joints = new List<Transform>();
    private readonly List<LineRenderer> bones = new List<LineRenderer>();
    private Transform skeleton;
    private Camera viewCamera;
    private Material boneMaterial;
    private Material jointMaterial;
    private string path = "";
    private string message = "选择 BVH 文件，或加载内置样例。";
    private bool playing;
    private float time;
    private float speed = 1f;
    private float yaw = 25f;
    private float pitch = 15f;
    private float distance = 360f;
    private Vector3 target = new Vector3(0f, 90f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<BvhDemo>() == null) new GameObject("BVH Demo").AddComponent<BvhDemo>();
    }

    private void Awake()
    {
        path = Path.Combine(Application.streamingAssetsPath, "sample_walk.bvh");
        viewCamera = Camera.main;
        if (viewCamera == null)
        {
            var cameraObject = new GameObject("BVH Camera");
            viewCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        viewCamera.clearFlags = CameraClearFlags.SolidColor;
        viewCamera.backgroundColor = new Color(0.075f, 0.105f, 0.12f);
        viewCamera.fieldOfView = 50f;
        viewCamera.nearClipPlane = 0.1f;
        viewCamera.farClipPlane = 10000f;
        boneMaterial = new Material(Shader.Find("Sprites/Default"));
        jointMaterial = new Material(Shader.Find("Standard"));
        jointMaterial.color = new Color(0.95f, 0.69f, 0.34f);
        UpdateCamera();
        if (File.Exists(path)) LoadFile(path);
    }

    private void OnDestroy()
    {
        if (boneMaterial != null) Destroy(boneMaterial);
        if (jointMaterial != null) Destroy(jointMaterial);
    }

    private void LoadFile(string file)
    {
        try
        {
            BvhClip next = BvhClip.Load(file);
            ClearSkeleton();
            clip = next;
            path = file;
            time = 0f;
            playing = true;
            skeleton = new GameObject("Skeleton").transform;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < clip.Joints.Count; i++)
            {
                BvhClip.Joint joint = clip.Joints[i];
                var node = new GameObject(joint.Name).transform;
                node.SetParent(joint.Parent < 0 ? skeleton : joints[joint.Parent], false);
                joints.Add(node);
                if (joint.Parent >= 0)
                {
                    var line = new GameObject("Bone " + joint.Name).AddComponent<LineRenderer>();
                    line.transform.SetParent(skeleton, false);
                    line.material = boneMaterial;
                    line.positionCount = 2;
                    line.startWidth = line.endWidth = 1.6f;
                    line.startColor = line.endColor = new Color(0.35f, 0.87f, 0.78f);
                    line.numCapVertices = 4;
                    bones.Add(line);
                }
                else bones.Add(null);
                if (joint.Channels.Length > 0)
                {
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.name = "Joint marker";
                    marker.transform.SetParent(node, false);
                    marker.transform.localScale = Vector3.one * 3.5f;
                    marker.GetComponent<Renderer>().sharedMaterial = jointMaterial;
                    Destroy(marker.GetComponent<Collider>());
                }
            }
            ApplyPose(0f);
            foreach (Transform node in joints) bounds.Encapsulate(node.position);
            target = bounds.center;
            distance = Mathf.Max(80f, bounds.size.magnitude * 1.7f);
            UpdateCamera();
            message = Path.GetFileName(file) + " · " + clip.Joints.Count + " 关节 · " + clip.FrameCount + " 帧";
        }
        catch (Exception error)
        {
            message = "加载失败：" + error.Message;
            Debug.LogWarning(error);
        }
    }

    private void ClearSkeleton()
    {
        if (skeleton != null) Destroy(skeleton.gameObject);
        joints.Clear();
        bones.Clear();
    }

    private void ApplyPose(float seconds)
    {
        if (clip == null) return;
        float frame = Mathf.Clamp(seconds / clip.FrameTime, 0f, clip.FrameCount - 1);
        int first = Mathf.FloorToInt(frame);
        int second = Mathf.Min(first + 1, clip.FrameCount - 1);
        float blend = frame - first;
        for (int i = 0; i < joints.Count; i++)
        {
            Vector3 p0, p1;
            Quaternion r0, r1;
            clip.Pose(i, first, out p0, out r0);
            clip.Pose(i, second, out p1, out r1);
            joints[i].localPosition = Vector3.Lerp(p0, p1, blend);
            joints[i].localRotation = Quaternion.Slerp(r0, r1, blend);
        }
        for (int i = 0; i < bones.Count; i++)
        {
            if (bones[i] == null) continue;
            bones[i].SetPosition(0, joints[clip.Joints[i].Parent].position);
            bones[i].SetPosition(1, joints[i].position);
        }
    }

    private void Update()
    {
        if (clip != null && playing && clip.Duration > 0f)
        {
            time += Time.deltaTime * speed;
            if (time > clip.Duration) time %= clip.Duration;
            ApplyPose(time);
        }
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * 3f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 3f, -80f, 80f);
            UpdateCamera();
        }
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            distance = Mathf.Clamp(distance * Mathf.Pow(0.9f, wheel), 10f, 5000f);
            UpdateCamera();
        }
    }

    private void UpdateCamera()
    {
        if (viewCamera == null) return;
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        viewCamera.transform.position = target + orbit * (Vector3.back * distance);
        viewCamera.transform.LookAt(target);
    }

    private static string Clock(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        return string.Format("{0:00}:{1:00.00}", minutes, seconds - minutes * 60f);
    }

    private void OnGUI()
    {
        float width = Mathf.Min(460f, Screen.width - 24f);
        GUILayout.BeginArea(new Rect(12f, 12f, width, 240f), GUI.skin.box);
        GUILayout.Label("BVH MOTION PLAYER");
        GUILayout.Label(message);
        GUILayout.BeginHorizontal();
        path = GUILayout.TextField(path, GUILayout.MinWidth(80f));
        if (GUILayout.Button("读取", GUILayout.Width(54f))) LoadFile(path);
#if UNITY_EDITOR
        if (GUILayout.Button("浏览", GUILayout.Width(54f)))
        {
            string selected = EditorUtility.OpenFilePanel("选择 BVH 文件", Path.GetDirectoryName(path), "bvh");
            if (!string.IsNullOrEmpty(selected)) LoadFile(selected);
        }
#endif
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("样例", GUILayout.Width(54f))) LoadFile(Path.Combine(Application.streamingAssetsPath, "sample_walk.bvh"));
        GUI.enabled = clip != null;
        if (GUILayout.Button(playing ? "暂停" : "播放", GUILayout.Width(54f))) playing = !playing;
        if (GUILayout.Button("重播", GUILayout.Width(54f))) { time = 0f; playing = true; ApplyPose(time); }
        GUILayout.FlexibleSpace();
        GUILayout.Label(clip == null ? "00:00 / 00:00" : Clock(time) + " / " + Clock(clip.Duration));
        GUILayout.EndHorizontal();
        if (clip != null)
        {
            float newTime = GUILayout.HorizontalSlider(time, 0f, Mathf.Max(clip.Duration, 0.001f));
            if (!Mathf.Approximately(newTime, time)) { time = Mathf.Min(newTime, clip.Duration); ApplyPose(time); }
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("速度", GUILayout.Width(36f));
        speed = GUILayout.HorizontalSlider(speed, 0.1f, 3f);
        GUILayout.Label(speed.ToString("0.0") + "x", GUILayout.Width(38f));
        GUILayout.EndHorizontal();
        GUI.enabled = true;
        GUILayout.EndArea();
    }
}
