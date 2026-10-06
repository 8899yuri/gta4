using System;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Native;

namespace TGHMod
{
    public class TGHMod : Script
    {
        private const Keys SpiritKey = Keys.T;
        private const Keys PossessKey = Keys.G;
        private const Keys ReturnKey = Keys.H;

        private const float PossessRadius = 5.0f;
        private const int RagdollMilliseconds = 1800;

        private Ped niko;
        private Ped possessed;

        private bool spiritMode;
        private bool possessing;
        private bool possessionReady;

        private Camera spiritCamera;
        private float camX;
        private float camY;
        private float camZ;
        private float camYaw;
        private float camPitch = -12.0f;

        public TGHMod()
        {
            Interval = 20;
            KeyDown += TGHMod_KeyDown;
            Tick += TGHMod_Tick;

            Game.DisplayText("TGHMod loaded  |  T Spirit  G Possess  H Return", 5000);
        }

        private void TGHMod_KeyDown(object sender, GTA.KeyEventArgs e)
        {
            if (e.Key == SpiritKey)
            {
                ToggleSpirit();
            }
            else if (e.Key == PossessKey)
            {
                TryPossess();
            }
            else if (e.Key == ReturnKey)
            {
                ReturnToNiko();
            }
        }

        private void TGHMod_Tick(object sender, EventArgs e)
        {
            try
            {
                if (niko == null || !Exists(niko))
                    niko = Player.Character;

                if (spiritMode)
                {
                    UpdateSpiritCamera();
                    return;
                }

                if (possessing && possessionReady && Exists(possessed) && !IsDead(possessed))
                {
                    // The real player entity remains Niko internally, but is hidden.
                    // The selected NPC is continuously synchronized to Niko's position,
                    // making the NPC visibly receive the normal GTA player movement.
                    possessed.Position = niko.Position;
                    possessed.Heading = niko.Heading;
                }
                else if (possessing && (!Exists(possessed) || IsDead(possessed)))
                {
                    ReturnToNiko();
                }
            }
            catch
            {
                // Never let a transient native/entity error kill the whole script.
            }
        }

        private void ToggleSpirit()
        {
            if (possessing)
            {
                Game.DisplayText("Return to Niko first (H).");
                return;
            }

            if (spiritMode)
            {
                ExitSpirit();
                return;
            }

            if (!Exists(niko))
                niko = Player.Character;

            Vector3 p = niko.Position;
            camX = p.X;
            camY = p.Y;
            camZ = p.Z + 2.0f;

            float heading = niko.Heading;
            camYaw = heading;

            Call("SET_PLAYER_CONTROL", Player, false);
            Call("FREEZE_CHAR_POSITION", niko, true);
            Call("SET_CHAR_VISIBLE", niko, false);

            CreateSpiritCamera();
            spiritMode = true;

            Game.DisplayText("SPIRIT MODE  |  WASD move  Q/E rotate  T return", 3000);
        }

        private void ExitSpirit()
        {
            DestroySpiritCamera();

            Call("SET_CHAR_VISIBLE", niko, true);
            Call("FREEZE_CHAR_POSITION", niko, false);
            Call("SET_PLAYER_CONTROL", Player, true);

            spiritMode = false;
            Game.DisplayText("Spirit mode off.", 1500);
        }

        private void CreateSpiritCamera()
        {
            try
            {
                GTA.Native.Pointer cameraPointer = typeof(Camera);
                Function.Call("CREATE_CAM", 14, cameraPointer);
                spiritCamera = cameraPointer;

                Function.Call("SET_CAM_ACTIVE", spiritCamera, true);
                Function.Call("SET_CAM_PROPAGATE", spiritCamera, true);
                Function.Call("SET_CAM_FOV", spiritCamera, 70.0f);

                ApplySpiritCamera();
            }
            catch
            {
                spiritCamera = null;
            }
        }

        private void DestroySpiritCamera()
        {
            if (spiritCamera != null)
            {
                try
                {
                    Function.Call("SET_CAM_ACTIVE", spiritCamera, false);
                    Function.Call("SET_CAM_PROPAGATE", spiritCamera, false);
                    Function.Call("DESTROY_CAM", spiritCamera);
                }
                catch { }

                spiritCamera = null;
            }
        }

