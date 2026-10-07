// TGHMod 完整版（C#，ScriptHookDotNet，命名空间 GTA） // 所有 API 都对照过你上传的 ScriptHookDotNet_decompiled.cs。 // // 按键： // T 灵魂出窍：Niko 的身体原地冻结，镜头脱离，可以自由飞行 // G 附身：镜头正对着的 NPC 倒地抽搐 1.8 秒，然后你来操控它 // H 返回：回到 Niko，NPC 恢复普通 AI（灵魂/附身状态下都可以按） // // 灵魂状态下的镜头操作： // W/S 前后，A/D 左右，Space 升，C 降，Shift 加速，Q/E 左右转，R/F 抬头/低头 // 附身状态下： // W/A/S/D 相对镜头方向移动（Shift 跑），Q/E 转镜头 // // 说明：这个 API 里 Player.Character 是只读的，没有"让玩家直接变成另一个 NPC"的接口， // 所以"附身"是用镜头跟随 + 脚本给 NPC 下移动指令来实现的。 // 抽搐力度可以改下面的 ShakeForce；如果觉得 NPC 抖得太猛或没反应，调这个数。 using System; using System.Windows.Forms; // 只用来拿 Keys 枚举 using GTA; namespace TGHModNS { public class TGHScript : Script { private enum Mode { Idle, Spirit, Convulse, Possessed } // ---- 可调参数 ---- private const int ConvulseMs = 1800; // 抽搐时长 private const float ShakeForce = 0.35f; // 抽搐时每次施加的小力 private const int ShakeIntervalMs = 120; // 抽搐施力间隔 private const float SpiritSpeed = 0.35f; // 灵魂镜头每帧移动距离 private const float PickRange = 40.0f; // 附身目标最远距离 private const float PickWidth = 3.0f; // 目标离镜头视线的最大偏差（米） private const float FollowDist = 5.0f; // 附身后镜头距离 private const float FollowHeight = 2.2f; // 附身后镜头高度 private const int MoveOrderMs = 200; // 每隔多久给 NPC 下一次移动指令 private Mode mode = Mode.Idle; private Ped niko; private Ped target; private Camera cam; private Vector3 camPos; private float yaw; // 弧度，0 朝北(+Y) private float pitch; // 弧度，正值抬头 private int convulseEnd; private int nextShake; private int nextMoveOrder; private bool wasMoving; private readonly Random rng = new Random(); public TGHScript() { this.Interval = 20; this.Tick += new EventHandler(this.OnTick); this.KeyDown += new GTA.KeyEventHandler(this.OnKeyDown); } // ================= 按键 ================= private void OnKeyDown(object sender, GTA.KeyEventArgs e) { try { if (e.Key == Keys.T) SpiritOn(); else if (e.Key == Keys.G) Possess(); else if (e.Key == Keys.H) ReturnToNiko(); } catch (Exception ex) { Game.DisplayText("TGH 出错(按键): " + ex.Message, 4000); SafeReset(); } } // ================= 主循环 ================= private void OnTick(object sender, EventArgs e) { if (mode == Mode.Idle) return; try { if (niko == null || !niko.Exists() || niko.isDead) { ReturnToNiko(); return; } switch (mode) { case Mode.Spirit: UpdateSpirit(); break; case Mode.Convulse: UpdateConvulse(); break; case Mode.Possessed: UpdatePossessed(); break; } } catch (Exception ex) { Game.DisplayText("TGH 出错(循环): " + ex.Message, 4000); SafeReset(); } } // ================= T：灵魂出窍 ================= private void SpiritOn() { if (mode != Mode.Idle) return; niko = Player.Character; if (niko == null || !niko.Exists() || niko.isDead) return; Vector3 p = niko.Position; yaw = niko.Heading * (float)Math.PI / 180.0f; pitch = -0.35f; camPos = new Vector3(p.X, p.Y, p.Z + 3.0f); niko.FreezePosition = true; Player.CanControlCharacter = false; cam = new Camera(); cam.Position = camPos; cam.Activate(); ApplyCamLook(); mode = Mode.Spirit; Game.DisplayText("灵魂出窍：对准目标按 G 附身，H 返回", 3000); } private void UpdateSpirit() { ReadCamRotationKeys(); Vector3 fwdH = new Vector3(-(float)Math.Sin(yaw), (float)Math.Cos(yaw), 0f); Vector3 right = new Vector3((float)Math.Cos(yaw), (float)Math.Sin(yaw), 0f); float speed = SpiritSpeed * (isKeyPressed(Keys.ShiftKey) ? 3.0f : 1.0f); if (isKeyPressed(Keys.W)) camPos = camPos + fwdH * speed; if (isKeyPressed(Keys.S)) camPos = camPos - fwdH * speed; if (isKeyPressed(Keys.D)) camPos = camPos + right * speed; if (isKeyPressed(Keys.A)) camPos = camPos - right * speed; if (isKeyPressed(Keys.Space)) camPos = new Vector3(camPos.X, camPos.Y, camPos.Z + speed); if (isKeyPressed(Keys.C)) camPos = new Vector3(camPos.X, camPos.Y, camPos.Z - speed); cam.Position = camPos; ApplyCamLook(); } // ================= G：附身 ================= private void Possess() { if (mode != Mode.Spirit) return; Ped t = FindTargetInView(); if (t == null) { Game.DisplayText("没有对准的目标，把镜头对准一个人再按 G", 2500); return; } target = t; target.Task.ClearAllImmediately(); target.BlockPermanentEvents = true; // 防止它被吓跑/还手 target.ForceRagdoll(ConvulseMs, false); convulseEnd = Environment.TickCount + ConvulseMs; nextShake = Environment.TickCount; mode = Mode.Convulse; } private Ped FindTargetInView() { Vector3 dir = CamDirection(); Ped[] peds = World.GetPeds(camPos, PickRange); if (peds == null) return null; Ped best = null; float bestOff = PickWidth; for (int i = 0; i < peds.Length; i++) { Ped p = peds[i]; if (p == null || !p.Exists() || p.isDead) continue; if (p.Equals(niko)) continue; Vector3 v = p.Position - camPos; float t = v.X * dir.X + v.Y * dir.Y + v.Z * dir.Z; // 沿视线方向的距离 if (t <= 0f || t > PickRange) continue; Vector3 closest = new Vector3(dir.X * t, dir.Y * t, dir.Z * t); Vector3 off = v - closest; float offLen = off.Length(); // 离视线的偏差 if (offLen < bestOff) { bestOff = offLen; best = p; } } return best; } private void UpdateConvulse() { if (target == null || !target.Exists() || target.isDead) { ReturnToNiko(); return; } FollowTarget(); int now = Environment.TickCount; // 抽搐：每隔一小段时间给一点随机的小力 if (now - nextShake >= 0) { float rx = ((float)rng.NextDouble() - 0.5f) * 2f * ShakeForce; float ry = ((float)rng.NextDouble() - 0.5f) * 2f * ShakeForce; target.ApplyForce(new Vector3(rx, ry, 0.05f)); nextShake = now + ShakeIntervalMs; } if (now - convulseEnd >= 0) { target.Task.ClearAllImmediately(); wasMoving = false; mode = Mode.Possessed; Game.DisplayText("附身成功：WASD 移动，Shift 跑，H 返回", 3000); } } // ================= 附身后的操控 ================= private void UpdatePossessed() { if (target == null || !target.Exists() || target.isDead) { Game.DisplayText("被附身的人没了", 2000); ReturnToNiko(); return; } ReadCamRotationKeys(); FollowTarget(); float f = (isKeyPressed(Keys.W) ? 1f : 0f) - (isKeyPressed(Keys.S) ? 1f : 0f); float r = (isKeyPressed(Keys.D) ? 1f : 0f) - (isKeyPressed(Keys.A) ? 1f : 0f); if (f != 0f || r != 0f) { float fx = -(float)Math.Sin(yaw), fy = (float)Math.Cos(yaw); // 镜头前方 float rx = (float)Math.Cos(yaw), ry = (float)Math.Sin(yaw); // 镜头右方 float dx = fx * f + rx * r; float dy = fy * f + ry * r; float len = (float)Math.Sqrt(dx * dx + dy * dy); dx /= len; dy /= len; int now = Environment.TickCount; if (now - nextMoveOrder >= 0) { Vector3 p = target.Position; Vector3 dest = new Vector3(p.X + dx * 6f, p.Y + dy * 6f, p.Z); if (isKeyPressed(Keys.ShiftKey)) target.Task.RunTo(dest, true); else target.Task.GoTo(dest, true); nextMoveOrder = now + MoveOrderMs; } wasMoving = true; } else if (wasMoving) { target.Task.ClearAll(); wasMoving = false; } } // ================= H：返回 ================= private void ReturnToNiko() { if (mode == Mode.Idle) return; SafeReset(); Game.DisplayText("已返回 Niko", 2000); } // 无论发生什么异常，都把游戏状态恢复回来，避免卡在灵魂状态 private void SafeReset() { try { if (target != null && target.Exists()) { target.Task.ClearAll(); target.BlockPermanentEvents = false; } } catch { } try { Game.DefaultCamera.Activate(); if (cam != null) cam.Delete(); } catch { } cam = null; try { if (niko != null && niko.Exists()) niko.FreezePosition = false; } catch { } try { Player.CanControlCharacter = true; } catch { } target = null; wasMoving = false; mode = Mode.Idle; } // ================= 镜头辅助 ================= private void ReadCamRotationKeys() { const float rot = 0.04f; if (isKeyPressed(Keys.Q)) yaw += rot; if (isKeyPressed(Keys.E)) yaw -= rot; if (mode == Mode.Spirit) { if (isKeyPressed(Keys.R)) pitch += rot; if (isKeyPressed(Keys.F)) pitch -= rot; if (pitch > 1.4f) pitch = 1.4f; if (pitch < -1.4f) pitch = -1.4f; } } private Vector3 CamDirection() { float cp = (float)Math.Cos(pitch); return new Vector3(-(float)Math.Sin(yaw) * cp, (float)Math.Cos(yaw) * cp, (float)Math.Sin(pitch)); } private void ApplyCamLook() { Vector3 d = CamDirection(); cam.LookAt(new Vector3(camPos.X + d.X * 10f, camPos.Y + d.Y * 10f, camPos.Z + d.Z * 10f)); } private void FollowTarget() { Vector3 p = target.Position; float fx = -(float)Math.Sin(yaw), fy = (float)Math.Cos(yaw); camPos = new Vector3(p.X - fx * FollowDist, p.Y - fy * FollowDist, p.Z + FollowHeight); cam.Position = camPos; cam.LookAt(target); } } }// TGHMod 完整版（C#，ScriptHookDotNet，命名空间 GTA）
// 所有 API 都对照过你上传的 ScriptHookDotNet_decompiled.cs。
//
// 按键：
//   T  灵魂出窍：Niko 的身体原地冻结，镜头脱离，可以自由飞行
//   G  附身：镜头正对着的 NPC 倒地抽搐 1.8 秒，然后你来操控它
//   H  返回：回到 Niko，NPC 恢复普通 AI（灵魂/附身状态下都可以按）
//
// 灵魂状态下的镜头操作：
//   W/S 前后，A/D 左右，Space 升，C 降，Shift 加速，Q/E 左右转，R/F 抬头/低头
// 附身状态下：
//   W/A/S/D 相对镜头方向移动（Shift 跑），Q/E 转镜头
//
// 说明：这个 API 里 Player.Character 是只读的，没有"让玩家直接变成另一个 NPC"的接口，
// 所以"附身"是用镜头跟随 + 脚本给 NPC 下移动指令来实现的。
// 抽搐力度可以改下面的 ShakeForce；如果觉得 NPC 抖得太猛或没反应，调这个数。

