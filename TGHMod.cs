using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;

namespace TGHMod
{
    public class Main : Script
    {
        public Main()
        {
            KeyDown += OnKeyDown;
        }

        // 修复：明确指定 System.Windows.Forms.KeyEventArgs，避免与 GTA 里的类型冲突
        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Keys.T)
            {
                Ped player = Game.Player.Character;

                if (player != null)
                {
                    UI.Notify(
                        "TGHMod loaded - Player: " +
                        player.Model.Hash
                    );
                }
            }
        }
    }
}
