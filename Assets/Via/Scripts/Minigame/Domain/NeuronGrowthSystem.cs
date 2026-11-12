using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Via.Minigame.Domain
{
    public sealed class Attractor
    {
        public Vector2 Position { get; }
        public bool Consumed { get; internal set; }
        internal Attractor(Vector2 position) { Position = position; }
    }

    public sealed class BranchZone
    {
        public Vector2 Position { get; }
        public float Radius { get; }
        internal BranchZone(Vector2 position, float radius) { Position = position; Radius = radius; }
    }

    public sealed class GrowthTip
    {
        public int NodeId { get; internal set; }
        public Vector2 Position { get; internal set; }
        public bool HasForced { get; internal set; }
        public Vector2 ForcedTarget { get; internal set; }
        internal Attractor Target;
        internal int LockedZone = -1;
        internal GrowthTip(int node, Vector2 position) { NodeId = node; Position = position; }
    }

    public sealed class NeuronGrowthSystem
    {
        private const float BranchAngleDegrees = 45f;

        private readonly NeuronNetwork network;
        private readonly System.Random random;
        private readonly List<GrowthTip> tips = new List<GrowthTip>();
        private readonly List<Attractor> attractors = new List<Attractor>();
        private readonly List<BranchZone> branchZones = new List<BranchZone>();
        private readonly float radius;
        private readonly float speed;
        private readonly float joinDistance;
        public ReadOnlyCollection<GrowthTip> Tips { get; }
        public ReadOnlyCollection<Attractor> Attractors { get; }
        public ReadOnlyCollection<BranchZone> BranchZones { get; }
        public bool IsGrowing => tips.Exists(tip => tip.Target != null || tip.HasForced);

        public NeuronGrowthSystem(NeuronNetwork network, float radius, float speed,
            float joinDistance, System.Random random)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.radius = radius;
            this.speed = speed;
            this.joinDistance = joinDistance;
            Tips = tips.AsReadOnly();
            Attractors = attractors.AsReadOnly();
            BranchZones = branchZones.AsReadOnly();
            tips.Add(new GrowthTip(0, network.Nodes[0].Position));
        }

        public void PlaceAttractor(Vector2 position)
        {
            attractors.Add(new Attractor(position));
            AssignTargets();
        }

        public void PlaceBranchZone(Vector2 position, float zoneRadius)
        {
            if (zoneRadius <= 0f) return;
            branchZones.Add(new BranchZone(position, zoneRadius));
        }

        // Recomputes idle growth ends after a topology edit (cut/reshape): every degree-1 node except the origin.
        public void ResetTips()
        {
            tips.Clear();
            if (network.Edges.Count == 0)
            {
                tips.Add(new GrowthTip(0, network.Nodes[0].Position));
                AssignTargets();
                return;
            }
            for (int i = 0; i < network.Nodes.Count; i++)
                if (i != 0 && network.Degree(i) == 1)
                    tips.Add(new GrowthTip(i, network.Nodes[i].Position));
            AssignTargets();
        }

        public NeuronGrowthSystem CopyFor(NeuronNetwork copiedNetwork, bool resetTips = false)
        {
            var copy = new NeuronGrowthSystem(copiedNetwork, radius, speed, joinDistance, new System.Random());
            copy.tips.Clear();
            foreach (var point in attractors)
                copy.attractors.Add(new Attractor(point.Position) { Consumed = point.Consumed });
            foreach (var zone in branchZones)
                copy.branchZones.Add(new BranchZone(zone.Position, zone.Radius));
            if (resetTips)
            {
                copy.ResetTips();
                return copy;
            }
            foreach (var tip in tips)
            {
                var cloned = new GrowthTip(tip.NodeId, tip.Position)
                {
                    HasForced = tip.HasForced,
                    ForcedTarget = tip.ForcedTarget,
                    LockedZone = tip.LockedZone
                };
                if (tip.Target != null) cloned.Target = copy.attractors[attractors.IndexOf(tip.Target)];
                copy.tips.Add(cloned);
            }
            return copy;
        }

        private void AssignTargets()
        {
            foreach (Attractor point in attractors)
            {
                if (point.Consumed) continue;
                var candidates = new List<GrowthTip>();
                float nearest = float.PositiveInfinity;
                foreach (GrowthTip tip in tips)
                {
                    if (tip.Target != null || tip.HasForced) continue;
                    float distance = Vector2.Distance(tip.Position, point.Position);
                    if (distance > radius || distance < 0.0001f) continue;
                    if (distance < nearest - 0.0001f)
                    {
                        nearest = distance;
                        candidates.Clear();
                        candidates.Add(tip);
                    }
                    else if (Mathf.Abs(distance - nearest) <= 0.0001f) candidates.Add(tip);
                }
                if (candidates.Count == 0) continue;
                candidates[random.Next(candidates.Count)].Target = point;
                point.Consumed = true;
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            var snapshot = new List<GrowthTip>(tips);
            foreach (GrowthTip tip in snapshot)
            {
                if (!tips.Contains(tip)) continue;

                if (tip.HasForced)
                {
                    tip.Position = Vector2.MoveTowards(tip.Position, tip.ForcedTarget, speed * deltaTime);
                    if ((tip.Position - tip.ForcedTarget).sqrMagnitude > 0.000001f) continue;
                    tip.NodeId = network.Connect(tip.NodeId, tip.ForcedTarget, joinDistance);
                    tip.Position = network.Nodes[tip.NodeId].Position;
                    tip.HasForced = false;
                    tip.LockedZone = -1;
                    continue;
                }

                if (tip.Target != null)
                {
                    tip.Position = Vector2.MoveTowards(tip.Position, tip.Target.Position, speed * deltaTime);
                    if ((tip.Position - tip.Target.Position).sqrMagnitude <= 0.000001f)
                    {
                        tip.NodeId = network.Connect(tip.NodeId, tip.Target.Position, joinDistance);
                        tip.Position = network.Nodes[tip.NodeId].Position;
                        tip.Target = null;
                    }
                }

                TryBranch(tip);
            }
            AssignTargets();
        }

        private void TryBranch(GrowthTip tip)
        {
            if (tip.HasForced || tip.Target == null) return;
            Vector2 direction = (tip.Target.Position - tip.Position);
            if (direction.sqrMagnitude < 1e-6f) return;
            direction = direction.normalized;

            for (int z = 0; z < branchZones.Count; z++)
            {
                if (z == tip.LockedZone) continue;
                BranchZone zone = branchZones[z];
                if ((tip.Position - zone.Position).sqrMagnitude > zone.Radius * zone.Radius) continue;

                Vector2 plus = Rotate(direction, BranchAngleDegrees);
                Vector2 minus = Rotate(direction, -BranchAngleDegrees);
                tip.Target.Consumed = true;

                var first = new GrowthTip(tip.NodeId, tip.Position)
                { HasForced = true, ForcedTarget = ExitPoint(zone, tip.Position, plus), LockedZone = z };
                var second = new GrowthTip(tip.NodeId, tip.Position)
                { HasForced = true, ForcedTarget = ExitPoint(zone, tip.Position, minus), LockedZone = z };
                tips.Remove(tip);
                tips.Add(first);
                tips.Add(second);
                return;
            }
        }

        private static Vector2 ExitPoint(BranchZone zone, Vector2 from, Vector2 direction)
        {
            Vector2 offset = from - zone.Position;
            float b = Vector2.Dot(offset, direction);
            float c = offset.sqrMagnitude - zone.Radius * zone.Radius;
            float discriminant = b * b - c;
            if (discriminant < 0f) return from + direction * zone.Radius;
            float t = -b + Mathf.Sqrt(discriminant);
            if (t < 0f) t = 0f;
            return from + direction * t;
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r);
            float sin = Mathf.Sin(r);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }
    }
}