using System;
using System.Windows.Forms;   // 只用来拿 Keys 枚举
using GTA;

namespace TGHModNS
{
    public class TGHScript : Script
    {
        private enum Mode { Idle, Spirit, Convulse, Possessed }

        // ---- 可调参数 ----
        private const int ConvulseMs = 1800;      // 抽搐时长
        private const float ShakeForce = 0.35f;   // 抽搐时每次施加的小力
        private const int ShakeIntervalMs = 120;  // 抽搐施力间隔
        private const float SpiritSpeed = 0.35f;  // 灵魂镜头每帧移动距离
        private const float PickRange = 40.0f;    // 附身目标最远距离
        private const float PickWidth = 3.0f;     // 目标离镜头视线的最大偏差（米）
        private const float FollowDist = 5.0f;    // 附身后镜头距离
        private const float FollowHeight = 2.2f;  // 附身后镜头高度
        private const int MoveOrderMs = 200;      // 每隔多久给 NPC 下一次移动指令

        private Mode mode = Mode.Idle;
        private Ped niko;
        private Ped target;
        private Camera cam;

        private Vector3 camPos;
        private float yaw;     // 弧度，0 朝北(+Y)
        private float pitch;   // 弧度，正值抬头

        private int convulseEnd;
        private int nextShake;
        private int nextMoveOrder;
        private bool wasMoving;
        private readonly Random rng = new Random();

