// TGHMod 骨架（C#，面向 GTA IV 的 ScriptHookDotNet，命名空间 GTA）
//
// 重要：GPT 之前用的 UI.Notify、Game.Player、System.Windows.Forms 的 KeyEventArgs
// 都是 GTA V（ScriptHookVDotNet）或 WinForms 的写法，不是 GTA IV 版的。
// GTA IV 版（据我所知）的对应写法：
//   提示文字  Game.DisplayText("文字", 毫秒)
//   玩家      Game.LocalPlayer，角色是 Game.LocalPlayer.Character
//   按键事件  KeyDown，处理函数参数必须是 GTA.KeyEventArgs（不是 WinForms 的）
//
// 这个版本只做一件事：验证能编译、能加载、T/G/H 有反应。
// 编译通过后，再按 API 清单加入灵魂出窍/附身的真正逻辑。

using System;
using System.Windows.Forms;   // 只用来拿 Keys 枚举
using GTA;

namespace TGHModNS
{
    public class TGHScript : Script
    {
        private enum Mode { Idle, Spirit, Possessed }
        private Mode mode = Mode.Idle;

        public TGHScript()
        {
            this.Interval = 100;
            this.Tick += new EventHandler(this.OnTick);
            // 注意：这里必须写全名 GTA.KeyEventHandler，避免和 WinForms 的同名类型冲突
            this.KeyDown += new GTA.KeyEventHandler(this.OnKeyDown);
        }

        private void OnTick(object sender, EventArgs e)
        {
            // 以后在这里放：镜头跟随、NPC 移动、1.8 秒抽搐计时等逻辑
        }

        // 参数类型必须是 GTA.KeyEventArgs
        private void OnKeyDown(object sender, GTA.KeyEventArgs e)
        {
            if (e.Key == Keys.T)
            {
                mode = Mode.Spirit;
                Game.DisplayText("TGH: 灵魂出窍 (T)", 2000);
            }
            else if (e.Key == Keys.G)
            {
                if (mode == Mode.Spirit)
                {
                    mode = Mode.Possessed;
                    Game.DisplayText("TGH: 附身 (G)", 2000);
                }
            }
            else if (e.Key == Keys.H)
            {
                mode = Mode.Idle;
                Game.DisplayText("TGH: 返回 (H)", 2000);
            }
        }
    }
}