        private void UpdateSpiritCamera()
        {
            if (spiritCamera == null)
                return;

            float speed = isKeyPressed(Keys.ShiftKey) ? 0.55f : 0.22f;

            float yawRad = camYaw * (float)Math.PI / 180.0f;
            float forwardX = (float)Math.Cos(yawRad);
            float forwardY = (float)Math.Sin(yawRad);
            float rightX = -forwardY;
            float rightY = forwardX;

            if (isKeyPressed(Keys.W))
            {
                camX += forwardX * speed;
                camY += forwardY * speed;
            }
            if (isKeyPressed(Keys.S))
            {
                camX -= forwardX * speed;
                camY -= forwardY * speed;
            }
            if (isKeyPressed(Keys.A))
            {
                camX -= rightX * speed;
                camY -= rightY * speed;
            }
            if (isKeyPressed(Keys.D))
            {
                camX += rightX * speed;
                camY += rightY * speed;
            }
            if (isKeyPressed(Keys.Space))
                camZ += speed;
            if (isKeyPressed(Keys.ControlKey))
                camZ -= speed;

            if (isKeyPressed(Keys.Q))
                camYaw -= 2.0f;
            if (isKeyPressed(Keys.E))
                camYaw += 2.0f;

            if (isKeyPressed(Keys.Up))
                camPitch -= 1.0f;
            if (isKeyPressed(Keys.Down))
                camPitch += 1.0f;

            if (camPitch < -80.0f) camPitch = -80.0f;
            if (camPitch > 80.0f) camPitch = 80.0f;

            ApplySpiritCamera();
        }

        private void ApplySpiritCamera()
        {
            if (spiritCamera == null)
                return;

            Function.Call("SET_CAM_POS", spiritCamera, camX, camY, camZ);
            Function.Call("SET_CAM_ROT", spiritCamera, camPitch, 0.0f, camYaw);
        }

        private void TryPossess()
        {
            if (spiritMode)
            {
                Game.DisplayText("Exit spirit mode first (T).");
                return;
            }

            if (possessing)
                return;

            if (!Exists(niko))
                niko = Player.Character;

            if (IsInVehicle(niko))
            {
                Game.DisplayText("Exit the vehicle first.");
                return;
            }

            Ped target = FindClosestPed(niko, PossessRadius);

            if (!Exists(target))
            {
                Game.DisplayText("No NPC close enough.");
                return;
            }

            if (IsDead(target))
            {
                Game.DisplayText("That NPC is unavailable.");
                return;
            }

            possessed = target;
            possessionReady = false;
            possessing = true;

            try
            {
                possessed.BlockPermanentEvents = true;
                possessed.Invincible = true;

                // Put the NPC into a real GTA IV ragdoll state first.
                Function.Call(
                    "SWITCH_PED_TO_RAGDOLL",
                    possessed,
                    RagdollMilliseconds,
                    RagdollMilliseconds,
                    0,
                    true,
                    true,
                    false
                );

                // Hide the real player entity; the selected NPC becomes the visible body.
                Call("SET_CHAR_VISIBLE", niko, false);
                Call("SET_PLAYER_CONTROL", Player, true);

                Wait(RagdollMilliseconds);

                if (!Exists(possessed) || IsDead(possessed))
                {
                    ReturnToNiko();
                    return;
                }

                possessed.Task.ClearAll();
                possessed.Position = niko.Position;
                possessed.Heading = niko.Heading;

                possessionReady = true;
                Game.DisplayText("POSSESSED  |  WASD move  Shift run  H return", 2500);
            }
            catch
            {
                ReturnToNiko();
            }
        }

        private void ReturnToNiko()
        {
            if (spiritMode)
                ExitSpirit();

            if (!Exists(niko))
                niko = Player.Character;

            try
            {
                if (Exists(possessed))
                {
                    // Leave the NPC where the possessed body ended up.
                    possessed.BlockPermanentEvents = false;
                    possessed.Invincible = false;
                    possessed.Task.ClearAll();
                }
            }
            catch { }

            try
            {
                Call("SET_CHAR_VISIBLE", niko, true);
                Call("FREEZE_CHAR_POSITION", niko, false);
                Call("SET_PLAYER_CONTROL", Player, true);
            }
            catch { }

            possessing = false;
            possessionReady = false;
            possessed = null;

            Game.DisplayText("Returned to Niko.", 1500);
        }

        private Ped FindClosestPed(Ped origin, float radius)
        {
            Ped[] all = World.GetAllPeds();
            Ped closest = null;
            float best = radius;

            if (all == null)
                return null;

            foreach (Ped ped in all)
            {
                if (!Exists(ped) || ped == origin)
                    continue;

                if (IsDead(ped) || IsInVehicle(ped))
                    continue;

                try
                {
                    float d = ped.Position.DistanceTo(origin.Position);
                    if (d < best)
                    {
                        best = d;
                        closest = ped;
                    }
                }
                catch { }
            }

            return closest;
        }

        private bool Exists(Ped ped)
        {
            return ped != null && ped.Exists();
        }

        private bool IsDead(Ped ped)
        {
            try
            {
                return ped.isDead;
            }
            catch
            {
                return Function.Call<bool>("IS_CHAR_DEAD", ped);
            }
        }

        private bool IsInVehicle(Ped ped)
        {
            try
            {
                return ped.isInVehicle();
            }
            catch
            {
                return Function.Call<bool>("IS_CHAR_IN_ANY_CAR", ped);
            }
        }

        private static void Call(string nativeName, params object[] args)
        {
            Function.Call(nativeName, args);
        }
    }
}