        public TGHScript()
        {
            this.Interval = 20;
            this.Tick += new EventHandler(this.OnTick);
            this.KeyDown += new GTA.KeyEventHandler(this.OnKeyDown);
        }

        // ================= 按键 =================
        private void OnKeyDown(object sender, GTA.KeyEventArgs e)
        {
            try
            {
                if (e.Key == Keys.T) SpiritOn();
                else if (e.Key == Keys.G) Possess();
                else if (e.Key == Keys.H) ReturnToNiko();
            }
            catch (Exception ex)
            {
                Game.DisplayText("TGH 出错(按键): " + ex.Message, 4000);
                SafeReset();
            }
        }

        // ================= 主循环 =================
        private void OnTick(object sender, EventArgs e)
        {
            if (mode == Mode.Idle) return;

            try
            {
                if (niko == null || !niko.Exists() || niko.isDead)
                {
                    ReturnToNiko();
                    return;
                }

                switch (mode)
                {
                    case Mode.Spirit:     UpdateSpirit();     break;
                    case Mode.Convulse:   UpdateConvulse();   break;
                    case Mode.Possessed:  UpdatePossessed();  break;
                }
            }
            catch (Exception ex)
            {
                Game.DisplayText("TGH 出错(循环): " + ex.Message, 4000);
                SafeReset();
            }
        }

