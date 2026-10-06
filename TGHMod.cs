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

        private void OnKeyDown(object sender, KeyEventArgs e)
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
