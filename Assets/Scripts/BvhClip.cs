using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed class BvhClip
{
    public sealed class Joint
    {
        public string Name;
        public int Parent;
        public Vector3 Offset;
        public string[] Channels;
        public int ChannelStart;
    }

    public readonly List<Joint> Joints = new List<Joint>();
    public float[] Values;
    public int FrameCount;
    public float FrameTime;
    public int ChannelCount;
    public float Duration { get { return FrameCount > 1 ? (FrameCount - 1) * FrameTime : 0f; } }

    public static BvhClip Load(string path)
    {
        return Parse(File.ReadAllText(path));
    }

    public static BvhClip Parse(string source)
    {
        var tokens = Regex.Matches(source, @"\{|\}|[^\s{}:]+|:");
        var parser = new Parser(tokens);
        return parser.Read();
    }

    public void Pose(int jointIndex, int frame, out Vector3 position, out Quaternion rotation)
    {
        Joint joint = Joints[jointIndex];
        position = new Vector3(joint.Offset.x, joint.Offset.y, -joint.Offset.z);
        Quaternion bvhRotation = Quaternion.identity;
        int start = frame * ChannelCount + joint.ChannelStart;

        for (int i = 0; i < joint.Channels.Length; i++)
        {
            float value = Values[start + i];
            switch (joint.Channels[i].ToLowerInvariant())
            {
                case "xposition": position.x += value; break;
                case "yposition": position.y += value; break;
                case "zposition": position.z -= value; break;
                case "xrotation": bvhRotation *= Quaternion.AngleAxis(value, Vector3.right); break;
                case "yrotation": bvhRotation *= Quaternion.AngleAxis(value, Vector3.up); break;
                case "zrotation": bvhRotation *= Quaternion.AngleAxis(value, Vector3.forward); break;
            }
        }

        // Reflect the BVH right-handed Z axis into Unity's left-handed space.
        rotation = new Quaternion(-bvhRotation.x, -bvhRotation.y, bvhRotation.z, bvhRotation.w);
    }

    private sealed class Parser
    {
        private readonly MatchCollection tokens;
        private int index;
        private readonly BvhClip clip = new BvhClip();

        public Parser(MatchCollection tokens) { this.tokens = tokens; }

        private string Next()
        {
            if (index >= tokens.Count) throw new FormatException("BVH 文件意外结束。");
            return tokens[index++].Value;
        }

        private void Expect(string value)
        {
            string actual = Next();
            if (!actual.Equals(value, StringComparison.OrdinalIgnoreCase))
                throw new FormatException("预期 " + value + "，实际为 " + actual + "。");
        }

        private float Number()
        {
            string token = Next();
            float value;
            if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException("无效数值：" + token);
            return value;
        }

        private int Integer()
        {
            string token = Next();
            int value;
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 0)
                throw new FormatException("无效整数：" + token);
            return value;
        }

        public BvhClip Read()
        {
            Expect("HIERARCHY");
            ReadJoint(-1, "ROOT");
            Expect("MOTION");
            Expect("Frames");
            Expect(":");
            clip.FrameCount = Integer();
            Expect("Frame");
            Expect("Time");
            Expect(":");
            clip.FrameTime = Number();
            if (clip.FrameCount == 0 || clip.FrameTime <= 0 || clip.ChannelCount == 0)
                throw new FormatException("帧数、帧间隔和通道数必须大于零。");
            long total = (long)clip.FrameCount * clip.ChannelCount;
            if (total > int.MaxValue) throw new FormatException("BVH 文件过大。");
            clip.Values = new float[(int)total];
            for (int i = 0; i < clip.Values.Length; i++) clip.Values[i] = Number();
            if (index != tokens.Count) throw new FormatException("运动数据后存在多余内容。");
            return clip;
        }

        private void ReadJoint(int parent, string type)
        {
            Expect(type);
            string name = type == "End" ? "End Site" : Next();
            if (type == "End") Expect("Site");
            Expect("{");
            var joint = new Joint { Name = name, Parent = parent, ChannelStart = clip.ChannelCount, Channels = new string[0] };
            int jointIndex = clip.Joints.Count;
            clip.Joints.Add(joint);
            Expect("OFFSET");
            joint.Offset = new Vector3(Number(), Number(), Number());
            if (type != "End")
            {
                Expect("CHANNELS");
                int count = Integer();
                joint.Channels = new string[count];
                for (int i = 0; i < count; i++)
                {
                    string channel = Next();
                    if (!Regex.IsMatch(channel, "^[XYZ](position|rotation)$", RegexOptions.IgnoreCase))
                        throw new FormatException("不支持的通道：" + channel);
                    joint.Channels[i] = channel;
                }
                clip.ChannelCount += count;
                while (index < tokens.Count && tokens[index].Value != "}")
                {
                    string child = tokens[index].Value;
                    if (child != "JOINT" && child != "End") throw new FormatException("未知关节类型：" + child);
                    ReadJoint(jointIndex, child);
                }
            }
            Expect("}");
        }
    }
}