        // ================= T：灵魂出窍 =================
        private void SpiritOn()
        {
            if (mode != Mode.Idle) return;

            niko = Player.Character;
            if (niko == null || !niko.Exists() || niko.isDead) return;

            Vector3 p = niko.Position;
            yaw = niko.Heading * (float)Math.PI / 180.0f;
            pitch = -0.35f;
            camPos = new Vector3(p.X, p.Y, p.Z + 3.0f);

            niko.FreezePosition = true;
            Player.CanControlCharacter = false;

            cam = new Camera();
            cam.Position = camPos;
            cam.Activate();
            ApplyCamLook();

            mode = Mode.Spirit;
            Game.DisplayText("灵魂出窍：对准目标按 G 附身，H 返回", 3000);
        }

        private void UpdateSpirit()
        {
            ReadCamRotationKeys();

            Vector3 fwdH = new Vector3(-(float)Math.Sin(yaw), (float)Math.Cos(yaw), 0f);
            Vector3 right = new Vector3((float)Math.Cos(yaw), (float)Math.Sin(yaw), 0f);
            float speed = SpiritSpeed * (isKeyPressed(Keys.ShiftKey) ? 3.0f : 1.0f);

            if (isKeyPressed(Keys.W)) camPos = camPos + fwdH * speed;
            if (isKeyPressed(Keys.S)) camPos = camPos - fwdH * speed;
            if (isKeyPressed(Keys.D)) camPos = camPos + right * speed;
            if (isKeyPressed(Keys.A)) camPos = camPos - right * speed;
            if (isKeyPressed(Keys.Space)) camPos = new Vector3(camPos.X, camPos.Y, camPos.Z + speed);
            if (isKeyPressed(Keys.C)) camPos = new Vector3(camPos.X, camPos.Y, camPos.Z - speed);

            cam.Position = camPos;
            ApplyCamLook();
        }

        // ================= G：附身 =================
        private void Possess()
        {
            if (mode != Mode.Spirit) return;

            Ped t = FindTargetInView();
            if (t == null)
            {
                Game.DisplayText("没有对准的目标，把镜头对准一个人再按 G", 2500);
                return;
            }

            target = t;
            target.Task.ClearAllImmediately();
            target.BlockPermanentEvents = true;   // 防止它被吓跑/还手
            target.ForceRagdoll(ConvulseMs, false);

            convulseEnd = Environment.TickCount + ConvulseMs;
            nextShake = Environment.TickCount;
            mode = Mode.Convulse;
        }

        private Ped FindTargetInView()
        {
            Vector3 dir = CamDirection();
            Ped[] peds = World.GetPeds(camPos, PickRange);
            if (peds == null) return null;

            Ped best = null;
            float bestOff = PickWidth;

            for (int i = 0; i < peds.Length; i++)
            {
                Ped p = peds[i];
                if (p == null || !p.Exists() || p.isDead) continue;
                if (p.Equals(niko)) continue;

                Vector3 v = p.Position - camPos;
                float t = v.X * dir.X + v.Y * dir.Y + v.Z * dir.Z;   // 沿视线方向的距离
                if (t <= 0f || t > PickRange) continue;

                Vector3 closest = new Vector3(dir.X * t, dir.Y * t, dir.Z * t);
                Vector3 off = v - closest;
                float offLen = off.Length();                          // 离视线的偏差
                if (offLen < bestOff)
                {
                    bestOff = offLen;
                    best = p;
                }
            }
            return best;
        }

        private void UpdateConvulse()
        {
            if (target == null || !target.Exists() || target.isDead)
            {
                ReturnToNiko();
                return;
            }

            FollowTarget();

            int now = Environment.TickCount;

            // 抽搐：每隔一小段时间给一点随机的小力
            if (now - nextShake >= 0)
            {
                float rx = ((float)rng.NextDouble() - 0.5f) * 2f * ShakeForce;
                float ry = ((float)rng.NextDouble() - 0.5f) * 2f * ShakeForce;
                target.ApplyForce(new Vector3(rx, ry, 0.05f));
                nextShake = now + ShakeIntervalMs;
            }

            if (now - convulseEnd >= 0)
            {
                target.Task.ClearAllImmediately();
                wasMoving = false;
                mode = Mode.Possessed;
                Game.DisplayText("附身成功：WASD 移动，Shift 跑，H 返回", 3000);
            }
        }

        // ================= 附身后的操控 =================
        private void UpdatePossessed()
        {
            if (target == null || !target.Exists() || target.isDead)
            {
                Game.DisplayText("被附身的人没了", 2000);
                ReturnToNiko();
                return;
            }

            ReadCamRotationKeys();
            FollowTarget();

            float f = (isKeyPressed(Keys.W) ? 1f : 0f) - (isKeyPressed(Keys.S) ? 1f : 0f);
            float r = (isKeyPressed(Keys.D) ? 1f : 0f) - (isKeyPressed(Keys.A) ? 1f : 0f);

            if (f != 0f || r != 0f)
            {
                float fx = -(float)Math.Sin(yaw), fy = (float)Math.Cos(yaw);   // 镜头前方
                float rx = (float)Math.Cos(yaw),  ry = (float)Math.Sin(yaw);   // 镜头右方
                float dx = fx * f + rx * r;
                float dy = fy * f + ry * r;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                dx /= len; dy /= len;

                int now = Environment.TickCount;
                if (now - nextMoveOrder >= 0)
                {
                    Vector3 p = target.Position;
                    Vector3 dest = new Vector3(p.X + dx * 6f, p.Y + dy * 6f, p.Z);
                    if (isKeyPressed(Keys.ShiftKey)) target.Task.RunTo(dest, true);
                    else                              target.Task.GoTo(dest, true);
                    nextMoveOrder = now + MoveOrderMs;
                }
                wasMoving = true;
            }
            else if (wasMoving)
            {
                target.Task.ClearAll();
                wasMoving = false;
            }
        }

        // ================= H：返回 =================
        private void ReturnToNiko()
        {
            if (mode == Mode.Idle) return;
            SafeReset();
            Game.DisplayText("已返回 Niko", 2000);
        }

        // 无论发生什么异常，都把游戏状态恢复回来，避免卡在灵魂状态
        private void SafeReset()
        {
            try
            {
                if (target != null && target.Exists())
                {
                    target.Task.ClearAll();
                    target.BlockPermanentEvents = false;
                }
            }
            catch { }

            try
            {
                Game.DefaultCamera.Activate();
                if (cam != null) cam.Delete();
            }
            catch { }
            cam = null;

            try
            {
                if (niko != null && niko.Exists()) niko.FreezePosition = false;
            }
            catch { }

            try { Player.CanControlCharacter = true; } catch { }

            target = null;
            wasMoving = false;
            mode = Mode.Idle;
        }

        // ================= 镜头辅助 =================
        private void ReadCamRotationKeys()
        {
            const float rot = 0.04f;
            if (isKeyPressed(Keys.Q)) yaw += rot;
            if (isKeyPressed(Keys.E)) yaw -= rot;
            if (mode == Mode.Spirit)
            {
                if (isKeyPressed(Keys.R)) pitch += rot;
                if (isKeyPressed(Keys.F)) pitch -= rot;
                if (pitch > 1.4f) pitch = 1.4f;
                if (pitch < -1.4f) pitch = -1.4f;
            }
        }

        private Vector3 CamDirection()
        {
            float cp = (float)Math.Cos(pitch);
            return new Vector3(-(float)Math.Sin(yaw) * cp, (float)Math.Cos(yaw) * cp, (float)Math.Sin(pitch));
        }

        private void ApplyCamLook()
        {
            Vector3 d = CamDirection();
            cam.LookAt(new Vector3(camPos.X + d.X * 10f, camPos.Y + d.Y * 10f, camPos.Z + d.Z * 10f));
        }

        private void FollowTarget()
        {
            Vector3 p = target.Position;
            float fx = -(float)Math.Sin(yaw), fy = (float)Math.Cos(yaw);
            camPos = new Vector3(p.X - fx * FollowDist, p.Y - fy * FollowDist, p.Z + FollowHeight);
            cam.Position = camPos;
            cam.LookAt(target);
        }
    }
}
